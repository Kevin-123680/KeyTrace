using KeyTrace.Core.Input;
using KeyTrace.Core.Models;

namespace KeyTrace.Core.Aggregation;

/// <summary>屏幕点击热区网格：把整个虚拟桌面切成 20 × 10 个格子（每格约 5% 屏幕）。</summary>
public static class ClickZoneGrid
{
    public const int Columns = 20;
    public const int Rows = 10;

    public static int ToIndex(int x, int y, ScreenArea area)
    {
        if (area.Width <= 0 || area.Height <= 0)
        {
            area = ScreenArea.Fallback;
        }

        var fx = (x - (double)area.X) / area.Width;
        var fy = (y - (double)area.Y) / area.Height;
        var column = (int)Math.Floor(fx * Columns);
        var row = (int)Math.Floor(fy * Rows);

        column = Math.Clamp(column, 0, Columns - 1);
        row = Math.Clamp(row, 0, Rows - 1);
        return (row * Columns) + column;
    }

    public static (int Column, int Row) ToCell(int index)
    {
        if (index < 0)
        {
            index = 0;
        }

        var clamped = Math.Clamp(index, 0, (Columns * Rows) - 1);
        return (clamped % Columns, clamped / Columns);
    }
}

/// <summary>
/// 处理层：把原始输入聚合成"按小时"的计数器，供存储层批量落盘。
/// 线程安全；钩子线程、落盘定时器与界面线程会并发访问。
/// </summary>
public sealed class DataAggregator
{
    /// <summary>两次采样之间超过该像素距离就当作"瞬移"（SetCursorPos / 切换显示器），不计入距离。</summary>
    public const double MaxSingleMovePixels = 4000;

    private const int WpmWindowSeconds = 60;

    private readonly object _gate = new();
    private readonly Dictionary<(DateOnly Date, int Hour), HourBucket> _buckets = new();
    private readonly Dictionary<DateOnly, HashSet<int>> _activeMinutes = new();
    private readonly Func<ScreenArea> _screenAreaProvider;

    // WPM 环形缓冲：按秒统计"可打印键"数量。
    private readonly int[] _typedPerSecond = new int[WpmWindowSeconds];
    private readonly long[] _typedSecondStamp = new long[WpmWindowSeconds];

    private bool _hasPointerAnchor;
    private int _lastPointerX;
    private int _lastPointerY;
    private int _scrollRemainder;
    private double _peakWpm;
    private int _keysSincePeakUpdate;

    public DataAggregator(Func<ScreenArea>? screenAreaProvider = null, bool captureClickZones = false)
    {
        _screenAreaProvider = screenAreaProvider ?? (() => Win32.GetVirtualScreenArea());
        CaptureClickZones = captureClickZones;
    }

    public bool CaptureClickZones { get; set; }

    /// <summary>记录一次按键（只在 KeyDown 时调用，KeyUp 不计数）。</summary>
    public void RecordKey(int virtualKey, bool isTyping, DateTimeOffset at)
    {
        var local = at.DateTime;
        var date = DateOnly.FromDateTime(local);

        lock (_gate)
        {
            var bucket = GetBucket(date, local.Hour);
            bucket.KeyCounts.TryGetValue(virtualKey, out var existing);
            bucket.KeyCounts[virtualKey] = existing + 1;

            MarkActiveMinute(date, (local.Hour * 60) + local.Minute);

            if (isTyping)
            {
                RecordTyping(at);
            }
        }
    }

    /// <summary>记录一次快捷键组合。</summary>
    public void RecordCombo(string combo, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(combo))
        {
            return;
        }

        var local = at.DateTime;
        var date = DateOnly.FromDateTime(local);

