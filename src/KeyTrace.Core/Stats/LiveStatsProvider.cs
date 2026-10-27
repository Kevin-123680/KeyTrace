using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Models;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Storage;

namespace KeyTrace.Core.Stats;

/// <summary>今日实时数据（数据库已落盘部分 + 内存中尚未落盘的部分）。</summary>
public sealed class TodayLive
{
    public DateOnly Date { get; init; }
    public int TotalKeys { get; init; }
    public long MousePixels { get; init; }
    public int TotalClicks { get; init; }
    public int LeftClicks { get; init; }
    public int RightClicks { get; init; }
    public int MiddleClicks { get; init; }
    public int ScrollSteps { get; init; }
    public int ActiveMinutes { get; init; }
    public double MetersPerPixel { get; init; } = Calibration.CalibrationProfile.DefaultMetersPerPixel;
    public double WordsPerMinute { get; init; }
    public double PeakWordsPerMinute { get; init; }
    public int[] HourlyKeys { get; init; } = new int[24];
    public IReadOnlyDictionary<int, int> KeyCounts { get; init; } = new Dictionary<int, int>();

    public double Meters => MousePixels * MetersPerPixel;

    public int BusiestHour => StatsCalculator.BusiestHour(HourlyKeys);
}

/// <summary>把"库里的历史"与"内存里的在途数据"合并成界面直接可用的视图。</summary>
public sealed class LiveStatsProvider
{
    private readonly IStatsRepository _repository;
    private readonly DataAggregator _aggregator;
    private readonly SettingsService _settings;
    private readonly IClock _clock;

    public LiveStatsProvider(
        IStatsRepository repository,
        DataAggregator aggregator,
        SettingsService settings,
        IClock? clock = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? SystemClock.Instance;
    }

    public DateOnly Today => DateOnly.FromDateTime(_clock.Now.DateTime);

    public TodayLive GetToday()
    {
        var today = Today;
        var now = _clock.Now;
        var stored = _repository.GetDaily(today) ?? DailyStat.Empty(today);
        var storedHourly = _repository.GetHourly(today);
        var storedKeys = _repository.GetKeys(today);
        var pending = _aggregator.Snapshot(today, now);

        var hourly = new int[24];
        foreach (var item in storedHourly)
        {
            if (item.Hour is >= 0 and < 24)
            {
                hourly[item.Hour] += item.KeyCount;
            }
        }

        foreach (var pair in pending.HourlyKeys)
        {
            if (pair.Key is >= 0 and < 24)
            {
                hourly[pair.Key] += pair.Value;
            }
        }

        var keyCounts = new Dictionary<int, int>(storedKeys.Count + pending.KeyCounts.Count);
        foreach (var item in storedKeys)
        {
            keyCounts[item.KeyCode] = keyCounts.TryGetValue(item.KeyCode, out var existing)
                ? existing + item.Count
                : item.Count;
        }

        foreach (var pair in pending.KeyCounts)
        {
            keyCounts[pair.Key] = keyCounts.TryGetValue(pair.Key, out var existing)
                ? existing + pair.Value
                : pair.Value;
        }

        return new TodayLive
        {
            Date = today,
            TotalKeys = stored.TotalKeys + pending.Keys,
            MousePixels = stored.MousePixels + pending.MousePixels,
            TotalClicks = stored.TotalClicks + pending.Clicks,
            LeftClicks = stored.LeftClicks + pending.LeftClicks,
            RightClicks = stored.RightClicks + pending.RightClicks,
            MiddleClicks = stored.MiddleClicks + pending.MiddleClicks,
            ScrollSteps = stored.ScrollSteps + pending.ScrollSteps,
            ActiveMinutes = Math.Min(1440, stored.ActiveMinutes + pending.NewActiveMinutes),
            MetersPerPixel = _settings.Current.MetersPerPixel,
            WordsPerMinute = pending.WordsPerMinute,
            PeakWordsPerMinute = pending.PeakWordsPerMinute,
            HourlyKeys = hourly,
            KeyCounts = keyCounts,
        };
    }

