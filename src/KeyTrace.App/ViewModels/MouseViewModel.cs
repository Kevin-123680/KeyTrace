using System.Collections.ObjectModel;
using System.Windows.Input;
using KeyTrace.App.Controls;
using KeyTrace.App.Infrastructure;
using KeyTrace.Core;
using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Stats;
using KeyTrace.Core.Storage;

namespace KeyTrace.App.ViewModels;

/// <summary>鼠标分析：近 7 天里程、按键占比、滚轮、点击热区与校准入口。</summary>
public sealed class MouseViewModel : PageViewModel
{
    private readonly IStatsRepository _repository;
    private readonly LiveStatsProvider _live;
    private readonly SettingsService _settings;
    private readonly IClock _clock;

    private IReadOnlyList<double> _weekSeries = Array.Empty<double>();
    private IReadOnlyList<string> _weekLabels = Array.Empty<string>();
    private IReadOnlyList<DonutSegment> _segments = Array.Empty<DonutSegment>();
    private string _todayMetersText = "0 m";
    private string _weekMetersText = "0 m";
    private string _monthMetersText = "0 m";
    private string _averageText = "0 m";
    private string _leftText = "0";
    private string _rightText = "0";
    private string _middleText = "0";
    private string _scrollText = "0";
    private string _pixelsText = "0";
    private string _calibrationText = string.Empty;
    private string _weekRangeText = string.Empty;
    private bool _hasClickZones;
    private bool _hasData;
    private string _zoneEmptyText = "在设置中开启「记录点击热区」后，这里会显示点击分布";

