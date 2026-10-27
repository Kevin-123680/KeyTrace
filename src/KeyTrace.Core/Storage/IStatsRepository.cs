using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Models;

namespace KeyTrace.Core.Storage;

/// <summary>统计数据的读写契约（SQLite 实现见 <see cref="SqliteStatsRepository"/>）。</summary>
public interface IStatsRepository
{
    /// <summary>建表 / 迁移，必须在其它操作前调用。</summary>
    void Initialize();

    /// <summary>把一次增量合并进数据库（只做累加，重复调用不会重复计数）。</summary>
    void ApplyDelta(AggregateDelta delta, double metersPerPixel);

    DailyStat? GetDaily(DateOnly date);

    IReadOnlyList<DailyStat> GetDailyRange(DateOnly from, DateOnly to);

    IReadOnlyList<KeyDailyStat> GetKeys(DateOnly date);

    /// <summary>区间内按按键汇总，按次数倒序。</summary>
    IReadOnlyList<KeySummary> GetKeyTotals(DateOnly from, DateOnly to);

    IReadOnlyList<HourlyStat> GetHourly(DateOnly date);

    IReadOnlyList<HourlyStat> GetHourlyRange(DateOnly from, DateOnly to);

    /// <summary>区间内按快捷键汇总，按次数倒序。</summary>
    IReadOnlyList<ComboSummary> GetComboTotals(DateOnly from, DateOnly to, int limit = 10);

    IReadOnlyList<ClickZoneStat> GetClickZones(DateOnly from, DateOnly to);

    TotalsSummary GetTotals();

    IReadOnlyList<DateOnly> GetActiveDates(DateOnly from, DateOnly to);

    IReadOnlyList<AchievementRecord> GetAchievementRecords();

    void UnlockAchievement(string id, DateTimeOffset unlockedAt);

    IReadOnlyList<KeyDailyStat> GetAllKeyDaily();

    IReadOnlyList<HourlyStat> GetAllHourly();

    IReadOnlyList<ComboStat> GetAllCombos();

    /// <summary>清空所有统计数据与成就。</summary>
    void ClearAll();
}