    /// <summary>近 N 天（含今天）的每日趋势，今天会叠加上在途数据。</summary>
    public IReadOnlyList<DailyPoint> GetRecentDays(int days)
    {
        var today = Today;
        var from = today.AddDays(-(Math.Clamp(days, 1, 400) - 1));
        var stored = _repository.GetDailyRange(from, today).ToDictionary(static item => item.Date);
        var pending = _aggregator.Snapshot(today, _clock.Now);
        var result = new List<DailyPoint>(days);

        for (var date = from; date <= today; date = date.AddDays(1))
        {
            stored.TryGetValue(date, out var stat);
            var isToday = date == today;

            result.Add(new DailyPoint
            {
                Date = date,
                Keys = (stat?.TotalKeys ?? 0) + (isToday ? pending.Keys : 0),
                MousePixels = (stat?.MousePixels ?? 0) + (isToday ? pending.MousePixels : 0),
                Clicks = (stat?.TotalClicks ?? 0) + (isToday ? pending.Clicks : 0),
                ActiveMinutes = (stat?.ActiveMinutes ?? 0) + (isToday ? pending.NewActiveMinutes : 0),
            });
        }

        return result;
    }

    /// <summary>本周（周一起）与上周的对比。</summary>
    public WeekComparison GetWeekComparison()
    {
        var today = Today;
        var thisWeekStart = WeekComparison.WeekStart(today);
        var lastWeekStart = thisWeekStart.AddDays(-7);
        var lastWeekEnd = thisWeekStart.AddDays(-1);

        var thisWeek = _repository.GetDailyRange(thisWeekStart, today);
        var lastWeek = _repository.GetDailyRange(lastWeekStart, lastWeekEnd);

        var pending = _aggregator.Snapshot(today, _clock.Now);
        var thisKeys = thisWeek.Sum(static item => item.TotalKeys) + pending.Keys;
        var thisPixels = thisWeek.Sum(static item => item.MousePixels) + pending.MousePixels;
        var thisClicks = thisWeek.Sum(static item => item.TotalClicks) + pending.Clicks;
        var thisMinutes = thisWeek.Sum(static item => item.ActiveMinutes) + pending.NewActiveMinutes;

        var thisWeekDays = (today.DayNumber - thisWeekStart.DayNumber) + 1;
        var lastWeekDays = Math.Min(7, thisWeekDays);

        return new WeekComparison
        {
            ThisWeekStart = thisWeekStart,
            LastWeekStart = lastWeekStart,
            ThisWeekKeys = thisKeys,
            LastWeekKeys = lastWeek.Sum(static item => item.TotalKeys),
            ThisWeekPixels = thisPixels,
            LastWeekPixels = lastWeek.Sum(static item => item.MousePixels),
            ThisWeekClicks = thisClicks,
            LastWeekClicks = lastWeek.Sum(static item => item.TotalClicks),
            ThisWeekActiveMinutes = thisMinutes,
            LastWeekActiveMinutes = lastWeek.Take(lastWeekDays).Sum(static item => item.ActiveMinutes),
        };
    }

    public TotalsSummary GetTotals()
    {
        var totals = _repository.GetTotals();
        var pending = _aggregator.Snapshot(Today, _clock.Now);

        return new TotalsSummary
        {
            TotalKeys = totals.TotalKeys + pending.Keys,
            TotalClicks = totals.TotalClicks + pending.Clicks,
            MousePixels = totals.MousePixels + pending.MousePixels,
            MouseMeters = totals.MouseMeters + (pending.MousePixels * _settings.Current.MetersPerPixel),
            ScrollSteps = totals.ScrollSteps + pending.ScrollSteps,
            ActiveDays = totals.ActiveDays,
            FirstDate = totals.FirstDate,
        };
    }

    public int GetStreak()
    {
        var today = Today;
        var dates = _repository.GetActiveDates(today.AddDays(-800), today);
        var list = new List<DateOnly>(dates);
        if (!list.Contains(today) && _aggregator.Snapshot(today, _clock.Now).Keys > 0)
        {
            list.Add(today);
        }

        return StatsCalculator.ComputeStreak(list, today);
    }
}
