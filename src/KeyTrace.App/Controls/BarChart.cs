using System.Windows;
using System.Windows.Media;

namespace KeyTrace.App.Controls;

/// <summary>24 小时分布柱状图。</summary>
public sealed class BarChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
        nameof(Labels),
        typeof(IReadOnlyList<string>),
        typeof(BarChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush),
        typeof(Brush),
        typeof(BarChart),
        new FrameworkPropertyMetadata(ChartHelper.Freeze(Color.FromArgb(0x55, 0x6C, 0x8C, 0xFF)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightBrushProperty = DependencyProperty.Register(
        nameof(HighlightBrush),
        typeof(Brush),
        typeof(BarChart),
        new FrameworkPropertyMetadata(ChartHelper.Freeze(Color.FromRgb(0x6C, 0x8C, 0xFF)), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowValuesProperty = DependencyProperty.Register(
        nameof(ShowValues),
        typeof(bool),
        typeof(BarChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush HighlightBrush
    {
        get => (Brush)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public bool ShowValues
    {
        get => (bool)GetValue(ShowValuesProperty);
        set => SetValue(ShowValuesProperty, value);
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
            ChartHelper.DrawEmpty(context, "暂无数据", width, height, dpi);
            return;
        }

        var count = values.Count;
        var labels = Labels;

        const double top = 12;
        const double bottom = 22;
        var plotHeight = Math.Max(10, height - top - bottom);

        var max = 0.0;
        for (var index = 0; index < count; index++)
        {
            if (values[index] > max)
            {
                max = values[index];
            }
        }

        if (max <= 0)
        {
            max = 1;
        }

        var slot = width / count;
        var barWidth = Math.Max(3, slot * 0.58);

        // 基线
        var baseLinePen = new Pen(ChartHelper.GridBrush, 1);
        context.DrawLine(baseLinePen, new Point(0, top + plotHeight), new Point(width, top + plotHeight));

        for (var index = 0; index < count; index++)
        {
            var value = values[index];
            var barHeight = value <= 0 ? 0 : Math.Max(2, plotHeight * (value / max));
            var x = (slot * index) + ((slot - barWidth) / 2);
            var y = top + plotHeight - barHeight;
            var brush = value > 0 && Math.Abs(value - max) < 0.000001 ? HighlightBrush : BarBrush;

            if (barHeight > 0)
            {
                context.DrawRoundedRectangle(brush, null, new Rect(x, y, barWidth, barHeight), 3, 3);
            }

            if (ShowValues && value > 0 && slot > 26)
            {
                ChartHelper.DrawText(
                    context,
                    KeyTrace.App.Infrastructure.Format.Compact(value),
                    new Point(x + (barWidth / 2), y - 14),
                    ChartHelper.MutedBrush,
                    9,
                    dpi,
                    center: true);
            }

            if (labels is not null && index < labels.Count)
            {
                var show = count <= 12 || index % 3 == 0;
                if (show)
                {
                    ChartHelper.DrawText(
                        context,
                        labels[index],
                        new Point(x + (barWidth / 2), top + plotHeight + 5),
                        ChartHelper.MutedBrush,
                        9.5,
                        dpi,
                        center: true);
                }
            }
        }
    }
}
