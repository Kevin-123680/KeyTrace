using KeyTrace.Core.Input;
using KeyTrace.Core.Settings;

namespace KeyTrace.Core.Privacy;

/// <summary>
/// 隐私静默判定：当前台窗口命中"敏感进程"或"敏感标题关键词"时，整段时间不产生任何计数。
/// 判定只使用前台进程名与窗口标题，二者都不落盘。
/// </summary>
public sealed class PrivacyFilter
{
    public bool ShouldSuppress(ForegroundWindowInfo? foreground, AppSettings settings)
    {
        if (foreground is null || settings is null || !settings.PrivacyFilterEnabled)
        {
            return false;
        }

        var processName = NormalizeProcessName(foreground.ProcessName);
        if (processName.Length > 0)
        {
            foreach (var entry in settings.PrivacyProcessBlocklist)
            {
                var candidate = NormalizeProcessName(entry);
                if (candidate.Length == 0)
                {
                    continue;
                }

                if (processName.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
                    processName.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        var title = foreground.WindowTitle;
        if (!string.IsNullOrEmpty(title))
        {
            foreach (var keyword in settings.PrivacyTitleKeywords)
            {
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    title.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
