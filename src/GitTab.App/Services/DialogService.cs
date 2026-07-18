using System.Windows;
using System.Windows.Controls;
using GitTab.App.Localization;
using GitTab.App.ViewModels;
using GitTab.App.Views;
using GitTab.Core.Gitignore;
using GitTab.Core.Models;
using Microsoft.Win32;

namespace GitTab.App.Services;

/// <summary>Credentials collected from the GUI auth prompt.</summary>
public sealed record CredentialInput(string User, string Secret);

public interface IDialogService
{
    string? PickFolder(string? title = null);
    void Info(string message, string? title = null);
    void Error(string message, string? title = null);
    bool Confirm(string message, string? title = null);
    string? Prompt(string message, string? title = null, string? initial = null);

    /// <summary>Multi-line text prompt (e.g. editing a commit message); null if cancelled.</summary>
    string? PromptMultiline(string message, string? title = null, string? initial = null);

    /// <summary>Prompt for a username + password/token for <paramref name="host"/>; null if cancelled.</summary>
    CredentialInput? PromptCredentials(string host);

    /// <summary>Show the Settings dialog bound to the given view model.</summary>
    void ShowSettings(object viewModel);

    /// <summary>Show the "create repository" dialog; returns true if the user confirmed.</summary>
    bool ShowNewRepository(NewRepositoryViewModel vm);

    /// <summary>Show the "clone repository" dialog; returns true if the user confirmed.</summary>
    bool ShowClone(CloneViewModel vm);

    /// <summary>Show the remotes manager dialog.</summary>
    void ShowRemotes(RemotesViewModel vm);

    /// <summary>Show the reflog "history &amp; undo" dialog.</summary>
    void ShowReflog(ReflogViewModel vm);

    /// <summary>Show the partial-staging (per-hunk) dialog.</summary>
    void ShowHunkStage(HunkStageViewModel vm);

    /// <summary>Show the file-history dialog.</summary>
    void ShowFileHistory(FileHistoryViewModel vm);

    /// <summary>Show the Ctrl+P command palette; returns true if the user confirmed a command.</summary>
    bool ShowCommandPalette(CommandPaletteViewModel vm);

    /// <summary>Show the compare (two-ref diff) dialog.</summary>
    void ShowCompare(CompareViewModel vm);

    /// <summary>Show the content-search (pickaxe) dialog; returns true if the user picked a commit.</summary>
    bool ShowContentSearch(ContentSearchViewModel vm);

    /// <summary>Shows the .gitignore generator for the repo; returns true if a file was written.</summary>
    bool ShowGitignoreGenerator(string workingDir);

    void ShowBlame(string filePath, IReadOnlyList<BlameLine> lines);

    /// <summary>Shows the interactive-rebase planner; returns the plan, or null if cancelled.</summary>
    IReadOnlyList<RebaseTodoItem>? ShowInteractiveRebase(IReadOnlyList<RebaseTodoItem> items);

    /// <summary>Shows the 3-way conflict resolver for a file; returns true if the file was written.</summary>
    bool ShowConflictResolver(string workingDir, string relativePath, string? baseText, string? ours, string? theirs);
}

public sealed class DialogService : IDialogService
{
    private readonly ILocalizationService _loc;
    private readonly IGitignoreService _gitignore;

    public DialogService(ILocalizationService loc, IGitignoreService gitignore)
    {
        _loc = loc;
        _gitignore = gitignore;
    }

    public bool ShowGitignoreGenerator(string workingDir)
    {
        var vm = new GitignoreDialogViewModel(_gitignore, workingDir);
        var dialog = new GitignoreDialog { DataContext = vm, Owner = Owner() };
        dialog.ShowDialog();
        return vm.Written;
    }

    public void ShowBlame(string filePath, IReadOnlyList<BlameLine> lines)
    {
        var dialog = new BlameView(filePath, lines) { Owner = Owner() };
        dialog.ShowDialog();
    }

    public IReadOnlyList<RebaseTodoItem>? ShowInteractiveRebase(IReadOnlyList<RebaseTodoItem> items)
    {
        var vm = new InteractiveRebaseViewModel(items);
        var dialog = new InteractiveRebaseDialog { DataContext = vm, Owner = Owner() };
        return dialog.ShowDialog() == true ? vm.Plan : null;
    }

