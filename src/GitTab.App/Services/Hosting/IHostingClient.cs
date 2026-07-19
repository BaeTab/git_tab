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
}
