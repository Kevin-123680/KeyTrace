using System.Windows.Media;
using KeyTrace.App.Infrastructure;

namespace KeyTrace.App.ViewModels;

/// <summary>概览页的大数字卡片。</summary>
public sealed class StatCardViewModel : ObservableObject
{
    private string _value = "0";
    private string _caption = string.Empty;

    public StatCardViewModel(string title, string unit, string glyph, Brush accent)
    {
        Title = title;
        Unit = unit;
        Glyph = glyph;
        Accent = accent;
    }

    public string Title { get; }

    public string Unit { get; }

    public string Glyph { get; }

    public Brush Accent { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Caption
    {
        get => _caption;
        set => SetProperty(ref _caption, value);
    }
}

/// <summary>本周 / 上周指标对比行。</summary>
public sealed class WeekMetricViewModel
{
    public required string Label { get; init; }

    public required string ThisWeek { get; init; }

    public required string LastWeek { get; init; }

    public required string DeltaText { get; init; }

    public required Brush DeltaBrush { get; init; }
}

/// <summary>成就徽章。</summary>
public sealed class AchievementItemViewModel
{
    public required string Glyph { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required bool IsUnlocked { get; init; }

    public required Brush Background { get; init; }

    public required Brush Border { get; init; }

    public required Brush Foreground { get; init; }

    public string Tooltip => IsUnlocked ? $"已解锁 · {Title}" : $"未解锁 · {Description}";
}

/// <summary>键盘热图上的一个键。</summary>
public sealed class KeyHeatItemViewModel
{
    public required int VirtualKey { get; init; }

    public required string Label { get; init; }

    public required int Count { get; init; }

    public required double Width { get; init; }

    public required Brush Background { get; init; }

    public required Brush Foreground { get; init; }

    public required Brush Border { get; init; }

    public string Tooltip => Count > 0 ? $"{Label} · {Format.Number(Count)} 次" : $"{Label} · 未使用";
}

/// <summary>横向排行条（Top 按键 / 快捷键）。</summary>
public sealed class RankBarItemViewModel
{
    public required int Rank { get; init; }

    public required string Name { get; init; }

    public required int Count { get; init; }

    public required double Ratio { get; init; }

    public string CountText => Format.Number(Count) + " 次";
}

/// <summary>7 × 24 热力网格的一个格子。</summary>
public sealed class HeatCellViewModel
{
    public required Brush Background { get; init; }

    public required string Tooltip { get; init; }
}

/// <summary>月历中的一天。</summary>
public sealed class CalendarCellViewModel
{
    public required int Day { get; init; }

    public required bool IsToday { get; init; }

    public required bool HasData { get; init; }

    public required Brush Background { get; init; }

    public required Brush Foreground { get; init; }

    public required string Tooltip { get; init; }

    public string DayText => Day > 0 ? Day.ToString() : string.Empty;

    public bool IsEmpty => Day <= 0;
}

/// <summary>键盘行容器（每行 15 个单位宽度）。</summary>
public sealed class KeyRowViewModel
{
    public required IReadOnlyList<KeyHeatItemViewModel> Keys { get; init; }
}
