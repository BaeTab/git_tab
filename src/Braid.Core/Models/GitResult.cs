namespace Braid.Core.Models;

/// <summary>Outcome of a git.exe invocation. Never throws for non-zero exit; callers inspect <see cref="Success"/>.</summary>
public sealed class GitResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }

    /// <summary>The command line that was run (for logging/UI display), e.g. "git push origin main".</summary>
    public string CommandLine { get; init; } = string.Empty;

    /// <summary>True when git exited 0.</summary>
    public bool Success => ExitCode == 0;

    /// <summary>True when the process could not be started / timed out (exit code &lt; 0).</summary>
    public bool Faulted => ExitCode < 0;

    /// <summary>Combined stderr+stdout, trimmed — convenient for surfacing to the user.</summary>
    public string CombinedOutput
    {
        get
        {
            var err = StandardError.Trim();
            var outp = StandardOutput.Trim();
            if (err.Length > 0 && outp.Length > 0) return err + "\n" + outp;
            return err.Length > 0 ? err : outp;
        }
    }

    public static GitResult Fault(string commandLine, string message) => new()
    {
        ExitCode = -1,
        StandardOutput = string.Empty,
        StandardError = message,
        CommandLine = commandLine
    };
}
