using System.Globalization;
using System.Text;
using KeyTrace.Core.Models;

namespace KeyTrace.Core.Storage;

/// <summary>把统计数据导出为 CSV（UTF-8 BOM，Excel 可直接打开中文）。</summary>
public static class CsvExporter
{
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static IReadOnlyList<string> ExportAll(IStatsRepository repository, string directory, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        var files = new List<string>();

        var from = new DateOnly(2000, 1, 1);
        var to = DateOnly.FromDateTime(exportedAt.DateTime);

        var daily = repository.GetDailyRange(from, to);
        files.Add(WriteFile(
            Path.Combine(directory, "daily_stats.csv"),
            new[] { "date", "total_keys", "total_clicks", "mouse_pixels", "mouse_meters", "active_min", "left_clicks", "right_clicks", "middle_clicks", "scroll_steps" },
            daily.Select(static item => new[]
            {
                item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.TotalKeys.ToString(CultureInfo.InvariantCulture),
                item.TotalClicks.ToString(CultureInfo.InvariantCulture),
                item.MousePixels.ToString(CultureInfo.InvariantCulture),
                item.MouseMeters.ToString("F3", CultureInfo.InvariantCulture),
                item.ActiveMinutes.ToString(CultureInfo.InvariantCulture),
                item.LeftClicks.ToString(CultureInfo.InvariantCulture),
                item.RightClicks.ToString(CultureInfo.InvariantCulture),
                item.MiddleClicks.ToString(CultureInfo.InvariantCulture),
                item.ScrollSteps.ToString(CultureInfo.InvariantCulture),
            })));

        var keys = repository.GetAllKeyDaily();
        files.Add(WriteFile(
            Path.Combine(directory, "key_daily.csv"),
            new[] { "date", "key_code", "key_name", "count" },
            keys.Select(static item => new[]
            {
                item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.KeyCode.ToString(CultureInfo.InvariantCulture),
                item.KeyName,
                item.Count.ToString(CultureInfo.InvariantCulture),
            })));

        var hourly = repository.GetAllHourly();
        files.Add(WriteFile(
            Path.Combine(directory, "hourly_dist.csv"),
            new[] { "date", "hour", "key_count", "mouse_pixels", "mouse_meters" },
            hourly.Select(static item => new[]
            {
                item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Hour.ToString(CultureInfo.InvariantCulture),
                item.KeyCount.ToString(CultureInfo.InvariantCulture),
                item.MousePixels.ToString(CultureInfo.InvariantCulture),
                item.MouseMeters.ToString("F3", CultureInfo.InvariantCulture),
            })));

        var combos = repository.GetAllCombos();
        files.Add(WriteFile(
            Path.Combine(directory, "combo_daily.csv"),
            new[] { "date", "combo", "count" },
            combos.Select(static item => new[]
            {
                item.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Combo,
                item.Count.ToString(CultureInfo.InvariantCulture),
            })));

        return files;
    }

    private static string WriteFile(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder(8192);
        builder.AppendLine(string.Join(',', header.Select(Escape)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        }

        File.WriteAllText(path, builder.ToString(), Utf8WithBom);
        return path;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        if (!needsQuotes)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
