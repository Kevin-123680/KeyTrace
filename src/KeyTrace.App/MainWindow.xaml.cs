using System.ComponentModel;
using System.Windows;

namespace KeyTrace.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>由 App 在退出流程中设置，避免关闭窗口被托盘逻辑拦截。</summary>
    public bool AllowClose { get; set; }

    /// <summary>用户点了关闭按钮（此时由 App 决定"缩到托盘"还是"退出"）。</summary>
    public event EventHandler? CloseRequested;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (AllowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
