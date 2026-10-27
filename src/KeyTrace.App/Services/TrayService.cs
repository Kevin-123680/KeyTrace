using System.Windows.Controls;
using H.NotifyIcon;
using KeyTrace.App.Infrastructure;

namespace KeyTrace.App.Services;

/// <summary>系统托盘图标：左键打开主窗口，右键菜单含暂停 / 打开 / 退出。</summary>
public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly MenuItem _pauseItem;
    private bool _disposed;

    public TrayService(Action showWindow, Action togglePause, Action showSettings, Action exit)
    {
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(togglePause);

        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "打开主窗口" };
        openItem.Click += (_, _) => showWindow();
        menu.Items.Add(openItem);

        var settingsItem = new MenuItem { Header = "打开设置…" };
        settingsItem.Click += (_, _) => showSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());

        _pauseItem = new MenuItem { Header = "暂停记录" };
        _pauseItem.Click += (_, _) => togglePause();
        menu.Items.Add(_pauseItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "退出键迹" };
        exitItem.Click += (_, _) => exit();
        menu.Items.Add(exitItem);

        _icon = new TaskbarIcon
        {
            IconSource = IconLoader.Load(),
            ToolTipText = "键迹 KeyTrace",
            ContextMenu = menu,
            LeftClickCommand = new RelayCommand(showWindow),
            NoLeftClickDelay = true,
        };

        // 某些精简/虚拟机环境不支持 Efficiency Mode（SetProcessQualityOfServiceLevel 会抛
        // COMException 0x80070001），关掉它让托盘图标回归最基本的实现。
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void SetTooltip(string text)
    {
        if (_disposed)
        {
            return;
        }

        _icon.ToolTipText = text;
    }

    public void SetPaused(bool paused)
    {
        if (_disposed)
        {
            return;
        }

        _pauseItem.Header = paused ? "恢复记录" : "暂停记录";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.Dispose();
    }
}
