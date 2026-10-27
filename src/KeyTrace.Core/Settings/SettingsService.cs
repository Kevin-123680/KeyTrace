using KeyTrace.Core.Diagnostics;

namespace KeyTrace.Core.Settings;

/// <summary>
/// 设置的单一事实来源。所有修改都经过 <see cref="Update"/>，从而保证落盘并通知订阅者
/// （例如采集服务需要实时拿到最新的校准系数）。
/// </summary>
public sealed class SettingsService
{
    private readonly ISettingsStore _store;
    private readonly IAppLogger _logger;
    private readonly object _gate = new();

    public SettingsService(ISettingsStore store, IAppLogger? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? NullLogger.Instance;

        var loaded = _store.Load();
        loaded.Normalize();
        Current = loaded;
    }

    /// <summary>设置变化（暂停、校准系数、静默名单等）后触发。</summary>
    public event EventHandler? Changed;

    public AppSettings Current { get; private set; }

    public bool IsPaused => Current.CollectionPaused;

    public double MetersPerPixel => Current.MetersPerPixel;

    public void Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        lock (_gate)
        {
            mutate(Current);
            Current.Normalize();
            _store.Save(Current);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool paused)
    {
        if (Current.CollectionPaused == paused)
        {
            return;
        }

        Update(settings => settings.CollectionPaused = paused);
        _logger.Info(paused ? "采集已暂停。" : "采集已恢复。");
    }

    public void SetMetersPerPixel(double metersPerPixel)
    {
        Update(settings => settings.MetersPerPixel = metersPerPixel);
    }

    public void Reload()
    {
        lock (_gate)
        {
            var loaded = _store.Load();
            loaded.Normalize();
            Current = loaded;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
