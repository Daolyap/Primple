using Microsoft.Win32;
using System.Windows;

namespace Primple.App.Services;

/// <summary>
/// Interface for dialog and file operations.
/// </summary>
public interface IDialogService
{
    string? OpenFile(string title, string filter);
    string? SaveFile(string title, string filter, string defaultFileName = "");
    string? SelectFolder(string title);
    MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information);
    bool ShowConfirmation(string message, string title);
}

/// <summary>
/// Service for dialog and file operations.
/// </summary>
public class DialogService : IDialogService
{
    public string? OpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string title, string filter, string defaultFileName = "")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectFolder(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public MessageBoxResult ShowMessage(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
    {
        return MessageBox.Show(message, title, buttons, icon);
    }

    public bool ShowConfirmation(string message, string title)
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
