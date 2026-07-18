namespace Braid.Core.Models;

public enum FileChangeKind
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Untracked,
    Ignored,
    Conflicted,
    Unmodified
}

/// <summary>A single changed path, either in a commit or in the working tree.</summary>
public sealed class FileChange
{
    public required string Path { get; init; }

    /// <summary>Previous path for renames/copies; null otherwise.</summary>
    public string? OldPath { get; init; }

    public required FileChangeKind Kind { get; init; }

    /// <summary>True if this entry is staged (in the index); false if it is a working-tree change.</summary>
    public bool IsStaged { get; init; }

    public bool IsBinary { get; init; }

    public string DisplayPath => OldPath is { Length: > 0 } && OldPath != Path
        ? $"{OldPath} → {Path}"
        : Path;
}

/// <summary>Working-tree status: staged vs. unstaged change lists plus conflict info.</summary>
public sealed class WorkingTreeStatus
{
    public IReadOnlyList<FileChange> Staged { get; init; } = Array.Empty<FileChange>();
    public IReadOnlyList<FileChange> Unstaged { get; init; } = Array.Empty<FileChange>();

    public bool HasConflicts =>
        Staged.Any(c => c.Kind == FileChangeKind.Conflicted) ||
        Unstaged.Any(c => c.Kind == FileChangeKind.Conflicted);

    public bool IsClean => Staged.Count == 0 && Unstaged.Count == 0;
}