        lock (_gate)
        {
            var bucket = GetBucket(date, local.Hour);
            bucket.ComboCounts.TryGetValue(combo, out var existing);
            bucket.ComboCounts[combo] = existing + 1;
        }
    }

    /// <summary>记录鼠标移动：与上一次坐标做欧氏距离累加。</summary>
    public void RecordPointerMove(int x, int y, DateTimeOffset at)
    {
        var local = at.DateTime;
        var date = DateOnly.FromDateTime(local);

        lock (_gate)
        {
            if (_hasPointerAnchor)
            {
                var dx = (double)x - _lastPointerX;
                var dy = (double)y - _lastPointerY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));

                if (distance > 0 && distance <= MaxSingleMovePixels)
                {
                    var bucket = GetBucket(date, local.Hour);
                    bucket.MousePixels += distance;
                    MarkActiveMinute(date, (local.Hour * 60) + local.Minute);
                }
            }

            _lastPointerX = x;
            _lastPointerY = y;
            _hasPointerAnchor = true;
        }
    }

    /// <summary>记录一次鼠标点击（只统计按下，按下 + 抬起只算一次）。</summary>
    public void RecordClick(MouseButton button, int x, int y, DateTimeOffset at)
    {
        var local = at.DateTime;
        var date = DateOnly.FromDateTime(local);

        lock (_gate)
        {
            var bucket = GetBucket(date, local.Hour);
            switch (button)
            {
                case MouseButton.Left:
                    bucket.LeftClicks++;
                    break;
                case MouseButton.Right:
                    bucket.RightClicks++;
                    break;
                case MouseButton.Middle:
                    bucket.MiddleClicks++;
                    break;
            }

            if (CaptureClickZones)
            {
                var index = ClickZoneGrid.ToIndex(x, y, _screenAreaProvider());
                bucket.ClickZones.TryGetValue(index, out var existing);
                bucket.ClickZones[index] = existing + 1;
            }

            MarkActiveMinute(date, (local.Hour * 60) + local.Minute);
        }
    }

    /// <summary>记录滚轮。Windows 每格 = 120，这里换算成"格数"。</summary>
    public void RecordWheel(int delta, DateTimeOffset at)
    {
        var local = at.DateTime;
        var date = DateOnly.FromDateTime(local);

        lock (_gate)
        {
            _scrollRemainder += delta;
            var steps = _scrollRemainder / 120;
            _scrollRemainder -= steps * 120;

            if (steps == 0)
            {
                return;
            }

            var bucket = GetBucket(date, local.Hour);
            bucket.ScrollSteps += steps;
            MarkActiveMinute(date, (local.Hour * 60) + local.Minute);
        }
    }

    /// <summary>休息（或进入静默）后重新建立鼠标坐标锚点，避免把"跳变"算进距离。</summary>
    public void ResetPointerAnchor()
    {
        lock (_gate)
        {
            _hasPointerAnchor = false;
        }
    }

    /// <summary>当前小时（含跨天跨小时）的增量，并清空缓冲区。</summary>
    public AggregateDelta Drain(DateTimeOffset at)
    {
        lock (_gate)
        {
            if (_buckets.Count == 0 && _activeMinutes.Count == 0)
            {
                return AggregateDelta.Empty(at);
            }

            var hours = new List<HourAggregate>(_buckets.Count);
            foreach (var pair in _buckets)
            {
                var bucket = pair.Value;
                hours.Add(new HourAggregate
                {
                    Date = pair.Key.Date,
                    Hour = pair.Key.Hour,
                    KeyCounts = new Dictionary<int, int>(bucket.KeyCounts),
                    ComboCounts = new Dictionary<string, int>(bucket.ComboCounts, StringComparer.Ordinal),
                    ClickZones = new Dictionary<int, int>(bucket.ClickZones),
                    MousePixels = (long)Math.Round(bucket.MousePixels),
                    LeftClicks = bucket.LeftClicks,
                    RightClicks = bucket.RightClicks,
                    MiddleClicks = bucket.MiddleClicks,
                    ScrollSteps = bucket.ScrollSteps,
                });
            }

            var minutes = new Dictionary<DateOnly, IReadOnlyList<int>>(_activeMinutes.Count);
            foreach (var pair in _activeMinutes)
            {
                var list = pair.Value.ToList();
                list.Sort();
                minutes[pair.Key] = list;
            }

            _buckets.Clear();
            _activeMinutes.Clear();
            _scrollRemainder = 0;

            return new AggregateDelta
            {
                Hours = hours,
                ActiveMinutes = minutes,
                CapturedAt = at,
            };
        }
    }

    /// <summary>界面实时数据：不改变任何计数状态。</summary>
    public AggregatorSnapshot Snapshot(DateOnly today, DateTimeOffset at)
    {
        lock (_gate)
        {
            var keyCounts = new Dictionary<int, int>();
            var hourly = new Dictionary<int, int>();
            var keys = 0;
            var clicks = 0;
            var left = 0;
            var right = 0;
            var middle = 0;
            var scroll = 0;
            long pixels = 0;

            foreach (var pair in _buckets)
            {
                if (pair.Key.Date != today)
                {
                    continue;
                }

                var bucket = pair.Value;
                foreach (var keyPair in bucket.KeyCounts)
                {
                    keyCounts.TryGetValue(keyPair.Key, out var existing);
                    keyCounts[keyPair.Key] = existing + keyPair.Value;
                    keys += keyPair.Value;
                    hourly.TryGetValue(pair.Key.Hour, out var hourExisting);
                    hourly[pair.Key.Hour] = hourExisting + keyPair.Value;
                }

                left += bucket.LeftClicks;
                right += bucket.RightClicks;
                middle += bucket.MiddleClicks;
                scroll += bucket.ScrollSteps;
                pixels += (long)Math.Round(bucket.MousePixels);
                clicks += bucket.LeftClicks + bucket.RightClicks + bucket.MiddleClicks;
            }

            var activeMinutes = _activeMinutes.TryGetValue(today, out var set) ? set.Count : 0;

            return new AggregatorSnapshot
            {
                Date = today,
                Keys = keys,
                Clicks = clicks,
                LeftClicks = left,
                RightClicks = right,
                MiddleClicks = middle,
                ScrollSteps = scroll,
                MousePixels = pixels,
                NewActiveMinutes = activeMinutes,
                KeyCounts = keyCounts,
                HourlyKeys = hourly,
                WordsPerMinute = ComputeWordsPerMinute(at),
                PeakWordsPerMinute = _peakWpm,
            };
        }
    }

    /// <summary>清空内存缓冲（清空数据 / 退出前调用）。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _buckets.Clear();
            _activeMinutes.Clear();
            _hasPointerAnchor = false;
            _scrollRemainder = 0;
            _peakWpm = 0;
            _keysSincePeakUpdate = 0;
            Array.Clear(_typedPerSecond);
            Array.Clear(_typedSecondStamp);
        }
    }

    private HourBucket GetBucket(DateOnly date, int hour)
    {
        var key = (date, hour);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new HourBucket();
            _buckets[key] = bucket;
        }

        return bucket;
    }

    private void MarkActiveMinute(DateOnly date, int minuteOfDay)
    {
        if (!_activeMinutes.TryGetValue(date, out var set))
        {
            set = new HashSet<int>();
            _activeMinutes[date] = set;
        }

        set.Add(Math.Clamp(minuteOfDay, 0, (24 * 60) - 1));
    }

    private void RecordTyping(DateTimeOffset at)
    {
        var second = at.ToUnixTimeSeconds();
        var index = (int)(((second % WpmWindowSeconds) + WpmWindowSeconds) % WpmWindowSeconds);

        if (_typedSecondStamp[index] != second)
        {
            _typedSecondStamp[index] = second;
            _typedPerSecond[index] = 0;
        }

        _typedPerSecond[index]++;

        if (++_keysSincePeakUpdate >= 5)
        {
            _keysSincePeakUpdate = 0;
            var current = ComputeWordsPerMinuteNoLock(at);
            if (current > _peakWpm)
            {
                _peakWpm = current;
            }
        }
    }

    private double ComputeWordsPerMinute(DateTimeOffset at) => ComputeWordsPerMinuteNoLock(at);

    private double ComputeWordsPerMinuteNoLock(DateTimeOffset at)
    {
        var nowSeconds = at.ToUnixTimeSeconds();
        var typed = 0;
        var oldestAge = 0L;

        for (var i = 0; i < WpmWindowSeconds; i++)
        {
            var stamp = _typedSecondStamp[i];
            if (stamp <= 0)
            {
                continue;
            }

            var age = nowSeconds - stamp;
            if (age < 0 || age >= WpmWindowSeconds)
            {
                continue;
            }

            typed += _typedPerSecond[i];
            if (age > oldestAge)
            {
                oldestAge = age;
            }
        }

        if (typed <= 0)
        {
            return 0;
        }

        var elapsedSeconds = Math.Clamp(oldestAge + 1, 5, WpmWindowSeconds);
        // 常规换算：5 个字符 ≈ 1 个单词。
        return typed / 5.0 / (elapsedSeconds / 60.0);
    }

    private sealed class HourBucket
    {
        public Dictionary<int, int> KeyCounts { get; } = new();

        public Dictionary<string, int> ComboCounts { get; } = new(StringComparer.Ordinal);

        public Dictionary<int, int> ClickZones { get; } = new();

        public double MousePixels { get; set; }

        public int LeftClicks { get; set; }

        public int RightClicks { get; set; }

        public int MiddleClicks { get; set; }

        public int ScrollSteps { get; set; }
    }
}
