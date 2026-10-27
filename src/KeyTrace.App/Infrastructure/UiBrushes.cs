using System.Windows.Media;

namespace KeyTrace.App.Infrastructure;

/// <summary>供 ViewModel 预计算画刷时使用的只读调色板（与 Themes/Theme.xaml 保持一致）。</summary>
public static class UiBrushes
{
    public static SolidColorBrush TextPrimary { get; } = Create(0xE8, 0xEB, 0xF2);

    public static SolidColorBrush TextSecondary { get; } = Create(0x9A, 0xA3, 0xB8);

    public static SolidColorBrush TextMuted { get; } = Create(0x6B, 0x73, 0x87);

    public static SolidColorBrush Border { get; } = Create(0x2A, 0x30, 0x40);

    public static SolidColorBrush Surface { get; } = Create(0x17, 0x1A, 0x23);

    public static SolidColorBrush AccentSoft { get; } = Create(0x24, 0x30, 0x55);

    public static SolidColorBrush Accent { get; } = Create(0x6C, 0x8C, 0xFF);

    public static SolidColorBrush Success { get; } = Create(0x4E, 0xCB, 0x8D);

    public static SolidColorBrush Warning { get; } = Create(0xFF, 0xB8, 0x6B);

    public static SolidColorBrush Danger { get; } = Create(0xFF, 0x6B, 0x6B);

    public static SolidColorBrush Violet { get; } = Create(0xA9, 0x8B, 0xFF);

    private static SolidColorBrush Create(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
