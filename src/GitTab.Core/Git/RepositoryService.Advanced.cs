using System.IO;
using GitTab.Core.Diff;
using GitTab.Core.Models;
using LibGit2Sharp;

namespace GitTab.Core.Git;

/// <summary>
/// Advanced Git operations added in the Tier-1/2 batch: commit/tag signing + verification,
/// worktrees, Git LFS, richer submodules, patch import/export, extra stash operations, and
/// sparse-checkout. Kept in a partial file so the core facade stays readable.
/// </summary>
public sealed partial class RepositoryService
{
    // ---------------------------------------------------------------- signing

    /// <summary>Verify a commit's signature (git's <c>%G?</c> code).</summary>
    public async Task<CommitSignature> GetSignatureStatusAsync(string sha, CancellationToken ct = default)
    {
        if (!GitArg.IsSafe(sha)) return CommitSignature.None;
        var r = await Run(new[] { "log", "-1", "--format=%G?", sha }, ct).ConfigureAwait(false);
        if (!r.Success) return CommitSignature.Unknown;
        return r.StandardOutput.Trim() switch
        {
            "G" => CommitSignature.Good,
            "U" => CommitSignature.GoodUntrusted,
            "B" => CommitSignature.Bad,
            "N" or "" => CommitSignature.None,
            _ => CommitSignature.Unknown // E / X / Y / R
        };
    }

    /// <summary>Current signing configuration: whether commits are signed, the key, and format (openpgp/ssh).</summary>
    public async Task<(bool Enabled, string? Key, string? Format)> GetSigningConfigAsync(CancellationToken ct = default)
    {
        var enabled = (await Run(new[] { "config", "--get", "commit.gpgsign" }, ct).ConfigureAwait(false)).StandardOutput.Trim();
        var key = (await Run(new[] { "config", "--get", "user.signingkey" }, ct).ConfigureAwait(false)).StandardOutput.Trim();
        var fmt = (await Run(new[] { "config", "--get", "gpg.format" }, ct).ConfigureAwait(false)).StandardOutput.Trim();
        return (enabled.Equals("true", StringComparison.OrdinalIgnoreCase),
                key.Length == 0 ? null : key,
                fmt.Length == 0 ? null : fmt);
    }

    /// <summary>Write signing configuration to the repository (local) config.</summary>
    public async Task<GitResult> SetSigningConfigAsync(bool enabled, string? key, string? format, CancellationToken ct = default)
    {
        var r = await Run(new[] { "config", "--local", "commit.gpgsign", enabled ? "true" : "false" }, ct).ConfigureAwait(false);
        if (!r.Success) return r;
        if (!string.IsNullOrWhiteSpace(format))
        {
            var f = await Run(new[] { "config", "--local", "gpg.format", format! }, ct).ConfigureAwait(false);
            if (!f.Success) return f;
        }
        if (!string.IsNullOrWhiteSpace(key))
        {
            var k = await Run(new[] { "config", "--local", "user.signingkey", key! }, ct).ConfigureAwait(false);
            if (!k.Success) return k;
        }
        return r;
    }

    // ---------------------------------------------------------------- worktrees

