using System.Collections.ObjectModel;
using System.Windows.Media;
using KeyTrace.App.Infrastructure;
using KeyTrace.Core;
using KeyTrace.Core.Achievements;
using KeyTrace.Core.Models;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Stats;


namespace KeyTrace.App.ViewModels;

/// <summary>今日概览：四个大数字 + 24 小时趋势 + 本周对比 + 成就。</summary>
public sealed class DashboardViewModel : PageViewModel
{

    private readonly LiveStatsProvider _live;
    private readonly SettingsService _settings;
    private readonly AchievementService _achievements;
    private readonly IClock _clock;

    private IReadOnlyList<double> _hourlySeries = Array.Empty<double>();
    private string _todayText = string.Empty;
    private string _hourlySummary = string.Empty;
    private string _wpmText = "0";
    private string _peakWpmText = "0";
    private double _goalProgress;
    private string _goalText = string.Empty;
    private string _goalPercentText = "0%";
    private bool _hasGoal;
    private string _streakText = "0 天";
    private string _activeDaysText = "0 天";
    private string _totalKeysText = "0";
    private string _totalDistanceText = "0 m";
    private string _booksText = string.Empty;
    private string _unlockedSummary = string.Empty;
    private string _weekRangeText = string.Empty;
    private string _weekHeadline = string.Empty;

    public DashboardViewModel(
        LiveStatsProvider live,
        SettingsService settings,
        AchievementService achievements,
        IClock? clock = null)
        : base("今日概览", "今天敲了多少、移动了多远，一眼看清", "🏠")
    {
        _live = live;
        _settings = settings;
        _achievements = achievements;
        _clock = clock ?? SystemClock.Instance;

        Cards = new ObservableCollection<StatCardViewModel>
        {
            new("今日按键", "次", "⌨", UiBrushes.Accent),
            new("鼠标移动", string.Empty, "🖱", UiBrushes.Success),
            new("鼠标点击", "次", "👆", UiBrushes.Warning),
            new("活跃时长", string.Empty, "⏱", UiBrushes.Violet),
        };

        HourlyLabels = Enumerable.Range(0, 24).Select(static hour => hour.ToString()).ToArray();
    }

    public ObservableCollection<StatCardViewModel> Cards { get; }

    public ObservableCollection<WeekMetricViewModel> WeekMetrics { get; } = new();

    public ObservableCollection<AchievementItemViewModel> Achievements { get; } = new();

    public IReadOnlyList<string> HourlyLabels { get; }

    public IReadOnlyList<double> HourlySeries
    {
        get => _hourlySeries;
        private set => SetProperty(ref _hourlySeries, value);
    }

    public string TodayText
    {
        get => _todayText;
        private set => SetProperty(ref _todayText, value);
    }

    public string HourlySummary
    {
        get => _hourlySummary;
        private set => SetProperty(ref _hourlySummary, value);
    }

    public string WpmText
    {
        get => _wpmText;
        private set => SetProperty(ref _wpmText, value);
    }

    public string PeakWpmText
    {
        get => _peakWpmText;
        private set => SetProperty(ref _peakWpmText, value);
    }

    public double GoalProgress
    {
        get => _goalProgress;
        private set => SetProperty(ref _goalProgress, value);
    }

    public string GoalText
    {
        get => _goalText;
        private set => SetProperty(ref _goalText, value);
    }

    public string GoalPercentText
    {
        get => _goalPercentText;
        private set => SetProperty(ref _goalPercentText, value);
    }

    public bool HasGoal
    {
        get => _hasGoal;
        private set => SetProperty(ref _hasGoal, value);
    }

    public string StreakText
    {
        get => _streakText;
        private set => SetProperty(ref _streakText, value);
    }

    public string ActiveDaysText
    {
        get => _activeDaysText;
        private set => SetProperty(ref _activeDaysText, value);
    }

    public string TotalKeysText
    {
        get => _totalKeysText;
        private set => SetProperty(ref _totalKeysText, value);
    }

    public string TotalDistanceText
    {
        get => _totalDistanceText;
        private set => SetProperty(ref _totalDistanceText, value);
    }

