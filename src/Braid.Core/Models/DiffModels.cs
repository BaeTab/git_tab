namespace Braid.Core.Models;

public enum DiffLineKind
{
    Context,
    Added,
    Removed,
    HunkHeader,
    FileHeader,
    NoNewline
}

public sealed class DiffLine
{
    public required DiffLineKind Kind { get; init; }
    public required string Text { get; init; }
    public int? OldLineNumber { get; init; }
    public int? NewLineNumber { get; init; }
}

public sealed class DiffHunk
{
    public required string Header { get; init; }
    public int OldStart { get; init; }
    public int OldCount { get; init; }
    public int NewStart { get; init; }
    public int NewCount { get; init; }
    public IReadOnlyList<DiffLine> Lines { get; init; } = Array.Empty<DiffLine>();
}

/// <summary>Parsed unified diff for one file.</summary>
public sealed class FileDiff
{
    public required string Path { get; init; }
    public string? OldPath { get; init; }
    public bool IsBinary { get; init; }

    /// <summary>Raw unified diff text (as produced by git/libgit2), for verbatim display.</summary>
    public string RawPatch { get; init; } = string.Empty;

    public IReadOnlyList<DiffHunk> Hunks { get; init; } = Array.Empty<DiffHunk>();

    public int AddedLines { get; init; }
    public int RemovedLines { get; init; }

    /// <summary>True when there is genuinely no textual change (e.g. mode-only or unchanged).</summary>
    public bool IsEmpty => !IsBinary && Hunks.Count == 0;
}
