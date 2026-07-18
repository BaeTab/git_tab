using System.Diagnostics;
using System.Text;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using Microsoft.Extensions.Logging;

namespace GitTab.Core.Git;

/// <summary>
/// Runs git.exe out-of-process. stdout/stderr are captured asynchronously to avoid pipe
/// deadlocks. GIT_TERMINAL_PROMPT=0 stops git from blocking on a stdin credential prompt
/// (the GUI credential helper still works); GIT_PAGER/PAGER are disabled so output is raw.
/// </summary>
public sealed class GitCommandRunner : IGitCommandRunner
{
    private readonly ILogger<GitCommandRunner> _logger;
    private readonly string _gitExe;
    private readonly string? _askPassExe;
    private readonly TimeSpan _timeout;

    /// <param name="gitExecutable">Full path to git.exe (see <see cref="GitExecutableLocator"/>), or "git" for PATH.</param>
    /// <param name="askPassExe">
    /// Path to an executable that answers git credential prompts (our own exe in <c>--askpass</c>
    /// mode). When set, HTTPS auth works with no credential helper installed — git calls it via
    /// GIT_ASKPASS and it returns credentials from the GUI-managed store.
    /// </param>
    public GitCommandRunner(ILogger<GitCommandRunner> logger, string gitExecutable = "git",
        string? askPassExe = null, TimeSpan? timeout = null)
    {
        _logger = logger;
        _gitExe = gitExecutable;
        _askPassExe = askPassExe;
        _timeout = timeout ?? TimeSpan.FromMinutes(5);
    }

    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var commandLine = "git " + string.Join(' ', arguments.Select(Quote));

        var psi = new ProcessStartInfo
        {
            FileName = _gitExe,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        // Deterministic, non-interactive-safe environment.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["GIT_PAGER"] = "cat";
        psi.Environment["PAGER"] = "cat";
        psi.Environment["LC_ALL"] = "C";
        // Never open an interactive editor (would hang a windowless process). ":" is the shell
        // no-op that git runs via its bundled sh, so --continue/merge keep their default messages.
        psi.Environment["GIT_EDITOR"] = ":";

        // GUI credential provider: git calls this exe for username/password when no credential
        // helper supplies them, so HTTPS auth works even on a bare git with nothing configured.
        if (!string.IsNullOrEmpty(_askPassExe))
        {
            psi.Environment["GIT_ASKPASS"] = _askPassExe;
            psi.Environment["GITTAB_ASKPASS"] = "1";
        }

        if (environment is not null)
            foreach (var kv in environment)
                psi.Environment[kv.Key] = kv.Value;

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var outDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var errDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) outDone.TrySetResult(true);
            else stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) errDone.TrySetResult(true);
            else stderr.AppendLine(e.Data);
        };

        try
        {
            if (!process.Start())
                return GitResult.Fault(commandLine, "git 프로세스를 시작하지 못했습니다. (Failed to start git.)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start git for: {Command}", commandLine);
            return GitResult.Fault(commandLine,
                $"git 실행 파일을 찾을 수 없습니다. PATH에 git이 있는지 확인하세요. ({ex.Message})");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        // We do not send stdin; close it so anything waiting on it fails fast.
        try { process.StandardInput.Close(); } catch { /* ignore */ }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            await Task.WhenAll(outDone.Task, errDone.Task).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var reason = cancellationToken.IsCancellationRequested
                ? "작업이 취소되었습니다. (Cancelled.)"
                : $"git 명령이 {_timeout.TotalSeconds:0}초 내에 끝나지 않아 중단했습니다. (Timed out.)";
            _logger.LogWarning("git command cancelled/timed out: {Command}", commandLine);
            return GitResult.Fault(commandLine, reason);
        }

        var result = new GitResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
            CommandLine = commandLine
        };

        if (result.Success)
            _logger.LogDebug("git ok: {Command}", commandLine);
        else
            _logger.LogInformation("git exit {Code}: {Command}\n{Err}", result.ExitCode, commandLine, result.StandardError.Trim());

        return result;
    }

    public async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var r = await RunAsync(Environment.CurrentDirectory, new[] { "--version" }, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return r.Success && r.StandardOutput.Contains("git version", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }

    private static string Quote(string arg) =>
        arg.Length == 0 || arg.Contains(' ') ? $"\"{arg}\"" : arg;
}
