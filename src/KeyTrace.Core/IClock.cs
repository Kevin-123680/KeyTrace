namespace KeyTrace.Core;

/// <summary>可替换的时钟，便于单元测试。</summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset Now => DateTimeOffset.Now;
}

internal static class ClockExtensions
{
    /// <summary>本地日期（跨天聚合的划分依据）。</summary>
    public static DateOnly LocalDate(this IClock clock) => DateOnly.FromDateTime(clock.Now.DateTime);
}