    public bool ShowConflictResolver(string workingDir, string relativePath, string? baseText, string? ours, string? theirs)
    {
        var full = System.IO.Path.Combine(workingDir, relativePath);
        var vm = new ConflictResolverViewModel(relativePath, full, baseText, ours, theirs);
        var dialog = new ConflictResolverDialog { DataContext = vm, Owner = Owner() };
        dialog.ShowDialog();
        return vm.Written;
    }

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

    public void ShowSettings(object viewModel)
    {
        var win = new SettingsWindow { DataContext = viewModel, Owner = Owner() };
        win.ShowDialog();
    }

    public bool ShowNewRepository(NewRepositoryViewModel vm)
    {
        var win = new NewRepositoryDialog { DataContext = vm, Owner = Owner() };
        return win.ShowDialog() == true;
    }

    public bool ShowClone(CloneViewModel vm)
    {
        var win = new CloneDialog { DataContext = vm, Owner = Owner() };
        return win.ShowDialog() == true;
    }

    public void ShowRemotes(RemotesViewModel vm)
    {
        var win = new RemotesDialog { DataContext = vm, Owner = Owner() };
        win.ShowDialog();
    }

    public void ShowReflog(ReflogViewModel vm)
    {
        var win = new ReflogDialog { DataContext = vm, Owner = Owner() };
        win.ShowDialog();
    }

    public void ShowHunkStage(HunkStageViewModel vm)
    {
        var win = new HunkStageDialog { DataContext = vm, Owner = Owner() };
        win.ShowDialog();
    }

    public void ShowFileHistory(FileHistoryViewModel vm)
    {
        var win = new FileHistoryDialog { DataContext = vm, Owner = Owner() };
        win.ShowDialog();
    }

    public bool ShowCommandPalette(CommandPaletteViewModel vm)
    {
        var win = new CommandPaletteWindow { DataContext = vm, Owner = Owner() };
        return win.ShowDialog() == true;
    }

    public void ShowCompare(CompareViewModel vm)
    {
        var win = new CompareDialog { DataContext = vm, Owner = Owner() };
        win.ShowDialog();
    }

    public bool ShowContentSearch(ContentSearchViewModel vm)
    {
        var win = new ContentSearchDialog { DataContext = vm, Owner = Owner() };
        return win.ShowDialog() == true;
    }

    public CredentialInput? PromptCredentials(string host)
    {
        var dialog = new Window
        {
            Title = _loc.T("Auth.Title"),
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Owner(),
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["Brush.Window"]
        };

        var title = new TextBlock
        {
            Text = string.Format(_loc.T("Auth.Message"), host),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var userLabel = new TextBlock { Text = _loc.T("Auth.User"), Margin = new Thickness(0, 0, 0, 4) };
        var userBox = new TextBox { MinWidth = 380 };

        var secretLabel = new TextBlock { Text = _loc.T("Auth.Secret"), Margin = new Thickness(0, 10, 0, 4) };
        var secretBox = new PasswordBox { MinWidth = 380 };

        var hint = new TextBlock
        {
            Text = _loc.T("Auth.Hint"),
            Style = (Style)Application.Current.Resources["MutedText"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var ok = new Button { Content = _loc.T("Common.OK"), IsDefault = true, MinWidth = 84, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = _loc.T("Common.Cancel"), IsCancel = true, MinWidth = 84 };
        CredentialInput? result = null;
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(secretBox.Password)) { secretBox.Focus(); return; }
            result = new CredentialInput(userBox.Text.Trim(), secretBox.Password);
            dialog.DialogResult = true;
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(title);
        panel.Children.Add(userLabel);
        panel.Children.Add(userBox);
        panel.Children.Add(secretLabel);
        panel.Children.Add(secretBox);
        panel.Children.Add(hint);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        userBox.Focus();
        return dialog.ShowDialog() == true ? result : null;
    }

    public string? PromptMultiline(string message, string? title = null, string? initial = null)
    {
        var dialog = new Window
        {
            Title = title ?? _loc.T("App.Title"),
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Owner(),
            ResizeMode = ResizeMode.CanResize,
            ShowInTaskbar = false,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["Brush.Window"]
        };

        var label = new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        var box = new TextBox
        {
            Text = initial ?? string.Empty,
            MinWidth = 460,
            MinHeight = 120,
            MaxHeight = 320,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        box.SelectAll();

        var ok = new Button { Content = _loc.T("Common.OK"), IsDefault = false, MinWidth = 84, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = _loc.T("Common.Cancel"), IsCancel = true, MinWidth = 84 };
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
