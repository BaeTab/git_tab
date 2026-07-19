namespace GitTab.App.Services.Hosting;

/// <summary>An open pull request (GitHub) or merge request (GitLab).</summary>
public sealed record PullRequestInfo(int Number, string Title, string Author, string State, string Url, string SourceBranch);

/// <summary>An issue on the hosting service.</summary>
public sealed record IssueInfo(int Number, string Title, string Author, string State, string Url);

/// <summary>A single comment on a pull/merge request or issue conversation.</summary>
public sealed record CommentInfo(string Author, string Body, string CreatedAt, string? AvatarUrl = null);

/// <summary>Outcome of an operation that can fail without throwing (e.g. posting a comment) — carries
/// a short diagnostic message on failure (missing token scope, network error, HTTP status, etc.).</summary>
public sealed record HostingResult(bool Success, string? Error = null)
{
    public static HostingResult Ok() => new(true);
    public static HostingResult Fail(string error) => new(false, error);
}

/// <summary>Combined CI state for a commit.</summary>
public enum CiStatus
{
    None,      // no CI / unknown
    Pending,   // running or queued
    Success,   // all checks passed
    Failure    // at least one check failed
}
