namespace GitTab.Core.Models;

/// <summary>A linked working tree (`git worktree`).</summary>
public sealed class WorktreeInfo
{
    public required string Path { get; init; }
    public string? Branch { get; init; }     // friendly branch name, or null when detached/bare
    public string? HeadSha { get; init; }
    public bool IsBare { get; init; }
    public bool IsDetached { get; init; }
    public bool IsLocked { get; init; }
    public bool IsCurrent { get; init; }      // the worktree currently open in the app
}

/// <summary>A submodule with its checkout status.</summary>
public sealed class SubmoduleInfo
{
    public required string Path { get; init; }
    public string? Url { get; init; }
    public string? HeadSha { get; init; }

    /// <summary>Human-readable status: "initialized", "not-initialized", or "out-of-date".</summary>
    public string Status { get; init; } = string.Empty;
}

/// <summary>Git LFS state for the repository.</summary>
public sealed class LfsStatus
{
    public required bool Available { get; init; }         // is `git lfs` installed / usable
    public IReadOnlyList<string> TrackedPatterns { get; init; } = Array.Empty<string>();
    public int TrackedFileCount { get; init; }
}

/// <summary>A contributor and how many commits they authored.</summary>
public sealed class ContributorInfo
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required int Commits { get; init; }
}

/// <summary>High-level repository statistics for the dashboard.</summary>
public sealed class RepoStats
{
    public int CommitCount { get; init; }
    public int BranchCount { get; init; }
    public int TagCount { get; init; }
    public IReadOnlyList<ContributorInfo> Contributors { get; init; } = Array.Empty<ContributorInfo>();
    public DateTimeOffset? LastActivity { get; init; }
}

/// <summary>Result of verifying a commit's signature (maps git's `%G?` codes).</summary>
public enum CommitSignature
{
    None,        // N — unsigned
    Good,        // G — good signature
    GoodUntrusted, // U — good but untrusted
    Bad,         // B — bad signature
    Unknown      // E/X/Y/R — expired, revoked, or cannot check
}

/// <summary>
/// What merging a branch into HEAD would do, computed without touching the working tree
/// (LibGit2Sharp merges the two commits in memory). Lets the UI warn about conflicts *before* the
/// user starts a merge they would have to abort.
/// </summary>
public sealed class MergePreview
{
    /// <summary>HEAD already contains the other branch — merging would do nothing.</summary>
    public bool AlreadyUpToDate { get; init; }

    /// <summary>HEAD is an ancestor of the other branch, so the merge is a fast-forward.</summary>
    public bool CanFastForward { get; init; }

    /// <summary>Paths that would conflict, empty when the merge would apply cleanly.</summary>
    public IReadOnlyList<string> ConflictingPaths { get; init; } = Array.Empty<string>();

    public bool HasConflicts => ConflictingPaths.Count > 0;
}
