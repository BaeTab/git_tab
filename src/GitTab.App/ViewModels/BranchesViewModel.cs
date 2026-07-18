using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Local/remote branch trees and branch operations (CLI-backed).</summary>
public sealed partial class BranchesViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogger<BranchesViewModel> _logger;

    public BranchesViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc,
        ILogger<BranchesViewModel> logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        _loc = loc;
        _logger = logger;
    }

    public event Func<Task>? RepositoryChanged;

    public ObservableCollection<BranchNodeViewModel> Local { get; } = new();
    public ObservableCollection<BranchNodeViewModel> Remote { get; } = new();

    [ObservableProperty] private BranchNodeViewModel? _selectedBranch;
    [ObservableProperty] private string? _currentBranchName;

    public void Refresh(IReadOnlyList<BranchInfo> branches)
    {
        var selected = SelectedBranch?.FriendlyName;
        Local.Clear();
        Remote.Clear();
        foreach (var b in branches.OrderByDescending(b => b.IsCurrent).ThenBy(b => b.FriendlyName, StringComparer.OrdinalIgnoreCase))
        {
            var node = new BranchNodeViewModel { Model = b };
            (b.IsRemote ? Remote : Local).Add(node);
            if (b.IsCurrent) CurrentBranchName = b.FriendlyName;
        }
        SelectedBranch = Local.Concat(Remote).FirstOrDefault(n => n.FriendlyName == selected);
    }

    public void Clear()
    {
        Local.Clear();
        Remote.Clear();
        CurrentBranchName = null;
    }

    private async Task NotifyChanged()
    {
        if (RepositoryChanged is not null) await RepositoryChanged.Invoke();
    }

    [RelayCommand]
    private async Task Checkout(BranchNodeViewModel? node)
    {
        if (node is null || node.IsCurrent) return;
        var op = node.IsRemote
            ? () => _repo.RunRawAsync(new[] { "checkout", "--track", node.FriendlyName })
            : (Func<Task<GitResult>>)(() => _repo.CheckoutAsync(node.FriendlyName));
        if (await GitUi.RunAsync(op, _dialogs, _loc, _logger)) await NotifyChanged();
    }

    [RelayCommand]
    private async Task NewBranch()
    {
        var name = _dialogs.Prompt(_loc.T("Branch.NewName.Prompt"), _loc.T("Branch.New"));
        if (string.IsNullOrWhiteSpace(name)) return;
        if (await GitUi.RunAsync(() => _repo.CreateBranchAsync(name, checkout: true), _dialogs, _loc, _logger))
            await NotifyChanged();
    }

    [RelayCommand]
    private async Task Rename(BranchNodeViewModel? node)
    {
        if (node is null || node.IsRemote) return;
        var name = _dialogs.Prompt(_loc.T("Branch.RenamePrompt"), _loc.T("Branch.Rename"), node.FriendlyName);
        if (string.IsNullOrWhiteSpace(name) || name == node.FriendlyName) return;
        if (await GitUi.RunAsync(() => _repo.RenameBranchAsync(node.FriendlyName, name), _dialogs, _loc, _logger))
            await NotifyChanged();
    }

    [RelayCommand]
    private Task Delete(BranchNodeViewModel? node) => DeleteInternal(node, force: false);

    [RelayCommand]
    private Task ForceDelete(BranchNodeViewModel? node) => DeleteInternal(node, force: true);

    private async Task DeleteInternal(BranchNodeViewModel? node, bool force)
    {
        if (node is null || node.IsRemote || node.IsCurrent) return;
        if (!_dialogs.Confirm(_loc.T("Confirm.DeleteBranchBody", node.FriendlyName), _loc.T("Confirm.DeleteBranchTitle")))
            return;
        if (await GitUi.RunAsync(() => _repo.DeleteBranchAsync(node.FriendlyName, force), _dialogs, _loc, _logger))
            await NotifyChanged();
    }

    [RelayCommand]
    private async Task Merge(BranchNodeViewModel? node)
    {
        if (node is null || node.IsCurrent) return;
        if (await GitUi.RunAsync(() => _repo.MergeAsync(node.FriendlyName), _dialogs, _loc, _logger))
            await NotifyChanged();
    }

    [RelayCommand]
    private async Task Rebase(BranchNodeViewModel? node)
    {
        if (node is null || node.IsCurrent) return;
        if (await GitUi.RunAsync(() => _repo.RebaseAsync(node.FriendlyName), _dialogs, _loc, _logger))
            await NotifyChanged();
    }
}
