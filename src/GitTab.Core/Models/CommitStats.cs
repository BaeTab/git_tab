namespace GitTab.Core.Models;

/// <summary>Per-commit change summary (relative to the first parent), for the graph's Changes column.</summary>
public sealed class CommitStats
{
    public required int FilesChanged { get; init; }
    public required int Additions { get; init; }
    public required int Deletions { get; init; }

    public int Total => Additions + Deletions;
}
