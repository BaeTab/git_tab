using System.Collections.ObjectModel;
using System.Diagnostics;
using GitTab.App.Localization;
using GitTab.App.Services.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the hosting dialog: loads the repo remote's open pull/merge requests and issues.</summary>
public sealed partial class HostingViewModel : ObservableObject
{
    private readonly IHostingClient _hosting;
    private readonly string _remoteUrl;
    private readonly ILogger _logger;

    public HostingViewModel(IHostingClient hosting, string remoteUrl, ILocalizationService loc, ILogger logger)
    {
        _hosting = hosting;
        _remoteUrl = remoteUrl;
        Loc = loc;
        _logger = logger;
        _ = LoadAsync();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<PullRequestInfo> PullRequests { get; } = new();
    public ObservableCollection<IssueInfo> Issues { get; } = new();

    [ObservableProperty] private bool _isLoading;

    public bool IsEmpty => !IsLoading && PullRequests.Count == 0 && Issues.Count == 0;
    public bool HasResults => !IsLoading && (PullRequests.Count > 0 || Issues.Count > 0);

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var prs = await _hosting.GetPullRequestsAsync(_remoteUrl).ConfigureAwait(true);
            PullRequests.Clear();
            foreach (var pr in prs) PullRequests.Add(pr);

            var issues = await _hosting.GetIssuesAsync(_remoteUrl).ConfigureAwait(true);
            Issues.Clear();
            foreach (var issue in issues) Issues.Add(issue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load hosting data for {RemoteUrl}", _remoteUrl);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasResults));
        }
    }

    [RelayCommand]
    private void Open(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to open {Url}", url); }
    }
}
