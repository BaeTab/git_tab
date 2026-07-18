namespace GitTab.Graph.Models;

/// <summary>
/// How a <see cref="LaneSegment"/> occupies its row, in terms of vertical span. The renderer
/// uses this to know whether an edge crosses the whole row or only a half.
/// </summary>
public enum LaneKind
{
    /// <summary>Full row height: a lane passing straight through (top edge → bottom edge).</summary>
    Straight,

    /// <summary>Top half: an edge coming from above that terminates at this row's node
    /// (top edge at <see cref="LaneSegment.FromLane"/> → node center at <see cref="LaneSegment.ToLane"/>).</summary>
    Merge,

    /// <summary>Bottom half: an edge leaving this row's node toward a parent
    /// (node center at <see cref="LaneSegment.FromLane"/> → bottom edge at <see cref="LaneSegment.ToLane"/>).</summary>
    Branch
}

/// <summary>One edge drawn within a single graph row.</summary>
public readonly record struct LaneSegment(int FromLane, int ToLane, int ColorIndex, LaneKind Kind);