    public async Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(CancellationToken ct = default)
    {
        var r = await Run(new[] { "worktree", "list", "--porcelain" }, ct).ConfigureAwait(false);
        if (!r.Success) return Array.Empty<WorktreeInfo>();

        string current;
        lock (_sync) { current = _workingDir ?? string.Empty; }

        var list = new List<WorktreeInfo>();
        string? path = null, head = null, branch = null;
        bool bare = false, detached = false, locked = false;

        void Flush()
        {
            if (path is null) return;
            list.Add(new WorktreeInfo
            {
                Path = path,
                HeadSha = head,
                Branch = branch,
                IsBare = bare,
                IsDetached = detached,
                IsLocked = locked,
                IsCurrent = SamePath(path, current)
            });
            path = head = branch = null;
            bare = detached = locked = false;
        }

        foreach (var raw in r.StandardOutput.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) { Flush(); continue; }
            if (line.StartsWith("worktree ", StringComparison.Ordinal)) { Flush(); path = line["worktree ".Length..].Trim(); }
            else if (line.StartsWith("HEAD ", StringComparison.Ordinal)) head = line["HEAD ".Length..].Trim();
            else if (line.StartsWith("branch ", StringComparison.Ordinal)) branch = ShortenRef(line["branch ".Length..].Trim());
            else if (line == "bare") bare = true;
            else if (line == "detached") detached = true;
            else if (line.StartsWith("locked", StringComparison.Ordinal)) locked = true;
        }
        Flush();
        return list;
    }

    public Task<GitResult> WorktreeAddAsync(string path, string? branch, bool createBranch, CancellationToken ct = default)
    {
        if (Guard(path) is { } bad) return Task.FromResult(bad);
        var args = new List<string> { "worktree", "add" };
        if (createBranch && !string.IsNullOrWhiteSpace(branch))
        {
            if (Guard(branch) is { } b) return Task.FromResult(b);
            args.Add("-b"); args.Add(branch!);
            args.Add(path);
        }
        else
        {
            args.Add(path);
            if (!string.IsNullOrWhiteSpace(branch))
            {
                if (Guard(branch) is { } b) return Task.FromResult(b);
                args.Add(branch!);
            }
        }
        return Run(args, ct);
    }

    public Task<GitResult> WorktreeRemoveAsync(string path, bool force, CancellationToken ct = default)
    {
        if (Guard(path) is { } bad) return Task.FromResult(bad);
        var args = new List<string> { "worktree", "remove" };
        if (force) args.Add("--force");
        args.Add(path);
        return Run(args, ct);
    }

    public Task<GitResult> WorktreePruneAsync(CancellationToken ct = default)
        => Run(new[] { "worktree", "prune" }, ct);

    // ---------------------------------------------------------------- Git LFS

    public async Task<LfsStatus> GetLfsStatusAsync(CancellationToken ct = default)
    {
        var ver = await Run(new[] { "lfs", "version" }, ct).ConfigureAwait(false);
        if (!ver.Success) return new LfsStatus { Available = false };

        var patterns = new List<string>();
        var track = await Run(new[] { "lfs", "track" }, ct).ConfigureAwait(false);
        if (track.Success)
        {
            foreach (var raw in track.StandardOutput.Split('\n'))
            {
                var t = raw.Trim();
                if (t.Length == 0 || t.StartsWith("Listing", StringComparison.Ordinal)) continue;
                var idx = t.IndexOf(" (", StringComparison.Ordinal);
                patterns.Add(idx > 0 ? t[..idx].Trim() : t);
            }
        }

        int count = 0;
        var ls = await Run(new[] { "lfs", "ls-files" }, ct).ConfigureAwait(false);
        if (ls.Success) count = ls.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

        return new LfsStatus { Available = true, TrackedPatterns = patterns, TrackedFileCount = count };
    }

    public Task<GitResult> LfsTrackAsync(string pattern, CancellationToken ct = default)
        => Guard(pattern) is { } bad ? Task.FromResult(bad) : Run(new[] { "lfs", "track", pattern }, ct);

    public Task<GitResult> LfsUntrackAsync(string pattern, CancellationToken ct = default)
        => Guard(pattern) is { } bad ? Task.FromResult(bad) : Run(new[] { "lfs", "untrack", pattern }, ct);

    public Task<GitResult> LfsPullAsync(CancellationToken ct = default)
        => Run(new[] { "lfs", "pull" }, ct);

    // ---------------------------------------------------------------- submodules (rich)

    public IReadOnlyList<SubmoduleInfo> GetSubmodules()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var list = new List<SubmoduleInfo>();
            foreach (var s in repo.Submodules)
            {
                var st = s.RetrieveStatus();
                string status = st.HasFlag(SubmoduleStatus.WorkDirUninitialized) ? "not-initialized"
                    : (st.HasFlag(SubmoduleStatus.WorkDirModified)
                       || st.HasFlag(SubmoduleStatus.WorkDirFilesModified)
                       || s.HeadCommitId != s.WorkDirCommitId) ? "out-of-date"
                    : "initialized";
                list.Add(new SubmoduleInfo
                {
                    Path = s.Path,
                    Url = s.Url,
                    HeadSha = s.HeadCommitId?.Sha,
                    Status = status
                });
            }
            return list;
        }
    }

    public Task<GitResult> SubmoduleAddAsync(string url, string path, CancellationToken ct = default)
        => Guard(url, path) is { } bad ? Task.FromResult(bad) : Run(new[] { "submodule", "add", url, path }, ct);

    public Task<GitResult> SubmoduleSyncAsync(CancellationToken ct = default)
        => Run(new[] { "submodule", "sync", "--recursive" }, ct);

    public Task<GitResult> SubmoduleDeinitAsync(string path, bool force, CancellationToken ct = default)
    {
        if (Guard(path) is { } bad) return Task.FromResult(bad);
        var args = new List<string> { "submodule", "deinit" };
        if (force) args.Add("-f");
        args.Add("--"); args.Add(path);
        return Run(args, ct);
    }

    // ---------------------------------------------------------------- patch import/export

    /// <summary>Export a commit as a mailbox <c>.patch</c> file (git format-patch).</summary>
    public async Task<GitResult> ExportCommitPatchAsync(string sha, string filePath, CancellationToken ct = default)
    {
        if (Guard(sha) is { } bad) return bad;
        var r = await Run(new[] { "format-patch", "-1", "--stdout", sha }, ct).ConfigureAwait(false);
        if (!r.Success) return r;
        await File.WriteAllTextAsync(filePath, r.StandardOutput, ct).ConfigureAwait(false);
        return r;
    }

    /// <summary>Apply a patch file. <paramref name="asCommits"/> uses <c>git am</c> (recreates commits);
    /// otherwise <c>git apply</c> (working-tree changes only).</summary>
    public Task<GitResult> ApplyPatchAsync(string patchFile, bool asCommits, CancellationToken ct = default)
    {
        if (Guard(patchFile) is { } bad) return Task.FromResult(bad);
        return Run(asCommits ? new[] { "am", patchFile } : new[] { "apply", patchFile }, ct);
    }

    // ---------------------------------------------------------------- stash (extra)

    /// <summary>Raw diff of a stash entry (for preview).</summary>
    public async Task<string> GetStashDiffAsync(int index, CancellationToken ct = default)
    {
        var r = await Run(new[] { "stash", "show", "-p", $"stash@{{{index}}}" }, ct).ConfigureAwait(false);
        return r.Success ? r.StandardOutput : r.CombinedOutput;
    }

    /// <summary>Create a branch from a stash and drop it (git stash branch).</summary>
    public Task<GitResult> StashToBranchAsync(int index, string branch, CancellationToken ct = default)
        => Guard(branch) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "stash", "branch", branch, $"stash@{{{index}}}" }, ct);

    // ---------------------------------------------------------------- sparse-checkout

    public async Task<IReadOnlyList<string>> GetSparseCheckoutPatternsAsync(CancellationToken ct = default)
    {
        var r = await Run(new[] { "sparse-checkout", "list" }, ct).ConfigureAwait(false);
        if (!r.Success) return Array.Empty<string>();
        return r.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();
    }

    public Task<GitResult> SparseCheckoutSetAsync(IReadOnlyList<string> patterns, bool cone, CancellationToken ct = default)
    {
        foreach (var p in patterns)
            if (Guard(p) is { } bad) return Task.FromResult(bad);
        var args = new List<string> { "sparse-checkout", "set" };
        if (cone) args.Add("--cone");
        args.AddRange(patterns);
        return Run(args, ct);
    }

    public Task<GitResult> SparseCheckoutDisableAsync(CancellationToken ct = default)
        => Run(new[] { "sparse-checkout", "disable" }, ct);

    // ---------------------------------------------------------------- image diff / raw bytes

    /// <summary>Raw bytes of <paramref name="path"/> as of commit <paramref name="sha"/>, or null if absent.</summary>
    public byte[]? GetBlobBytes(string sha, string path)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            if (repo.Lookup<Commit>(sha) is not { } commit) return null;
            if (commit[path]?.Target is not Blob blob) return null;
            using var s = blob.GetContentStream();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }

    /// <summary>Raw bytes of the working-tree file, or null if it doesn't exist.</summary>
    public byte[]? GetWorkingBytes(string path)
    {
        var full = Path.Combine(EnsureWorkingDir(), path);
        return File.Exists(full) ? File.ReadAllBytes(full) : null;
    }

    // ---------------------------------------------------------------- whitespace-ignoring diff

    /// <summary>A commit file diff produced by git with options: ignore whitespace (-w) and/or a
    /// custom context-line count (-U&lt;n&gt;; pass a negative value for git's default of 3).</summary>
    public async Task<FileDiff> GetCommitFileDiffWithOptionsAsync(string sha, string path, bool ignoreWhitespace, int contextLines, CancellationToken ct = default)
    {
        if (!GitArg.IsSafe(sha)) return new FileDiff { Path = path };
        var args = new List<string> { "show", "--format=" };
        if (ignoreWhitespace) args.Add("-w");
        if (contextLines >= 0) args.Add($"-U{contextLines}");
        args.Add(sha);
        args.Add("--");
        args.Add(path);
        var r = await Run(args, ct).ConfigureAwait(false);
        return ParseRawDiff(path, r.StandardOutput);
    }

    /// <summary>A working-tree file diff with options: ignore whitespace (-w) and/or a custom
    /// context-line count (-U&lt;n&gt;; negative = git default).</summary>
    public async Task<FileDiff> GetWorkingFileDiffWithOptionsAsync(string path, bool staged, bool ignoreWhitespace, int contextLines, CancellationToken ct = default)
    {
        var args = new List<string> { "diff" };
        if (ignoreWhitespace) args.Add("-w");
        if (contextLines >= 0) args.Add($"-U{contextLines}");
        if (staged) args.Add("--staged");
        args.Add("--");
        args.Add(path);
        var r = await Run(args, ct).ConfigureAwait(false);
        return ParseRawDiff(path, r.StandardOutput);
    }

    private static FileDiff ParseRawDiff(string path, string raw)
        => string.IsNullOrWhiteSpace(raw)
            ? new FileDiff { Path = path }
            : UnifiedDiffParser.Parse(path, null, raw, isBinary: false);

    // ---------------------------------------------------------------- author / file restore

    /// <summary>Rewrite HEAD's author (name/email) without changing its message.</summary>
    public Task<GitResult> AmendAuthorAsync(string name, string email, CancellationToken ct = default)
    {
        if (Guard(name, email) is { } bad) return Task.FromResult(bad);
        return Run(new[] { "commit", "--amend", "--no-edit", $"--author={name} <{email}>" }, ct);
    }

    /// <summary>Restore <paramref name="path"/> in the working tree to its content at <paramref name="sha"/>.</summary>
    public Task<GitResult> RestoreFileAsync(string sha, string path, CancellationToken ct = default)
        => Guard(sha) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "checkout", sha, "--", path }, ct);

    // ---------------------------------------------------------------- config

    public async Task<string?> GetConfigAsync(string key, bool global, CancellationToken ct = default)
    {
        if (!GitArg.IsSafe(key)) return null;
        var args = new List<string> { "config" };
        if (global) args.Add("--global");
        args.Add("--get");
        args.Add(key);
        var r = await Run(args, ct).ConfigureAwait(false);
        var v = r.StandardOutput.Trim();
        return r.Success && v.Length > 0 ? v : null;
    }

    public Task<GitResult> SetConfigAsync(string key, string value, bool global, CancellationToken ct = default)
    {
        if (Guard(key) is { } bad) return Task.FromResult(bad);
        var args = new List<string> { "config" };
        if (global) args.Add("--global");
        args.Add(key);
        args.Add(value);
        return Run(args, ct);
    }

    // ---------------------------------------------------------------- statistics

    public async Task<RepoStats> GetRepoStatsAsync(CancellationToken ct = default)
    {
        int commitCount = 0;
        var cnt = await Run(new[] { "rev-list", "--count", "HEAD" }, ct).ConfigureAwait(false);
        if (cnt.Success) int.TryParse(cnt.StandardOutput.Trim(), out commitCount);

        var contributors = new List<ContributorInfo>();
        var sl = await Run(new[] { "shortlog", "-sne", "HEAD" }, ct).ConfigureAwait(false);
        if (sl.Success)
        {
            foreach (var line in sl.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int tab = line.IndexOf('\t');
                if (tab < 0 || !int.TryParse(line[..tab].Trim(), out var c)) continue;
                var who = line[(tab + 1)..].Trim();
                string name = who, email = string.Empty;
                int lt = who.LastIndexOf('<');
                if (lt >= 0 && who.EndsWith(">", StringComparison.Ordinal))
                {
                    name = who[..lt].Trim();
                    email = who[(lt + 1)..^1].Trim();
                }
                contributors.Add(new ContributorInfo { Name = name, Email = email, Commits = c });
            }
        }

        int branchCount, tagCount;
        DateTimeOffset? last;
        lock (_sync)
        {
            var repo = EnsureOpen();
            branchCount = repo.Branches.Count(b => !b.IsRemote);
            tagCount = repo.Tags.Count();
            last = repo.Head.Tip?.Author.When;
        }

        return new RepoStats
        {
            CommitCount = commitCount,
            BranchCount = branchCount,
            TagCount = tagCount,
            Contributors = contributors,
            LastActivity = last
        };
    }

    // ---------------------------------------------------------------- helpers

    private static string? ShortenRef(string reference)
        => reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..]
           : reference.StartsWith("refs/remotes/", StringComparison.Ordinal) ? reference["refs/remotes/".Length..]
           : reference;

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        static string Norm(string p) => p.Replace('\\', '/').TrimEnd('/');
        return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
    }
}
