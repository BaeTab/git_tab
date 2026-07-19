using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the submodule manager: list, add, sync, update, and deinit the repository's submodules.</summary>
public sealed partial class SubmoduleViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public SubmoduleViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        Refresh();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<SubmoduleInfo> Submodules { get; } = new();

    /// <summary>True if any submodule was added/synced/updated/deinitialized, so the host can reload.</summary>
    public bool Changed { get; private set; }

    private void Refresh()
    {
        Submodules.Clear();
        try { foreach (var s in _repo.GetSubmodules()) Submodules.Add(s); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read submodules"); }
    }

    [RelayCommand]
    private async Task Add()
    {
        var url = _dialogs.Prompt(Loc.T("Submodule.UrlPrompt"), Loc.T("Submodule.Add"));
        if (string.IsNullOrWhiteSpace(url)) return;
        var path = _dialogs.Prompt(Loc.T("Submodule.PathPrompt"), Loc.T("Submodule.Add"));
        if (string.IsNullOrWhiteSpace(path)) return;
        if (await GitUi.RunAsync(() => _repo.SubmoduleAddAsync(url.Trim(), path.Trim()), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Sync()
    {
        if (await GitUi.RunAsync(() => _repo.SubmoduleSyncAsync(), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Update()
    {
        if (await GitUi.RunAsync(() => _repo.SubmoduleUpdateAsync(), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Deinit(SubmoduleInfo? sm)
    {
        if (sm is null) return;
        if (!_dialogs.Confirm(Loc.T("Submodule.DeinitConfirm", sm.Path), Loc.T("Submodule.Deinit"))) return;
        if (await GitUi.RunAsync(() => _repo.SubmoduleDeinitAsync(sm.Path, force: true), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }
}
