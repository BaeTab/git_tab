using System.Collections.ObjectModel;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

public sealed partial class CommitDetailsViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ILogger<CommitDetailsViewModel> _logger;

    public CommitDetailsViewModel(IRepositoryService repo, ILogger<CommitDetailsViewModel> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public DiffViewModel Diff { get; } = new();
    public ObservableCollection<FileChangeViewModel> Files { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommit))]
    [NotifyPropertyChangedFor(nameof(Sha))]
    [NotifyPropertyChangedFor(nameof(ShortSha))]
    [NotifyPropertyChangedFor(nameof(MessageFull))]
    [NotifyPropertyChangedFor(nameof(AuthorName))]
    [NotifyPropertyChangedFor(nameof(DateText))]
    [NotifyPropertyChangedFor(nameof(ParentsText))]
    private CommitInfo? _commit;

    [ObservableProperty] private FileChangeViewModel? _selectedFile;

    public bool HasCommit => Commit is not null;
    public string Sha => Commit?.Sha ?? string.Empty;
    public string ShortSha => Commit?.ShortSha ?? string.Empty;
    public string MessageFull => Commit?.MessageFull ?? string.Empty;
    public string AuthorName => Commit is null ? string.Empty : $"{Commit.AuthorName} <{Commit.AuthorEmail}>";
    public string DateText => Commit is null ? string.Empty : Commit.WhenUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string ParentsText => Commit is null ? string.Empty
        : string.Join("  ", Commit.ParentShas.Select(p => p.Length >= 7 ? p[..7] : p));

    public void Load(CommitInfo? commit)
    {
        Commit = commit;
        Files.Clear();
        Diff.Clear();
        SelectedFile = null;
        if (commit is null) return;

        try
        {
            foreach (var change in _repo.GetCommitChanges(commit.Sha))
                Files.Add(new FileChangeViewModel { Model = change });
            SelectedFile = Files.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load changes for {Sha}", commit.Sha);
        }
    }

    partial void OnSelectedFileChanged(FileChangeViewModel? value)
    {
        if (Commit is null || value is null) { Diff.Clear(); return; }
        try
        {
            Diff.Show(_repo.GetCommitFileDiff(Commit.Sha, value.Path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load diff for {File}", value.Path);
            Diff.Clear();
        }
    }
}
