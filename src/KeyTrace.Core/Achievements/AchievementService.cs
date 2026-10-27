using KeyTrace.Core.Models;

namespace KeyTrace.Core.Achievements;

/// <summary>成就的触发条件。context 是判定所需的全部统计数据。</summary>
public sealed class AchievementContext
{
    public DateOnly Date { get; init; }
    public int TodayKeys { get; init; }
    public int TodayClicks { get; init; }
    public double TodayMeters { get; init; }
    public int NightKeys { get; init; }
    public int EarlyKeys { get; init; }
    public double PeakWordsPerMinute { get; init; }
    public long TotalKeys { get; init; }
    public double TotalMeters { get; init; }
    public int ActiveDays { get; init; }
    public int Streak { get; init; }
}

public sealed class AchievementDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }
    public required Func<AchievementContext, bool> IsUnlocked { get; init; }
}

public sealed class AchievementStatus
{
    public required AchievementDefinition Definition { get; init; }
    public bool IsUnlocked { get; init; }
    public DateTimeOffset? UnlockedAt { get; init; }
    public double Progress { get; init; }
}

/// <summary>内置成就清单。</summary>
public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } = new List<AchievementDefinition>
    {
        new()
        {
            Id = "first_day", Title = "启程", Glyph = "🌱",
            Description = "完成第一天的记录",
            IsUnlocked = static context => context.ActiveDays >= 1,
        },
        new()
        {
            Id = "daily_1k", Title = "千字日", Glyph = "⌨",
            Description = "单日按键 1,000 次",
            IsUnlocked = static context => context.TodayKeys >= 1_000,
        },
        new()
        {
            Id = "daily_5k", Title = "五千字日", Glyph = "📝",
            Description = "单日按键 5,000 次",
            IsUnlocked = static context => context.TodayKeys >= 5_000,
        },
        new()
        {
            Id = "daily_10k", Title = "万字日", Glyph = "🔥",
            Description = "单日按键 10,000 次",
            IsUnlocked = static context => context.TodayKeys >= 10_000,
        },
        new()
        {
            Id = "total_100k", Title = "十万次", Glyph = "💯",
            Description = "累计按键 100,000 次",
            IsUnlocked = static context => context.TotalKeys >= 100_000,
        },
        new()
        {
            Id = "total_1m", Title = "百万次", Glyph = "🏆",
            Description = "累计按键 1,000,000 次",
            IsUnlocked = static context => context.TotalKeys >= 1_000_000,
        },
        new()
        {
            Id = "mouse_10km", Title = "十公里", Glyph = "🖱",
            Description = "鼠标累计移动 10 公里",
            IsUnlocked = static context => context.TotalMeters >= 10_000,
        },
        new()
        {
            Id = "mouse_1000km", Title = "千公里", Glyph = "🚀",
            Description = "鼠标累计移动 1,000 公里",
            IsUnlocked = static context => context.TotalMeters >= 1_000_000,
        },
        new()
        {
            Id = "marathon", Title = "鼠标马拉松", Glyph = "🏃",
            Description = "单日鼠标移动 10 公里",
            IsUnlocked = static context => context.TodayMeters >= 10_000,
        },
        new()
        {
            Id = "streak_7", Title = "一周不断", Glyph = "📅",
            Description = "连续使用 7 天",
            IsUnlocked = static context => context.Streak >= 7,
        },
        new()
        {
            Id = "streak_30", Title = "月度坚持", Glyph = "🗓",
            Description = "连续使用 30 天",
            IsUnlocked = static context => context.Streak >= 30,
        },
        new()
        {
            Id = "streak_100", Title = "百日", Glyph = "💎",
            Description = "连续使用 100 天",
            IsUnlocked = static context => context.Streak >= 100,
        },
        new()
        {
            Id = "night_owl", Title = "夜猫子", Glyph = "🌙",
            Description = "凌晨 0-4 点按键 500 次",
            IsUnlocked = static context => context.NightKeys >= 500,
        },
        new()
        {
            Id = "early_bird", Title = "早起鸟", Glyph = "🌅",
            Description = "清晨 5-6 点按键 200 次",
            IsUnlocked = static context => context.EarlyKeys >= 200,
        },
        new()
        {
            Id = "click_master", Title = "点击狂人", Glyph = "🖱",
            Description = "单日鼠标点击 2,000 次",
            IsUnlocked = static context => context.TodayClicks >= 2_000,
        },
        new()
        {
            Id = "wordsmith", Title = "打字机", Glyph = "⚡",
            Description = "实时打字速度达到 80 WPM",
            IsUnlocked = static context => context.PeakWordsPerMinute >= 80,
        },
    };
}

/// <summary>成就判定与解锁落库。</summary>
public sealed class AchievementService
{
    private readonly Storage.IStatsRepository _repository;

    public AchievementService(Storage.IStatsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public IReadOnlyList<AchievementStatus> Evaluate(AchievementContext context, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(context);

        var records = _repository.GetAchievementRecords();
        var unlocked = records.ToDictionary(static item => item.Id, static item => item.UnlockedAt, StringComparer.Ordinal);
        var results = new List<AchievementStatus>(AchievementCatalog.All.Count);
        var changed = false;

        foreach (var definition in AchievementCatalog.All)
        {
            var isUnlocked = unlocked.ContainsKey(definition.Id);
            if (!isUnlocked)
            {
                bool shouldUnlock;
                try
                {
                    shouldUnlock = definition.IsUnlocked(context);
                }
                catch
                {
                    shouldUnlock = false;
                }

                if (shouldUnlock)
                {
                    _repository.UnlockAchievement(definition.Id, now);
                    unlocked[definition.Id] = now;
                    isUnlocked = true;
                    changed = true;
                }
            }

            results.Add(new AchievementStatus
            {
                Definition = definition,
                IsUnlocked = isUnlocked,
                UnlockedAt = unlocked.TryGetValue(definition.Id, out var at) ? at : null,
                Progress = isUnlocked ? 1 : 0,
            });
        }

        _ = changed;
        return results;
    }
}
