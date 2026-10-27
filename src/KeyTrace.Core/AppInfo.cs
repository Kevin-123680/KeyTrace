namespace KeyTrace.Core;

/// <summary>应用元信息（界面"关于"页使用）。</summary>
public static class AppInfo
{
    public const string ProductName = "键迹 KeyTrace";

    public const string Tagline = "本地键鼠行为统计 · 隐私优先";

    /// <summary>发布前请替换为你自己的仓库地址。</summary>
    public const string RepositoryUrl = "https://github.com/your-account/KeyTrace";

    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string DataDirectory => AppPaths.RootDirectory;
}
