using System.Globalization;

namespace KeyTrace.App.Infrastructure;

/// <summary>统一的数字/时长格式化。</summary>
public static class Format
{
    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Number(double value) => value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>1.2k / 3.4M，用于坐标轴与紧凑卡片。</summary>
    public static string Compact(double value)
    {
        var absolute = Math.Abs(value);
        return absolute switch
        {
            >= 100_000_000 => (value / 100_000_000).ToString("0.#", CultureInfo.CurrentCulture) + "亿",
            >= 10_000 => (value / 10_000).ToString("0.#", CultureInfo.CurrentCulture) + "万",
            >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.CurrentCulture) + "k",
            _ => value.ToString("0.#", CultureInfo.CurrentCulture),
        };
    }

    /// <summary>米 → 米/公里，保留一位小数。</summary>
    public static string Distance(double meters)
    {
        if (double.IsNaN(meters) || meters < 0)
        {
            return "0 m";
        }

        if (meters >= 100_000)
        {
            return (meters / 1000.0).ToString("N0", CultureInfo.CurrentCulture) + " km";
        }

        if (meters >= 1000)
        {
            return (meters / 1000.0).ToString("0.0", CultureInfo.CurrentCulture) + " km";
        }

        return meters.ToString("0.#", CultureInfo.CurrentCulture) + " m";
    }

    /// <summary>分钟 → "3 小时 12 分"。</summary>
    public static string Duration(int minutes)
    {
        if (minutes <= 0)
        {
            return "0 分";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;

        if (hours <= 0)
        {
            return rest + " 分";
        }

        return rest == 0 ? $"{hours} 小时" : $"{hours} 小时 {rest} 分";
    }

    public static string Percent(double value)
    {
        var sign = value > 0 ? "+" : string.Empty;
        return sign + value.ToString("0.#", CultureInfo.CurrentCulture) + "%";
    }

    public static string Hour(int hour) => $"{hour:00}:00";

    public static string Weekday(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "周一",
        DayOfWeek.Tuesday => "周二",
        DayOfWeek.Wednesday => "周三",
        DayOfWeek.Thursday => "周四",
        DayOfWeek.Friday => "周五",
        DayOfWeek.Saturday => "周六",
        _ => "周日",
    };

    public static string ShortDate(DateOnly date) => $"{date.Month}/{date.Day}";

    public static string ChineseDate(DateOnly date) => $"{date.Year} 年 {date.Month} 月 {date.Day} 日";
}
