using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the Git LFS manager: tracked patterns, track/untrack, and pulling LFS content.</summary>
public sealed partial class LfsViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public LfsViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _ = RefreshAsync();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<string> Patterns { get; } = new();

    [ObservableProperty] private bool _available;
    [ObservableProperty] private int _trackedFileCount;

    /// <summary>True if a pattern was tracked/untracked or content was pulled, so the host can reload.</summary>
    public bool Changed { get; private set; }

    public async Task RefreshAsync()
    {
        try
        {
            var status = await _repo.GetLfsStatusAsync().ConfigureAwait(true);
            Available = status.Available;
            TrackedFileCount = status.TrackedFileCount;
            Patterns.Clear();
            foreach (var p in status.TrackedPatterns) Patterns.Add(p);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read LFS status"); }
    }

    [RelayCommand]
    private async Task Track()
    {
        var pattern = _dialogs.Prompt(Loc.T("Lfs.PatternPrompt"), Loc.T("Lfs.Track"), "*.psd");
        if (string.IsNullOrWhiteSpace(pattern)) return;
        if (await GitUi.RunAsync(() => _repo.LfsTrackAsync(pattern.Trim()), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Untrack(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return;
        if (!_dialogs.Confirm(Loc.T("Lfs.UntrackConfirm", pattern), Loc.T("Common.Delete"))) return;
        if (await GitUi.RunAsync(() => _repo.LfsUntrackAsync(pattern), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Pull()
    {
        if (await GitUi.RunAsync(() => _repo.LfsPullAsync(), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }
}
