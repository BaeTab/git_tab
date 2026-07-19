namespace GitTab.App.Services.Hosting;

/// <summary>An open pull request (GitHub) or merge request (GitLab).</summary>
public sealed record PullRequestInfo(int Number, string Title, string Author, string State, string Url, string SourceBranch);

/// <summary>An issue on the hosting service.</summary>
public sealed record IssueInfo(int Number, string Title, string Author, string State, string Url);

/// <summary>Combined CI state for a commit.</summary>
public enum CiStatus
{
    None,      // no CI / unknown
    Pending,   // running or queued
    Success,   // all checks passed
    Failure    // at least one check failed
}
