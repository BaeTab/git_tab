using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>A contributor row for display, with a 0..1 fraction of the top contributor's commits.</summary>
public sealed record ContributorRow(string Name, string Email, int Commits, double Fraction);

/// <summary>Backs the repository-statistics dashboard: commit/branch/tag counts and top contributors.</summary>
public sealed partial class StatsViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ILogger _logger;

    public StatsViewModel(IRepositoryService repo, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        Loc = loc;
        _logger = logger;
        _ = RefreshAsync();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<ContributorRow> Contributors { get; } = new();

    [ObservableProperty] private int _commitCount;
    [ObservableProperty] private int _branchCount;
    [ObservableProperty] private int _tagCount;
    [ObservableProperty] private string _lastActivity = "";
    [ObservableProperty] private bool _isLoading;

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var stats = await _repo.GetRepoStatsAsync().ConfigureAwait(true);
            CommitCount = stats.CommitCount;
            BranchCount = stats.BranchCount;
            TagCount = stats.TagCount;
            LastActivity = stats.LastActivity?.LocalDateTime.ToString("yyyy-MM-dd") ?? "";

            var top = stats.Contributors.Count > 0 ? stats.Contributors[0].Commits : 0;
            Contributors.Clear();
            foreach (var c in stats.Contributors.Take(20))
                Contributors.Add(new ContributorRow(c.Name, c.Email, c.Commits, top > 0 ? (double)c.Commits / top : 0));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load repository statistics");
        }
        finally
        {
            IsLoading = false;
        }
    }
}
