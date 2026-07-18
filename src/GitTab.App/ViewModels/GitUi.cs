using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Models;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Runs a git write/network operation and surfaces failures to the user without crashing.</summary>
public static class GitUi
{
    public static async Task<bool> RunAsync(
        Func<Task<GitResult>> op,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogger logger)
    {
        try
        {
            var result = await op().ConfigureAwait(true);
            if (!result.Success)
            {
                logger.LogWarning("git failed ({Code}): {Cmd}\n{Out}", result.ExitCode, result.CommandLine, result.CombinedOutput);
                var body = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
                dialogs.Error(body, loc.T("Error.GitFailed"));
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "git operation threw");
            dialogs.Error(ex.Message, loc.T("Error.GitFailed"));
            return false;
        }
    }
}
