using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using KeyTrace.Core.Storage;
using Xunit;

namespace KeyTrace.Core.Tests;

public class RepositoryTests
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    private static AggregateDelta BuildDelta(DateOnly date, int hour = 9)
        => new()
        {
            CapturedAt = new DateTimeOffset(2026, 5, 20, hour, 59, 0, TimeSpan.FromHours(8)),
            Hours = new[]
            {
                new HourAggregate
                {
                    Date = date,
                    Hour = hour,
                    KeyCounts = new Dictionary<int, int> { [0x41] = 3, [0x42] = 2 },
                    ComboCounts = new Dictionary<string, int>(StringComparer.Ordinal) { ["Ctrl+C"] = 2 },
                    ClickZones = new Dictionary<int, int> { [ClickZoneGrid.ToIndex(100, 100, new ScreenArea(0, 0, 1000, 500))] = 1 },
                    MousePixels = 500,
                    LeftClicks = 2,
                    RightClicks = 1,
                    MiddleClicks = 0,
                    ScrollSteps = 4,
                },
            },
            ActiveMinutes = new Dictionary<DateOnly, IReadOnlyList<int>>
            {
                [date] = new[] { 540, 541 },
            },
        };

    [Fact]
    public void ApplyDelta_WritesDailyKeysAndHourlyRows()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);

        var daily = temp.Repository.GetDaily(Day);
        Assert.NotNull(daily);
        Assert.Equal(5, daily!.TotalKeys);
        Assert.Equal(3, daily.TotalClicks);
        Assert.Equal(2, daily.LeftClicks);
        Assert.Equal(1, daily.RightClicks);
        Assert.Equal(4, daily.ScrollSteps);
        Assert.Equal(500, daily.MousePixels);
        Assert.Equal(0.05, daily.MouseMeters, 9);
        Assert.Equal(2, daily.ActiveMinutes);

        var keys = temp.Repository.GetKeys(Day);
        Assert.Equal(2, keys.Count);
        Assert.Equal(0x41, keys[0].KeyCode);
        Assert.Equal("A", keys[0].KeyName);
        Assert.Equal(3, keys[0].Count);

        var hourly = Assert.Single(temp.Repository.GetHourly(Day));
        Assert.Equal(9, hourly.Hour);
        Assert.Equal(5, hourly.KeyCount);
        Assert.Equal(500, hourly.MousePixels);
        Assert.Equal(0.05, hourly.MouseMeters, 9);
    }

    [Fact]
    public void ApplyDelta_IsAdditive()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);
        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);

        var daily = temp.Repository.GetDaily(Day)!;
        Assert.Equal(10, daily.TotalKeys);
        Assert.Equal(1000, daily.MousePixels);
        Assert.Equal(0.1, daily.MouseMeters, 9);

        // 同一分钟重复写入不会重复计算活跃时长
        Assert.Equal(2, daily.ActiveMinutes);

        Assert.Equal(4, Assert.Single(temp.Repository.GetComboTotals(Day, Day)).Count);
    }

    [Fact]
    public void GetTotals_SumsAcrossDays()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(BuildDelta(Day, 9), 0.0001);
        temp.Repository.ApplyDelta(BuildDelta(Day.AddDays(1), 10), 0.0001);

        var totals = temp.Repository.GetTotals();

        Assert.Equal(10, totals.TotalKeys);
        Assert.Equal(6, totals.TotalClicks);
        Assert.Equal(1000, totals.MousePixels);
        Assert.Equal(2, totals.ActiveDays);
        Assert.Equal(Day, totals.FirstDate);

        var dates = temp.Repository.GetActiveDates(Day, Day.AddDays(5));
        Assert.Equal(2, dates.Count);
    }

    [Fact]
    public void GetKeyTotals_GroupsAcrossDays()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);
        temp.Repository.ApplyDelta(BuildDelta(Day.AddDays(1)), 0.0001);

        var totals = temp.Repository.GetKeyTotals(Day, Day.AddDays(1));

        Assert.Equal(2, totals.Count);
        Assert.Equal(0x41, totals[0].KeyCode);
        Assert.Equal(6, totals[0].Count);
    }

    [Fact]
    public void HourlyRange_ReturnsRowsInOrder()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(BuildDelta(Day, 10), 0.0001);
        temp.Repository.ApplyDelta(BuildDelta(Day, 8), 0.0001);

        var rows = temp.Repository.GetHourlyRange(Day, Day);

        Assert.Equal(2, rows.Count);
        Assert.Equal(8, rows[0].Hour);
        Assert.Equal(10, rows[1].Hour);
    }

    [Fact]
    public void Achievements_CanBeUnlockedOnlyOnce()
    {
        using var temp = new TempRepository();
        var at = new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.FromHours(8));

        temp.Repository.UnlockAchievement("first_day", at);
        temp.Repository.UnlockAchievement("first_day", at.AddHours(1));

        var records = temp.Repository.GetAchievementRecords();

        var record = Assert.Single(records);
        Assert.Equal("first_day", record.Id);
        Assert.Equal(at, record.UnlockedAt);
    }

    [Fact]
    public void ClearAll_RemovesEverything()
    {
        using var temp = new TempRepository();
        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);
        temp.Repository.UnlockAchievement("first_day", DateTimeOffset.Now);

        temp.Repository.ClearAll();

        Assert.Null(temp.Repository.GetDaily(Day));
        Assert.Empty(temp.Repository.GetKeys(Day));
        Assert.Empty(temp.Repository.GetHourly(Day));
        Assert.Empty(temp.Repository.GetAchievementRecords());
        Assert.Equal(0, temp.Repository.GetTotals().TotalKeys);
    }

    [Fact]
    public void ClickZones_ArePersistedWithTheirGridCells()
    {
        using var temp = new TempRepository();
        var delta = BuildDelta(Day);

        temp.Repository.ApplyDelta(delta, 0.0001);

        var zone = Assert.Single(temp.Repository.GetClickZones(Day, Day));
        Assert.Equal(1, zone.Count);
        Assert.InRange(zone.XBucket, 0, ClickZoneGrid.Columns - 1);
        Assert.InRange(zone.YBucket, 0, ClickZoneGrid.Rows - 1);
    }

    [Fact]
    public void CsvExport_WritesFourFilesWithHeaders()
    {
        using var temp = new TempRepository();
        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);

        var output = Path.Combine(temp.DirectoryPath, "export");
        var files = CsvExporter.ExportAll(
            temp.Repository,
            output,
            new DateTimeOffset(2026, 5, 21, 9, 0, 0, TimeSpan.FromHours(8)));

        Assert.Equal(4, files.Count);
        Assert.All(files, static file => Assert.True(File.Exists(file)));

        var daily = File.ReadAllText(Path.Combine(output, "daily_stats.csv"));
        Assert.Contains("date,total_keys,total_clicks", daily);
        Assert.Contains("2026-05-20,5,3", daily);

        var combos = File.ReadAllText(Path.Combine(output, "combo_daily.csv"));
        Assert.Contains("Ctrl+C", combos);
    }

    [Fact]
    public void Schema_IsIdempotent()
    {
        using var temp = new TempRepository();

        // 再次初始化不应抛异常、也不应丢数据
        temp.Repository.Initialize();
        temp.Repository.ApplyDelta(BuildDelta(Day), 0.0001);
        temp.Repository.Initialize();

        Assert.Equal(5, temp.Repository.GetDaily(Day)!.TotalKeys);
    }

    [Fact]
    public void EmptyDelta_IsIgnored()
    {
        using var temp = new TempRepository();

        temp.Repository.ApplyDelta(AggregateDelta.Empty(DateTimeOffset.Now), 0.0001);

        Assert.Equal(0, temp.Repository.GetTotals().TotalKeys);
    }
}
