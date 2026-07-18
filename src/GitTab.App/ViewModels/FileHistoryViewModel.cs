using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>One commit in a file's history, with the file's diff shown when selected.</summary>
public sealed record FileHistoryEntry(string Sha, string ShortSha, string Summary, string Author, string WhenText);

/// <summary>Backs the file-history dialog: the commits that touched a file, and the file's diff at
/// the selected commit.</summary>
public sealed partial class FileHistoryViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ILogger _logger;
    private readonly string _path;

    public FileHistoryViewModel(IRepositoryService repo, ILocalizationService loc, ILogger logger, string path)
    {
        _repo = repo;
        Loc = loc;
        _logger = logger;
        _path = path;
        FilePath = path;

        try
        {
            foreach (var c in _repo.GetFileHistory(path))
                Entries.Add(new FileHistoryEntry(
                    c.Sha,
                    c.Sha.Length >= 7 ? c.Sha[..7] : c.Sha,
                    c.Summary,
                    c.AuthorName,
                    RelativeTime.Format(c.WhenUtc, loc)));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read file history for {Path}", path); }

        SelectedEntry = Entries.FirstOrDefault();
    }

    public ILocalizationService Loc { get; }
    public string FilePath { get; }
    public ObservableCollection<FileHistoryEntry> Entries { get; } = new();
    public DiffViewModel Diff { get; } = new();

    [ObservableProperty] private FileHistoryEntry? _selectedEntry;

    partial void OnSelectedEntryChanged(FileHistoryEntry? value)
    {
        if (value is null) { Diff.Clear(); return; }
        try { Diff.Show(_repo.GetCommitFileDiff(value.Sha, _path)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Diff failed for {Sha}:{Path}", value.Sha, _path); Diff.Clear(); }
    }
}
