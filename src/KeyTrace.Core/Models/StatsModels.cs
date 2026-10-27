using System.Globalization;

namespace KeyTrace.Core.Models;

/// <summary>鼠标按键。</summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

/// <summary>一天的汇总数据，对应 <c>daily_stats</c> 表。</summary>
public sealed class DailyStat
{
    public DateOnly Date { get; init; }
    public int TotalKeys { get; init; }
    public double MouseMeters { get; init; }
    public int TotalClicks { get; init; }
    public int ActiveMinutes { get; init; }
    public long MousePixels { get; init; }
    public int LeftClicks { get; init; }
    public int RightClicks { get; init; }
    public int MiddleClicks { get; init; }
    public int ScrollSteps { get; init; }

    public static DailyStat Empty(DateOnly date) => new() { Date = date };
}

/// <summary>某个按键在某天的计数，对应 <c>key_daily</c> 表。</summary>
public sealed class KeyDailyStat
{
    public DateOnly Date { get; init; }
    public int KeyCode { get; init; }
    public string KeyName { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>区间内某个按键的总计数（跨天的汇总视图）。</summary>
public sealed class KeySummary
{
    public int KeyCode { get; init; }
    public string KeyName { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>区间内某个快捷键的总计数（跨天的汇总视图）。</summary>
public sealed class ComboSummary
{
    public string Combo { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>某天某个小时的汇总，对应 <c>hourly_dist</c> 表。</summary>
public sealed class HourlyStat
{
    public DateOnly Date { get; init; }
    public int Hour { get; init; }
    public int KeyCount { get; init; }
    public double MouseMeters { get; init; }
    public long MousePixels { get; init; }
}

/// <summary>快捷键组合计数，对应 <c>combo_daily</c> 表。</summary>
public sealed class ComboStat
{
    public DateOnly Date { get; init; }
    public string Combo { get; init; } = string.Empty;
    public int Count { get; init; }
}

/// <summary>屏幕点击热区（按 5% 屏幕格归一化）。</summary>
public sealed class ClickZoneStat
{
    public DateOnly Date { get; init; }
    public int XBucket { get; init; }
    public int YBucket { get; init; }
    public int Count { get; init; }
}

/// <summary>已解锁成就。</summary>
public sealed class AchievementRecord
{
    public string Id { get; init; } = string.Empty;
    public DateTimeOffset UnlockedAt { get; init; }
}

/// <summary>累计统计。</summary>
public sealed class TotalsSummary
{
    public long TotalKeys { get; init; }
    public long TotalClicks { get; init; }
    public long MousePixels { get; init; }
    public double MouseMeters { get; init; }
    public int ScrollSteps { get; init; }
    public int ActiveDays { get; init; }
    public DateOnly? FirstDate { get; init; }

    public static TotalsSummary Empty => new();
}

/// <summary>单日趋势点（用于折线图）。</summary>
public sealed class DailyPoint
{
    public DateOnly Date { get; init; }
    public int Keys { get; init; }
    public long MousePixels { get; init; }
    public int Clicks { get; init; }
    public int ActiveMinutes { get; init; }
}

/// <summary>本周 vs 上周对比。</summary>
public sealed class WeekComparison
{
    public DateOnly ThisWeekStart { get; init; }
    public DateOnly LastWeekStart { get; init; }
    public int ThisWeekKeys { get; init; }
    public int LastWeekKeys { get; init; }
    public long ThisWeekPixels { get; init; }
    public long LastWeekPixels { get; init; }
    public int ThisWeekClicks { get; init; }
    public int LastWeekClicks { get; init; }
    public int ThisWeekActiveMinutes { get; init; }
    public int LastWeekActiveMinutes { get; init; }

    public double KeysDeltaPercent => Percent(ThisWeekKeys, LastWeekKeys);
    public double PixelsDeltaPercent => Percent(ThisWeekPixels, LastWeekPixels);
    public double ClicksDeltaPercent => Percent(ThisWeekClicks, LastWeekClicks);
    public double ActiveMinutesDeltaPercent => Percent(ThisWeekActiveMinutes, LastWeekActiveMinutes);

    /// <summary>相对变化百分比；上周为空时返回 0，避免出现 "∞%"。</summary>
    public static double Percent(double current, double previous)
    {
        if (previous <= 0)
        {
            return 0;
        }

        return (current - previous) / previous * 100.0;
    }

    public static DateOnly WeekStart(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7; // 周一为一周起点
        return date.AddDays(-offset);
    }

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
