namespace GitTab.Core.Models;

public enum RefKind
{
    LocalBranch,
    RemoteBranch,
    Tag,
    Head
}

/// <summary>A branch/tag/HEAD label attached to a commit row (rendered as a chip).</summary>
public sealed class RefLabel
{
    public required string Name { get; init; }
    public required RefKind Kind { get; init; }
    public required string TargetSha { get; init; }

    /// <summary>True when this label is the currently checked-out ref.</summary>
    public bool IsCurrent { get; init; }
}

public sealed class BranchInfo
{
    public required string CanonicalName { get; init; }   // e.g. refs/heads/main, refs/remotes/origin/main
    public required string FriendlyName { get; init; }    // e.g. main, origin/main
    public required bool IsRemote { get; init; }
    public bool IsCurrent { get; init; }
    public string? TipSha { get; init; }
    public string? UpstreamFriendlyName { get; init; }
    public int? Ahead { get; init; }
    public int? Behind { get; init; }
    public string? RemoteName { get; init; }
}

public sealed class TagInfo
{
    public required string Name { get; init; }
    public required string TargetSha { get; init; }
    public bool IsAnnotated { get; init; }
}

public sealed class HeadInfo
{
    public required bool IsDetached { get; init; }
    public required bool IsUnborn { get; init; }
    public string? BranchFriendlyName { get; init; }
    public string? TipSha { get; init; }
}

/// <summary>A configured remote (e.g. origin) and its URL.</summary>
public sealed class RemoteInfo
{
    public required string Name { get; init; }
    public required string Url { get; init; }
}

/// <summary>One entry in HEAD's reflog — a past position of HEAD you can restore to (undo).</summary>
public sealed class ReflogEntry
{
    public required int Index { get; init; }          // HEAD@{Index}
    public required string Sha { get; init; }         // the commit HEAD pointed to after this step
    public required string ShortSha { get; init; }
    public required string Message { get; init; }     // e.g. "commit: ...", "pull", "reset: ..."
    public required DateTimeOffset When { get; init; }
}
