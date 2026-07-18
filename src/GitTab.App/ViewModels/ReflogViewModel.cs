using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// Backs the "history &amp; undo" dialog: lists recent HEAD positions from the reflog and lets the
/// user restore to any of them — a safety net so beginners can undo a bad commit/merge/reset.
/// </summary>
public sealed partial class ReflogViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public ReflogViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        Refresh();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<ReflogEntry> Entries { get; } = new();

    /// <summary>True if HEAD was moved, so the host can reload.</summary>
    public bool Changed { get; private set; }

    private void Refresh()
    {
        Entries.Clear();
        try { foreach (var e in _repo.GetReflog(200)) Entries.Add(e); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read reflog"); }
    }

    /// <summary>Undo the most recent operation (restore HEAD to its previous position).</summary>
    [RelayCommand]
    private Task UndoLast() => Entries.Count > 1 ? RestoreTo(Entries[1]) : Task.CompletedTask;

    [RelayCommand]
    private Task Restore(ReflogEntry? entry) => entry is null ? Task.CompletedTask : RestoreTo(entry);

    private async Task RestoreTo(ReflogEntry entry)
    {
        if (!_dialogs.Confirm(Loc.T("Reflog.ResetConfirm", entry.ShortSha), Loc.T("Common.Warning")))
            return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "reset", "--hard", entry.Sha }), _dialogs, Loc, _logger))
        {
            Changed = true;
            Refresh();
        }
    }
}
