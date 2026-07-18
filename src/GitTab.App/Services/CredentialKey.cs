namespace GitTab.App.Services;

/// <summary>Maps a git remote URL to a per-host credential key and a display label.</summary>
public static class CredentialKey
{
    public const string Prefix = "GitTab:";

    /// <summary>"GitTab:https://github.com" — scheme + host, ignoring userinfo/port/path so a token
    /// entered once is reused for every repo on that host. Returns null for non-HTTP(S) remotes.</summary>
    public static string? FromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("http" or "https")) return null;
        return $"{Prefix}{uri.Scheme}://{uri.Host}";
    }

    /// <summary>Human-readable host for the prompt (e.g. "github.com").</summary>
    public static string HostLabel(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return uri.Host;
        return string.IsNullOrWhiteSpace(url) ? "remote" : url!;
    }
}
