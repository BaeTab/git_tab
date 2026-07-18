namespace GitTab.Core.Models;

/// <summary>
/// UI-agnostic snapshot of a single commit. Immutable.
/// </summary>
public sealed class CommitInfo
{
    public required string Sha { get; init; }

    /// <summary>Abbreviated (7-char) SHA for display.</summary>
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;

    /// <summary>Parent SHAs in order. Empty for a root commit; 2+ for merges.</summary>
    public required IReadOnlyList<string> ParentShas { get; init; }

    public required string Summary { get; init; }

    public string MessageFull { get; init; } = string.Empty;

    public required string AuthorName { get; init; }

    public string AuthorEmail { get; init; } = string.Empty;

    public required DateTimeOffset WhenUtc { get; init; }

    public bool IsMerge => ParentShas.Count > 1;
}
