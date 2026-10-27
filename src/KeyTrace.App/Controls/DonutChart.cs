using System.Windows;
using System.Windows.Media;

namespace KeyTrace.App.Controls;

/// <summary>环形占比图（左键 / 右键 / 中键）。</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments),
        typeof(IReadOnlyList<DonutSegment>),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterTextProperty = DependencyProperty.Register(
        nameof(CenterText),
        typeof(string),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CenterCaptionProperty = DependencyProperty.Register(
        nameof(CenterCaption),
        typeof(string),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness),
        typeof(double),
        typeof(DonutChart),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<DonutSegment>? Segments
    {
        get => (IReadOnlyList<DonutSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string CenterText
    {
        get => (string)GetValue(CenterTextProperty);
        set => SetValue(CenterTextProperty, value);
    }

    public string CenterCaption
    {
        get => (string)GetValue(CenterCaptionProperty);
        set => SetValue(CenterCaptionProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var segments = Segments;
        var total = 0.0;
        if (segments is not null)
        {
            foreach (var segment in segments)
            {
                total += Math.Max(0, segment.Value);
            }
        }

        var diameter = Math.Min(width, height) - 16;
        if (diameter <= 10)
        {
            return;
        }

        var radius = (diameter - Thickness) / 2;
        var center = new Point(width / 2, height / 2);

        // 底环
        context.DrawEllipse(null, new Pen(ChartHelper.Freeze(Color.FromRgb(0x26, 0x2C, 0x3C)), Thickness), center, radius, radius);

        if (total > 0)
        {
            var startAngle = -90.0;
            foreach (var segment in segments!)
            {
                var value = Math.Max(0, segment.Value);
                if (value <= 0)
                {
                    continue;
                }

                var sweep = value / total * 360.0;
                DrawArc(context, segment.Brush, center, radius, Thickness, startAngle, sweep);
                startAngle += sweep;
            }
        }
        else
        {
            ChartHelper.DrawText(context, "暂无点击", new Point(center.X, center.Y - 7), ChartHelper.EmptyBrush, 11, dpi, center: true);
        }

        if (total > 0)
        {
            var text = ChartHelper.Text(CenterText ?? string.Empty, 22, ChartHelper.Freeze(Color.FromRgb(0xE8, 0xEB, 0xF2)), dpi, semiBold: true);
            context.DrawText(text, new Point(center.X - (text.Width / 2), center.Y - text.Height - 2));

            if (!string.IsNullOrEmpty(CenterCaption))
            {
                var caption = ChartHelper.Text(CenterCaption, 11, ChartHelper.MutedBrush, dpi);
                context.DrawText(caption, new Point(center.X - (caption.Width / 2), center.Y + 2));
            }
        }
    }

    private static void DrawArc(DrawingContext context, Brush brush, Point center, double radius, double thickness, double startAngle, double sweep)
    {
        if (sweep <= 0)
        {
            return;
        }

        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat,
        };
        pen.Freeze();

        if (sweep >= 359.99)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = PointOnCircle(center, radius, startAngle);
        var end = PointOnCircle(center, radius, startAngle + sweep);
        var geometry = new StreamGeometry();
        using (var geo = geometry.Open())
        {
            geo.BeginFigure(start, false, false);
            geo.ArcTo(end, new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }

        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + (radius * Math.Cos(radians)), center.Y + (radius * Math.Sin(radians)));
    }
}
