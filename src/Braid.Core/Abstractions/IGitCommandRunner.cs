using Braid.Core.Models;

namespace Braid.Core.Abstractions;

/// <summary>
/// Low-level runner for git.exe. Used for all write/network operations (commit, checkout,
/// branch, merge, rebase, fetch, pull, push) where the CLI's auth/credential-helper
/// integration and stability beat an in-process library.
/// </summary>
public interface IGitCommandRunner
{
    /// <summary>Runs <c>git</c> with the given arguments in <paramref name="workingDirectory"/>.
    /// Never throws for non-zero exit codes — inspect <see cref="GitResult.Success"/>.</summary>
    Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);

    /// <summary>True if a usable git executable was found on the system.</summary>
    Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default);
}
