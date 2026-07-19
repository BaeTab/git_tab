using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

public sealed record UpdateInfo(Version Version, string TagName, string ReleaseUrl, string? InstallerUrl, string? Notes, string? ChecksumUrl = null);

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

            string? installerUrl = null, checksumUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    var dl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    if (name is null || dl is null) continue;
                    if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) checksumUrl = dl;
                    else if (installerUrl is null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) installerUrl = dl;
                }
            }

            var releaseUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            _logger.LogInformation("Update available: {Tag} (checksum {HasSum})", tag, checksumUrl is not null ? "yes" : "no");
            return new UpdateInfo(latest, tag!, releaseUrl, installerUrl, notes, checksumUrl);
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

            await using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await WriteStreamToFileAsync(src, path, total, progress, ct).ConfigureAwait(false);

            // Integrity: verify the download against the release's published SHA-256 before we ever
            // launch it. If a checksum is published and doesn't match, refuse (possible tampering).
            if (!string.IsNullOrWhiteSpace(update.ChecksumUrl))
            {
                var expected = await FetchExpectedHashAsync(http, update.ChecksumUrl!, ct).ConfigureAwait(false);
                if (expected is null)
                {
                    _logger.LogWarning("Could not read published checksum; refusing to launch unverified installer.");
                    TryDelete(path);
                    return null;
                }
                if (!await VerifyChecksumAsync(path, expected, ct).ConfigureAwait(false))
                {
                    _logger.LogError("Installer checksum mismatch; refusing to launch.");
                    TryDelete(path);
                    return null;
                }
                _logger.LogInformation("Installer checksum verified.");
            }
            else
            {
                _logger.LogWarning("Release has no .sha256 asset; launching without integrity verification.");
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

    /// <summary>True if the expected hash (first hex token of a checksum file) equals the actual hash.</summary>
    public static bool HashesMatch(string? expectedChecksumFileContent, string? actualHex)
    {
        if (string.IsNullOrWhiteSpace(expectedChecksumFileContent) || string.IsNullOrWhiteSpace(actualHex)) return false;
        // Checksum files are typically "<hex>  <filename>"; take the first whitespace-delimited token.
        var token = expectedChecksumFileContent.Trim().Split(new[] { ' ', '\t', '\r', '\n', '*' },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is not null && token.Equals(actualHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> FetchExpectedHashAsync(HttpClient http, string url, CancellationToken ct)
    {
        try { return await http.GetStringAsync(url, ct).ConfigureAwait(false); }
        catch { return null; }
    }

    /// <summary>Streams <paramref name="source"/> into <paramref name="path"/> and CLOSES the file
    /// before returning, so the caller can immediately hash it. (Hashing while the write stream is
    /// still open throws, because <see cref="File.Create(string)"/> holds it with FileShare.None.)</summary>
    public static async Task WriteStreamToFileAsync(Stream source, string path, long total,
        IProgress<double>? progress, CancellationToken ct)
    {
        await using var dst = File.Create(path);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }
    }

    /// <summary>True if the file at <paramref name="path"/> matches the published checksum content.</summary>
    public static async Task<bool> VerifyChecksumAsync(string path, string expectedChecksumContent, CancellationToken ct = default)
        => HashesMatch(expectedChecksumContent, await ComputeSha256Async(path, ct).ConfigureAwait(false));

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
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
