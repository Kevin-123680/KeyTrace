using System.Windows;

namespace KeyTrace.App.Services;

/// <summary>对话框抽象，便于 ViewModel 测试与替换。</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    void ShowInfo(string title, string message);

    string? PickFolder(string title);
}

public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    public void ShowInfo(string title, string message)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
