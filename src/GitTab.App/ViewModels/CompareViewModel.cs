using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the compare dialog: pick two refs and see their changed files + per-file diff.</summary>
public sealed partial class CompareViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ILogger _logger;

    public CompareViewModel(IRepositoryService repo, ILocalizationService loc, ILogger logger, string? from, string? to)
    {
        _repo = repo;
        Loc = loc;
        _logger = logger;

        try
        {
            foreach (var b in repo.GetBranches().Select(b => b.FriendlyName)) Refs.Add(b);
            foreach (var t in repo.GetTags().Select(t => t.Name)) if (!Refs.Contains(t)) Refs.Add(t);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Loading refs for compare failed"); }

        _fromRef = from ?? Refs.FirstOrDefault();
        _toRef = to ?? Refs.FirstOrDefault();
        Reload();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<string> Refs { get; } = new();
    public ObservableCollection<FileChangeViewModel> ChangedFiles { get; } = new();
    public DiffViewModel Diff { get; } = new();

    [ObservableProperty] private string? _fromRef;
    [ObservableProperty] private string? _toRef;
    [ObservableProperty] private FileChangeViewModel? _selectedFile;

    partial void OnFromRefChanged(string? value) => Reload();
    partial void OnToRefChanged(string? value) => Reload();

    partial void OnSelectedFileChanged(FileChangeViewModel? value)
    {
        if (value is null || FromRef is null || ToRef is null) { Diff.Clear(); return; }
        try { Diff.Show(_repo.GetFileDiffBetween(FromRef, ToRef, value.Path)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Compare diff failed for {Path}", value.Path); Diff.Clear(); }
    }

    private void Reload()
    {
        ChangedFiles.Clear();
        if (FromRef is null || ToRef is null) { Diff.Clear(); return; }
        try
        {
            foreach (var c in _repo.GetChangesBetween(FromRef, ToRef))
                ChangedFiles.Add(new FileChangeViewModel { Model = c });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Compare failed"); }
        SelectedFile = ChangedFiles.FirstOrDefault();
    }
}
