using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using KeyTrace.Core.Stats;
using KeyTrace.Core.Text;
using Xunit;

namespace KeyTrace.Core.Tests;

public class StatsCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 5, 20);

    [Fact]
    public void Streak_CountsConsecutiveDaysEndingToday()
    {
        var dates = new[]
        {
            Today.AddDays(-2), Today.AddDays(-1), Today,
        };

        Assert.Equal(3, StatsCalculator.ComputeStreak(dates, Today));
    }

    [Fact]
    public void Streak_ContinuesFromYesterdayWhenTodayIsEmpty()
    {
        var dates = new[]
        {
            Today.AddDays(-3), Today.AddDays(-2), Today.AddDays(-1),
        };

        Assert.Equal(3, StatsCalculator.ComputeStreak(dates, Today));
    }

    [Fact]
    public void Streak_BreaksOnGap()
    {
        var dates = new[]
        {
            Today, Today.AddDays(-1), Today.AddDays(-3),
        };

        Assert.Equal(2, StatsCalculator.ComputeStreak(dates, Today));
    }

    [Fact]
    public void Streak_IsZeroWithoutAnyActivity()
    {
        Assert.Equal(0, StatsCalculator.ComputeStreak(Array.Empty<DateOnly>(), Today));
        Assert.Equal(0, StatsCalculator.ComputeStreak(new[] { Today.AddDays(-10) }, Today));
    }

    [Fact]
    public void BusiestHour_ReturnsPeakHour()
    {
        var hours = new int[24];
        hours[9] = 30;
        hours[21] = 45;
        hours[14] = 12;

        Assert.Equal(21, StatsCalculator.BusiestHour(hours));
        Assert.Equal(-1, StatsCalculator.BusiestHour(new int[24]));
    }

    [Fact]
    public void SplitByPeriod_SumsHourRanges()
    {
        var hours = new int[24];
        hours[2] = 5;   // 深夜
        hours[8] = 10;  // 上午
        hours[15] = 20; // 下午
        hours[22] = 30; // 晚上

        var (night, morning, afternoon, evening) = StatsCalculator.SplitByPeriod(hours);

        Assert.Equal(5, night);
        Assert.Equal(10, morning);
        Assert.Equal(20, afternoon);
        Assert.Equal(30, evening);
    }

    [Fact]
    public void BooksEquivalent_UsesTwoHundredThousandChars()
    {
        Assert.Equal(1.0, StatsCalculator.BooksEquivalent(200_000), 9);
        Assert.Equal(0.5, StatsCalculator.BooksEquivalent(100_000), 9);
    }

    [Fact]
    public void WeekStart_IsMonday()
    {
        // 2026-05-20 是周三
        Assert.Equal(DayOfWeek.Wednesday, Today.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 5, 18), WeekComparison.WeekStart(Today));

        var sunday = new DateOnly(2026, 5, 24);
        Assert.Equal(new DateOnly(2026, 5, 18), WeekComparison.WeekStart(sunday));
    }

    [Fact]
    public void Percent_HandlesZeroBaseline()
    {
        Assert.Equal(0, WeekComparison.Percent(100, 0), 6);
        Assert.Equal(50, WeekComparison.Percent(150, 100), 6);
        Assert.Equal(-50, WeekComparison.Percent(50, 100), 6);
    }
}

public class KeyNameMapTests
{
    [Theory]
    [InlineData(0x41, "A")]
    [InlineData(0x5A, "Z")]
    [InlineData(0x31, "1")]
    [InlineData(0x20, "Space")]
    [InlineData(0x0D, "Enter")]
    [InlineData(0x70, "F1")]
    [InlineData(0x87, "F24")]
    public void GetName_ReturnsReadableNames(int key, string expected)
    {
        Assert.Equal(expected, KeyNameMap.GetName(key));
    }

    [Fact]
    public void GetName_FallsBackToVirtualKeyCode()
    {
        Assert.Equal("VK_FF", KeyNameMap.GetName(0xFF));
    }

    [Theory]
    [InlineData(0x41, true)]
    [InlineData(0x20, true)]
    [InlineData(0x08, true)]
    [InlineData(0x25, false)]
    [InlineData(0x70, false)]
    [InlineData(0x1B, false)]
    public void IsTypingKey_ClassifiesKeys(int key, bool expected)
    {
        Assert.Equal(expected, KeyNameMap.IsTypingKey(key));
    }

    [Fact]
    public void FormatCombo_BuildsShortcutName()
    {
        var modifiers = KeyModifiers.Control | KeyModifiers.Shift;

        Assert.Equal("Ctrl+Shift+S", KeyNameMap.FormatCombo(modifiers, 0x53));
        Assert.Equal("S", KeyNameMap.FormatCombo(KeyModifiers.None, 0x53));
    }

    [Fact]
    public void KeyboardLayout_HasFiveRowsOfFifteenUnits()
    {
        Assert.Equal(5, KeyboardLayout.Rows.Count);
        Assert.All(KeyboardLayout.Rows, static row => Assert.Equal(15.0, row.Sum(static key => key.WidthUnits), 3));
        Assert.True(KeyboardLayout.KeyCount > 60);
    }
}
