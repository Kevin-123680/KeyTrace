using KeyTrace.Core.Achievements;
using Xunit;

namespace KeyTrace.Core.Tests;

public class AchievementTests
{
    private static AchievementContext Context(
        int activeDays = 1,
        int todayKeys = 0,
        long totalKeys = 0,
        double totalMeters = 0,
        int streak = 0,
        int todayClicks = 0)
        => new()
        {
            Date = new DateOnly(2026, 5, 20),
            ActiveDays = activeDays,
            TodayKeys = todayKeys,
            TotalKeys = totalKeys,
            TotalMeters = totalMeters,
            Streak = streak,
            TodayClicks = todayClicks,
        };

    [Fact]
    public void FirstDay_UnlocksImmediately()
    {
        using var temp = new TempRepository();
        var service = new AchievementService(temp.Repository);

        var results = service.Evaluate(Context(), DateTimeOffset.Now);

        var firstDay = Assert.Single(results, static item => item.Definition.Id == "first_day");
        Assert.True(firstDay.IsUnlocked);
        Assert.Single(temp.Repository.GetAchievementRecords());
    }

    [Fact]
    public void HighKeyCount_UnlocksTieredAchievements()
    {
        using var temp = new TempRepository();
        var service = new AchievementService(temp.Repository);

        var results = service.Evaluate(Context(todayKeys: 12_000), DateTimeOffset.Now);

        Assert.True(Assert.Single(results, static item => item.Definition.Id == "daily_1k").IsUnlocked);
        Assert.True(Assert.Single(results, static item => item.Definition.Id == "daily_5k").IsUnlocked);
        Assert.True(Assert.Single(results, static item => item.Definition.Id == "daily_10k").IsUnlocked);
        Assert.False(Assert.Single(results, static item => item.Definition.Id == "total_1m").IsUnlocked);
        Assert.False(Assert.Single(results, static item => item.Definition.Id == "streak_7").IsUnlocked);
    }

    [Fact]
    public void Evaluate_IsIdempotent()
    {
        using var temp = new TempRepository();
        var service = new AchievementService(temp.Repository);

        var first = service.Evaluate(Context(streak: 30, totalKeys: 1_000_000), DateTimeOffset.Now);
        var unlockedFirst = first.Count(static item => item.IsUnlocked);

        var second = service.Evaluate(Context(streak: 30, totalKeys: 1_000_000), DateTimeOffset.Now);
        var unlockedSecond = second.Count(static item => item.IsUnlocked);

        Assert.Equal(unlockedFirst, unlockedSecond);
        Assert.Equal(unlockedFirst, temp.Repository.GetAchievementRecords().Count);
    }

    [Fact]
    public void Catalog_IdsAreUnique()
    {
        var ids = AchievementCatalog.All.Select(static item => item.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(AchievementCatalog.All, static item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Title));
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
        });
    }

    [Fact]
    public void StreakAndDistance_UnlockTheirBadges()
    {
        using var temp = new TempRepository();
        var service = new AchievementService(temp.Repository);

        var results = service.Evaluate(
            Context(streak: 30, totalMeters: 1_000_000, todayClicks: 2_500),
            DateTimeOffset.Now);

        Assert.True(Assert.Single(results, static item => item.Definition.Id == "streak_30").IsUnlocked);
        Assert.True(Assert.Single(results, static item => item.Definition.Id == "mouse_1000km").IsUnlocked);
        Assert.True(Assert.Single(results, static item => item.Definition.Id == "click_master").IsUnlocked);
    }
}
