using System.Text;
using GitTab.Core.Abstractions;
using GitTab.Core.Diff;
using GitTab.Core.Models;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

namespace GitTab.Core.Git;

/// <summary>
/// Single Git facade. Reads use LibGit2Sharp; writes/network use git.exe via
/// <see cref="IGitCommandRunner"/>. Repository access is serialized by <see cref="_sync"/>
/// because LibGit2Sharp's <see cref="Repository"/> is not thread-safe.
/// </summary>
public sealed class RepositoryService : IRepositoryService
{
    private readonly IGitCommandRunner _git;
    private readonly ILogger<RepositoryService> _logger;
    private readonly object _sync = new();

    private Repository? _repo;
    private string? _repoPath;      // .git dir / path used to open
    private string? _workingDir;    // working directory for CLI calls

    public RepositoryService(IGitCommandRunner git, ILogger<RepositoryService> logger)
    {
        _git = git;
        _logger = logger;
    }

    public string? CurrentRepositoryPath => _workingDir ?? _repoPath;
    public bool IsOpen => _repo is not null;

    public string? Discover(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var gitDir = Repository.Discover(path);
            if (gitDir is null) return null;
            using var probe = new Repository(gitDir);
            return probe.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   ?? gitDir;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Discover failed for {Path}", path);
            return null;
        }
    }

    public void Open(string path)
    {
        lock (_sync)
        {
            var gitDir = Repository.Discover(path)
                ?? throw new RepositoryException($"Git 저장소를 찾을 수 없습니다: {path}");
            _repo?.Dispose();
            _repo = new Repository(gitDir);
            _repoPath = gitDir;
            _workingDir = _repo.Info.WorkingDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                          ?? Path.GetDirectoryName(gitDir.TrimEnd(Path.DirectorySeparatorChar));
            _logger.LogInformation("Opened repository {WorkingDir}", _workingDir);
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            _repo?.Dispose();
            _repo = null;
            _repoPath = null;
            _workingDir = null;
        }
    }

    public void Refresh()
    {
        lock (_sync)
        {
            if (_repoPath is null) return;
            _repo?.Dispose();
            _repo = new Repository(_repoPath);
        }
    }

    private Repository EnsureOpen()
        => _repo ?? throw new RepositoryException("열린 저장소가 없습니다. (No repository is open.)");

    private string EnsureWorkingDir()
        => _workingDir ?? throw new RepositoryException("열린 저장소가 없습니다. (No repository is open.)");

    // ---------------------------------------------------------------- reads

    public HeadInfo GetHead()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            if (repo.Info.IsHeadUnborn)
                return new HeadInfo { IsDetached = false, IsUnborn = true, BranchFriendlyName = repo.Head.FriendlyName };

            return new HeadInfo
            {
                IsDetached = repo.Info.IsHeadDetached,
                IsUnborn = false,
                BranchFriendlyName = repo.Info.IsHeadDetached ? null : repo.Head.FriendlyName,
                TipSha = repo.Head.Tip?.Sha
            };
        }
    }

    public IReadOnlyList<CommitInfo> GetCommits(int max = 5000)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();

            var tips = new List<object>();
            foreach (var b in repo.Branches)
                if (b.Tip is not null) tips.Add(b.Tip);
            foreach (var t in repo.Tags)
            {
                var target = (t.PeeledTarget ?? t.Target) as Commit;
                if (target is not null) tips.Add(target);
            }
            if (repo.Head.Tip is not null) tips.Add(repo.Head.Tip);

            if (tips.Count == 0)
                return Array.Empty<CommitInfo>();

            var filter = new CommitFilter
            {
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                IncludeReachableFrom = tips
            };

            var result = new List<CommitInfo>(Math.Min(max, 1024));
            foreach (var c in repo.Commits.QueryBy(filter))
            {
                result.Add(new CommitInfo
                {
                    Sha = c.Sha,
                    ParentShas = c.Parents.Select(p => p.Sha).ToArray(),
                    Summary = c.MessageShort ?? string.Empty,
                    MessageFull = c.Message ?? string.Empty,
                    AuthorName = c.Author?.Name ?? "(unknown)",
                    AuthorEmail = c.Author?.Email ?? string.Empty,
                    WhenUtc = c.Author?.When ?? c.Committer?.When ?? DateTimeOffset.MinValue
                });
                if (result.Count >= max) break;
            }
            return result;
        }
    }

    public IReadOnlyList<BranchInfo> GetBranches()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var list = new List<BranchInfo>();
            foreach (var b in repo.Branches)
            {
                var tracking = b.IsRemote ? null : b.TrackingDetails;
                list.Add(new BranchInfo
                {
                    CanonicalName = b.CanonicalName,
                    FriendlyName = b.FriendlyName,
                    IsRemote = b.IsRemote,
                    IsCurrent = b.IsCurrentRepositoryHead,
                    TipSha = b.Tip?.Sha,
                    UpstreamFriendlyName = b.TrackedBranch?.FriendlyName,
                    Ahead = tracking?.AheadBy,
                    Behind = tracking?.BehindBy,
                    RemoteName = b.IsRemote ? b.RemoteName : b.TrackedBranch?.RemoteName
                });
            }
            return list;
        }
    }

    public IReadOnlyList<TagInfo> GetTags()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            return repo.Tags.Select(t => new TagInfo
            {
                Name = t.FriendlyName,
                TargetSha = (t.PeeledTarget ?? t.Target).Sha,
                IsAnnotated = t.IsAnnotated
            }).ToArray();
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> GetRefLabelsBySha()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var map = new Dictionary<string, List<RefLabel>>(StringComparer.Ordinal);

            void AddLabel(string? sha, RefLabel label)
            {
                if (string.IsNullOrEmpty(sha)) return;
                if (!map.TryGetValue(sha, out var l)) map[sha] = l = new List<RefLabel>();
                l.Add(label);
            }

            foreach (var b in repo.Branches)
            {
                AddLabel(b.Tip?.Sha, new RefLabel
                {
                    Name = b.FriendlyName,
                    Kind = b.IsRemote ? RefKind.RemoteBranch : RefKind.LocalBranch,
                    TargetSha = b.Tip?.Sha ?? string.Empty,
                    IsCurrent = b.IsCurrentRepositoryHead
                });
            }

            foreach (var t in repo.Tags)
            {
                var sha = (t.PeeledTarget ?? t.Target).Sha;
                AddLabel(sha, new RefLabel { Name = t.FriendlyName, Kind = RefKind.Tag, TargetSha = sha });
            }

            // Detached HEAD gets its own chip so the user still sees where they are.
            if (repo.Info.IsHeadDetached && repo.Head.Tip is not null)
            {
                AddLabel(repo.Head.Tip.Sha, new RefLabel
                {
                    Name = "HEAD",
                    Kind = RefKind.Head,
                    TargetSha = repo.Head.Tip.Sha,
                    IsCurrent = true
                });
            }

            return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<RefLabel>)kv.Value, StringComparer.Ordinal);
        }
    }

    public WorkingTreeStatus GetStatus()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var options = new StatusOptions
            {
                IncludeUntracked = true,
                RecurseUntrackedDirs = true,
                DetectRenamesInIndex = true,
                DetectRenamesInWorkDir = true,
                IncludeIgnored = false
            };

            var staged = new List<FileChange>();
            var unstaged = new List<FileChange>();

            foreach (var entry in repo.RetrieveStatus(options))
            {
                var s = entry.State;
                if (s == FileStatus.Ignored || s == FileStatus.Unaltered) continue;

                // Index (staged) side
                if (s.HasFlag(FileStatus.NewInIndex))
                    staged.Add(Make(entry.FilePath, null, FileChangeKind.Added, true));
                if (s.HasFlag(FileStatus.ModifiedInIndex))
                    staged.Add(Make(entry.FilePath, null, FileChangeKind.Modified, true));
                if (s.HasFlag(FileStatus.DeletedFromIndex))
                    staged.Add(Make(entry.FilePath, null, FileChangeKind.Deleted, true));
                if (s.HasFlag(FileStatus.RenamedInIndex))
                    staged.Add(Make(entry.FilePath, entry.HeadToIndexRenameDetails?.OldFilePath, FileChangeKind.Renamed, true));
                if (s.HasFlag(FileStatus.TypeChangeInIndex))
                    staged.Add(Make(entry.FilePath, null, FileChangeKind.TypeChanged, true));

                // Working-directory (unstaged) side
                if (s.HasFlag(FileStatus.NewInWorkdir))
                    unstaged.Add(Make(entry.FilePath, null, FileChangeKind.Untracked, false));
                if (s.HasFlag(FileStatus.ModifiedInWorkdir))
                    unstaged.Add(Make(entry.FilePath, null, FileChangeKind.Modified, false));
                if (s.HasFlag(FileStatus.DeletedFromWorkdir))
                    unstaged.Add(Make(entry.FilePath, null, FileChangeKind.Deleted, false));
                if (s.HasFlag(FileStatus.RenamedInWorkdir))
                    unstaged.Add(Make(entry.FilePath, entry.IndexToWorkDirRenameDetails?.OldFilePath, FileChangeKind.Renamed, false));
                if (s.HasFlag(FileStatus.TypeChangeInWorkdir))
                    unstaged.Add(Make(entry.FilePath, null, FileChangeKind.TypeChanged, false));
                if (s.HasFlag(FileStatus.Conflicted))
                    unstaged.Add(Make(entry.FilePath, null, FileChangeKind.Conflicted, false));
            }

            return new WorkingTreeStatus { Staged = staged, Unstaged = unstaged };

            static FileChange Make(string path, string? oldPath, FileChangeKind kind, bool staged) =>
                new() { Path = path, OldPath = oldPath, Kind = kind, IsStaged = staged };
        }
    }

    public IReadOnlyList<FileChange> GetCommitChanges(string sha)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var commit = repo.Lookup<Commit>(sha)
                ?? throw new RepositoryException($"커밋을 찾을 수 없습니다: {sha}");
            var parent = commit.Parents.FirstOrDefault();
            var changes = repo.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree);

            var list = new List<FileChange>();
            foreach (var c in changes)
            {
                list.Add(new FileChange
                {
                    Path = c.Path,
                    OldPath = c.OldPath != c.Path ? c.OldPath : null,
                    Kind = MapChangeKind(c.Status),
                    IsStaged = false
                });
            }
            return list.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public CommitStats GetCommitStats(string sha)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var commit = repo.Lookup<Commit>(sha);
            if (commit is null) return new CommitStats { FilesChanged = 0, Additions = 0, Deletions = 0 };
            var parent = commit.Parents.FirstOrDefault();
            var patch = repo.Diff.Compare<Patch>(parent?.Tree, commit.Tree);
            return new CommitStats
            {
                FilesChanged = patch.Count(),
                Additions = patch.LinesAdded,
                Deletions = patch.LinesDeleted
            };
        }
    }

    public FileDiff GetCommitFileDiff(string sha, string path)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var commit = repo.Lookup<Commit>(sha)
                ?? throw new RepositoryException($"커밋을 찾을 수 없습니다: {sha}");
            var parent = commit.Parents.FirstOrDefault();

            var patch = repo.Diff.Compare<Patch>(parent?.Tree, commit.Tree, new[] { path });
            var entry = patch.FirstOrDefault();
            if (entry is null)
                return new FileDiff { Path = path, RawPatch = string.Empty };

            return UnifiedDiffParser.Parse(entry.Path, entry.OldPath != entry.Path ? entry.OldPath : null,
                entry.Patch ?? string.Empty, entry.IsBinaryComparison);
        }
    }

    public FileDiff GetWorkingFileDiff(string path, bool staged)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            Patch patch;
            if (staged)
            {
                var headTree = repo.Info.IsHeadUnborn ? null : repo.Head.Tip?.Tree;
                patch = repo.Diff.Compare<Patch>(headTree, DiffTargets.Index, new[] { path });
            }
            else
            {
                patch = repo.Diff.Compare<Patch>(new[] { path }, includeUntracked: true);
            }

            var entry = patch.FirstOrDefault();
            if (entry is null)
                return new FileDiff { Path = path, RawPatch = string.Empty };

            return UnifiedDiffParser.Parse(entry.Path, entry.OldPath != entry.Path ? entry.OldPath : null,
                entry.Patch ?? string.Empty, entry.IsBinaryComparison);
        }
    }

    // ---------------------------------------------------------------- advanced reads

    public RepositoryStateInfo GetState()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var op = repo.Info.CurrentOperation switch
            {
                CurrentOperation.Merge => RepositoryOperation.Merge,
                CurrentOperation.Revert or CurrentOperation.RevertSequence => RepositoryOperation.Revert,
                CurrentOperation.CherryPick or CurrentOperation.CherryPickSequence => RepositoryOperation.CherryPick,
                CurrentOperation.Rebase or CurrentOperation.RebaseInteractive or CurrentOperation.RebaseMerge
                    or CurrentOperation.ApplyMailboxOrRebase => RepositoryOperation.Rebase,
                CurrentOperation.Bisect => RepositoryOperation.Bisect,
                _ => RepositoryOperation.None
            };

            var conflicts = repo.Index.Conflicts
                .Select(c => (c.Ours ?? c.Theirs ?? c.Ancestor)?.Path)
                .Where(p => p is not null)
                .Select(p => p!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new RepositoryStateInfo { Operation = op, ConflictedPaths = conflicts };
        }
    }

    public IReadOnlyList<StashInfo> GetStashes()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var list = new List<StashInfo>();
            int i = 0;
            foreach (var s in repo.Stashes)
            {
                list.Add(new StashInfo
                {
                    Index = i++,
                    Message = s.Message,
                    Sha = s.WorkTree?.Sha ?? s.Index?.Sha ?? string.Empty
                });
            }
            return list;
        }
    }

    public IReadOnlyList<BlameLine> GetBlame(string path)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var full = Path.Combine(_workingDir ?? string.Empty, path);
            var lines = File.Exists(full) ? File.ReadAllLines(full) : Array.Empty<string>();
            var result = new List<BlameLine>(lines.Length);

            BlameHunkCollection? blame = null;
            try { blame = repo.Blame(path); }
            catch (Exception ex) { _logger.LogDebug(ex, "Blame failed for {Path}", path); }

            for (int i = 0; i < lines.Length; i++)
            {
                Commit? commit = null;
                if (blame is not null)
                {
                    try { commit = blame.HunkForLine(i).FinalCommit; }
                    catch { /* line outside blame (e.g. uncommitted) */ }
                }
                result.Add(new BlameLine
                {
                    LineNumber = i + 1,
                    Sha = commit?.Sha ?? string.Empty,
                    Author = commit?.Author?.Name ?? string.Empty,
                    When = commit?.Author?.When ?? DateTimeOffset.MinValue,
                    Summary = commit?.MessageShort ?? string.Empty,
                    Content = lines[i]
                });
            }
            return result;
        }
    }

    public (string? Base, string? Ours, string? Theirs) GetConflictVersions(string path)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var conflict = repo.Index.Conflicts[path];
            if (conflict is null) return (null, null, null);

            string? Read(IndexEntry? entry)
            {
                if (entry is null) return null;
                try { return repo.Lookup<Blob>(entry.Id)?.GetContentText(); }
                catch (Exception ex) { _logger.LogDebug(ex, "Reading conflict blob failed for {Path}", path); return null; }
            }

            return (Read(conflict.Ancestor), Read(conflict.Ours), Read(conflict.Theirs));
        }
    }

    public IReadOnlyList<string> GetSubmodulePaths()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            return repo.Submodules.Select(s => s.Path).ToArray();
        }
    }

    public string? GetRemoteUrl(string remote = "origin")
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var r = repo.Network.Remotes[remote] ?? repo.Network.Remotes.FirstOrDefault();
            return r?.Url;
        }
    }

    public IReadOnlyList<RemoteInfo> GetRemotes()
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            return repo.Network.Remotes
                .Select(r => new RemoteInfo { Name = r.Name, Url = r.Url })
                .ToArray();
        }
    }

    public IReadOnlyList<GitTab.Core.Models.ReflogEntry> GetReflog(int max = 100)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var list = new List<GitTab.Core.Models.ReflogEntry>();
            int i = 0;
            foreach (var e in repo.Refs.Log("HEAD"))
            {
                var sha = e.To?.Sha ?? string.Empty;
                list.Add(new GitTab.Core.Models.ReflogEntry
                {
                    Index = i,
                    Sha = sha,
                    ShortSha = sha.Length >= 7 ? sha[..7] : sha,
                    Message = e.Message ?? string.Empty,
                    When = e.Committer?.When ?? default,
                    Committer = e.Committer?.Name
                });
                if (++i >= max) break;
            }
            return list;
        }
    }

    public IReadOnlyList<CommitInfo> GetCommitsBetween(string excludeSha, string includeSha)
    {
        lock (_sync)
        {
            var repo = EnsureOpen();
            var filter = new CommitFilter
            {
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                IncludeReachableFrom = includeSha,
                ExcludeReachableFrom = excludeSha
            };
            return repo.Commits.QueryBy(filter).Select(c => new CommitInfo
            {
                Sha = c.Sha,
                ParentShas = c.Parents.Select(p => p.Sha).ToArray(),
                Summary = c.MessageShort ?? string.Empty,
                MessageFull = c.Message ?? string.Empty,
                AuthorName = c.Author?.Name ?? "(unknown)",
                AuthorEmail = c.Author?.Email ?? string.Empty,
                WhenUtc = c.Author?.When ?? DateTimeOffset.MinValue
            }).ToArray();
        }
    }

    // ---------------------------------------------------------------- writes / network

    public Task<GitResult> StageAsync(string path, CancellationToken ct = default)
        => Run(new[] { "add", "--", path }, ct);

    public Task<GitResult> StageAllAsync(CancellationToken ct = default)
        => Run(new[] { "add", "-A" }, ct);

    public Task<GitResult> UnstageAsync(string path, CancellationToken ct = default)
        => Run(new[] { "restore", "--staged", "--", path }, ct);

    public async Task<GitResult> DiscardAsync(string path, CancellationToken ct = default)
    {
        // Discard tracked changes back to HEAD (staged + worktree).
        var restore = await Run(new[] { "restore", "--source=HEAD", "--staged", "--worktree", "--", path }, ct)
            .ConfigureAwait(false);
        if (restore.Success) return restore;

        // Likely an untracked/new file — remove it.
        return await Run(new[] { "clean", "-f", "--", path }, ct).ConfigureAwait(false);
    }

    public Task<GitResult> CommitAsync(string message, bool amend = false, CancellationToken ct = default)
    {
        var args = new List<string> { "commit", "-m", message };
        if (amend) args.Add("--amend");
        return Run(args, ct);
    }

    // Reject a user-provided name/ref/URL that git could mistake for an option (argument injection).
    private static GitResult? Guard(params string?[] values)
    {
        foreach (var v in values)
            if (!GitArg.IsSafe(v))
                return GitResult.Fault("git",
                    $"안전하지 않은 인자입니다: '{v ?? "(빈 값)"}' — '-'로 시작하거나 제어문자를 포함할 수 없습니다. " +
                    "(Unsafe argument: cannot start with '-' or contain control characters.)");
        return null;
    }

    public Task<GitResult> CheckoutAsync(string branchFriendlyName, CancellationToken ct = default)
        => Guard(branchFriendlyName) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "checkout", branchFriendlyName }, ct);

    public Task<GitResult> CreateBranchAsync(string name, bool checkout, string? startPoint = null, CancellationToken ct = default)
    {
        if (Guard(name) is { } bad) return Task.FromResult(bad);
        if (!string.IsNullOrWhiteSpace(startPoint) && Guard(startPoint) is { } badStart) return Task.FromResult(badStart);
        var args = new List<string>();
        if (checkout) { args.Add("checkout"); args.Add("-b"); args.Add(name); }
        else { args.Add("branch"); args.Add(name); }
        if (!string.IsNullOrWhiteSpace(startPoint)) args.Add(startPoint);
        return Run(args, ct);
    }

    public Task<GitResult> DeleteBranchAsync(string friendlyName, bool force, CancellationToken ct = default)
        => Guard(friendlyName) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "branch", force ? "-D" : "-d", friendlyName }, ct);

    public Task<GitResult> RenameBranchAsync(string oldName, string newName, CancellationToken ct = default)
        => Guard(oldName, newName) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "branch", "-m", oldName, newName }, ct);

    public Task<GitResult> MergeAsync(string branchFriendlyName, CancellationToken ct = default)
        => Guard(branchFriendlyName) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "merge", "--no-edit", branchFriendlyName }, ct);

    public Task<GitResult> RebaseAsync(string ontoBranchFriendlyName, CancellationToken ct = default)
        => Guard(ontoBranchFriendlyName) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "rebase", ontoBranchFriendlyName }, ct);

    public Task<GitResult> FetchAsync(string? remote = null, bool prune = true, CancellationToken ct = default)
    {
        var args = new List<string> { "fetch" };
        if (string.IsNullOrWhiteSpace(remote)) args.Add("--all");
        else args.Add(remote);
        if (prune) args.Add("--prune");
        return Run(args, ct);
    }

    public Task<GitResult> PullAsync(CancellationToken ct = default)
        => Run(new[] { "pull" }, ct);

    public Task<GitResult> PushAsync(bool setUpstream = false, string? remote = null, string? branch = null, CancellationToken ct = default)
    {
        var args = new List<string> { "push" };
        if (setUpstream)
        {
            args.Add("-u");
            args.Add(remote ?? "origin");
            args.Add(branch ?? CurrentBranchName() ?? "HEAD");
        }
        else if (!string.IsNullOrWhiteSpace(remote))
        {
            args.Add(remote);
            if (!string.IsNullOrWhiteSpace(branch)) args.Add(branch);
        }
        return Run(args, ct);
    }

    public Task<GitResult> RunRawAsync(IReadOnlyList<string> args, CancellationToken ct = default)
        => Run(args, ct);

    public async Task<GitResult> InitAsync(string path, string? initialBranch = "main", CancellationToken ct = default)
    {
        // Runs in the target folder directly — no repository is open yet, so we cannot use Run().
        Directory.CreateDirectory(path);
        var args = new List<string> { "init" };
        if (!string.IsNullOrWhiteSpace(initialBranch)) { args.Add("-b"); args.Add(initialBranch); }
        return await _git.RunAsync(path, args, cancellationToken: ct).ConfigureAwait(false);
    }

    // ---- stash ----

    public Task<GitResult> StashPushAsync(string? message, bool includeUntracked, CancellationToken ct = default)
    {
        var args = new List<string> { "stash", "push" };
        if (includeUntracked) args.Add("-u");
        if (!string.IsNullOrWhiteSpace(message)) { args.Add("-m"); args.Add(message); }
        return Run(args, ct);
    }

    public Task<GitResult> StashApplyAsync(int index, bool pop, CancellationToken ct = default)
        => Run(new[] { "stash", pop ? "pop" : "apply", $"stash@{{{index}}}" }, ct);

    public Task<GitResult> StashDropAsync(int index, CancellationToken ct = default)
        => Run(new[] { "stash", "drop", $"stash@{{{index}}}" }, ct);

    // ---- conflicts / in-progress operations ----

    public async Task<GitResult> AbortOperationAsync(CancellationToken ct = default)
    {
        var cmd = GetState().Operation switch
        {
            RepositoryOperation.Merge => "merge",
            RepositoryOperation.Rebase => "rebase",
            RepositoryOperation.CherryPick => "cherry-pick",
            RepositoryOperation.Revert => "revert",
            _ => null
        };
        if (cmd is null) return GitResult.Fault("git", "진행 중인 작업이 없습니다. (No operation to abort.)");
        return await Run(new[] { cmd, "--abort" }, ct).ConfigureAwait(false);
    }

    public async Task<GitResult> ContinueOperationAsync(CancellationToken ct = default)
    {
        return GetState().Operation switch
        {
            RepositoryOperation.Merge => await Run(new[] { "commit", "--no-edit" }, ct).ConfigureAwait(false),
            RepositoryOperation.Rebase => await Run(new[] { "rebase", "--continue" }, ct).ConfigureAwait(false),
            RepositoryOperation.CherryPick => await Run(new[] { "cherry-pick", "--continue" }, ct).ConfigureAwait(false),
            RepositoryOperation.Revert => await Run(new[] { "revert", "--continue" }, ct).ConfigureAwait(false),
            _ => GitResult.Fault("git", "진행 중인 작업이 없습니다. (No operation to continue.)")
        };
    }

    public Task<GitResult> MarkResolvedAsync(string path, CancellationToken ct = default)
        => Run(new[] { "add", "--", path }, ct);

    // ---- branches / tags / remotes / submodules ----

    public Task<GitResult> AddRemoteAsync(string name, string url, CancellationToken ct = default)
        => Guard(name, url) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "remote", "add", name, url }, ct);

    public Task<GitResult> SetRemoteUrlAsync(string name, string url, CancellationToken ct = default)
        => Guard(name, url) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "remote", "set-url", name, url }, ct);

    public Task<GitResult> RemoveRemoteAsync(string name, CancellationToken ct = default)
        => Guard(name) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "remote", "remove", name }, ct);

    public Task<GitResult> DeleteRemoteBranchAsync(string remote, string branch, CancellationToken ct = default)
        => Guard(remote, branch) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "push", remote, "--delete", branch }, ct);

    public Task<GitResult> CreateTagAsync(string name, string? target = null, CancellationToken ct = default)
    {
        if (Guard(name) is { } bad) return Task.FromResult(bad);
        if (!string.IsNullOrWhiteSpace(target) && Guard(target) is { } badTarget) return Task.FromResult(badTarget);
        var args = new List<string> { "tag", name };
        if (!string.IsNullOrWhiteSpace(target)) args.Add(target);
        return Run(args, ct);
    }

    public Task<GitResult> DeleteTagAsync(string name, CancellationToken ct = default)
        => Guard(name) is { } bad ? Task.FromResult(bad)
           : Run(new[] { "tag", "-d", name }, ct);

    public Task<GitResult> PushTagAsync(string name, string? remote = null, CancellationToken ct = default)
        => Guard(name, remote ?? "origin") is { } bad ? Task.FromResult(bad)
           : Run(new[] { "push", remote ?? "origin", name }, ct);

    public Task<GitResult> SubmoduleUpdateAsync(CancellationToken ct = default)
        => Run(new[] { "submodule", "update", "--init", "--recursive" }, ct);

    // ---- interactive rebase ----

    public async Task<GitResult> RebaseInteractiveAsync(string ontoSha, IReadOnlyList<RebaseTodoItem> plan, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        bool anyKeep = false;
        foreach (var item in plan)
        {
            var word = item.Action switch
            {
                RebaseAction.Pick => "pick",
                RebaseAction.Reword => "reword",
                RebaseAction.Squash => "squash",
                RebaseAction.Fixup => "fixup",
                RebaseAction.Drop => "drop",
                _ => "pick"
            };
            if (item.Action != RebaseAction.Drop) anyKeep = true;
            var shortSha = item.Sha.Length >= 7 ? item.Sha[..7] : item.Sha;
            sb.Append(word).Append(' ').Append(shortSha).Append(' ').Append(item.Summary).Append('\n');
        }
        if (!anyKeep) return GitResult.Fault("git rebase -i", "최소 한 개의 커밋은 남겨야 합니다. (Cannot drop all commits.)");

        var todoPath = Path.Combine(Path.GetTempPath(), $"gittab-rebase-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(todoPath, sb.ToString(), ct).ConfigureAwait(false);
        try
        {
            // git runs GIT_SEQUENCE_EDITOR via its bundled sh; `cp <ours> "<gittodo>"` overwrites the plan.
            var fwd = todoPath.Replace('\\', '/');
            var env = new Dictionary<string, string>
            {
                ["GIT_SEQUENCE_EDITOR"] = $"cp \"{fwd}\"",
                ["GIT_EDITOR"] = ":"
            };
            return await Run(new[] { "rebase", "-i", ontoSha }, env, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(todoPath); } catch { /* temp cleanup */ }
        }
    }

    private string? CurrentBranchName()
    {
        lock (_sync)
        {
            if (_repo is null || _repo.Info.IsHeadDetached || _repo.Info.IsHeadUnborn) return null;
            return _repo.Head.FriendlyName;
        }
    }

    private Task<GitResult> Run(IReadOnlyList<string> args, CancellationToken ct)
        => _git.RunAsync(EnsureWorkingDir(), args, cancellationToken: ct);

    private Task<GitResult> Run(IReadOnlyList<string> args, IReadOnlyDictionary<string, string> env, CancellationToken ct)
        => _git.RunAsync(EnsureWorkingDir(), args, env, ct);

    // ---------------------------------------------------------------- helpers

    private static FileChangeKind MapChangeKind(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => FileChangeKind.Added,
        ChangeKind.Deleted => FileChangeKind.Deleted,
        ChangeKind.Modified => FileChangeKind.Modified,
        ChangeKind.Renamed => FileChangeKind.Renamed,
        ChangeKind.Copied => FileChangeKind.Copied,
        ChangeKind.TypeChanged => FileChangeKind.TypeChanged,
        ChangeKind.Conflicted => FileChangeKind.Conflicted,
        ChangeKind.Unmodified => FileChangeKind.Unmodified,
        _ => FileChangeKind.Modified
    };

    public void Dispose()
    {
        lock (_sync)
        {
            _repo?.Dispose();
            _repo = null;
        }
    }
}
