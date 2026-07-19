using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services.Hosting;

/// <summary>
/// GitHub/GitLab REST implementation of <see cref="IHostingClient"/>. Reads the PAT already stored
/// for the remote's host in the Windows Credential Manager; when there's no token (or the host isn't
/// recognized), every method returns an empty result instead of calling the network or throwing.
/// </summary>
public sealed class HostingClient : IHostingClient
{
    private static readonly HttpClient Http = CreateClient();

    private readonly ICredentialStore _credentials;
    private readonly ILogger<HostingClient> _logger;

    public HostingClient(ICredentialStore credentials, ILogger<HostingClient> logger)
    {
        _credentials = credentials;
        _logger = logger;
    }

    public bool IsSupported(string? remoteUrl)
    {
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return false;
        return IsGitHub(r.Host) || IsGitLab(r.Host);
    }

    public async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(string remoteUrl, CancellationToken ct = default)
    {
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return Array.Empty<PullRequestInfo>();
        var pat = GetToken(remoteUrl);
        if (pat is null) return Array.Empty<PullRequestInfo>();

        try
        {
            if (IsGitHub(r.Host)) return await GitHubPullRequestsAsync(r, pat, ct).ConfigureAwait(false);
            if (IsGitLab(r.Host)) return await GitLabMergeRequestsAsync(r, pat, ct).ConfigureAwait(false);
            return Array.Empty<PullRequestInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch pull requests for {RemoteUrl}", remoteUrl);
            return Array.Empty<PullRequestInfo>();
        }
    }

    public async Task<IReadOnlyList<IssueInfo>> GetIssuesAsync(string remoteUrl, CancellationToken ct = default)
    {
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return Array.Empty<IssueInfo>();
        var pat = GetToken(remoteUrl);
        if (pat is null) return Array.Empty<IssueInfo>();

        try
        {
            if (IsGitHub(r.Host)) return await GitHubIssuesAsync(r, pat, ct).ConfigureAwait(false);
            if (IsGitLab(r.Host)) return await GitLabIssuesAsync(r, pat, ct).ConfigureAwait(false);
            return Array.Empty<IssueInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch issues for {RemoteUrl}", remoteUrl);
            return Array.Empty<IssueInfo>();
        }
    }

    public async Task<CiStatus> GetCommitStatusAsync(string remoteUrl, string sha, CancellationToken ct = default)
    {
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return CiStatus.None;
        var pat = GetToken(remoteUrl);
        if (pat is null) return CiStatus.None;

        try
        {
            if (IsGitHub(r.Host)) return await GitHubCommitStatusAsync(r, sha, pat, ct).ConfigureAwait(false);
            if (IsGitLab(r.Host)) return await GitLabCommitStatusAsync(r, sha, pat, ct).ConfigureAwait(false);
            return CiStatus.None;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch commit status for {RemoteUrl}", remoteUrl);
            return CiStatus.None;
        }
    }

    public async Task<IReadOnlyList<CommentInfo>> GetCommentsAsync(string remoteUrl, int number, bool isPullRequest, CancellationToken ct = default)
    {
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return Array.Empty<CommentInfo>();
        var pat = GetToken(remoteUrl);
        if (pat is null) return Array.Empty<CommentInfo>();

        try
        {
            if (IsGitHub(r.Host)) return await GitHubCommentsAsync(r, number, pat, ct).ConfigureAwait(false);
            if (IsGitLab(r.Host)) return await GitLabCommentsAsync(r, number, isPullRequest, pat, ct).ConfigureAwait(false);
            return Array.Empty<CommentInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch comments for {RemoteUrl} #{Number}", remoteUrl, number);
            return Array.Empty<CommentInfo>();
        }
    }

    public async Task<HostingResult> PostCommentAsync(string remoteUrl, int number, bool isPullRequest, string body, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) return HostingResult.Fail("Comment body is empty.");
        if (RemoteWeb.Parse(remoteUrl) is not { } r) return HostingResult.Fail("Unrecognized or unsupported remote URL.");
        var pat = GetToken(remoteUrl);
        if (pat is null) return HostingResult.Fail("No access token stored for this remote's host.");

