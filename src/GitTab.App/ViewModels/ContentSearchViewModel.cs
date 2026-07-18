using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>One pickaxe result: a commit that added/removed the searched term.</summary>
public sealed record ContentResult(string Sha, string ShortSha, string Summary, string Author, string WhenText);

/// <summary>Backs the content-search (pickaxe) dialog: find commits that introduced/removed a string.</summary>
public sealed partial class ContentSearchViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ILogger _logger;

    public ContentSearchViewModel(IRepositoryService repo, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        Loc = loc;
        _logger = logger;
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<ContentResult> Results { get; } = new();

    [ObservableProperty] private string _term = string.Empty;
    [ObservableProperty] private bool _useRegex;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _searched;
    [ObservableProperty] private bool _showEmpty;

    /// <summary>SHA the user picked to jump to, or null.</summary>
    public string? ChosenSha { get; private set; }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(Term) || IsSearching) return;
        IsSearching = true;
        Results.Clear();
        try
        {
            var commits = await _repo.SearchContentAsync(Term.Trim(), UseRegex);
            foreach (var c in commits)
                Results.Add(new ContentResult(
                    c.Sha,
                    c.Sha.Length >= 7 ? c.Sha[..7] : c.Sha,
                    c.Summary, c.AuthorName,
                    RelativeTime.Format(c.WhenUtc, Loc)));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Content search failed"); }
        finally { IsSearching = false; Searched = true; ShowEmpty = Results.Count == 0; }
    }

    public void Choose(string? sha) => ChosenSha = sha;
}
