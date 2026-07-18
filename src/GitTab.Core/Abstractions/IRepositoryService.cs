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
    bool IsOpen { get; }

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

    // ---- advanced reads ----
    RepositoryStateInfo GetState();
    IReadOnlyList<StashInfo> GetStashes();
    IReadOnlyList<BlameLine> GetBlame(string path);
    IReadOnlyList<string> GetSubmodulePaths();

    /// <summary>URL of the given remote (default "origin"), or null if it has none. Used to key
    /// stored credentials by host when prompting for authentication.</summary>
    string? GetRemoteUrl(string remote = "origin");

    /// <summary>All configured remotes (name + URL).</summary>
    IReadOnlyList<RemoteInfo> GetRemotes();

    /// <summary>Commits reachable from <paramref name="includeSha"/> but not <paramref name="excludeSha"/>,
    /// newest-first (used to build an interactive-rebase plan).</summary>
    IReadOnlyList<CommitInfo> GetCommitsBetween(string excludeSha, string includeSha);

    // ---- writes / network (git.exe) ----
    Task<GitResult> StageAsync(string path, CancellationToken ct = default);
    Task<GitResult> StageAllAsync(CancellationToken ct = default);
    Task<GitResult> UnstageAsync(string path, CancellationToken ct = default);
    Task<GitResult> DiscardAsync(string path, CancellationToken ct = default);
    Task<GitResult> CommitAsync(string message, bool amend = false, CancellationToken ct = default);

    Task<GitResult> CheckoutAsync(string branchFriendlyName, CancellationToken ct = default);
    Task<GitResult> CreateBranchAsync(string name, bool checkout, string? startPoint = null, CancellationToken ct = default);
    Task<GitResult> DeleteBranchAsync(string friendlyName, bool force, CancellationToken ct = default);
    Task<GitResult> RenameBranchAsync(string oldName, string newName, CancellationToken ct = default);
    Task<GitResult> MergeAsync(string branchFriendlyName, CancellationToken ct = default);
    Task<GitResult> RebaseAsync(string ontoBranchFriendlyName, CancellationToken ct = default);

    Task<GitResult> FetchAsync(string? remote = null, bool prune = true, CancellationToken ct = default);
    Task<GitResult> PullAsync(CancellationToken ct = default);
    Task<GitResult> PushAsync(bool setUpstream = false, string? remote = null, string? branch = null, CancellationToken ct = default);

    /// <summary>Create a new repository at <paramref name="path"/> (git init, creating the folder if needed).</summary>
    Task<GitResult> InitAsync(string path, string? initialBranch = "main", CancellationToken ct = default);

    Task<GitResult> AddRemoteAsync(string name, string url, CancellationToken ct = default);
    Task<GitResult> SetRemoteUrlAsync(string name, string url, CancellationToken ct = default);
    Task<GitResult> RemoveRemoteAsync(string name, CancellationToken ct = default);

    // ---- stash ----
    Task<GitResult> StashPushAsync(string? message, bool includeUntracked, CancellationToken ct = default);
    Task<GitResult> StashApplyAsync(int index, bool pop, CancellationToken ct = default);
    Task<GitResult> StashDropAsync(int index, CancellationToken ct = default);

    // ---- conflicts / in-progress operations ----
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
}
