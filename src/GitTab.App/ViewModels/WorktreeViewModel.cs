using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the worktree manager: list, add, remove, and prune the repository's linked working trees.</summary>
public sealed partial class WorktreeViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public WorktreeViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _ = RefreshAsync();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<WorktreeInfo> Worktrees { get; } = new();

    /// <summary>True if a worktree was added/removed, so the host can reload.</summary>
    public bool Changed { get; private set; }

    public async Task RefreshAsync()
    {
        try
        {
            var worktrees = await _repo.GetWorktreesAsync().ConfigureAwait(true);
            Worktrees.Clear();
            foreach (var wt in worktrees) Worktrees.Add(wt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read worktrees");
        }
    }

    [RelayCommand]
    private async Task Add()
    {
        var path = _dialogs.Prompt(Loc.T("Worktree.PathPrompt"), Loc.T("Worktree.Add"));
        if (string.IsNullOrWhiteSpace(path)) return;
        var branch = _dialogs.Prompt(Loc.T("Worktree.BranchPrompt"), Loc.T("Worktree.Add"));
        var hasBranch = !string.IsNullOrWhiteSpace(branch);
        if (await GitUi.RunAsync(
                () => _repo.WorktreeAddAsync(path.Trim(), hasBranch ? branch!.Trim() : null, hasBranch),
                _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Remove(WorktreeInfo? wt)
    {
        if (wt is null || wt.IsCurrent) return;
        if (!_dialogs.Confirm(Loc.T("Worktree.RemoveConfirm", wt.Path), Loc.T("Common.Delete"))) return;
        if (await GitUi.RunAsync(() => _repo.WorktreeRemoveAsync(wt.Path, force: true), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Prune()
    {
        if (await GitUi.RunAsync(() => _repo.WorktreePruneAsync(), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }
}
