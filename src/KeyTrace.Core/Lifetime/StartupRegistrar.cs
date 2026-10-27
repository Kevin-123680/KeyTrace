using KeyTrace.Core.Diagnostics;
using Microsoft.Win32;

namespace KeyTrace.Core.Lifetime;

/// <summary>
/// 开机自启：写 <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>。
/// 只写当前用户，不需要管理员权限。
/// </summary>
public sealed class StartupRegistrar
{
    public const string ValueName = "KeyTrace";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IAppLogger _logger;

    public StartupRegistrar(IAppLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.Warn("读取开机自启状态失败。", ex);
                return false;
            }
        }
    }

    public bool TrySetEnabled(bool enabled, out string? error, string? executablePath = null)
    {
        error = null;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                error = "无法打开注册表 Run 键。";
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.Info("已关闭开机自启。");
                return true;
            }

            var path = executablePath ?? ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "当前以开发方式运行（dotnet），无法写入自启路径；请使用发布后的 KeyTrace.exe。";
                return false;
            }

            key.SetValue(ValueName, BuildCommand(path, minimized: true), RegistryValueKind.String);
            _logger.Info($"已开启开机自启：{path}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("设置开机自启失败。", ex);
            error = ex.Message;
            return false;
        }
    }

    /// <summary>发布后的 exe 路径；dotnet 宿主（开发调试）返回 null。</summary>
    public static string? ResolveExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return path;
    }

    public static string BuildCommand(string executablePath, bool minimized)
        => minimized ? $"\"{executablePath}\" --minimized" : $"\"{executablePath}\"";
}
