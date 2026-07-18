using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Working-directory staging + commit.</summary>
public sealed partial class WorkingCopyViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogger<WorkingCopyViewModel> _logger;

    public WorkingCopyViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc,
        ILogger<WorkingCopyViewModel> logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        _loc = loc;
        _logger = logger;
    }

    /// <summary>Raised after a commit, so the host can reload history/branches.</summary>
    public event Func<Task>? RepositoryChanged;

    public DiffViewModel Diff { get; } = new();
    public ObservableCollection<FileChangeViewModel> Staged { get; } = new();
    public ObservableCollection<FileChangeViewModel> Unstaged { get; } = new();

    [ObservableProperty] private FileChangeViewModel? _selectedFile;
    [ObservableProperty] private bool _isClean = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CommitCommand))]
    private bool _amend;

    /// <summary>Conventional-commit types offered as a helper dropdown above the message box.</summary>
    public IReadOnlyList<string> CommitTypes { get; } = new[]
    {
        "feat", "fix", "docs", "style", "refactor", "perf", "test", "build", "ci", "chore", "revert"
    };

    [ObservableProperty] private string? _selectedCommitType;

    // Applying a type prepends "type: " (replacing any existing conventional prefix) so beginners
    // get well-formed messages without memorizing the convention.
    partial void OnSelectedCommitTypeChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var body = System.Text.RegularExpressions.Regex.Replace(
            CommitMessage ?? string.Empty, @"^\s*[a-z]+(\([^)]*\))?!?:\s*", string.Empty);
        CommitMessage = value + ": " + body;
    }

    public bool CanCommit => !string.IsNullOrWhiteSpace(CommitMessage) && (Staged.Count > 0 || Amend);

    public void Refresh()
    {
        var previouslySelected = SelectedFile?.Path;
        Staged.Clear();
        Unstaged.Clear();
        try
        {
            var status = _repo.GetStatus();
            foreach (var c in status.Staged) Staged.Add(new FileChangeViewModel { Model = c });
            foreach (var c in status.Unstaged) Unstaged.Add(new FileChangeViewModel { Model = c });
            IsClean = status.IsClean;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read working-tree status");
        }

        SelectedFile = Unstaged.Concat(Staged).FirstOrDefault(f => f.Path == previouslySelected);
        CommitCommand.NotifyCanExecuteChanged();
    }

    public void Clear()
    {
        Staged.Clear();
        Unstaged.Clear();
        Diff.Clear();
        CommitMessage = string.Empty;
        SelectedCommitType = null;
        Amend = false;
        IsClean = true;
    }

    partial void OnSelectedFileChanged(FileChangeViewModel? value)
    {
        if (value is null) { Diff.Clear(); return; }
        try { Diff.Show(_repo.GetWorkingFileDiff(value.Path, value.IsStaged)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to load working diff for {File}", value.Path); Diff.Clear(); }
    }

    [RelayCommand]
    private async Task Stage(FileChangeViewModel? file)
    {
        if (file is null) return;
        if (await GitUi.RunAsync(() => _repo.StageAsync(file.Path), _dialogs, _loc, _logger))
            Refresh();
    }

    [RelayCommand]
    private async Task Unstage(FileChangeViewModel? file)
    {
        if (file is null) return;
        if (await GitUi.RunAsync(() => _repo.UnstageAsync(file.Path), _dialogs, _loc, _logger))
            Refresh();
    }

    [RelayCommand]
    private async Task StagePartial(FileChangeViewModel? file)
    {
        if (file is null) return;
        var vm = new HunkStageViewModel(_repo, _dialogs, _loc, _logger, file.Path);
        _dialogs.ShowHunkStage(vm);
        if (vm.Changed)
        {
            Refresh();
            if (RepositoryChanged is not null) await RepositoryChanged.Invoke();
        }
    }

    [RelayCommand]
    private async Task Discard(FileChangeViewModel? file)
    {
        if (file is null) return;
        if (!_dialogs.Confirm(_loc.T("Confirm.DiscardBody", file.DisplayPath), _loc.T("Confirm.DiscardTitle")))
            return;
        if (await GitUi.RunAsync(() => _repo.DiscardAsync(file.Path), _dialogs, _loc, _logger))
            Refresh();
    }

    [RelayCommand]
    private async Task StageAll()
    {
        if (await GitUi.RunAsync(() => _repo.StageAllAsync(), _dialogs, _loc, _logger))
            Refresh();
    }

    [RelayCommand]
    private async Task UnstageAll()
    {
        var ok = true;
        foreach (var f in Staged.ToList())
            ok &= await GitUi.RunAsync(() => _repo.UnstageAsync(f.Path), _dialogs, _loc, _logger);
        if (ok) Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanCommit))]
    private async Task Commit()
    {
        var message = CommitMessage.Trim();
        var success = await GitUi.RunAsync(() => _repo.CommitAsync(message, Amend), _dialogs, _loc, _logger);
        if (!success) return;

        CommitMessage = string.Empty;
        SelectedCommitType = null;
        Amend = false;
        Refresh();
        if (RepositoryChanged is not null) await RepositoryChanged.Invoke();
    }
}