        try
        {
            if (IsGitHub(r.Host)) return await GitHubPostCommentAsync(r, number, body, pat, ct).ConfigureAwait(false);
            if (IsGitLab(r.Host)) return await GitLabPostCommentAsync(r, number, isPullRequest, body, pat, ct).ConfigureAwait(false);
            return HostingResult.Fail("Unsupported remote host.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post comment for {RemoteUrl} #{Number}", remoteUrl, number);
            return HostingResult.Fail(ex.Message);
        }
    }

    // ---- GitHub -------------------------------------------------------

    private static string GitHubApiBase(string host) =>
        host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? "https://api.github.com" : $"https://{host}/api/v3";

    private async Task<IReadOnlyList<PullRequestInfo>> GitHubPullRequestsAsync(
        (string Host, string Owner, string Repo) r, string pat, CancellationToken ct)
    {
        var url = $"{GitHubApiBase(r.Host)}/repos/{r.Owner}/{r.Repo}/pulls?state=open&per_page=50";
        using var req = GitHubRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<PullRequestInfo>();

        var list = new List<PullRequestInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add(new PullRequestInfo(
                item.GetProperty("number").GetInt32(),
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("user", out var user) ? user.GetProperty("login").GetString() ?? "" : "",
                item.GetProperty("state").GetString() ?? "",
                item.GetProperty("html_url").GetString() ?? "",
                item.TryGetProperty("head", out var head) ? head.GetProperty("ref").GetString() ?? "" : ""));
        }
        return list;
    }

    private async Task<IReadOnlyList<IssueInfo>> GitHubIssuesAsync(
        (string Host, string Owner, string Repo) r, string pat, CancellationToken ct)
    {
        var url = $"{GitHubApiBase(r.Host)}/repos/{r.Owner}/{r.Repo}/issues?state=open&per_page=50";
        using var req = GitHubRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<IssueInfo>();

        var list = new List<IssueInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("pull_request", out _)) continue; // GitHub also lists PRs as issues
            list.Add(new IssueInfo(
                item.GetProperty("number").GetInt32(),
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("user", out var user) ? user.GetProperty("login").GetString() ?? "" : "",
                item.GetProperty("state").GetString() ?? "",
                item.GetProperty("html_url").GetString() ?? ""));
        }
        return list;
    }

    private async Task<CiStatus> GitHubCommitStatusAsync(
        (string Host, string Owner, string Repo) r, string sha, string pat, CancellationToken ct)
    {
        var url = $"{GitHubApiBase(r.Host)}/repos/{r.Owner}/{r.Repo}/commits/{sha}/status";
        using var req = GitHubRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return CiStatus.None;

        var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
        return state switch
        {
            "success" => CiStatus.Success,
            "pending" => CiStatus.Pending,
            "failure" or "error" => CiStatus.Failure,
            _ => CiStatus.None
        };
    }

    private async Task<IReadOnlyList<CommentInfo>> GitHubCommentsAsync(
        (string Host, string Owner, string Repo) r, int number, string pat, CancellationToken ct)
    {
        // PR conversation comments live at the issues/comments endpoint too (a PR is an issue on GitHub).
        var url = $"{GitHubApiBase(r.Host)}/repos/{r.Owner}/{r.Repo}/issues/{number}/comments?per_page=100";
        using var req = GitHubRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<CommentInfo>();

        var list = new List<CommentInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var user = item.TryGetProperty("user", out var u) ? u : (JsonElement?)null;
            list.Add(new CommentInfo(
                user?.TryGetProperty("login", out var login) == true ? login.GetString() ?? "" : "",
                item.GetProperty("body").GetString() ?? "",
                item.TryGetProperty("created_at", out var created) ? created.GetString() ?? "" : "",
                user?.TryGetProperty("avatar_url", out var avatar) == true ? avatar.GetString() : null));
        }
        return list;
    }

    private async Task<HostingResult> GitHubPostCommentAsync(
        (string Host, string Owner, string Repo) r, int number, string body, string pat, CancellationToken ct)
    {
        var url = $"{GitHubApiBase(r.Host)}/repos/{r.Owner}/{r.Repo}/issues/{number}/comments";
        using var req = GitHubRequest(HttpMethod.Post, url, pat);
        req.Content = JsonBody(body);
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.IsSuccessStatusCode) return HostingResult.Ok();

        var reason = await DescribeFailureAsync(resp, ct).ConfigureAwait(false);
        _logger.LogInformation("GitHub comment POST {Url} returned {Status}", url, resp.StatusCode);
        return HostingResult.Fail(reason);
    }

    private static HttpRequestMessage GitHubRequest(string url, string pat) => GitHubRequest(HttpMethod.Get, url, pat);

    private static HttpRequestMessage GitHubRequest(HttpMethod method, string url, string pat)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        return req;
    }

    private static bool IsGitHub(string host) => host.Contains("github", StringComparison.OrdinalIgnoreCase);

    // ---- GitLab -------------------------------------------------------

    private static string GitLabProjectId((string Host, string Owner, string Repo) r) =>
        Uri.EscapeDataString($"{r.Owner}/{r.Repo}");

    private async Task<IReadOnlyList<PullRequestInfo>> GitLabMergeRequestsAsync(
        (string Host, string Owner, string Repo) r, string pat, CancellationToken ct)
    {
        var url = $"https://{r.Host}/api/v4/projects/{GitLabProjectId(r)}/merge_requests?state=opened&per_page=50";
        using var req = GitLabRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<PullRequestInfo>();

        var list = new List<PullRequestInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add(new PullRequestInfo(
                item.GetProperty("iid").GetInt32(),
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("author", out var author) ? author.GetProperty("username").GetString() ?? "" : "",
                item.GetProperty("state").GetString() ?? "",
                item.GetProperty("web_url").GetString() ?? "",
                item.GetProperty("source_branch").GetString() ?? ""));
        }
        return list;
    }

    private async Task<IReadOnlyList<IssueInfo>> GitLabIssuesAsync(
        (string Host, string Owner, string Repo) r, string pat, CancellationToken ct)
    {
        var url = $"https://{r.Host}/api/v4/projects/{GitLabProjectId(r)}/issues?state=opened&per_page=50";
        using var req = GitLabRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<IssueInfo>();

        var list = new List<IssueInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            list.Add(new IssueInfo(
                item.GetProperty("iid").GetInt32(),
                item.GetProperty("title").GetString() ?? "",
                item.TryGetProperty("author", out var author) ? author.GetProperty("username").GetString() ?? "" : "",
                item.GetProperty("state").GetString() ?? "",
                item.GetProperty("web_url").GetString() ?? ""));
        }
        return list;
    }

    private async Task<CiStatus> GitLabCommitStatusAsync(
        (string Host, string Owner, string Repo) r, string sha, string pat, CancellationToken ct)
    {
        var url = $"https://{r.Host}/api/v4/projects/{GitLabProjectId(r)}/repository/commits/{sha}";
        using var req = GitLabRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return CiStatus.None;

        if (!doc.RootElement.TryGetProperty("last_pipeline", out var pipeline) || pipeline.ValueKind == JsonValueKind.Null)
            return CiStatus.None;
        var status = pipeline.TryGetProperty("status", out var s) ? s.GetString() : null;
        return status switch
        {
            "running" or "pending" => CiStatus.Pending,
            "failed" => CiStatus.Failure,
            "success" => CiStatus.Success,
            _ => CiStatus.None
        };
    }

    /// <summary>GitLab uses separate "notes" endpoints for merge requests and issues, unlike GitHub's
    /// single issues/comments endpoint — <paramref name="isPullRequest"/> picks the right one.</summary>
    private async Task<IReadOnlyList<CommentInfo>> GitLabCommentsAsync(
        (string Host, string Owner, string Repo) r, int number, bool isPullRequest, string pat, CancellationToken ct)
    {
        var kind = isPullRequest ? "merge_requests" : "issues";
        var url = $"https://{r.Host}/api/v4/projects/{GitLabProjectId(r)}/{kind}/{number}/notes?per_page=100&order_by=created_at&sort=asc";
        using var req = GitLabRequest(url, pat);
        using var doc = await GetJsonAsync(req, ct).ConfigureAwait(false);
        if (doc is null) return Array.Empty<CommentInfo>();

        var list = new List<CommentInfo>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            // Skip system notes (e.g. "changed the description") — they aren't user comments.
            if (item.TryGetProperty("system", out var sys) && sys.ValueKind == JsonValueKind.True) continue;

            var author = item.TryGetProperty("author", out var a) ? a : (JsonElement?)null;
            list.Add(new CommentInfo(
                author?.TryGetProperty("username", out var username) == true ? username.GetString() ?? "" : "",
                item.GetProperty("body").GetString() ?? "",
                item.TryGetProperty("created_at", out var created) ? created.GetString() ?? "" : "",
                author?.TryGetProperty("avatar_url", out var avatar) == true && avatar.ValueKind == JsonValueKind.String ? avatar.GetString() : null));
        }
        return list;
    }

    private async Task<HostingResult> GitLabPostCommentAsync(
        (string Host, string Owner, string Repo) r, int number, bool isPullRequest, string body, string pat, CancellationToken ct)
    {
        var kind = isPullRequest ? "merge_requests" : "issues";
        var url = $"https://{r.Host}/api/v4/projects/{GitLabProjectId(r)}/{kind}/{number}/notes";
        using var req = GitLabRequest(HttpMethod.Post, url, pat);
        req.Content = JsonBody(body);
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.IsSuccessStatusCode) return HostingResult.Ok();

        var reason = await DescribeFailureAsync(resp, ct).ConfigureAwait(false);
        _logger.LogInformation("GitLab comment POST {Url} returned {Status}", url, resp.StatusCode);
        return HostingResult.Fail(reason);
    }

    private static HttpRequestMessage GitLabRequest(string url, string pat) => GitLabRequest(HttpMethod.Get, url, pat);

    private static HttpRequestMessage GitLabRequest(HttpMethod method, string url, string pat)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("PRIVATE-TOKEN", pat);
        return req;
    }

    private static bool IsGitLab(string host) => host.Contains("gitlab", StringComparison.OrdinalIgnoreCase);

    // ---- Shared ---------------------------------------------------------

    private string? GetToken(string? remoteUrl)
    {
        var target = CredentialKey.FromUrl(remoteUrl);
        return target is null ? null : _credentials.Get(target)?.Secret;
    }

    /// <summary>Sends the request; returns the parsed body, or null on a non-success status (caller
    /// disposes the returned document).</summary>
    private async Task<JsonDocument?> GetJsonAsync(HttpRequestMessage req, CancellationToken ct)
    {
        using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogInformation("Hosting API {Url} returned {Status}", req.RequestUri, resp.StatusCode);
            return null;
        }
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return JsonDocument.Parse(bytes);
    }

    /// <summary>Builds the {"body": "..."} JSON payload both GitHub's issue-comment and GitLab's note
    /// endpoints expect for creating a comment.</summary>
    private static StringContent JsonBody(string body) =>
        new(JsonSerializer.Serialize(new { body }), Encoding.UTF8, "application/json");

    /// <summary>Turns a failed response into a short, non-throwing diagnostic string. 401/403 is called
    /// out specifically since the most common cause of a failed comment post is a PAT that lacks write
    /// scope (read-only tokens can list PRs/issues but can't post).</summary>
    private static async Task<string> DescribeFailureAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var status = (int)resp.StatusCode;
        if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return $"HTTP {status} {resp.StatusCode}: the stored token likely lacks write/comment permission.";

        string text;
        try { text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
        catch { text = ""; }
        if (text.Length > 200) text = text[..200] + "…";
        return string.IsNullOrWhiteSpace(text) ? $"HTTP {status} {resp.StatusCode}" : $"HTTP {status} {resp.StatusCode}: {text}";
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitTab", AppInfo.Version));
        return http;
    }
}
