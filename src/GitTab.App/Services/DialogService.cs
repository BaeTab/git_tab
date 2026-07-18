using System.Windows;
using System.Windows.Controls;
using GitTab.App.Localization;
using Microsoft.Win32;

namespace GitTab.App.Services;

public interface IDialogService
{
    string? PickFolder(string? title = null);
    void Info(string message, string? title = null);
    void Error(string message, string? title = null);
    bool Confirm(string message, string? title = null);
    string? Prompt(string message, string? title = null, string? initial = null);
}

public sealed class DialogService : IDialogService
{
    private readonly ILocalizationService _loc;

    public DialogService(ILocalizationService loc) => _loc = loc;

    public string? PickFolder(string? title = null)
    {
        var dlg = new OpenFolderDialog
        {
            Title = title ?? _loc.T("Action.Open"),
            Multiselect = false
        };
        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    public void Info(string message, string? title = null) =>
        MessageBox.Show(Owner(), message, title ?? _loc.T("Common.Success"),
            MessageBoxButton.OK, MessageBoxImage.Information);

    public void Error(string message, string? title = null) =>
        MessageBox.Show(Owner(), message, title ?? _loc.T("Common.Error"),
            MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string? title = null) =>
        MessageBox.Show(Owner(), message, title ?? _loc.T("Common.Warning"),
            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    public string? Prompt(string message, string? title = null, string? initial = null)
    {
        var dialog = new Window
        {
            Title = title ?? _loc.T("App.Title"),
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Owner(),
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["Brush.Window"]
        };

        var label = new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        var box = new TextBox { Text = initial ?? string.Empty, MinWidth = 360 };
        box.SelectAll();

        var ok = new Button { Content = _loc.T("Common.OK"), IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = _loc.T("Common.Cancel"), IsCancel = true, MinWidth = 80 };
        string? result = null;
        ok.Click += (_, _) => { result = box.Text; dialog.DialogResult = true; };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(label);
        panel.Children.Add(box);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        box.Focus();
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(result) ? result.Trim() : null;
    }

    private static Window? Owner() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}
