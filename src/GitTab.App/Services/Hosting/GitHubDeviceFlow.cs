using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GitTab.App.Services.Hosting;

/// <summary>
/// GitHub OAuth <b>device flow</b> sign-in. The flow is fully implemented, but it requires a
/// registered GitHub OAuth App <see cref="ClientId"/> — which this open-source project does not ship.
/// Until a maintainer registers one and sets <see cref="ClientId"/> (or the
/// <c>GITTAB_GITHUB_CLIENT_ID</c> environment variable), <see cref="IsConfigured"/> is false and the
/// app authenticates with a Personal Access Token instead. See docs/RELEASE.md.
/// </summary>
public sealed class GitHubDeviceFlow
{
    // Empty by default — register an OAuth App at https://github.com/settings/apps and set this
    // (or the GITTAB_GITHUB_CLIENT_ID env var) to enable device sign-in.
    private const string ClientId = "";

    private const string Scope = "repo read:user";

    private static string? ResolvedClientId
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("GITTAB_GITHUB_CLIENT_ID");
            return !string.IsNullOrWhiteSpace(env) ? env : (ClientId.Length > 0 ? ClientId : null);
        }
    }

    public bool IsConfigured => ResolvedClientId is not null;

    public sealed record DeviceCode(string UserCode, string VerificationUri, string DeviceCodeValue, int Interval, int ExpiresIn);

    /// <summary>Step 1: request a user code + verification URL. Null when not configured or on error.</summary>
    public async Task<DeviceCode?> RequestDeviceCodeAsync(HttpClient http, CancellationToken ct = default)
    {
        if (ResolvedClientId is not { } clientId) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["scope"] = Scope
                })
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            return new DeviceCode(
                root.GetProperty("user_code").GetString() ?? string.Empty,
                root.GetProperty("verification_uri").GetString() ?? string.Empty,
                root.GetProperty("device_code").GetString() ?? string.Empty,
                root.TryGetProperty("interval", out var i) ? i.GetInt32() : 5,
                root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 900);
        }
        catch { return null; }
    }

    /// <summary>Step 2: poll until the user authorizes, returning the access token (or null).</summary>
    public async Task<string?> PollForTokenAsync(HttpClient http, DeviceCode code, CancellationToken ct = default)
    {
        if (ResolvedClientId is not { } clientId) return null;
        var deadline = code.ExpiresIn;
        var interval = Math.Max(1, code.Interval);
        for (int waited = 0; waited < deadline; waited += interval)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), ct).ConfigureAwait(false);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = clientId,
                        ["device_code"] = code.DeviceCodeValue,
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                    })
                };
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                var root = doc.RootElement;
                if (root.TryGetProperty("access_token", out var tok)) return tok.GetString();
                if (root.TryGetProperty("error", out var err) &&
                    err.GetString() is not ("authorization_pending" or "slow_down"))
                    return null; // access_denied / expired_token
            }
            catch { /* transient — keep polling until the deadline */ }
        }
        return null;
    }
}
