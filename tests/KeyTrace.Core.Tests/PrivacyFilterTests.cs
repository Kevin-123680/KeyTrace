using KeyTrace.Core.Input;
using KeyTrace.Core.Privacy;
using KeyTrace.Core.Settings;
using Xunit;

namespace KeyTrace.Core.Tests;

public class PrivacyFilterTests
{
    private readonly PrivacyFilter _filter = new();

    private static ForegroundWindowInfo Window(string process, string title = "")
        => new(IntPtr.Zero, process, title);

    [Fact]
    public void SensitiveProcess_IsSuppressed()
    {
        var settings = new AppSettings();

        Assert.True(_filter.ShouldSuppress(Window("KeePass"), settings));
        Assert.True(_filter.ShouldSuppress(Window("KeePassXC"), settings));
        Assert.True(_filter.ShouldSuppress(Window("1Password"), settings));
        Assert.True(_filter.ShouldSuppress(Window("KeePass.exe"), settings));
    }

    [Fact]
    public void NormalProcess_IsNotSuppressed()
    {
        var settings = new AppSettings();

        Assert.False(_filter.ShouldSuppress(Window("explorer", "我的电脑"), settings));
        Assert.False(_filter.ShouldSuppress(Window("devenv", "KeyTrace.sln"), settings));
    }

    [Fact]
    public void SensitiveTitle_IsSuppressed()
    {
        var settings = new AppSettings();

        Assert.True(_filter.ShouldSuppress(Window("chrome", "登录 - 请输入密码"), settings));
        Assert.True(_filter.ShouldSuppress(Window("firefox", "Account password"), settings));
        Assert.False(_filter.ShouldSuppress(Window("chrome", "GitHub - KeyTrace"), settings));
    }

    [Fact]
    public void DisabledFilter_NeverSuppresses()
    {
        var settings = new AppSettings { PrivacyFilterEnabled = false };

        Assert.False(_filter.ShouldSuppress(Window("KeePass", "密码"), settings));
    }

    [Fact]
    public void NullForeground_IsNotSuppressed()
    {
        Assert.False(_filter.ShouldSuppress(null, new AppSettings()));
    }

    [Fact]
    public void CustomBlocklist_IsRespected()
    {
        var settings = new AppSettings();
        settings.PrivacyProcessBlocklist.Add("bankclient");

        Assert.True(_filter.ShouldSuppress(Window("BankClient"), settings));
    }

    [Fact]
    public void EmptyEntries_AreIgnored()
    {
        var settings = new AppSettings();
        settings.PrivacyProcessBlocklist.Add("   ");
        settings.PrivacyTitleKeywords.Add(string.Empty);

        Assert.False(_filter.ShouldSuppress(Window("notepad", "无标题"), settings));
    }
}
