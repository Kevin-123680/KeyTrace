namespace KeyTrace.Core.Calibration;

/// <summary>
/// 像素 → 米的换算。默认假设 96 DPI（1 像素 = 1/96 英寸）；
/// 用户可在设置页用"划 10cm"反向校准，得到真实的比例系数。
/// </summary>
public sealed class CalibrationProfile
{
    /// <summary>96 DPI 下的默认值：0.0254 / 96 ≈ 0.000264583 米/像素。</summary>
    public const double DefaultMetersPerPixel = 0.0254 / 96.0;

    /// <summary>合法区间：约 2 米/像素 ~ 0.0001 米/像素，防止手工输入把统计搞成天文数字。</summary>
    public const double MinMetersPerPixel = 1e-6;
    public const double MaxMetersPerPixel = 0.01;

    public CalibrationProfile(double metersPerPixel)
    {
        MetersPerPixel = ClampMetersPerPixel(metersPerPixel);
    }

    public static CalibrationProfile Default { get; } = new(DefaultMetersPerPixel);

    public double MetersPerPixel { get; }

    /// <summary>等效鼠标 DPI（仅用于界面上给用户一个直观参考）。</summary>
    public double EquivalentDpi => 0.0254 / MetersPerPixel;

    public double ToMeters(long pixels) => pixels * MetersPerPixel;

    public double ToMeters(double pixels) => pixels * MetersPerPixel;

    public long FromMeters(double meters) => (long)Math.Round(meters / MetersPerPixel);

    public static double ClampMetersPerPixel(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return DefaultMetersPerPixel;
        }

        return Math.Clamp(value, MinMetersPerPixel, MaxMetersPerPixel);
    }

    /// <summary>核心公式：划过的物理距离 / 屏幕像素距离。</summary>
    public static double ComputeMetersPerPixel(double pixelDistance, double physicalCentimeters)
    {
        if (double.IsNaN(pixelDistance) || double.IsNaN(physicalCentimeters) ||
            pixelDistance < 50 || physicalCentimeters <= 0)
        {
            return DefaultMetersPerPixel;
        }

        return ClampMetersPerPixel(physicalCentimeters / 100.0 / pixelDistance);
    }
}

/// <summary>一次 DPI 校准会话：只累计像素距离，不关心方向。</summary>
public sealed class CalibrationSession
{
    private int _lastX;
    private int _lastY;
    private bool _hasLast;
    private double _pixels;
    private int _samples;

    public double AccumulatedPixels
    {
        get
        {
            lock (this)
            {
                return _pixels;
            }
        }
    }

    public int SampleCount
    {
        get
        {
            lock (this)
            {
                return _samples;
            }
        }
    }

    public bool IsReady => AccumulatedPixels >= 200;

    public void AddSample(int x, int y)
    {
        lock (this)
        {
            if (_hasLast)
            {
                var dx = (double)x - _lastX;
                var dy = (double)y - _lastY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (distance > 0)
                {
                    _pixels += distance;
                    _samples++;
                }
            }

            _lastX = x;
            _lastY = y;
            _hasLast = true;
        }
    }

    public void Reset()
    {
        lock (this)
        {
            _pixels = 0;
            _samples = 0;
            _hasLast = false;
            _lastX = 0;
            _lastY = 0;
        }
    }

    public double ComputeMetersPerPixel(double physicalCentimeters = 10.0)
        => CalibrationProfile.ComputeMetersPerPixel(AccumulatedPixels, physicalCentimeters);
}
