using GitTab.Core.Models;

namespace GitTab.Core.Abstractions;

/// <summary>
/// The single Git facade the UI talks to. Reads (log/branch/status/diff) are served by
/// LibGit2Sharp; writes and network operations are routed to git.exe. Callers never need to
/// know which backend handles a given call.
/// </summary>
public interface IRepositoryService : IDisposable
{
    string? CurrentRepositoryPath { get; }

    /// <summary>Returns the repository working directory if <paramref name="path"/> is inside a
    /// git repo (or is one), otherwise null.</summary>
    string? Discover(string path);

    /// <summary>Opens the repository containing <paramref name="path"/>. Throws
    /// <see cref="RepositoryException"/> if none is found.</summary>
    void Open(string path);

    void Close();

    /// <summary>Drops and re-reads cached git state (call after any write).</summary>
    void Refresh();

    // ---- reads (LibGit2Sharp) ----
    HeadInfo GetHead();
    IReadOnlyList<CommitInfo> GetCommits(int max = 5000);
    IReadOnlyList<BranchInfo> GetBranches();
    IReadOnlyList<TagInfo> GetTags();

    /// <summary>Map of commit SHA → ref labels (branches/tags/HEAD) pointing at it, for chips.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> GetRefLabelsBySha();

    WorkingTreeStatus GetStatus();

    IReadOnlyList<FileChange> GetCommitChanges(string sha);
    CommitStats GetCommitStats(string sha);
    FileDiff GetCommitFileDiff(string sha, string path);
    FileDiff GetWorkingFileDiff(string path, bool staged);

    /// <summary>Files that differ between two refs (branches/tags/commits).</summary>
    IReadOnlyList<FileChange> GetChangesBetween(string fromRef, string toRef);

    /// <summary>Diff of one file between two refs.</summary>
    FileDiff GetFileDiffBetween(string fromRef, string toRef, string path);

    // ---- advanced reads ----
    RepositoryStateInfo GetState();
    IReadOnlyList<StashInfo> GetStashes();
    IReadOnlyList<BlameLine> GetBlame(string path);

    /// <summary>The three merge stages of a conflicted file: common ancestor (base), ours, and theirs.
    /// Any may be null (e.g. add/add conflicts have no base).</summary>
    (string? Base, string? Ours, string? Theirs) GetConflictVersions(string path);
    IReadOnlyList<string> GetSubmodulePaths();

    /// <summary>URL of the given remote (default "origin"), or null if it has none. Used to key
    /// stored credentials by host when prompting for authentication.</summary>
    string? GetRemoteUrl(string remote = "origin");

    /// <summary>All configured remotes (name + URL).</summary>
    IReadOnlyList<RemoteInfo> GetRemotes();

    /// <summary>Recent HEAD reflog entries (newest first) — the history you can undo/restore to.</summary>
    IReadOnlyList<ReflogEntry> GetReflog(int max = 100);

    /// <summary>Commits reachable from <paramref name="includeSha"/> but not <paramref name="excludeSha"/>,
    /// newest-first (used to build an interactive-rebase plan).</summary>
    IReadOnlyList<CommitInfo> GetCommitsBetween(string excludeSha, string includeSha);

    /// <summary>Commits that touched <paramref name="path"/>, newest first (file history).</summary>
    IReadOnlyList<CommitInfo> GetFileHistory(string path, int max = 300);

    /// <summary>Commits that added/removed <paramref name="term"/> in the code (pickaxe: -S, or -G for regex).</summary>
    Task<IReadOnlyList<CommitInfo>> SearchContentAsync(string term, bool useRegex, CancellationToken ct = default);

    // ---- writes / network (git.exe) ----
    Task<GitResult> StageAsync(string path, CancellationToken ct = default);
    Task<GitResult> StageAllAsync(CancellationToken ct = default);
    Task<GitResult> UnstageAsync(string path, CancellationToken ct = default);
    Task<GitResult> DiscardAsync(string path, CancellationToken ct = default);
    Task<GitResult> CommitAsync(string message, bool amend = false, bool sign = false, bool signOff = false, CancellationToken ct = default);

