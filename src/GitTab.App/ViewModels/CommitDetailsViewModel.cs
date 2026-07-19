using System.Collections.ObjectModel;
using GitTab.App.Localization;
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

    /// <summary>Localized signature-verification badge text; only meaningful when <see cref="HasSignature"/>.</summary>
    [ObservableProperty] private string _signatureText = string.Empty;

    /// <summary>True when the selected commit carries a signature (so the badge is shown).</summary>
    [ObservableProperty] private bool _hasSignature;

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
        HasSignature = false;
        SignatureText = string.Empty;
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

        _ = LoadSignatureAsync(commit.Sha);
    }

    private async Task LoadSignatureAsync(string sha)
    {
        try
        {
            var status = await _repo.GetSignatureStatusAsync(sha);
            var key = status switch
            {
                CommitSignature.Good => "Sign.Verified",
                CommitSignature.GoodUntrusted => "Sign.Untrusted",
                CommitSignature.Bad => "Sign.Bad",
                CommitSignature.Unknown => "Sign.Unknown",
                _ => null   // None → no badge
            };
            // Ignore a stale result if the selection moved on while we were verifying.
            if (Commit?.Sha != sha) return;
            if (key is null) { HasSignature = false; SignatureText = string.Empty; }
            else { SignatureText = LocalizationService.Current.T(key); HasSignature = true; }
        }
        catch { HasSignature = false; }
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
