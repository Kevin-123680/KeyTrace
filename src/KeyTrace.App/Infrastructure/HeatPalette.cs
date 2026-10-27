using System.Windows.Media;
using KeyTrace.App.Infrastructure;

namespace KeyTrace.App.Infrastructure;

/// <summary>热图配色：从"几乎为空"到"高亮紫"的平滑渐变（32 级缓存，避免每次重建画刷）。</summary>
public static class HeatPalette
{
    private const int Levels = 32;

    private static readonly Color[] Stops =
    {
        Color.FromRgb(0x1E, 0x22, 0x30),
        Color.FromRgb(0x26, 0x2F, 0x55),
        Color.FromRgb(0x38, 0x4C, 0xA8),
        Color.FromRgb(0x6C, 0x8C, 0xFF),
        Color.FromRgb(0xA9, 0x8B, 0xFF),
    };

    private static readonly SolidColorBrush[] Cache = BuildCache();

    public static SolidColorBrush Empty { get; } = Freeze(Color.FromRgb(0x1B, 0x1F, 0x2B));

    public static SolidColorBrush Locked { get; } = Freeze(Color.FromRgb(0x24, 0x28, 0x35));

    public static SolidColorBrush Positive { get; } = Freeze(Color.FromRgb(0x4E, 0xCB, 0x8D));

    public static SolidColorBrush Negative { get; } = Freeze(Color.FromRgb(0xFF, 0x6B, 0x6B));

    public static SolidColorBrush Neutral { get; } = Freeze(Color.FromRgb(0x9A, 0xA3, 0xB8));

    public static SolidColorBrush ForIntensity(double intensity)
    {
        var clamped = double.IsNaN(intensity) ? 0 : Math.Clamp(intensity, 0, 1);
        var index = (int)Math.Round(clamped * (Levels - 1));
        return Cache[index];
    }

    /// <summary>增长率对应的颜色：涨为绿、跌为红、持平为灰。</summary>
    public static SolidColorBrush ForDelta(double delta)
    {
        if (double.IsNaN(delta) || Math.Abs(delta) < 0.05)
        {
            return Neutral;
        }

        return delta > 0 ? Positive : Negative;
    }

    private static SolidColorBrush[] BuildCache()
    {
        var cache = new SolidColorBrush[Levels];
        for (var index = 0; index < Levels; index++)
        {
            cache[index] = Freeze(Interpolate(index / (double)(Levels - 1)));
        }

        return cache;
    }

    private static Color Interpolate(double t)
    {
        var scaled = t * (Stops.Length - 1);
        var segment = Math.Min((int)scaled, Stops.Length - 2);
        var local = scaled - segment;
        var from = Stops[segment];
        var to = Stops[segment + 1];

        return Color.FromRgb(
            Lerp(from.R, to.R, local),
            Lerp(from.G, to.G, local),
            Lerp(from.B, to.B, local));
    }

    private static byte Lerp(byte from, byte to, double t) => (byte)Math.Round(from + ((to - from) * t));

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
