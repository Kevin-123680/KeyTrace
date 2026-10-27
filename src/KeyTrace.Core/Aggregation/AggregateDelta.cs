namespace KeyTrace.Core.Aggregation;

/// <summary>单个小时的增量聚合，落盘时以 UPSERT 累加。</summary>
public sealed class HourAggregate
{
    public DateOnly Date { get; init; }

    public int Hour { get; init; }

    public IReadOnlyDictionary<int, int> KeyCounts { get; init; } = new Dictionary<int, int>();

    public IReadOnlyDictionary<string, int> ComboCounts { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<int, int> ClickZones { get; init; } = new Dictionary<int, int>();

    public long MousePixels { get; init; }

    public int LeftClicks { get; init; }

    public int RightClicks { get; init; }

    public int MiddleClicks { get; init; }

    public int ScrollSteps { get; init; }

    public int ClickCount => LeftClicks + RightClicks + MiddleClicks;

    public int KeyCount
    {
        get
        {
            var total = 0;
            foreach (var count in KeyCounts.Values)
            {
                total += count;
            }

            return total;
        }
    }
}

/// <summary>一次落盘的完整增量。</summary>
public sealed class AggregateDelta
{
    public IReadOnlyList<HourAggregate> Hours { get; init; } = Array.Empty<HourAggregate>();

    /// <summary>日期 → 新增的活跃分钟（当天第几分钟，0..1439）。</summary>
    public IReadOnlyDictionary<DateOnly, IReadOnlyList<int>> ActiveMinutes { get; init; } =
        new Dictionary<DateOnly, IReadOnlyList<int>>();

    public DateTimeOffset CapturedAt { get; init; }

    public bool IsEmpty => Hours.Count == 0 && ActiveMinutes.Count == 0;

    public static AggregateDelta Empty(DateTimeOffset at) => new() { CapturedAt = at };
}

/// <summary>尚未落盘的在途数据，供界面实时显示。</summary>
public sealed class AggregatorSnapshot
{
    public DateOnly Date { get; init; }

    public int Keys { get; init; }

    public int Clicks { get; init; }

    public int LeftClicks { get; init; }

    public int RightClicks { get; init; }

    public int MiddleClicks { get; init; }

    public int ScrollSteps { get; init; }

    public long MousePixels { get; init; }

    public int NewActiveMinutes { get; init; }

    public IReadOnlyDictionary<int, int> KeyCounts { get; init; } = new Dictionary<int, int>();

    public IReadOnlyDictionary<int, int> HourlyKeys { get; init; } = new Dictionary<int, int>();

    public double WordsPerMinute { get; init; }

    public double PeakWordsPerMinute { get; init; }
}
