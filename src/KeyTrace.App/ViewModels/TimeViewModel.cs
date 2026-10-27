using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using KeyTrace.App.Infrastructure;
using KeyTrace.Core;
using KeyTrace.Core.Models;
using KeyTrace.Core.Stats;
using KeyTrace.Core.Storage;

namespace KeyTrace.App.ViewModels;

/// <summary>时间分析：一周 24 小时热力网格 + 月历热力图 + 连续天数。</summary>
public sealed class TimeViewModel : PageViewModel
{
    private readonly IStatsRepository _repository;
    private readonly LiveStatsProvider _live;
    private readonly IClock _clock;

    private int _monthOffset;
    private string _monthTitle = string.Empty;
    private string _weekRangeText = string.Empty;
    private string _streakText = "0 天";
    private string _activeDaysText = "0 天";
    private string _bestDayText = "—";
    private string _averageText = "0 次/天";
    private string _nightText = "0%";
    private string _monthSummary = string.Empty;
    private bool _hasMonthData;

    public TimeViewModel(IStatsRepository repository, LiveStatsProvider live, IClock? clock = null)
        : base("时间分析", "24 小时分布、月历热力图与连续使用天数", "📅")
    {
        _repository = repository;
        _live = live;
        _clock = clock ?? SystemClock.Instance;

        HourLabels = Enumerable.Range(0, 24)
            .Select(static hour => hour % 3 == 0 ? hour.ToString() : string.Empty)
            .ToArray();

        PreviousMonthCommand = new RelayCommand(GoToPreviousMonth);
        NextMonthCommand = new RelayCommand(GoToNextMonth);
    }

    public ObservableCollection<HeatCellViewModel> WeekHeatCells { get; } = new();

    public ObservableCollection<CalendarCellViewModel> CalendarCells { get; } = new();

    public IReadOnlyList<string> HourLabels { get; }

    public IReadOnlyList<string> WeekdayHeaders { get; } = new[] { "一", "二", "三", "四", "五", "六", "日" };

    public ICommand PreviousMonthCommand { get; }

    public ICommand NextMonthCommand { get; }

    public string MonthTitle
    {
        get => _monthTitle;
        private set => SetProperty(ref _monthTitle, value);
    }

