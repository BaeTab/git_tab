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
    public ObservableCollection<CommentInfo> Comments { get; } = new();

    [ObservableProperty] private bool _isLoading;

    public bool IsEmpty => !IsLoading && PullRequests.Count == 0 && Issues.Count == 0;
    public bool HasResults => !IsLoading && (PullRequests.Count > 0 || Issues.Count > 0);

    // ---- Comments (for whichever PR or issue is selected) -----------------

    [ObservableProperty] private PullRequestInfo? _selectedPullRequest;
    [ObservableProperty] private IssueInfo? _selectedIssue;
    [ObservableProperty] private bool _isLoadingComments;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PostCommentCommand))]
    private string _newCommentBody = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PostCommentCommand))]
    private bool _isPostingComment;

    [ObservableProperty] private string? _commentStatusMessage;

    private bool _syncingSelection;
    private int? _currentNumber;
    private bool _currentIsPullRequest;

    public bool HasSelectedItem => SelectedPullRequest is not null || SelectedIssue is not null;
    public bool HasComments => !IsLoadingComments && Comments.Count > 0;
    public bool CommentsEmpty => HasSelectedItem && !IsLoadingComments && Comments.Count == 0;

    partial void OnSelectedPullRequestChanged(PullRequestInfo? value)
    {
        if (_syncingSelection) return;
        if (value is null)
        {
            if (SelectedIssue is null) ClearComments();
            return;
        }
        _syncingSelection = true;
        SelectedIssue = null;
        _syncingSelection = false;
        _ = LoadCommentsAsync(value.Number, isPullRequest: true);
    }

    partial void OnSelectedIssueChanged(IssueInfo? value)
    {
        if (_syncingSelection) return;
        if (value is null)
        {
            if (SelectedPullRequest is null) ClearComments();
            return;
        }
        _syncingSelection = true;
        SelectedPullRequest = null;
        _syncingSelection = false;
        _ = LoadCommentsAsync(value.Number, isPullRequest: false);
    }

    private void ClearComments()
    {
        _currentNumber = null;
        Comments.Clear();
        CommentStatusMessage = null;
        NotifySelectionChanged();
    }

    private async Task LoadCommentsAsync(int number, bool isPullRequest)
    {
        _currentNumber = number;
        _currentIsPullRequest = isPullRequest;
        IsLoadingComments = true;
        CommentStatusMessage = null;
        NotifySelectionChanged();
        try
        {
            var comments = await _hosting.GetCommentsAsync(_remoteUrl, number, isPullRequest).ConfigureAwait(true);
            // The selection may have moved on again while this was in flight — drop stale results.
            if (_currentNumber != number || _currentIsPullRequest != isPullRequest) return;
            Comments.Clear();
            foreach (var c in comments) Comments.Add(c);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load comments for {RemoteUrl} #{Number}", _remoteUrl, number);
        }
        finally
        {
            if (_currentNumber == number && _currentIsPullRequest == isPullRequest)
            {
                IsLoadingComments = false;
                NotifySelectionChanged();
            }
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(CommentsEmpty));
        PostCommentCommand.NotifyCanExecuteChanged();
    }

    private bool CanPostComment() => _currentNumber is not null && !IsPostingComment && !string.IsNullOrWhiteSpace(NewCommentBody);

    [RelayCommand(CanExecute = nameof(CanPostComment))]
    private async Task PostComment()
    {
        if (_currentNumber is not { } number) return;
        var body = NewCommentBody.Trim();
        if (body.Length == 0) return;

        IsPostingComment = true;
        CommentStatusMessage = null;
        var isPullRequest = _currentIsPullRequest;
        try
        {
            var result = await _hosting.PostCommentAsync(_remoteUrl, number, isPullRequest, body).ConfigureAwait(true);
            if (result.Success)
            {
                NewCommentBody = string.Empty;
                CommentStatusMessage = Loc.T("Hosting.CommentPosted");
                await LoadCommentsAsync(number, isPullRequest).ConfigureAwait(true);
            }
            else
            {
                _logger.LogWarning("Failed to post comment for {RemoteUrl} #{Number}: {Error}", _remoteUrl, number, result.Error);
                CommentStatusMessage = Loc.T("Hosting.CommentPostFailed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post comment for {RemoteUrl} #{Number}", _remoteUrl, number);
            CommentStatusMessage = Loc.T("Hosting.CommentPostFailed");
        }
        finally
        {
            IsPostingComment = false;
        }
    }

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