    Task<GitResult> CheckoutAsync(string branchFriendlyName, CancellationToken ct = default);
    Task<GitResult> CreateBranchAsync(string name, bool checkout, string? startPoint = null, CancellationToken ct = default);
    Task<GitResult> DeleteBranchAsync(string friendlyName, bool force, CancellationToken ct = default);
    Task<GitResult> RenameBranchAsync(string oldName, string newName, CancellationToken ct = default);
    Task<GitResult> MergeAsync(string branchFriendlyName, CancellationToken ct = default);
    Task<GitResult> RebaseAsync(string ontoBranchFriendlyName, CancellationToken ct = default);

    Task<GitResult> FetchAsync(string? remote = null, bool prune = true, CancellationToken ct = default);
    Task<GitResult> PullAsync(CancellationToken ct = default);
    Task<GitResult> PushAsync(bool setUpstream = false, string? remote = null, string? branch = null, bool forceWithLease = false, CancellationToken ct = default);

    /// <summary>Create a new repository at <paramref name="path"/> (git init, creating the folder if needed).</summary>
    Task<GitResult> InitAsync(string path, string? initialBranch = "main", CancellationToken ct = default);

    /// <summary>Clone <paramref name="url"/> into <paramref name="targetPath"/>.
    /// <paramref name="blobless"/> makes it a partial clone (--filter=blob:none).</summary>
    Task<GitResult> CloneAsync(string url, string targetPath, bool blobless = false, CancellationToken ct = default);

    /// <summary>Create a tag. A non-null <paramref name="message"/> makes it annotated;
    /// <paramref name="sign"/> makes it a GPG/SSH-signed annotated tag.</summary>
    Task<GitResult> CreateTagAsync(string name, string? target = null, string? message = null, bool sign = false, CancellationToken ct = default);

    /// <summary>Change a commit's message (amend if it's HEAD, otherwise an interactive-rebase reword).</summary>
    Task<GitResult> RewordAsync(string sha, string newMessage, CancellationToken ct = default);
    Task<GitResult> AddRemoteAsync(string name, string url, CancellationToken ct = default);
    Task<GitResult> SetRemoteUrlAsync(string name, string url, CancellationToken ct = default);
    Task<GitResult> RemoveRemoteAsync(string name, CancellationToken ct = default);

    // ---- stash ----
    Task<GitResult> StashPushAsync(string? message, bool includeUntracked, IReadOnlyList<string>? paths = null, CancellationToken ct = default);
    Task<GitResult> StashApplyAsync(int index, bool pop, CancellationToken ct = default);
    Task<GitResult> StashDropAsync(int index, CancellationToken ct = default);

    /// <summary>Raw diff of a stash entry, for preview.</summary>
    Task<string> GetStashDiffAsync(int index, CancellationToken ct = default);

    /// <summary>Create a branch from a stash and drop it (git stash branch).</summary>
    Task<GitResult> StashToBranchAsync(int index, string branch, CancellationToken ct = default);

    // ---- conflicts / in-progress operations ----
    bool IsBisecting();
    Task<GitResult> BisectStartAsync(string goodSha, string badSha, CancellationToken ct = default);
    Task<GitResult> BisectMarkAsync(string term, CancellationToken ct = default);
    Task<GitResult> BisectResetAsync(CancellationToken ct = default);

    Task<GitResult> AbortOperationAsync(CancellationToken ct = default);
    Task<GitResult> ContinueOperationAsync(CancellationToken ct = default);
    Task<GitResult> MarkResolvedAsync(string path, CancellationToken ct = default);

