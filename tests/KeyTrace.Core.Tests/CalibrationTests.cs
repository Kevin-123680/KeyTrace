using KeyTrace.Core.Calibration;
using Xunit;

namespace KeyTrace.Core.Tests;

public class CalibrationTests
{
    [Fact]
    public void DefaultProfile_Matches96Dpi()
    {
        var profile = CalibrationProfile.Default;

        Assert.Equal(0.0254 / 96.0, profile.MetersPerPixel, 12);
        Assert.Equal(96.0, profile.EquivalentDpi, 3);
    }

    [Fact]
    public void Compute_FromTenCentimeters()
    {
        // 划了 10cm，屏幕移动 1000 像素 → 每像素 0.0001 米 = 0.1 毫米
        var metersPerPixel = CalibrationProfile.ComputeMetersPerPixel(1000, 10);

        Assert.Equal(0.0001, metersPerPixel, 12);
        Assert.Equal(10_000L, (long)new CalibrationProfile(metersPerPixel).ToMeters(100_000_000));
    }

    [Fact]
    public void Compute_RejectsInvalidInput()
    {
        Assert.Equal(CalibrationProfile.DefaultMetersPerPixel, CalibrationProfile.ComputeMetersPerPixel(0, 10));
        Assert.Equal(CalibrationProfile.DefaultMetersPerPixel, CalibrationProfile.ComputeMetersPerPixel(10, 0));
        Assert.Equal(CalibrationProfile.DefaultMetersPerPixel, CalibrationProfile.ComputeMetersPerPixel(double.NaN, 10));
    }

    [Theory]
    [InlineData(0, CalibrationProfile.DefaultMetersPerPixel)]
    [InlineData(-1, CalibrationProfile.DefaultMetersPerPixel)]
    [InlineData(1e-9, CalibrationProfile.MinMetersPerPixel)]
    [InlineData(99, CalibrationProfile.MaxMetersPerPixel)]
    public void Clamp_KeepsValuesInRange(double input, double expected)
    {
        Assert.Equal(expected, CalibrationProfile.ClampMetersPerPixel(input), 12);
    }

    [Fact]
    public void Session_AccumulatesPixelPath()
    {
        var session = new CalibrationSession();

        session.AddSample(0, 0);
        session.AddSample(100, 0);
        session.AddSample(100, 100);
        session.AddSample(100, 100); // 原地不动不计距离

        Assert.Equal(200, session.AccumulatedPixels, 6);
        Assert.Equal(2, session.SampleCount);

        var metersPerPixel = session.ComputeMetersPerPixel(10);
        Assert.Equal(0.10 / 200, metersPerPixel, 12);
    }

    [Fact]
    public void Session_ResetClearsState()
    {
        var session = new CalibrationSession();
        session.AddSample(0, 0);
        session.AddSample(10, 10);

        session.Reset();

        Assert.Equal(0, session.AccumulatedPixels);
        Assert.False(session.IsReady);
    }

    [Fact]
    public void FromMeters_IsInverseOfToMeters()
    {
        var profile = new CalibrationProfile(0.0002);

        Assert.Equal(5000, profile.FromMeters(1.0));
        Assert.Equal(1.0, profile.ToMeters(5000), 9);
    }
}
