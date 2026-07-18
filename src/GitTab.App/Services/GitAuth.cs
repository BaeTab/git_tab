using GitTab.App.Localization;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

/// <summary>
/// Shared HTTPS auth flow used by both the main window and the standalone Explorer dialogs: run a
/// network op, and if it fails with an authentication error, prompt for a username + token in the
/// GUI, store it in Windows Credential Manager, and retry once.
/// </summary>
public static class GitAuth
{
    /// <summary>Runs <paramref name="op"/>; on an auth failure prompts + stores creds + retries once.
    /// Returns the final <see cref="GitResult"/> (which may itself be a failure). May throw if the op throws.</summary>
    public static async Task<GitResult> RunWithRetryAsync(
        Func<Task<GitResult>> op,
        IRepositoryService repo, ICredentialStore credentials,
        IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        var result = await op().ConfigureAwait(true);
        if (result.Success || !IsAuthFailure(result)) return result;

        var input = dialogs.PromptCredentials(CredentialKey.HostLabel(repo.GetRemoteUrl()));
        if (input is null) return result;

        var key = CredentialKey.FromUrl(repo.GetRemoteUrl());
        if (key is not null)
        {
            try { credentials.Save(key, input.User, input.Secret); }
            catch (Exception ex) { logger.LogWarning(ex, "Saving credentials failed"); }
        }
        return await op().ConfigureAwait(true);
    }

    /// <summary>Heuristic: does this failure look like an authentication/authorization problem?</summary>
    public static bool IsAuthFailure(GitResult r)
    {
        var s = r.CombinedOutput;
        if (string.IsNullOrEmpty(s)) return false;
        string[] needles =
        {
            "Authentication failed", "could not read Username", "could not read Password",
            "terminal prompts disabled", "Invalid username or password",
            "Support for password authentication", "remote: HTTP Basic",
            "fatal: Authentication", "403 Forbidden", "401 Unauthorized",
            "Permission denied", "Login failed", "Repository not found"
        };
        return needles.Any(n => s.Contains(n, StringComparison.OrdinalIgnoreCase));
    }
}
