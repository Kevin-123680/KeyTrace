using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace KeyTrace.App.Controls;

/// <summary>绘图表时的共用工具（纯 DrawingContext 绘制，不依赖任何图表库）。</summary>
internal static class ChartHelper
{
    internal static readonly Brush MutedBrush = Freeze(Color.FromRgb(0x6B, 0x73, 0x87));
    internal static readonly Brush GridBrush = Freeze(Color.FromArgb(0x26, 0x6B, 0x73, 0x87));
    internal static readonly Brush EmptyBrush = Freeze(Color.FromRgb(0x55, 0x5D, 0x70));
    internal static readonly Typeface Regular = new("Segoe UI");
    internal static readonly Typeface SemiBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    internal static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    internal static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip, bool semiBold = false)
        => new(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            semiBold ? SemiBold : Regular,
            size,
            brush,
            pixelsPerDip);

    internal static void DrawText(DrawingContext context, string text, Point origin, Brush brush, double size, double pixelsPerDip, bool center = false, bool verticalCenter = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var formatted = Text(text, size, brush, pixelsPerDip);
        var x = center ? origin.X - (formatted.Width / 2) : origin.X;
        var y = verticalCenter ? origin.Y - (formatted.Height / 2) : origin.Y;
        context.DrawText(formatted, new Point(x, y));
    }

    /// <summary>按数据点数量决定 x 轴标签的稀疏程度，避免 30 天标签挤成一团。</summary>
    internal static int LabelStep(int count, int maxLabels = 8)
    {
        if (count <= maxLabels)
        {
            return 1;
        }

        return (int)Math.Ceiling(count / (double)maxLabels);
    }

    internal static void DrawEmpty(DrawingContext context, string? text, double width, double height, double pixelsPerDip)
    {
        var formatted = Text(string.IsNullOrWhiteSpace(text) ? "暂无数据" : text, 12, EmptyBrush, pixelsPerDip);
        context.DrawText(formatted, new Point((width - formatted.Width) / 2, (height - formatted.Height) / 2));
    }
}

/// <summary>多段环形图的数据项。</summary>
public sealed class DonutSegment
{
    public string Label { get; init; } = string.Empty;

    public double Value { get; init; }

    public Brush Brush { get; init; } = Brushes.SteelBlue;
}