    public string BooksText
    {
        get => _booksText;
        private set => SetProperty(ref _booksText, value);
    }

    public string UnlockedSummary
    {
        get => _unlockedSummary;
        private set => SetProperty(ref _unlockedSummary, value);
    }

    public string WeekRangeText
    {
        get => _weekRangeText;
        private set => SetProperty(ref _weekRangeText, value);
    }

    public string WeekHeadline
    {
        get => _weekHeadline;
        private set => SetProperty(ref _weekHeadline, value);
    }

    public override Task LoadAsync()
    {
        var now = _clock.Now;
        var today = DateOnly.FromDateTime(now.DateTime);
        var live = _live.GetToday();
        var totals = _live.GetTotals();
        var streak = _live.GetStreak();
        var comparison = _live.GetWeekComparison();
        var metersPerPixel = _settings.Current.MetersPerPixel;

        TodayText = $"{Format.ChineseDate(today)} · {Format.Weekday(today)}";
        ApplyLive(live);

        var totalMeters = totals.MousePixels * metersPerPixel;
        StreakText = $"{streak} 天";
        ActiveDaysText = $"{totals.ActiveDays} 天";
        TotalKeysText = Format.Number(totals.TotalKeys);
        TotalDistanceText = Format.Distance(totalMeters);
        BooksText = $"相当于写完 {StatsCalculator.BooksEquivalent(totals.TotalKeys):0.#} 本 20 万字的书";

        WeekRangeText = $"{Format.ShortDate(comparison.ThisWeekStart)} — {Format.ShortDate(today)} · 与上周同期对比";
        WeekHeadline = comparison.KeysDeltaPercent >= 0
            ? $"本周比上周多敲了 {Format.Number(Math.Max(0, comparison.ThisWeekKeys - comparison.LastWeekKeys))} 次"
            : $"本周比上周少敲了 {Format.Number(Math.Max(0, comparison.LastWeekKeys - comparison.ThisWeekKeys))} 次";

        BuildWeekMetrics(comparison);
        BuildAchievements(now, live, totals, streak, totalMeters);

        return Task.CompletedTask;
    }

    public override void OnLiveTick(TodayLive live)
    {
        ApplyLive(live);
        HourlySummary = BuildHourlySummary(live.HourlyKeys);

        // 在途数据也要反映到"累计"卡片，否则会出现"今日 > 累计"的观感。
        var totals = _live.GetTotals();
        var metersPerPixel = _settings.Current.MetersPerPixel;
        TotalKeysText = Format.Number(totals.TotalKeys);
        TotalDistanceText = Format.Distance(totals.MousePixels * metersPerPixel);
        ActiveDaysText = $"{totals.ActiveDays} 天";
        BooksText = $"相当于写完 {StatsCalculator.BooksEquivalent(totals.TotalKeys):0.#} 本 20 万字的书";
    }

    private void ApplyLive(TodayLive live)
    {
        var goal = _settings.Current.DailyKeyGoal;
        var metersPerPixel = _settings.Current.MetersPerPixel;
        var dpi = metersPerPixel > 0 ? 0.0254 / metersPerPixel : 0;

        Cards[0].Value = Format.Number(live.TotalKeys);
        Cards[0].Caption = goal > 0 ? $"每日目标 {Format.Number(goal)} 次" : "未设置每日目标";

        Cards[1].Value = Format.Distance(live.Meters);
        Cards[1].Caption = $"校准约 {dpi:0} DPI";

        Cards[2].Value = Format.Number(live.TotalClicks);
        Cards[2].Caption = $"左 {Format.Number(live.LeftClicks)} · 右 {Format.Number(live.RightClicks)} · 中 {Format.Number(live.MiddleClicks)}";

        Cards[3].Value = Format.Duration(live.ActiveMinutes);
        Cards[3].Caption = "按有输入的分钟累计";

        HourlySeries = live.HourlyKeys.Select(static value => (double)value).ToArray();
        HourlySummary = BuildHourlySummary(live.HourlyKeys);
        WpmText = live.WordsPerMinute.ToString("0");
        PeakWpmText = live.PeakWordsPerMinute.ToString("0");

        HasGoal = goal > 0;
        if (goal > 0)
        {
            var ratio = Math.Clamp(live.TotalKeys / (double)goal, 0, 1);
            GoalProgress = ratio * 100;
            GoalPercentText = (ratio * 100).ToString("0") + "%";
            GoalText = $"{Format.Number(live.TotalKeys)} / {Format.Number(goal)} 次";
        }
        else
        {
            GoalProgress = 0;
            GoalPercentText = "0%";
            GoalText = "在设置页可以开启每日小目标";
        }
    }

