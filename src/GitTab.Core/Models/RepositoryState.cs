namespace GitTab.Core.Models;

/// <summary>The pending multi-step operation a repository is in the middle of, if any.</summary>
public enum RepositoryOperation
{
    None,
    Merge,
    Rebase,
    CherryPick,
    Revert,
    Bisect
}

/// <summary>Snapshot of whether the repo is mid-operation and which paths are conflicted.</summary>
public sealed class RepositoryStateInfo
{
    public required RepositoryOperation Operation { get; init; }
    public IReadOnlyList<string> ConflictedPaths { get; init; } = Array.Empty<string>();

    public bool IsInProgress => Operation != RepositoryOperation.None;
    public bool HasConflicts => ConflictedPaths.Count > 0;
}
