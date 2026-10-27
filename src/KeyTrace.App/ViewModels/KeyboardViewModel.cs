using System.Collections.ObjectModel;
using KeyTrace.App.Infrastructure;
using KeyTrace.Core;
using KeyTrace.Core.Stats;
using KeyTrace.Core.Storage;
using KeyTrace.Core.Text;

namespace KeyTrace.App.ViewModels;

/// <summary>键盘分析：QWERTY 热图 + Top10 + 快捷键。</summary>
public sealed class KeyboardViewModel : PageViewModel
{
    private const double UnitWidth = 46;
    private const double KeyGap = 5;

    private readonly IStatsRepository _repository;
    private readonly LiveStatsProvider _live;
    private readonly IClock _clock;
    private readonly int _layoutKeyCount;

    private string _todayKeysText = "0";
    private string _uniqueKeysText = "0 / 0";
    private string _coverageText = "0%";
    private string _wpmText = "0";
    private string _peakWpmText = "0";
    private string _topKeyText = "—";
    private string _rangeText = string.Empty;
    private bool _hasData;
    private bool _hasCombos;
    private int _lastBuiltTotal = -1;

    public KeyboardViewModel(IStatsRepository repository, LiveStatsProvider live, IClock? clock = null)
        : base("键盘分析", "只统计按键码，不记录任何字符内容", "⌨")
    {
        _repository = repository;
        _live = live;
        _clock = clock ?? SystemClock.Instance;
        _layoutKeyCount = KeyboardLayout.Rows
            .SelectMany(static row => row)
            .Select(static key => key.VirtualKey)
            .Distinct()
            .Count();
    }

    public ObservableCollection<KeyRowViewModel> KeyRows { get; } = new();

    public ObservableCollection<RankBarItemViewModel> TopKeys { get; } = new();

    public ObservableCollection<RankBarItemViewModel> TopCombos { get; } = new();

    public string TodayKeysText
    {
        get => _todayKeysText;
        private set => SetProperty(ref _todayKeysText, value);
    }

    public string UniqueKeysText
    {
        get => _uniqueKeysText;
        private set => SetProperty(ref _uniqueKeysText, value);
    }

    public string CoverageText
    {
        get => _coverageText;
        private set => SetProperty(ref _coverageText, value);
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

    public string TopKeyText
    {
        get => _topKeyText;
        private set => SetProperty(ref _topKeyText, value);
    }

    public string RangeText
    {
        get => _rangeText;
        private set => SetProperty(ref _rangeText, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set => SetProperty(ref _hasData, value);
    }

    public bool HasCombos
    {
        get => _hasCombos;
        private set => SetProperty(ref _hasCombos, value);
    }

    public override Task LoadAsync()
    {
        var live = _live.GetToday();
        var today = live.Date;

        RangeText = $"{Format.ChineseDate(today)} · {Format.Weekday(today)}";
        TodayKeysText = Format.Number(live.TotalKeys);
        WpmText = live.WordsPerMinute.ToString("0");
        PeakWpmText = live.PeakWordsPerMinute.ToString("0");

        BuildHeatmap(live);
        BuildTopKeys(live);
        BuildCombos(today);
        UpdateCoverage(live);

        HasData = live.TotalKeys > 0;
        _lastBuiltTotal = live.TotalKeys;

        return Task.CompletedTask;
    }

    public override void OnLiveTick(TodayLive live)
    {
        WpmText = live.WordsPerMinute.ToString("0");
        PeakWpmText = live.PeakWordsPerMinute.ToString("0");

        if (live.TotalKeys != _lastBuiltTotal)
        {
            TodayKeysText = Format.Number(live.TotalKeys);
            BuildHeatmap(live);
            BuildTopKeys(live);
            UpdateCoverage(live);
            HasData = live.TotalKeys > 0;
            _lastBuiltTotal = live.TotalKeys;
        }
    }

    private void BuildHeatmap(TodayLive live)
    {
        var counts = live.KeyCounts;
        var max = counts.Count > 0 ? counts.Values.Max() : 0;

        KeyRows.Clear();
        foreach (var row in KeyboardLayout.Rows)
        {
            var items = new List<KeyHeatItemViewModel>(row.Count);
            foreach (var key in row)
            {
                counts.TryGetValue(key.VirtualKey, out var count);
                var intensity = max <= 0 || count <= 0
                    ? 0
                    : 0.18 + (0.82 * Math.Sqrt(count / (double)max));

                items.Add(new KeyHeatItemViewModel
                {
                    VirtualKey = key.VirtualKey,
                    Label = key.Label,
                    Count = count,
                    Width = (key.WidthUnits * UnitWidth) - KeyGap,
                    Background = HeatPalette.ForIntensity(intensity),
                    Foreground = count > 0 ? UiBrushes.TextPrimary : UiBrushes.TextMuted,
                    Border = count > 0 ? UiBrushes.Accent : UiBrushes.Border,
                });
            }

            KeyRows.Add(new KeyRowViewModel { Keys = items });
        }
    }

    private void BuildTopKeys(TodayLive live)
    {
        var counts = live.KeyCounts;
        TopKeys.Clear();

        if (counts.Count == 0)
        {
            TopKeyText = "—";
            return;
        }

        var max = counts.Values.Max();
        var top = counts
            .OrderByDescending(static pair => pair.Value)
            .ThenBy(static pair => pair.Key)
            .Take(10)
            .ToList();

        for (var index = 0; index < top.Count; index++)
        {
            var pair = top[index];
            TopKeys.Add(new RankBarItemViewModel
            {
                Rank = index + 1,
                Name = KeyNameMap.GetName(pair.Key),
                Count = pair.Value,
                Ratio = max <= 0 ? 0 : pair.Value / (double)max,
            });
        }

        TopKeyText = KeyNameMap.GetName(top[0].Key);
    }

    private void BuildCombos(DateOnly today)
    {
        TopCombos.Clear();
        var combos = _repository.GetComboTotals(today.AddDays(-6), today, 8);

        if (combos.Count == 0)
        {
            HasCombos = false;
            return;
        }

        var max = combos.Max(static item => item.Count);
        for (var index = 0; index < combos.Count; index++)
        {
            var item = combos[index];
            TopCombos.Add(new RankBarItemViewModel
            {
                Rank = index + 1,
                Name = item.Combo,
                Count = item.Count,
                Ratio = max <= 0 ? 0 : item.Count / (double)max,
            });
        }

        HasCombos = true;
    }

    private void UpdateCoverage(TodayLive live)
    {
        var used = live.KeyCounts.Count(static pair => pair.Value > 0);
        UniqueKeysText = $"{used} / {_layoutKeyCount}";
        var ratio = _layoutKeyCount <= 0 ? 0 : Math.Min(1.0, used / (double)_layoutKeyCount);
        CoverageText = (ratio * 100).ToString("0") + "%";
    }
}