    public string WeekRangeText
    {
        get => _weekRangeText;
        private set => SetProperty(ref _weekRangeText, value);
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

    public string BestDayText
    {
        get => _bestDayText;
        private set => SetProperty(ref _bestDayText, value);
    }

    public string AverageText
    {
        get => _averageText;
        private set => SetProperty(ref _averageText, value);
    }

    public string NightText
    {
        get => _nightText;
        private set => SetProperty(ref _nightText, value);
    }

    public string MonthSummary
    {
        get => _monthSummary;
        private set => SetProperty(ref _monthSummary, value);
    }

    public bool HasMonthData
    {
        get => _hasMonthData;
        private set => SetProperty(ref _hasMonthData, value);
    }

    public override Task LoadAsync()
    {
        _monthOffset = 0;
        var today = Today;
        BuildWeekHeat(today);
        BuildMonth(today);
        BuildStats(today);
        return Task.CompletedTask;
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.Now.DateTime);

    private void GoToPreviousMonth()
    {
        _monthOffset--;
        BuildMonth(Today);
    }

    private void GoToNextMonth()
    {
        if (_monthOffset >= 0)
        {
            return;
        }

        _monthOffset++;
        BuildMonth(Today);
    }

    private void BuildWeekHeat(DateOnly today)
    {
        var weekStart = WeekComparison.WeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        var stored = _repository.GetHourlyRange(weekStart, today);
        var live = _live.GetToday();

        var counts = new Dictionary<(DateOnly Date, int Hour), int>();
        foreach (var item in stored)
        {
            if (item.Date != today)
            {
                counts[(item.Date, item.Hour)] = item.KeyCount;
            }
        }

        for (var hour = 0; hour < live.HourlyKeys.Length && hour < 24; hour++)
        {
            counts[(today, hour)] = live.HourlyKeys[hour];
        }

        var max = counts.Count > 0 ? counts.Values.Max() : 0;

        WeekHeatCells.Clear();
        for (var dayIndex = 0; dayIndex < 7; dayIndex++)
        {
            var date = weekStart.AddDays(dayIndex);
            for (var hour = 0; hour < 24; hour++)
            {
                counts.TryGetValue((date, hour), out var count);
                var intensity = max <= 0 || count <= 0 ? 0 : Math.Sqrt(count / (double)max);

                WeekHeatCells.Add(new HeatCellViewModel
                {
                    Background = count == 0 ? HeatPalette.Empty : HeatPalette.ForIntensity(intensity),
                    Tooltip = $"{Format.ShortDate(date)} {Format.Weekday(date)} {hour:00}:00 · {Format.Number(count)} 次",
                });
            }
        }

        WeekRangeText = $"{Format.ShortDate(weekStart)} — {Format.ShortDate(weekEnd)}";
    }

    private void BuildMonth(DateOnly today)
    {
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(_monthOffset);
        var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

        MonthTitle = firstOfMonth.Year == today.Year
            ? $"{firstOfMonth.Month} 月" + (_monthOffset == 0 ? " · 本月" : string.Empty)
            : $"{firstOfMonth.Year} 年 {firstOfMonth.Month} 月";

        var daily = new Dictionary<DateOnly, int>();
        foreach (var stat in _repository.GetDailyRange(firstOfMonth, lastOfMonth))
        {
            daily[stat.Date] = stat.TotalKeys;
        }

        if (_monthOffset == 0)
        {
            daily[today] = _live.GetToday().TotalKeys;
        }

        var max = daily.Count > 0 ? daily.Values.Max() : 0;
        var monthTotal = daily.Values.Sum();
        var activeDays = daily.Count(static pair => pair.Value > 0);

        HasMonthData = max > 0;
        MonthSummary = monthTotal == 0
            ? "本月还没有记录"
            : $"本月合计 {Format.Number(monthTotal)} 次按键 · 活跃 {activeDays} 天";

        var leading = (((int)firstOfMonth.DayOfWeek + 6) % 7);
        var cursor = firstOfMonth.AddDays(-leading);

        CalendarCells.Clear();
        for (var index = 0; index < 42; index++)
        {
            var date = cursor.AddDays(index);
            if (date.Month != firstOfMonth.Month)
            {
                CalendarCells.Add(new CalendarCellViewModel
                {
                    Day = 0,
                    IsToday = false,
                    HasData = false,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Transparent,
                    Tooltip = string.Empty,
                });
                continue;
            }

            daily.TryGetValue(date, out var count);
            var intensity = max <= 0 || count <= 0 ? 0 : Math.Sqrt(count / (double)max);
            var isToday = date == today;

            CalendarCells.Add(new CalendarCellViewModel
            {
                Day = date.Day,
                IsToday = isToday,
                HasData = count > 0,
                Background = count == 0 ? HeatPalette.Empty : HeatPalette.ForIntensity(intensity),
                Foreground = count == 0 ? UiBrushes.TextMuted : UiBrushes.TextPrimary,
                Tooltip = $"{date.Month} 月 {date.Day} 日 · {Format.Number(count)} 次按键",
            });
        }
    }

    private void BuildStats(DateOnly today)
    {
        var recent = _live.GetRecentDays(30);
        var active = recent.Where(static day => day.Keys > 0).ToList();
        var best = recent.OrderByDescending(static day => day.Keys).FirstOrDefault();

        StreakText = $"{_live.GetStreak()} 天";
        ActiveDaysText = $"{_live.GetTotals().ActiveDays} 天";
        BestDayText = best is null || best.Keys == 0
            ? "—"
            : $"{Format.ShortDate(best.Date)} · {Format.Number(best.Keys)} 次";
        AverageText = active.Count == 0
            ? "0 次/天"
            : Format.Number(active.Sum(static day => day.Keys) / active.Count) + " 次/天";

        var lastWeek = _repository.GetHourlyRange(today.AddDays(-6), today);
        var totalKeys = lastWeek.Sum(static item => item.KeyCount);
        var nightKeys = lastWeek.Where(static item => item.Hour is >= 0 and < 5).Sum(static item => item.KeyCount);
        NightText = totalKeys <= 0 ? "0%" : (nightKeys * 100.0 / totalKeys).ToString("0") + "%";
    }
}
