namespace GitTab.App.Services.Hosting;

/// <summary>
/// Read-only access to a repository's GitHub/GitLab hosting: open pull/merge requests, issues, and
/// per-commit CI status. Authenticates with the PAT already stored for the remote's host (via the
/// credential store); returns empty/None when the host is unsupported or no token is available.
/// </summary>
public interface IHostingClient
{
    /// <summary>True when <paramref name="remoteUrl"/> is a recognized GitHub or GitLab remote.</summary>
    bool IsSupported(string? remoteUrl);

    Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(string remoteUrl, CancellationToken ct = default);
    Task<IReadOnlyList<IssueInfo>> GetIssuesAsync(string remoteUrl, CancellationToken ct = default);
    Task<CiStatus> GetCommitStatusAsync(string remoteUrl, string sha, CancellationToken ct = default);

    /// <summary>Fetch the conversation comments for a pull/merge request or issue, oldest first.
    /// <paramref name="isPullRequest"/> selects the merge-request vs. issue endpoint on GitLab (GitHub
    /// uses a single issues/comments endpoint for both). Returns an empty list on any failure — never
    /// throws to the UI.</summary>
    Task<IReadOnlyList<CommentInfo>> GetCommentsAsync(string remoteUrl, int number, bool isPullRequest, CancellationToken ct = default);

    /// <summary>Post a new comment on a pull/merge request or issue. <paramref name="isPullRequest"/>
    /// selects the merge-request vs. issue endpoint on GitLab. Never throws to the UI: auth/permission
    /// (the stored PAT may lack write scope) and network failures come back as a failed
    /// <see cref="HostingResult"/> with a diagnostic message.</summary>
    Task<HostingResult> PostCommentAsync(string remoteUrl, int number, bool isPullRequest, string body, CancellationToken ct = default);
}
