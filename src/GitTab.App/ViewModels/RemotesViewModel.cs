using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the remotes manager: list, add, edit-URL, and remove the repository's remotes.</summary>
public sealed partial class RemotesViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public RemotesViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        Refresh();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<RemoteInfo> Remotes { get; } = new();

    /// <summary>True if any remote was added/changed/removed, so the host can reload.</summary>
    public bool Changed { get; private set; }

    private void Refresh()
    {
        Remotes.Clear();
        try { foreach (var r in _repo.GetRemotes()) Remotes.Add(r); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read remotes"); }
    }

    [RelayCommand]
    private async Task Add()
    {
        var name = _dialogs.Prompt(Loc.T("Remote.NamePrompt"), Loc.T("Remote.Add"), "origin");
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = _dialogs.Prompt(Loc.T("Remote.UrlPrompt"), Loc.T("Remote.Add"));
        if (string.IsNullOrWhiteSpace(url)) return;
        if (await GitUi.RunAsync(() => _repo.AddRemoteAsync(name.Trim(), url.Trim()), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task EditUrl(RemoteInfo? remote)
    {
        if (remote is null) return;
        var url = _dialogs.Prompt(Loc.T("Remote.UrlPrompt"), Loc.T("Remote.Edit"), remote.Url);
        if (string.IsNullOrWhiteSpace(url) || url.Trim() == remote.Url) return;
        if (await GitUi.RunAsync(() => _repo.SetRemoteUrlAsync(remote.Name, url.Trim()), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task Remove(RemoteInfo? remote)
    {
        if (remote is null) return;
        if (!_dialogs.Confirm(Loc.T("Remote.RemoveConfirm", remote.Name), Loc.T("Remote.Remove"))) return;
        if (await GitUi.RunAsync(() => _repo.RemoveRemoteAsync(remote.Name), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }
}
