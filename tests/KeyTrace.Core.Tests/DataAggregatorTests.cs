using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using Xunit;

namespace KeyTrace.Core.Tests;

public class DataAggregatorTests
{
    private static readonly DateTimeOffset Base = new(2026, 5, 20, 9, 30, 0, TimeSpan.FromHours(8));

    [Fact]
    public void RecordKey_BucketsByHourAndDrains()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordKey(0x41, isTyping: true, Base);
        aggregator.RecordKey(0x42, isTyping: true, Base.AddMinutes(10));
        aggregator.RecordKey(0x43, isTyping: true, Base.AddHours(2));

        var delta = aggregator.Drain(Base.AddHours(3));

        Assert.Equal(2, delta.Hours.Count);

        var firstHour = Assert.Single(delta.Hours, static hour => hour.Hour == 9);
        Assert.Equal(2, firstHour.KeyCount);
        Assert.Equal(1, firstHour.KeyCounts[0x41]);
        Assert.Equal(1, firstHour.KeyCounts[0x42]);
        Assert.Equal(new DateOnly(2026, 5, 20), firstHour.Date);

        var secondHour = Assert.Single(delta.Hours, static hour => hour.Hour == 11);
        Assert.Equal(1, secondHour.KeyCount);

        // 排空之后不应再有数据
        Assert.True(aggregator.Drain(Base.AddHours(3)).IsEmpty);
    }

    [Fact]
    public void RecordPointerMove_AccumulatesEuclideanDistance()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordPointerMove(0, 0, Base);
        aggregator.RecordPointerMove(3, 4, Base.AddMilliseconds(10));   // 5
        aggregator.RecordPointerMove(3, 10, Base.AddMilliseconds(20));  // 6

        var delta = aggregator.Drain(Base);

        Assert.Equal(11, Assert.Single(delta.Hours).MousePixels);
    }

    [Fact]
    public void RecordPointerMove_IgnoresTeleportJumps()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordPointerMove(0, 0, Base);
        aggregator.RecordPointerMove(10_000, 10_000, Base.AddMilliseconds(5));

        var delta = aggregator.Drain(Base);

        Assert.True(delta.IsEmpty);
    }

    [Fact]
    public void ResetPointerAnchor_PreventsCrossSuppressionJumps()
    {
        var aggregator = new DataAggregator();
        aggregator.RecordPointerMove(0, 0, Base);

        aggregator.ResetPointerAnchor();
        aggregator.RecordPointerMove(400, 0, Base.AddSeconds(1)); // 重设锚点，不计距离
        aggregator.RecordPointerMove(403, 4, Base.AddSeconds(2)); // 5

        Assert.Equal(5, Assert.Single(aggregator.Drain(Base).Hours).MousePixels);
    }

    [Fact]
    public void RecordWheel_ConvertsDeltaToSteps()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordWheel(120, Base);   // +1 格
        aggregator.RecordWheel(60, Base);    // 不足一格，先记 60
        aggregator.RecordWheel(60, Base);    // 补足一格 → +1
        aggregator.RecordWheel(-120, Base);  // 反向一格 → -1

        Assert.Equal(1, Assert.Single(aggregator.Drain(Base).Hours).ScrollSteps);
    }

    [Fact]
    public void RecordClick_CountsButtonsAndZones()
    {
        var area = new ScreenArea(0, 0, 1000, 500);
        var aggregator = new DataAggregator(() => area, captureClickZones: true);

        aggregator.RecordClick(MouseButton.Left, 0, 0, Base);
        aggregator.RecordClick(MouseButton.Right, 999, 499, Base);
        aggregator.RecordClick(MouseButton.Middle, 500, 250, Base);

        var hour = Assert.Single(aggregator.Drain(Base).Hours);

        Assert.Equal(1, hour.LeftClicks);
        Assert.Equal(1, hour.RightClicks);
        Assert.Equal(1, hour.MiddleClicks);
        Assert.Equal(3, hour.ClickCount);
        Assert.Equal(3, hour.ClickZones.Values.Sum());

        var corner = ClickZoneGrid.ToCell(ClickZoneGrid.ToIndex(999, 499, area));
        Assert.Equal(19, corner.Column);
        Assert.Equal(9, corner.Row);
    }

    [Fact]
    public void ClickZones_AreNotRecordedWhenDisabled()
    {
        var aggregator = new DataAggregator(() => new ScreenArea(0, 0, 1920, 1080));

        aggregator.RecordClick(MouseButton.Left, 100, 100, Base);

        Assert.Empty(Assert.Single(aggregator.Drain(Base).Hours).ClickZones);
    }

    [Fact]
    public void ActiveMinutes_AreDeduplicatedPerMinute()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordKey(0x41, true, Base);
        aggregator.RecordKey(0x42, true, Base.AddSeconds(20));
        aggregator.RecordKey(0x43, true, Base.AddSeconds(40));
        aggregator.RecordKey(0x44, true, Base.AddMinutes(1));

        var delta = aggregator.Drain(Base.AddMinutes(2));
        var minutes = Assert.Single(delta.ActiveMinutes).Value;

        Assert.Equal(2, minutes.Count);
        Assert.Contains((9 * 60) + 30, minutes);
        Assert.Contains((9 * 60) + 31, minutes);
    }

    [Fact]
    public void Snapshot_DoesNotConsumePendingData()
    {
        var aggregator = new DataAggregator();
        var today = new DateOnly(2026, 5, 20);

        aggregator.RecordKey(0x41, true, Base);
        aggregator.RecordKey(0x41, true, Base.AddSeconds(1));

        var snapshot = aggregator.Snapshot(today, Base);

        Assert.Equal(2, snapshot.Keys);
        Assert.Equal(2, snapshot.KeyCounts[0x41]);
        Assert.Equal(1, snapshot.NewActiveMinutes);
        Assert.Equal(2, aggregator.Drain(Base).Hours.Sum(static hour => hour.KeyCount));
    }

    [Fact]
    public void Snapshot_IgnoresOtherDays()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordKey(0x41, true, Base);
        aggregator.RecordKey(0x42, true, Base.AddDays(-1));

        var snapshot = aggregator.Snapshot(new DateOnly(2026, 5, 20), Base);

        Assert.Equal(1, snapshot.Keys);
    }

    [Fact]
    public void WordsPerMinute_IsReasonable()
    {
        var aggregator = new DataAggregator();

        for (var index = 0; index < 60; index++)
        {
            aggregator.RecordKey(0x41, isTyping: true, Base.AddMilliseconds(index * 10));
        }

        var snapshot = aggregator.Snapshot(new DateOnly(2026, 5, 20), Base.AddSeconds(1));

        Assert.InRange(snapshot.WordsPerMinute, 60, 200);
        Assert.True(snapshot.PeakWordsPerMinute >= snapshot.WordsPerMinute - 1);
    }

    [Fact]
    public void WordsPerMinute_IgnoresNonTypingKeys()
    {
        var aggregator = new DataAggregator();

        for (var index = 0; index < 20; index++)
        {
            aggregator.RecordKey(0x25, isTyping: false, Base.AddMilliseconds(index * 10)); // ← 方向键
        }

        Assert.Equal(0, aggregator.Snapshot(new DateOnly(2026, 5, 20), Base).WordsPerMinute);
    }

    [Fact]
    public void RecordCombo_AggregatesByName()
    {
        var aggregator = new DataAggregator();

        aggregator.RecordCombo("Ctrl+C", Base);
        aggregator.RecordCombo("Ctrl+C", Base.AddSeconds(1));
        aggregator.RecordCombo("Ctrl+V", Base.AddSeconds(2));

        var hour = Assert.Single(aggregator.Drain(Base).Hours);

        Assert.Equal(2, hour.ComboCounts["Ctrl+C"]);
        Assert.Equal(1, hour.ComboCounts["Ctrl+V"]);
    }

    [Fact]
    public void Reset_ClearsPendingState()
    {
        var aggregator = new DataAggregator();
        aggregator.RecordKey(0x41, true, Base);

        aggregator.Reset();

        Assert.True(aggregator.Drain(Base).IsEmpty);
    }

    [Fact]
    public void ClickZoneGrid_MapsCornersAndClamps()
    {
        var area = new ScreenArea(-1920, 0, 3840, 1080);

        Assert.Equal(0, ClickZoneGrid.ToIndex(-1920, 0, area));
        Assert.Equal(199, ClickZoneGrid.ToIndex(1919, 1079, area));
        Assert.Equal(0, ClickZoneGrid.ToIndex(-99_999, -99_999, area));
        Assert.Equal(199, ClickZoneGrid.ToIndex(99_999, 99_999, area));
    }
}
