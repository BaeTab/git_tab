namespace GitTab.Graph.Models;

/// <summary>Layout result for a single commit row.</summary>
public sealed class GraphRow
{
    public required string CommitSha { get; init; }

    /// <summary>Column the commit's node is drawn in.</summary>
    public required int NodeLane { get; init; }

    /// <summary>Palette index for the node (NodeLane % palette size).</summary>
    public required int ColorIndex { get; init; }

    /// <summary>Every edge occupying this row (pass-throughs, incoming merges, outgoing branches).</summary>
    public required IReadOnlyList<LaneSegment> PassingLanes { get; init; }
}

/// <summary>Full layout: rows plus the maximum number of simultaneous lanes (for sizing the canvas).</summary>
public sealed class GraphLayout
{
    public required IReadOnlyList<GraphRow> Rows { get; init; }
    public required int LaneCount { get; init; }

    public static readonly GraphLayout Empty = new() { Rows = Array.Empty<GraphRow>(), LaneCount = 0 };
}
