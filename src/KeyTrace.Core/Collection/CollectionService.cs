using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Diagnostics;
using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using KeyTrace.Core.Privacy;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Storage;
using KeyTrace.Core.Text;

namespace KeyTrace.Core.Collection;

/// <summary>
/// 采集服务：把"原始输入 → 去重 → 隐私静默 → 聚合 → 定时落盘"串起来。
/// 实现 <see cref="IInputSink"/>，方法都由钩子线程调用，必须快速返回。
/// </summary>
public sealed class CollectionService : IInputSink, IDisposable
{
    private const int AutoRepeatWindowMilliseconds = 800;
    private const int SuppressionCacheMilliseconds = 500;

    private readonly IInputSource _source;
    private readonly DataAggregator _aggregator;
    private readonly IStatsRepository _repository;
    private readonly SettingsService _settings;
    private readonly PrivacyFilter _privacy;
    private readonly IForegroundProvider _foreground;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly int _suppressionCacheMilliseconds;

    private readonly object _keyGate = new();
    private readonly HashSet<int> _pressedKeys = new();
    private readonly Dictionary<int, long> _pressedAt = new();
    private KeyModifiers _modifiers = KeyModifiers.None;

    private Timer? _flushTimer;
    private bool _started;
    private bool _disposed;
    private volatile bool _privacySuppressed;
    private bool _suppressionCached;
    private long _suppressionCachedAt;
    private bool _suppressionCachedValue;

    public CollectionService(
        IInputSource source,
        DataAggregator aggregator,
        IStatsRepository repository,
        SettingsService settings,
        IClock? clock = null,
        PrivacyFilter? privacy = null,
        IForegroundProvider? foreground = null,
        IAppLogger? logger = null,
        int suppressionCacheMilliseconds = SuppressionCacheMilliseconds)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? SystemClock.Instance;
        _privacy = privacy ?? new PrivacyFilter();
        _foreground = foreground ?? new ForegroundWatcher();
        _logger = logger ?? NullLogger.Instance;
        _suppressionCacheMilliseconds = Math.Max(0, suppressionCacheMilliseconds);

