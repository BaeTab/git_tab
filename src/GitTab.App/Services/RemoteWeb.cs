namespace GitTab.App.Services;

/// <summary>
/// Turns a git remote URL into web URLs (repository home, "create pull/merge request") for GitHub
/// and GitLab, so Git Tab can open the right browser page without a hosting API token.
/// </summary>
public static class RemoteWeb
{
    /// <summary>Parse a remote URL into (host, owner, repo). Returns null if it isn't a recognizable
    /// https/ssh git URL with an owner/repo path.</summary>
    public static (string Host, string Owner, string Repo)? Parse(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();

        string host, path;
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            // git@host:owner/repo.git
            var at = url.IndexOf('@');
            var colon = url.IndexOf(':', at);
            if (colon < 0) return null;
            host = url[(at + 1)..colon];
            path = url[(colon + 1)..];
        }
        else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                 (uri.Scheme is "http" or "https" or "ssh"))
        {
            host = uri.Host;
            path = uri.AbsolutePath.TrimStart('/');
        }
        else
        {
            return null;
        }

        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            path = path[..^4];
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || string.IsNullOrEmpty(host)) return null;

        var owner = segments[0];
        var repo = segments[^1]; // last segment (supports GitLab subgroups a/b/repo)
        return (host, owner, repo);
    }

    /// <summary>The repository's web home page, or null.</summary>
    public static string? RepoUrl(string? remoteUrl)
    {
        if (Parse(remoteUrl) is not { } r) return null;
        return $"https://{r.Host}/{r.Owner}/{r.Repo}";
    }

    /// <summary>URL that opens the "create pull/merge request" page for <paramref name="branch"/>,
    /// pre-filled. Supports GitHub and GitLab; null otherwise.</summary>
    public static string? PullRequestUrl(string? remoteUrl, string? branch)
    {
        if (Parse(remoteUrl) is not { } r || string.IsNullOrWhiteSpace(branch)) return null;
        var b = Uri.EscapeDataString(branch.Trim());
        if (r.Host.Contains("github", StringComparison.OrdinalIgnoreCase))
            return $"https://{r.Host}/{r.Owner}/{r.Repo}/compare/{b}?expand=1";
        if (r.Host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
            return $"https://{r.Host}/{r.Owner}/{r.Repo}/-/merge_requests/new?merge_request%5Bsource_branch%5D={b}";
        return null;
    }
}
