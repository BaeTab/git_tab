using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

public sealed record UpdateInfo(Version Version, string TagName, string ReleaseUrl, string? InstallerUrl, string? Notes);

public interface IUpdateService
{
    /// <summary>Returns info about a newer GitHub release, or null if up to date / unreachable.</summary>
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>Downloads the installer asset to a temp file, returning its path (or null on failure).</summary>
    Task<string?> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Launches the installer and shuts down the app so it can be replaced.</summary>
    void LaunchInstallerAndExit(string installerPath);
}

/// <summary>Release-based updater backed by the GitHub Releases API. No custom update server.</summary>
public sealed class GitHubUpdateService : IUpdateService
{
    private static readonly Uri LatestReleaseApi =
        new($"https://api.github.com/repos/{AppInfo.RepoOwner}/{AppInfo.RepoName}/releases/latest");

    private readonly ILogger<GitHubUpdateService> _logger;

    public GitHubUpdateService(ILogger<GitHubUpdateService> logger) => _logger = logger;

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = CreateClient();
            using var resp = await http.GetAsync(LatestReleaseApi, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Update check: GitHub returned {Status}", resp.StatusCode);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) return null;

            var latest = ParseVersion(tag);
            if (latest is null || latest <= AppInfo.SemVer)
            {
                _logger.LogDebug("Update check: current {Current} is up to date (latest {Latest}).", AppInfo.SemVer, latest);
                return null;
            }

            string? installerUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        installerUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                        break;
                    }
                }
            }

            var releaseUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            _logger.LogInformation("Update available: {Tag}", tag);
            return new UpdateInfo(latest, tag!, releaseUrl, installerUrl, notes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            return null;
        }
    }

    public async Task<string?> DownloadInstallerAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.InstallerUrl)) return null;
        try
        {
            using var http = CreateClient();
            using var resp = await http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;
            var path = Path.Combine(Path.GetTempPath(), $"GitTab-Setup-{update.TagName}.exe");

            await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var dst = File.Create(path);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }

            _logger.LogInformation("Downloaded installer to {Path}", path);
            return path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Installer download failed");
            return null;
        }
    }

    public void LaunchInstallerAndExit(string installerPath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch installer {Path}", installerPath);
        }
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitTab-Updater", AppInfo.Version));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    private static Version? ParseVersion(string tag)
    {
        var s = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(s, out var v) ? v : null;
    }
}