    public MouseViewModel(
        IStatsRepository repository,
        LiveStatsProvider live,
        SettingsService settings,
        IClock? clock = null)
        : base("鼠标分析", "距离按相邻采样点累加，DPI 校准后换算成米", "🖱")
    {
        _repository = repository;
        _live = live;
        _settings = settings;
        _clock = clock ?? SystemClock.Instance;

        OpenCalibrationCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>用户点击"去校准"时请求主窗口切到设置页。</summary>
    public event EventHandler? SettingsRequested;

    public ICommand OpenCalibrationCommand { get; }

    public ObservableCollection<HeatCellViewModel> ZoneCells { get; } = new();

    public IReadOnlyList<string> WeekLabels
    {
        get => _weekLabels;
        private set => SetProperty(ref _weekLabels, value);
    }

    public IReadOnlyList<double> WeekSeries
    {
        get => _weekSeries;
        private set => SetProperty(ref _weekSeries, value);
    }

    public IReadOnlyList<DonutSegment> Segments
    {
        get => _segments;
        private set => SetProperty(ref _segments, value);
    }

    public string TodayMetersText
    {
        get => _todayMetersText;
        private set => SetProperty(ref _todayMetersText, value);
    }

    public string WeekMetersText
    {
        get => _weekMetersText;
        private set => SetProperty(ref _weekMetersText, value);
    }

    public string MonthMetersText
    {
        get => _monthMetersText;
        private set => SetProperty(ref _monthMetersText, value);
    }

    public string AverageText
    {
        get => _averageText;
        private set => SetProperty(ref _averageText, value);
    }

    public string LeftText
    {
        get => _leftText;
        private set => SetProperty(ref _leftText, value);
    }

    public string RightText
    {
        get => _rightText;
        private set => SetProperty(ref _rightText, value);
    }

    public string MiddleText
    {
        get => _middleText;
        private set => SetProperty(ref _middleText, value);
    }

    public string ScrollText
    {
        get => _scrollText;
        private set => SetProperty(ref _scrollText, value);
    }

    public string PixelsText
    {
        get => _pixelsText;
        private set => SetProperty(ref _pixelsText, value);
    }

    public string CalibrationText
    {
        get => _calibrationText;
        private set => SetProperty(ref _calibrationText, value);
    }

    public string WeekRangeText
    {
        get => _weekRangeText;
        private set => SetProperty(ref _weekRangeText, value);
    }

    public bool HasClickZones
    {
        get => _hasClickZones;
        private set => SetProperty(ref _hasClickZones, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    public string ZoneEmptyText
    {
        get => _zoneEmptyText;
        private set => SetProperty(ref _zoneEmptyText, value);
    }

    public override Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(_clock.Now.DateTime);
        var metersPerPixel = _settings.Current.MetersPerPixel;
        var live = _live.GetToday();

        TodayMetersText = Format.Distance(live.Meters);
        PixelsText = Format.Number(live.MousePixels) + " px";
        LeftText = Format.Number(live.LeftClicks);
        RightText = Format.Number(live.RightClicks);
        MiddleText = Format.Number(live.MiddleClicks);
        ScrollText = Format.Number(live.ScrollSteps);
        CalibrationText = $"当前换算：{metersPerPixel * 1000:0.000} 毫米/像素 · 约 {(metersPerPixel > 0 ? 0.0254 / metersPerPixel : 0):0} DPI";
        HasData = live.TotalClicks > 0 || live.MousePixels > 0;

        var week = _live.GetRecentDays(7);
        WeekSeries = week.Select(day => day.MousePixels * metersPerPixel).ToArray();
        WeekLabels = week.Select(static day => Format.Weekday(day.Date)[1..]).ToArray();
        WeekRangeText = $"{Format.ShortDate(week[0].Date)} — {Format.ShortDate(week[^1].Date)}";

        var weekMeters = week.Sum(static day => day.MousePixels) * metersPerPixel;
        var month = _live.GetRecentDays(30);
        var monthMeters = month.Sum(static day => day.MousePixels) * metersPerPixel;

        WeekMetersText = Format.Distance(weekMeters);
        MonthMetersText = Format.Distance(monthMeters);
        AverageText = Format.Distance(monthMeters / 30.0);

        Segments = new[]
        {
            new DonutSegment { Label = "左键", Value = live.LeftClicks, Brush = UiBrushes.Accent },
            new DonutSegment { Label = "右键", Value = live.RightClicks, Brush = UiBrushes.Warning },
            new DonutSegment { Label = "中键", Value = live.MiddleClicks, Brush = UiBrushes.Violet },
        };

        BuildZones(today);
        return Task.CompletedTask;
    }

    public override void OnLiveTick(TodayLive live)
    {
        TodayMetersText = Format.Distance(live.Meters);
        PixelsText = Format.Number(live.MousePixels) + " px";
        LeftText = Format.Number(live.LeftClicks);
        RightText = Format.Number(live.RightClicks);
        MiddleText = Format.Number(live.MiddleClicks);
        ScrollText = Format.Number(live.ScrollSteps);
        HasData = live.TotalClicks > 0 || live.MousePixels > 0;

        Segments = new[]
        {
            new DonutSegment { Label = "左键", Value = live.LeftClicks, Brush = UiBrushes.Accent },
            new DonutSegment { Label = "右键", Value = live.RightClicks, Brush = UiBrushes.Warning },
            new DonutSegment { Label = "中键", Value = live.MiddleClicks, Brush = UiBrushes.Violet },
        };
    }

    private void BuildZones(DateOnly today)
    {
        ZoneCells.Clear();

        if (!_settings.Current.CaptureClickZones)
        {
            HasClickZones = false;
            ZoneEmptyText = "在设置中开启「记录点击热区」后，这里会显示点击分布";
            return;
        }

        var zones = _repository.GetClickZones(today.AddDays(-29), today);
        if (zones.Count == 0)
        {
            HasClickZones = true;
            ZoneEmptyText = "还没有记录到点击位置";
            return;
        }

        var counts = new int[ClickZoneGrid.Columns * ClickZoneGrid.Rows];
        foreach (var zone in zones)
        {
            var index = (zone.YBucket * ClickZoneGrid.Columns) + zone.XBucket;
            if (index >= 0 && index < counts.Length)
            {
                counts[index] += zone.Count;
            }
        }

        var max = counts.Max();
        for (var index = 0; index < counts.Length; index++)
        {
            var count = counts[index];
            var intensity = max <= 0 ? 0 : Math.Sqrt(count / (double)max);
            var (column, row) = ClickZoneGrid.ToCell(index);

            ZoneCells.Add(new HeatCellViewModel
            {
                Background = count == 0 ? HeatPalette.Empty : HeatPalette.ForIntensity(intensity),
                Tooltip = $"横向 {column * 5}-{(column + 1) * 5}% · 纵向 {row * 10}-{(row + 1) * 10}% · {Format.Number(count)} 次",
            });
        }

        HasClickZones = true;
    }
}
