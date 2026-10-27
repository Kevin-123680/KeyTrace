using KeyTrace.Core.Input;
using KeyTrace.Core.Models;
using Xunit;

namespace KeyTrace.Core.Tests;

public class CollectionServiceTests
{
    [Fact]
    public void AutoRepeat_IsCountedOnlyOnce()
    {
        using var harness = new CollectionHarness();

        // 按住 A 不放：系统会连续发 KeyDown
        harness.Service.OnKey(0x41, isDown: true, isInjected: false);
        harness.Service.OnKey(0x41, isDown: true, isInjected: false);
        harness.Service.OnKey(0x41, isDown: true, isInjected: false);

        Assert.Equal(1, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);

        // 松开后再次按下，应当算第二次
        harness.Service.OnKey(0x41, isDown: false, isInjected: false);
        harness.Service.OnKey(0x41, isDown: true, isInjected: false);

        Assert.Equal(2, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);
    }

    [Fact]
    public void DifferentKeys_AreCountedSeparately()
    {
        using var harness = new CollectionHarness();

        harness.Service.OnKey(0x41, true, false);
        harness.Service.OnKey(0x42, true, false);
        harness.Service.OnKey(0x43, true, false);

        var snapshot = harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now);

        Assert.Equal(3, snapshot.Keys);
        Assert.Equal(1, snapshot.KeyCounts[0x41]);
        Assert.Equal(1, snapshot.KeyCounts[0x43]);
    }

    [Fact]
    public void Pause_StopsCountingAndResumeRestarts()
    {
        using var harness = new CollectionHarness();
        harness.Service.Start();

        harness.Service.OnKey(0x41, true, false);
        harness.Service.OnKey(0x41, false, false);

        harness.Settings.SetPaused(true);
        harness.Service.OnKey(0x42, true, false);
        harness.Service.OnKey(0x42, false, false);

        Assert.Equal(1, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);

        harness.Settings.SetPaused(false);
        harness.Service.OnKey(0x43, true, false);

        Assert.Equal(2, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);
    }

    [Fact]
    public void PrivacyFilter_SuppressesSensitiveWindow()
    {
        using var harness = new CollectionHarness();

        harness.Foreground.Current = new ForegroundWindowInfo(IntPtr.Zero, "KeePass", "KeyTrace 数据库密码");
        harness.Service.OnKey(0x41, true, false);
        harness.Service.OnKey(0x41, false, false);

        Assert.True(harness.Service.IsPrivacySuppressed);
        Assert.Equal(0, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);

        harness.Foreground.Current = new ForegroundWindowInfo(IntPtr.Zero, "explorer", "文件夹");
        harness.Service.OnKey(0x42, true, false);

        Assert.False(harness.Service.IsPrivacySuppressed);
        Assert.Equal(1, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);
    }

    [Fact]
    public void PrivacyFilter_SuppressesSensitiveTitle()
    {
        using var harness = new CollectionHarness();

        harness.Foreground.Current = new ForegroundWindowInfo(IntPtr.Zero, "chrome", "登录 - 请输入密码");
        harness.Service.OnKey(0x41, true, false);

        Assert.Equal(0, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);
    }

    [Fact]
    public void PrivacySuppression_DoesNotCountJumpDistance()
    {
        using var harness = new CollectionHarness();

        harness.Service.OnPointerMove(0, 0, false); // 建立锚点
        harness.Foreground.Current = new ForegroundWindowInfo(IntPtr.Zero, "1Password", "解锁");
        harness.Service.OnPointerMove(500, 500, false); // 静默，锚点被重置
        harness.Foreground.Current = new ForegroundWindowInfo(IntPtr.Zero, "devenv", "解决方案");

        harness.Service.OnPointerMove(1000, 1000, false); // 恢复后的第一帧只重建锚点
        harness.Service.OnPointerMove(1003, 1004, false); // 5 像素

        Assert.Equal(5, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).MousePixels);
    }

    [Fact]
    public void ShortcutCombos_AreRecorded()
    {
        using var harness = new CollectionHarness();
        harness.Service.Start();

        harness.Service.OnKey(0xA2, true, false);  // 左 Ctrl
        harness.Service.OnKey(0x43, true, false);  // C
        harness.Service.OnKey(0x43, false, false);
        harness.Service.OnKey(0xA2, false, false);

        harness.Service.OnKey(0xA2, true, false);
        harness.Service.OnKey(0x56, true, false);  // V

        harness.Service.Flush();

        var combos = harness.Temp.Repository.GetComboTotals(harness.Today, harness.Today);

        Assert.Contains(combos, static combo => combo.Combo == "Ctrl+C" && combo.Count == 1);
        Assert.Contains(combos, static combo => combo.Combo == "Ctrl+V" && combo.Count == 1);
    }

    [Fact]
    public void ShiftAlone_IsNotTreatedAsShortcut()
    {
        using var harness = new CollectionHarness();
        harness.Service.Start();

        harness.Service.OnKey(0xA0, true, false); // 左 Shift
        harness.Service.OnKey(0x41, true, false); // A

        harness.Service.Flush();

        Assert.Empty(harness.Temp.Repository.GetComboTotals(harness.Today, harness.Today));
    }

    [Fact]
    public void Flush_PersistsAndRaisesEvent()
    {
        using var harness = new CollectionHarness();
        var flushed = 0;
        harness.Service.Flushed += (_, _) => flushed++;

        harness.Service.OnKey(0x41, true, false);
        harness.Service.OnKey(0x42, true, false);
        harness.Service.Flush();

        var daily = harness.Temp.Repository.GetDaily(harness.Today);

        Assert.NotNull(daily);
        Assert.Equal(2, daily!.TotalKeys);
        Assert.Equal(1, flushed);

        // 再次 Flush 没有新数据，不应重复写
        harness.Service.Flush();
        Assert.Equal(2, harness.Temp.Repository.GetDaily(harness.Today)!.TotalKeys);
        Assert.Equal(1, flushed);
    }

    [Fact]
    public void InjectedInput_CanBeIgnored()
    {
        using var harness = new CollectionHarness(static settings => settings.IgnoreInjectedInput = true);
        var injected = true;

        harness.Service.OnKey(0x41, true, injected);
        Assert.Equal(0, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);

        harness.Service.OnKey(0x41, true, false);
        Assert.Equal(1, harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now).Keys);
    }

    [Fact]
    public void GenericModifierKeys_AreNormalized()
    {
        using var harness = new CollectionHarness();

        // 某些键盘只上报通用 VK_CONTROL(0x11)，应归一到左 Ctrl
        harness.Service.OnKey(0x11, true, false);

        var snapshot = harness.Aggregator.Snapshot(harness.Today, harness.Clock.Now);

        Assert.Equal(1, snapshot.KeyCounts[0xA2]);
    }

    [Fact]
    public void MouseClicksAndWheel_AreRecorded()
    {
        using var harness = new CollectionHarness(static settings => settings.CaptureClickZones = true);

        harness.Service.OnMouseButton(MouseButton.Left, 10, 10, false);
        harness.Service.OnMouseButton(MouseButton.Right, 20, 20, false);
        harness.Service.OnMouseWheel(120, 0, 0, false);

        harness.Service.Flush();

        var daily = harness.Temp.Repository.GetDaily(harness.Today)!;
        Assert.Equal(2, daily.TotalClicks);
        Assert.Equal(1, daily.LeftClicks);
        Assert.Equal(1, daily.RightClicks);
        Assert.Equal(1, daily.ScrollSteps);
        Assert.NotEmpty(harness.Temp.Repository.GetClickZones(harness.Today, harness.Today));
    }
}