    private static string BuildHourlySummary(IReadOnlyList<int> hourly)
    {
        var bestHour = -1;
        var bestValue = 0;
        for (var hour = 0; hour < hourly.Count && hour < 24; hour++)
        {
            if (hourly[hour] > bestValue)
            {
                bestValue = hourly[hour];
                bestHour = hour;
            }
        }

        return bestHour < 0
            ? "今天还没有记录，敲几下键盘试试"
            : $"最活跃 {bestHour:00}:00 · 峰值 {Format.Number(bestValue)} 次";
    }

    private void BuildWeekMetrics(WeekComparison comparison)
    {
        WeekMetrics.Clear();
        WeekMetrics.Add(Metric("按键", Format.Number(comparison.ThisWeekKeys), Format.Number(comparison.LastWeekKeys), comparison.KeysDeltaPercent));
        WeekMetrics.Add(Metric("鼠标里程", Format.Distance(comparison.ThisWeekPixels * _settings.Current.MetersPerPixel), Format.Distance(comparison.LastWeekPixels * _settings.Current.MetersPerPixel), comparison.PixelsDeltaPercent));
        WeekMetrics.Add(Metric("点击", Format.Number(comparison.ThisWeekClicks), Format.Number(comparison.LastWeekClicks), comparison.ClicksDeltaPercent));
        WeekMetrics.Add(Metric("活跃时长", Format.Duration(comparison.ThisWeekActiveMinutes), Format.Duration(comparison.LastWeekActiveMinutes), comparison.ActiveMinutesDeltaPercent));
    }

    private static WeekMetricViewModel Metric(string label, string thisWeek, string lastWeek, double delta) => new()
    {
        Label = label,
        ThisWeek = thisWeek,
        LastWeek = lastWeek,
        DeltaText = Format.Percent(delta),
        DeltaBrush = HeatPalette.ForDelta(delta),
    };

    private void BuildAchievements(DateTimeOffset now, TodayLive live, TotalsSummary totals, int streak, double totalMeters)
    {
        var night = live.HourlyKeys.Take(5).Sum();
        var early = live.HourlyKeys.Skip(5).Take(2).Sum();

        var context = new AchievementContext
        {
            Date = live.Date,
            TodayKeys = live.TotalKeys,
            TodayClicks = live.TotalClicks,
            TodayMeters = live.Meters,
            NightKeys = night,
            EarlyKeys = early,
            PeakWordsPerMinute = live.PeakWordsPerMinute,
            TotalKeys = totals.TotalKeys,
            TotalMeters = totalMeters,
            ActiveDays = totals.ActiveDays,
            Streak = streak,
        };

        var statuses = _achievements.Evaluate(context, now);

        Achievements.Clear();
        foreach (var status in statuses.OrderByDescending(static item => item.IsUnlocked))
        {
            Achievements.Add(new AchievementItemViewModel
            {
                Glyph = status.Definition.Glyph,
                Title = status.Definition.Title,
                Description = status.Definition.Description,
                IsUnlocked = status.IsUnlocked,
                Background = status.IsUnlocked ? UiBrushes.AccentSoft : HeatPalette.Locked,
                Border = status.IsUnlocked ? UiBrushes.Accent : UiBrushes.Border,
                Foreground = status.IsUnlocked ? UiBrushes.TextPrimary : UiBrushes.TextMuted,
            });
        }

        UnlockedSummary = $"已解锁 {statuses.Count(static item => item.IsUnlocked)} / {statuses.Count}";
    }
}
