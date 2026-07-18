using GitTab.Core.Models;
using GitTab.Graph.Models;

namespace GitTab.App.ViewModels;

/// <summary>A single history row: the graph layout for the row plus the commit's display data.</summary>
public sealed class CommitRowViewModel
{
    public required CommitInfo Commit { get; init; }
    public required GraphRow GraphRow { get; init; }
    public IReadOnlyList<RefLabel> Refs { get; init; } = Array.Empty<RefLabel>();

    public string Sha => Commit.Sha;
    public string ShortSha => Commit.ShortSha;
    public string Summary => Commit.Summary;
    public string AuthorName => Commit.AuthorName;
    public DateTimeOffset WhenUtc => Commit.WhenUtc;
    public bool IsMerge => Commit.IsMerge;

    /// <summary>True when HEAD points here (any current ref).</summary>
    public bool IsHead => Refs.Any(r => r.IsCurrent);
}
