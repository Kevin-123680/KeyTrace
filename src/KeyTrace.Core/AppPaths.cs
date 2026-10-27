namespace KeyTrace.Core;

/// <summary>
/// 应用在本机的落盘位置。默认 <c>%AppData%\KeyTrace</c>，
/// 若该目录不可写会依次回退到其它可写位置（详见 <see cref="TryInitialize"/>），
/// 避免在受限环境里直接崩溃。
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "KeyTrace";

    /// <summary>回退到程序所在目录时使用的子目录名（便携模式）。</summary>
    public const string PortableFolderName = "KeyTraceData";

    public static string RootDirectory { get; private set; } = ResolvePreferredRoot();

    public static string DatabaseFile => Path.Combine(RootDirectory, "data.db");

    public static string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string LogDirectory => Path.Combine(RootDirectory, "logs");

    public static string ExportDirectory => Path.Combine(RootDirectory, "exports");

    /// <summary>数据目录的候选顺序：显式环境变量 → Roaming → Local → 程序目录 → 临时目录。</summary>
    public static IReadOnlyList<string> Candidates { get; } = BuildCandidates();

    /// <summary>
    /// 依次尝试候选目录，第一个真正可写（能建目录、能写文件）的即作为数据目录。
    /// 失败时通过 <paramref name="error"/> 返回每个候选的失败原因。
    /// </summary>
    public static bool TryInitialize(out string error)
    {
        var failures = new List<string>();

        foreach (var candidate in Candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                Directory.CreateDirectory(Path.Combine(candidate, "logs"));

                // 目录能创建不代表能写入（ACL / 只读介质 / 沙箱），实际写一次探针。
                var probe = Path.Combine(candidate, ".write-probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);

                RootDirectory = candidate;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                failures.Add($"  · {candidate}  →  {ex.Message}");
            }
        }

        error = failures.Count == 0 ? "没有可用的候选目录。" : string.Join(Environment.NewLine, failures);
        return false;
    }

    /// <summary>确保当前数据目录存在（此时 RootDirectory 已通过 <see cref="TryInitialize"/> 验证过）。</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public static string ResolveRootDirectory(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            try
            {
                return Path.GetFullPath(overridePath.Trim());
            }
            catch
            {
                // 非法的 KEYTRACE_HOME 直接忽略，走默认值。
            }
        }

        return ResolvePreferredRoot();
    }

    private static string ResolvePreferredRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(appData)
            ? Path.Combine(Path.GetTempPath(), AppFolderName)
            : Path.Combine(appData, AppFolderName);
    }

    private static List<string> BuildCandidates()
    {
        var candidates = new List<string>();

        var overridePath = Environment.GetEnvironmentVariable("KEYTRACE_HOME");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            try
            {
                candidates.Add(Path.GetFullPath(overridePath.Trim()));
            }
            catch
            {
                // 非法路径忽略，继续用后面的候选。
            }
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            candidates.Add(Path.Combine(appData, AppFolderName));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, AppFolderName));
        }

        var executableDirectory = ResolveExecutableDirectory();
        if (!string.IsNullOrWhiteSpace(executableDirectory))
        {
            candidates.Add(Path.Combine(executableDirectory, PortableFolderName));
        }

        candidates.Add(Path.Combine(Path.GetTempPath(), AppFolderName));

        return candidates
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? ResolveExecutableDirectory()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var directory = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }

            return AppContext.BaseDirectory;
        }
        catch
        {
            return null;
        }
    }
}
