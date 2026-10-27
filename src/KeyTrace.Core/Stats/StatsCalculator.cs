using KeyTrace.Core.Models;

namespace KeyTrace.Core.Stats;

/// <summary>纯计算逻辑（无 IO），便于单元测试。</summary>
public static class StatsCalculator
{
    /// <summary>
    /// 连续使用天数：从今天（若今天还没数据则从昨天）往前数连续的活跃日期。
    /// </summary>
    public static int ComputeStreak(IReadOnlyCollection<DateOnly> activeDates, DateOnly today)
    {
        if (activeDates is null || activeDates.Count == 0)
        {
            return 0;
        }

        var set = new HashSet<DateOnly>(activeDates);
        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;

        while (set.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    public static int SumKeys(IEnumerable<KeySummary> keys)
    {
        var total = 0;
        foreach (var item in keys)
        {
            total += item.Count;
        }

        return total;
    }

    public static int MaxCount(IEnumerable<KeySummary> keys)
    {
        var max = 0;
        foreach (var item in keys)
        {
            if (item.Count > max)
            {
                max = item.Count;
            }
        }

        return max;
    }

    /// <summary>把 24 小时的按键数合并成 4 个时段（深夜/上午/下午/晚上），用于概览文案。</summary>
    public static (int Night, int Morning, int Afternoon, int Evening) SplitByPeriod(ReadOnlySpan<int> hourly)
    {
        var night = 0;
        var morning = 0;
        var afternoon = 0;
        var evening = 0;

        for (var hour = 0; hour < hourly.Length && hour < 24; hour++)
        {
            var value = hourly[hour];
            switch (hour)
            {
                case >= 0 and < 6:
                    night += value;
                    break;
                case >= 6 and < 12:
                    morning += value;
                    break;
                case >= 12 and < 18:
                    afternoon += value;
                    break;
                default:
                    evening += value;
                    break;
            }
        }

        return (night, morning, afternoon, evening);
    }

    /// <summary>找出最活跃的小时（0-23），没有数据时返回 -1。</summary>
    public static int BusiestHour(ReadOnlySpan<int> hourly)
    {
        var best = -1;
        var bestValue = 0;
        for (var hour = 0; hour < hourly.Length && hour < 24; hour++)
        {
            if (hourly[hour] > bestValue)
            {
                bestValue = hourly[hour];
                best = hour;
            }
        }

        return best;
    }

    /// <summary>把累计按键数换算成"相当于打了多少本 20 万字的书"。</summary>
    public static double BooksEquivalent(long totalKeys) => totalKeys / 200_000.0;
}