    // ---- branches / tags / remotes / rebase / submodules ----
    Task<GitResult> DeleteRemoteBranchAsync(string remote, string branch, CancellationToken ct = default);
    Task<GitResult> DeleteTagAsync(string name, CancellationToken ct = default);
    Task<GitResult> PushTagAsync(string name, string? remote = null, CancellationToken ct = default);
    Task<GitResult> RebaseInteractiveAsync(string ontoSha, IReadOnlyList<RebaseTodoItem> plan, CancellationToken ct = default);
    Task<GitResult> SubmoduleUpdateAsync(CancellationToken ct = default);

    /// <summary>Raw git passthrough for advanced actions (used by higher layers sparingly).</summary>
    Task<GitResult> RunRawAsync(IReadOnlyList<string> args, CancellationToken ct = default);

    // ---- signing ----
    Task<CommitSignature> GetSignatureStatusAsync(string sha, CancellationToken ct = default);
    Task<(bool Enabled, string? Key, string? Format)> GetSigningConfigAsync(CancellationToken ct = default);
    Task<GitResult> SetSigningConfigAsync(bool enabled, string? key, string? format, CancellationToken ct = default);

    // ---- worktrees ----
    Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(CancellationToken ct = default);
    Task<GitResult> WorktreeAddAsync(string path, string? branch, bool createBranch, CancellationToken ct = default);
    Task<GitResult> WorktreeRemoveAsync(string path, bool force, CancellationToken ct = default);
    Task<GitResult> WorktreePruneAsync(CancellationToken ct = default);

    // ---- Git LFS ----
    Task<LfsStatus> GetLfsStatusAsync(CancellationToken ct = default);
    Task<GitResult> LfsTrackAsync(string pattern, CancellationToken ct = default);
    Task<GitResult> LfsUntrackAsync(string pattern, CancellationToken ct = default);
    Task<GitResult> LfsPullAsync(CancellationToken ct = default);

    // ---- submodules (rich) ----
    IReadOnlyList<SubmoduleInfo> GetSubmodules();
    Task<GitResult> SubmoduleAddAsync(string url, string path, CancellationToken ct = default);
    Task<GitResult> SubmoduleSyncAsync(CancellationToken ct = default);
    Task<GitResult> SubmoduleDeinitAsync(string path, bool force, CancellationToken ct = default);

    // ---- image diff / raw bytes ----
    byte[]? GetBlobBytes(string sha, string path);
    byte[]? GetWorkingBytes(string path);

    // ---- whitespace-ignoring diff ----
    Task<FileDiff> GetCommitFileDiffIgnoreWsAsync(string sha, string path, CancellationToken ct = default);
    Task<FileDiff> GetWorkingFileDiffIgnoreWsAsync(string path, bool staged, CancellationToken ct = default);

    // ---- patch import/export ----
    Task<GitResult> ExportCommitPatchAsync(string sha, string filePath, CancellationToken ct = default);
    Task<GitResult> ApplyPatchAsync(string patchFile, bool asCommits, CancellationToken ct = default);

    // ---- sparse-checkout ----
    Task<IReadOnlyList<string>> GetSparseCheckoutPatternsAsync(CancellationToken ct = default);
    Task<GitResult> SparseCheckoutSetAsync(IReadOnlyList<string> patterns, bool cone, CancellationToken ct = default);
    Task<GitResult> SparseCheckoutDisableAsync(CancellationToken ct = default);

    // ---- author / file restore ----
    Task<GitResult> AmendAuthorAsync(string name, string email, CancellationToken ct = default);
    Task<GitResult> RestoreFileAsync(string sha, string path, CancellationToken ct = default);

    // ---- config ----
    Task<string?> GetConfigAsync(string key, bool global, CancellationToken ct = default);
    Task<GitResult> SetConfigAsync(string key, string value, bool global, CancellationToken ct = default);

    // ---- statistics ----
    Task<RepoStats> GetRepoStatsAsync(CancellationToken ct = default);
}
