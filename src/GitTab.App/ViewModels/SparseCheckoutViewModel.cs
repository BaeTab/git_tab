using System.Collections.ObjectModel;
using System.Linq;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the sparse-checkout manager: list, add, and remove the repository's sparse-checkout patterns.</summary>
public sealed partial class SparseCheckoutViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public SparseCheckoutViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _ = RefreshAsync();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<string> Patterns { get; } = new();

    /// <summary>True while sparse-checkout is enabled (any patterns are configured).</summary>
    [ObservableProperty]
    private bool _active;

    /// <summary>Whether new pattern sets are applied in cone mode.</summary>
    [ObservableProperty]
    private bool _coneMode = true;

    /// <summary>True if the pattern set was added/changed/removed/disabled, so the host can reload.</summary>
    public bool Changed { get; private set; }

    public async Task RefreshAsync()
    {
        try
        {
            var patterns = await _repo.GetSparseCheckoutPatternsAsync().ConfigureAwait(true);
            Patterns.Clear();
            foreach (var p in patterns) Patterns.Add(p);
            Active = Patterns.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read sparse-checkout patterns");
        }
    }

    [RelayCommand]
    private async Task Add()
    {
        var pattern = _dialogs.Prompt(Loc.T("Sparse.PatternPrompt"), Loc.T("Sparse.Add"));
        if (string.IsNullOrWhiteSpace(pattern)) return;
        var patterns = Patterns.Append(pattern.Trim()).ToList();
        if (await GitUi.RunAsync(() => _repo.SparseCheckoutSetAsync(patterns, ConeMode), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Remove(string? pattern)
    {
        if (pattern is null) return;
        var patterns = Patterns.Where(p => p != pattern).ToList();
        Func<Task<GitResult>> op = patterns.Count == 0
            ? () => _repo.SparseCheckoutDisableAsync()
            : () => _repo.SparseCheckoutSetAsync(patterns, ConeMode);
        if (await GitUi.RunAsync(op, _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task Disable()
    {
        if (!_dialogs.Confirm(Loc.T("Sparse.DisableConfirm"), Loc.T("Sparse.Disable"))) return;
        if (await GitUi.RunAsync(() => _repo.SparseCheckoutDisableAsync(), _dialogs, Loc, _logger))
        {
            Changed = true;
            await RefreshAsync();
        }
    }
}