        ApplySettings();
    }

    /// <summary>每次成功落盘后触发（参数是本次写入的增量）。</summary>
    public event EventHandler<AggregateDelta>? Flushed;

    public bool IsRunning => _started;

    public bool IsPrivacySuppressed => _privacySuppressed;

    public bool IsPaused => _settings.Current.CollectionPaused;

    public IInputSource Source => _source;

    /// <summary>在途计数（清空数据时需要一并丢弃）。</summary>
    public DataAggregator Aggregator => _aggregator;

    /// <summary>设置后，鼠标移动会同时喂给校准会话（设置页的"划 10cm"）。</summary>
    public Calibration.CalibrationSession? Calibration { get; set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        ApplySettings();
        _source.Start();
        _started = true;
        _settings.Changed += OnSettingsChanged;
        RestartFlushTimer();

        _logger.Info("采集服务已启动。");
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _settings.Changed -= OnSettingsChanged;

        var timer = Interlocked.Exchange(ref _flushTimer, null);
        timer?.Dispose();

        try
        {
            _source.Stop();
        }
        catch (Exception ex)
        {
            _logger.Error("停止输入源失败。", ex);
        }

        Flush();
        _logger.Info("采集服务已停止。");
    }

    public void Flush()
    {
        AggregateDelta delta;
        try
        {
            delta = _aggregator.Drain(_clock.Now);
        }
        catch (Exception ex)
        {
            _logger.Error("聚合器排空失败。", ex);
            return;
        }

        if (delta.IsEmpty)
        {
            return;
        }

        try
        {
            _repository.ApplyDelta(delta, _settings.Current.MetersPerPixel);
            Flushed?.Invoke(this, delta);
        }
        catch (Exception ex)
        {
            _logger.Error("写入数据库失败，本次增量已丢弃。", ex);
        }
    }

    // ---- IInputSink（钩子线程） ----

    public void OnKey(int virtualKey, bool isDown, bool isInjected)
    {
        var key = VirtualKeys.NormalizeModifier(virtualKey);

        if (isInjected && _settings.Current.IgnoreInjectedInput)
        {
            return;
        }

        UpdateModifierState(key, isDown);

        if (!isDown)
        {
            lock (_keyGate)
            {
                _pressedKeys.Remove(key);
                _pressedAt.Remove(key);
            }

            return;
        }

        if (!TryClaimKeyPress(key))
        {
            return;
        }

        if (ShouldSkipInput())
        {
            return;
        }

        var settings = _settings.Current;
        var at = _clock.Now;
        _aggregator.RecordKey(key, KeyNameMap.IsTypingKey(key), at);

        if (settings.TrackShortcutCombos && !VirtualKeys.IsModifier(key))
        {
            var modifiers = ReadModifiers();
            if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Windows)) != 0)
            {
                _aggregator.RecordCombo(KeyNameMap.FormatCombo(modifiers, key), at);
            }
        }
    }

    public void OnPointerMove(int x, int y, bool isInjected)
    {
        Calibration?.AddSample(x, y);

        if (isInjected && _settings.Current.IgnoreInjectedInput)
        {
            return;
        }

        if (ShouldSkipInput())
        {
            return;
        }

        _aggregator.RecordPointerMove(x, y, _clock.Now);
    }

    public void OnMouseButton(MouseButton button, int x, int y, bool isInjected)
    {
        if (isInjected && _settings.Current.IgnoreInjectedInput)
        {
            return;
        }

        if (ShouldSkipInput())
        {
            return;
        }

        _aggregator.RecordClick(button, x, y, _clock.Now);
    }

    public void OnMouseWheel(int delta, int x, int y, bool isInjected)
    {
        if (isInjected && _settings.Current.IgnoreInjectedInput)
        {
            return;
        }

        if (ShouldSkipInput())
        {
            return;
        }

        _aggregator.RecordWheel(delta, _clock.Now);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _source.Dispose();
    }

    private bool TryClaimKeyPress(int key)
    {
        var now = Environment.TickCount64;

        lock (_keyGate)
        {
            if (_pressedKeys.Contains(key))
            {
                // 自动连发：按住不放时系统每 ~30ms 发一次 KeyDown，只算第一次。
                if (_pressedAt.TryGetValue(key, out var pressedAt) &&
                    now - pressedAt <= AutoRepeatWindowMilliseconds)
                {
                    return false;
                }

                // 超过窗口仍认为"按住"，说明漏掉了 KeyUp（例如窗口切换），按新按键处理。
            }

            _pressedKeys.Add(key);
            _pressedAt[key] = now;
            return true;
        }
    }

    private bool ShouldSkipInput()
    {
        var settings = _settings.Current;

        if (settings.CollectionPaused)
        {
            return true;
        }

        var suppressed = IsPrivacySuppressedCached(settings);
        if (suppressed)
        {
            // 静默期间不累加距离，恢复后重新建立坐标锚点。
            _aggregator.ResetPointerAnchor();
        }

        return suppressed;
    }

    private bool IsPrivacySuppressedCached(AppSettings settings)
    {
        if (!settings.PrivacyFilterEnabled)
        {
            _privacySuppressed = false;
            return false;
        }

        var now = Environment.TickCount64;
        if (_suppressionCacheMilliseconds > 0 &&
            _suppressionCached &&
            now - _suppressionCachedAt < _suppressionCacheMilliseconds)
        {
            return _suppressionCachedValue;
        }

        var foreground = _foreground.GetForeground();
        var suppressed = _privacy.ShouldSuppress(foreground, settings);

        _suppressionCached = true;
        _suppressionCachedAt = now;
        _suppressionCachedValue = suppressed;

        if (suppressed != _privacySuppressed)
        {
            _logger.Info(suppressed ? "检测到敏感窗口，已自动静默。" : "敏感窗口已关闭，恢复采集。");
        }

        _privacySuppressed = suppressed;
        return suppressed;
    }

    /// <summary>从我们自己跟踪的按键状态读取修饰键，避免 GetAsyncKeyState 对注入输入/测试环境的依赖。</summary>
    private KeyModifiers ReadModifiers()
    {
        lock (_keyGate)
        {
            return _modifiers;
        }
    }

    private void UpdateModifierState(int key, bool isDown)
    {
        var flag = MapModifier(key);
        if (flag == KeyModifiers.None)
        {
            return;
        }

        lock (_keyGate)
        {
            if (isDown)
            {
                _modifiers |= flag;
            }
            else
            {
                _modifiers &= ~flag;
            }
        }
    }

    private static KeyModifiers MapModifier(int key) => key switch
    {
        VirtualKeys.LControl or VirtualKeys.RControl => KeyModifiers.Control,
        VirtualKeys.LMenu or VirtualKeys.RMenu => KeyModifiers.Alt,
        VirtualKeys.LShift or VirtualKeys.RShift => KeyModifiers.Shift,
        VirtualKeys.LWin or VirtualKeys.RWin => KeyModifiers.Windows,
        _ => KeyModifiers.None,
    };

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ApplySettings();
        RestartFlushTimer();
    }

    private void ApplySettings()
    {
        _aggregator.CaptureClickZones = _settings.Current.CaptureClickZones;

        // 设置变化后立刻失效隐私缓存，暂停/恢复立即生效。
        _suppressionCached = false;
    }

    private void RestartFlushTimer()
    {
        if (!_started)
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_settings.Current.FlushIntervalSeconds, 5, 600));
        var timer = Interlocked.Exchange(ref _flushTimer, null);
        timer?.Dispose();

        _flushTimer = new Timer(OnFlushTimer, null, interval, interval);
    }

    private void OnFlushTimer(object? state)
    {
        try
        {
            Flush();
        }
        catch (Exception ex)
        {
            _logger.Error("定时落盘失败。", ex);
        }
    }
}
