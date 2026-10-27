using System.Windows;
using System.Windows.Media;

namespace KeyTrace.App.Controls;

/// <summary>轻量折线图：支持面积渐变、网格与稀疏标签。</summary>
public sealed class LineChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
        nameof(Labels),
        typeof(IReadOnlyList<string>),
        typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(LineChart),
        new FrameworkPropertyMetadata(ChartHelper.Freeze(Color.FromRgb(0x6C, 0x8C, 0xFF)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(LineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowAreaProperty = DependencyProperty.Register(
        nameof(ShowArea),
        typeof(bool),
        typeof(LineChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText),
        typeof(string),
        typeof(LineChart),
        new FrameworkPropertyMetadata("暂无数据", FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IReadOnlyList<string>? Labels
    {
        get => (IReadOnlyList<string>?)GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool ShowArea
    {
        get => (bool)GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
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

        var values = Values;
        if (values is null || values.Count == 0)
        {
            ChartHelper.DrawEmpty(context, EmptyText, width, height, dpi);
            return;
        }

        const double left = 48;
        const double right = 10;
        const double top = 12;
        const double bottom = 24;
        var plotWidth = Math.Max(10, width - left - right);
        var plotHeight = Math.Max(10, height - top - bottom);

        var max = 0.0;
        foreach (var value in values)
        {
            if (value > max)
            {
                max = value;
            }
        }

        if (max <= 0)
        {
            max = 1;
        }

        // 网格与 y 轴刻度
        var gridPen = new Pen(ChartHelper.GridBrush, 1);
        for (var step = 0; step <= 4; step++)
        {
            var y = top + (plotHeight * step / 4.0);
            context.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
            var label = KeyTrace.App.Infrastructure.Format.Compact(max * (4 - step) / 4.0);
            ChartHelper.DrawText(context, label, new Point(left - 8, y - 7), ChartHelper.MutedBrush, 10, dpi);
        }

        var count = values.Count;
        double X(int index) => count == 1
            ? left + (plotWidth / 2)
            : left + (plotWidth * index / (count - 1.0));

        double Y(double value) => top + plotHeight - (Math.Clamp(value, 0, max) / max * plotHeight);

        // 面积
        if (ShowArea && count > 1)
        {
            var areaGeometry = new StreamGeometry();
            using (var geo = areaGeometry.Open())
            {
                geo.BeginFigure(new Point(X(0), top + plotHeight), true, true);
                geo.LineTo(new Point(X(0), Y(values[0])), true, false);
                for (var index = 1; index < count; index++)
                {
                    geo.LineTo(new Point(X(index), Y(values[index])), true, false);
                }

                geo.LineTo(new Point(X(count - 1), top + plotHeight), true, false);
            }

            areaGeometry.Freeze();
            context.DrawGeometry(Fill ?? CreateDefaultFill(), null, areaGeometry);
        }

        // 折线
        var lineGeometry = new StreamGeometry();
        using (var geo = lineGeometry.Open())
        {
            geo.BeginFigure(new Point(X(0), Y(values[0])), false, false);
            for (var index = 1; index < count; index++)
            {
                geo.LineTo(new Point(X(index), Y(values[index])), true, false);
            }
        }

        lineGeometry.Freeze();
        var linePen = new Pen(Stroke, 2.2)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        linePen.Freeze();
        context.DrawGeometry(null, linePen, lineGeometry);

        // 数据点（点数不多时）
        if (count <= 14)
        {
            for (var index = 0; index < count; index++)
            {
                context.DrawEllipse(Stroke, null, new Point(X(index), Y(values[index])), 2.6, 2.6);
            }
        }

        // x 轴标签
        var labels = Labels;
        if (labels is not null && labels.Count > 0)
        {
            var step = ChartHelper.LabelStep(count);
            for (var index = 0; index < count; index += step)
            {
                if (index >= labels.Count)
                {
                    break;
                }

                ChartHelper.DrawText(context, labels[index], new Point(X(index), top + plotHeight + 6), ChartHelper.MutedBrush, 10, dpi, center: true);
            }
        }
    }

    private Brush CreateDefaultFill()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, 0x6C, 0x8C, 0xFF), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0x05, 0x6C, 0x8C, 0xFF), 1));
        gradient.Freeze();
        return gradient;
    }
}
