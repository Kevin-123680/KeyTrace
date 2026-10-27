using KeyTrace.Core.Calibration;

namespace KeyTrace.Core.Settings;

/// <summary>用户设置，持久化到 <c>%AppData%\KeyTrace\settings.json</c>。</summary>
public sealed class AppSettings
{
    /// <summary>默认放行的敏感进程名单（只按进程名匹配，不读取任何内容）。</summary>
    public static readonly string[] DefaultProcessBlocklist =
    {
        "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane",
        "enpass", "nordpass", "roboform", "keepass2", "credentialuibroker",
        "logonui", "consent", "securityhealthhost", "msipass",
    };

    /// <summary>默认的敏感标题关键词。</summary>
    public static readonly string[] DefaultTitleKeywords =
    {
        "密码", "password", "passphrase", "助记词", "恢复密钥", "验证码",
        "银行卡", "信用卡", "网银", "网上银行", "支付密码",
    };

    /// <summary>配置结构版本，便于以后迁移。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>暂停采集（暂停期间不产生任何计数）。</summary>
    public bool CollectionPaused { get; set; }

    /// <summary>每个屏幕像素对应的物理米数，由校准得到。</summary>
    public double MetersPerPixel { get; set; } = CalibrationProfile.DefaultMetersPerPixel;

    /// <summary>是否记录鼠标点击的屏幕热区（默认关闭，隐私优先）。</summary>
    public bool CaptureClickZones { get; set; }

    /// <summary>忽略程序合成（SendInput / 自动化）的输入。</summary>
    public bool IgnoreInjectedInput { get; set; }

    /// <summary>开机自启。</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>关闭主窗口时最小化到托盘。</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>开机自启时直接隐藏到托盘。</summary>
    public bool StartMinimized { get; set; } = true;

    /// <summary>敏感窗口自动静默。</summary>
    public bool PrivacyFilterEnabled { get; set; } = true;

    /// <summary>静默进程名（不含 .exe，大小写不敏感）。</summary>
    public List<string> PrivacyProcessBlocklist { get; set; } = new(DefaultProcessBlocklist);

    /// <summary>静默标题关键词（大小写不敏感）。</summary>
    public List<string> PrivacyTitleKeywords { get; set; } = new(DefaultTitleKeywords);

    /// <summary>落盘间隔（秒）。</summary>
    public int FlushIntervalSeconds { get; set; } = 30;

    /// <summary>是否统计快捷键组合（Ctrl+C 等）。</summary>
    public bool TrackShortcutCombos { get; set; } = true;

    /// <summary>每日按键小目标，0 表示关闭。</summary>
    public int DailyKeyGoal { get; set; } = 5000;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            SchemaVersion = SchemaVersion,
            CollectionPaused = CollectionPaused,
            MetersPerPixel = MetersPerPixel,
            CaptureClickZones = CaptureClickZones,
            IgnoreInjectedInput = IgnoreInjectedInput,
            StartWithWindows = StartWithWindows,
            MinimizeToTray = MinimizeToTray,
            StartMinimized = StartMinimized,
            PrivacyFilterEnabled = PrivacyFilterEnabled,
            PrivacyProcessBlocklist = new List<string>(PrivacyProcessBlocklist),
            PrivacyTitleKeywords = new List<string>(PrivacyTitleKeywords),
            FlushIntervalSeconds = FlushIntervalSeconds,
            TrackShortcutCombos = TrackShortcutCombos,
            DailyKeyGoal = DailyKeyGoal,
        };
    }

    /// <summary>把外部输入（可能被手工编辑过）收敛到合法区间。</summary>
    public void Normalize()
    {
        SchemaVersion = 1;
        MetersPerPixel = CalibrationProfile.ClampMetersPerPixel(MetersPerPixel);
        FlushIntervalSeconds = Math.Clamp(FlushIntervalSeconds, 5, 600);
        DailyKeyGoal = Math.Clamp(DailyKeyGoal, 0, 1_000_000);

        PrivacyProcessBlocklist = SanitizeList(PrivacyProcessBlocklist);
        PrivacyTitleKeywords = SanitizeList(PrivacyTitleKeywords);
    }

    private static List<string> SanitizeList(IEnumerable<string>? source)
    {
        if (source is null)
        {
            return new List<string>();
        }

        return source
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
