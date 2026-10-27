using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Stats;
using Xunit;

namespace KeyTrace.Core.Tests;

public class LiveStatsProviderTests
{
    private static AggregateDelta Delta(DateOnly date, int hour, int keys, long pixels)
        => new()
        {
            CapturedAt = new DateTimeOffset(2026, 5, 20, hour, 0, 0, TimeSpan.FromHours(8)),
            Hours = new[]
            {
                new HourAggregate
                {
                    Date = date,
                    Hour = hour,
                    KeyCounts = new Dictionary<int, int> { [0x41] = keys },
                    MousePixels = pixels,
                    LeftClicks = 2,
                },
            },
        };

    [Fact]
    public void Today_MergesStoredAndPendingData()
    {
        using var temp = new TempRepository();
        var clock = new FakeClock(new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified));
        var settings = new SettingsService(new MemorySettingsStore());
        settings.SetMetersPerPixel(0.0001);
        var aggregator = new DataAggregator();
        var provider = new LiveStatsProvider(temp.Repository, aggregator, settings, clock);

        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 20), 9, 5, 500), 0.0001);

        aggregator.RecordKey(0x41, isTyping: true, clock.Now);
        aggregator.RecordPointerMove(0, 0, clock.Now);
        aggregator.RecordPointerMove(30, 40, clock.Now);

        var today = provider.GetToday();

        Assert.Equal(6, today.TotalKeys);
        Assert.Equal(550, today.MousePixels);
        Assert.Equal(0.055, today.Meters, 9);
        Assert.Equal(5, today.HourlyKeys[9]);
        Assert.Equal(1, today.HourlyKeys[10]);
        Assert.Equal(6, today.KeyCounts[0x41]);
        Assert.Equal(2, today.TotalClicks);
    }

    [Fact]
    public void WeekComparison_SplitsThisAndLastWeek()
    {
        using var temp = new TempRepository();
        var clock = new FakeClock(new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified));
        var settings = new SettingsService(new MemorySettingsStore());
        var aggregator = new DataAggregator();
        var provider = new LiveStatsProvider(temp.Repository, aggregator, settings, clock);

        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 18), 9, 5, 1000), 0.0001);
        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 12), 9, 10, 2000), 0.0001);
        aggregator.RecordKey(0x42, isTyping: true, clock.Now);

        var comparison = provider.GetWeekComparison();

        Assert.Equal(new DateOnly(2026, 5, 18), comparison.ThisWeekStart);
        Assert.Equal(6, comparison.ThisWeekKeys);
        Assert.Equal(10, comparison.LastWeekKeys);
        Assert.Equal(-40, comparison.KeysDeltaPercent, 6);
        Assert.Equal(1000, comparison.ThisWeekPixels);
        Assert.Equal(2000, comparison.LastWeekPixels);
    }

    [Fact]
    public void RecentDays_FillsMissingDatesWithZero()
    {
        using var temp = new TempRepository();
        var clock = new FakeClock(new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified));
        var settings = new SettingsService(new MemorySettingsStore());
        var aggregator = new DataAggregator();
        var provider = new LiveStatsProvider(temp.Repository, aggregator, settings, clock);

        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 18), 9, 7, 700), 0.0001);

        var days = provider.GetRecentDays(5);

        Assert.Equal(5, days.Count);
        Assert.Equal(new DateOnly(2026, 5, 16), days[0].Date);
        Assert.Equal(new DateOnly(2026, 5, 20), days[^1].Date);
        Assert.Equal(7, days.Single(static day => day.Date == new DateOnly(2026, 5, 18)).Keys);
        Assert.Equal(0, days[0].Keys);
    }

    [Fact]
    public void GetTotals_AddsPendingToStoredTotals()
    {
        using var temp = new TempRepository();
        var clock = new FakeClock(new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified));
        var settings = new SettingsService(new MemorySettingsStore());
        var aggregator = new DataAggregator();
        var provider = new LiveStatsProvider(temp.Repository, aggregator, settings, clock);

        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 19), 9, 10, 1000), 0.0001);
        aggregator.RecordKey(0x41, true, clock.Now);

        var totals = provider.GetTotals();

        Assert.Equal(11, totals.TotalKeys);
        Assert.Equal(1000, totals.MousePixels);
        Assert.Equal(1, totals.ActiveDays);
    }

    [Fact]
    public void Streak_CountsTodayWhenOnlyPendingDataExists()
    {
        using var temp = new TempRepository();
        var clock = new FakeClock(new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified));
        var settings = new SettingsService(new MemorySettingsStore());
        var aggregator = new DataAggregator();
        var provider = new LiveStatsProvider(temp.Repository, aggregator, settings, clock);

        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 18), 9, 1, 0), 0.0001);
        temp.Repository.ApplyDelta(Delta(new DateOnly(2026, 5, 19), 9, 1, 0), 0.0001);
        aggregator.RecordKey(0x41, true, clock.Now);

        Assert.Equal(3, provider.GetStreak());
    }
}
