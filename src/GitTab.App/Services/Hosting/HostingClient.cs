using System.Net.Http;
using System.Net.Http.Headers;
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

    private static HttpRequestMessage GitHubRequest(string url, string pat)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
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

    private static HttpRequestMessage GitLabRequest(string url, string pat)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
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

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitTab", AppInfo.Version));
        return http;
    }
}
