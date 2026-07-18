using GitTab.Graph.Models;

namespace GitTab.Graph;

/// <summary>
/// Assigns each commit to a lane (column) and produces the edge segments that connect commits,
/// in a single top-to-bottom pass. Pure and UI-agnostic.
///
/// State: <c>active</c> is the list of open lanes; <c>active[i]</c> is the SHA that lane i is
/// "waiting to meet" next (null = free). Because input is newest-first and topologically
/// ordered, any lane waiting for a commit was opened by a child already seen above it.
///
/// Segment vertical semantics (see <see cref="LaneKind"/>):
///   Straight — full-height pass-through (top edge → bottom edge, same column).
///   Merge    — top half: an edge from above ending at this node (top → node center).
///   Branch   — bottom half: an edge from this node to a parent (node center → bottom).
/// </summary>
public sealed class GraphLayoutEngine
{
    public const int DefaultPaletteSize = 10;

    private readonly int _paletteSize;

    public GraphLayoutEngine(int paletteSize = DefaultPaletteSize)
    {
        if (paletteSize < 1) throw new ArgumentOutOfRangeException(nameof(paletteSize));
        _paletteSize = paletteSize;
    }

    public GraphLayout Build(IReadOnlyList<GraphCommit> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        if (commits.Count == 0) return GraphLayout.Empty;

        var rows = new List<GraphRow>(commits.Count);
        var active = new List<string?>();
        int maxLanes = 0;

        foreach (var commit in commits)
        {
            var sha = commit.Sha;

            // Snapshot the lane state *before* this commit — these are the incoming edges.
            var lanesAbove = new List<string?>(active);

            // --- (a) node lane: leftmost lane already waiting for this commit, else a fresh lane.
            int nodeLane = -1;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] == sha)
                {
                    if (nodeLane < 0) nodeLane = i; // leftmost match wins
                    active[i] = null;               // (b) every lane waiting for c is consumed here
                }
            }
            if (nodeLane < 0)
                nodeLane = AllocateLane(active, null); // branch tip / head with no child above

            // --- (c) route parents onto lanes.
            var parentLanes = new List<(string Sha, int Lane)>(commit.ParentShas.Count);
            for (int k = 0; k < commit.ParentShas.Count; k++)
            {
                var parent = commit.ParentShas[k];
                int existing = IndexOf(active, parent);
                int lane;
                if (existing >= 0)
                {
                    // Parent already awaited on another lane — connect to it, don't open a new one.
                    lane = existing;
                }
                else if (k == 0)
                {
                    // First parent inherits this commit's lane (straight continuation downward).
                    active[nodeLane] = parent;
                    lane = nodeLane;
                }
                else
                {
                    // Additional parents (merge) open a new lane (diagonal branch downward).
                    lane = AllocateLane(active, parent);
                }
                parentLanes.Add((parent, lane));
            }
            // Root commit (no parents): its lane simply ends — leave active[nodeLane] == null.

            maxLanes = Math.Max(maxLanes, active.Count);

            // --- (d) emit segments.
            var segments = new List<LaneSegment>(lanesAbove.Count + parentLanes.Count);

            // Incoming from above + pass-throughs.
            for (int i = 0; i < lanesAbove.Count; i++)
            {
                var t = lanesAbove[i];
                if (t is null) continue;
                if (t == sha)
                    segments.Add(new LaneSegment(i, nodeLane, Color(i), LaneKind.Merge));  // top half → node
                else
                    segments.Add(new LaneSegment(i, i, Color(i), LaneKind.Straight));       // full-height pass-through
            }

            // Outgoing from node to each distinct parent lane.
            var emitted = new HashSet<int>();
            foreach (var (_, lane) in parentLanes)
            {
                if (!emitted.Add(lane)) continue;
                segments.Add(new LaneSegment(nodeLane, lane, Color(lane), LaneKind.Branch)); // bottom half from node
            }

            rows.Add(new GraphRow
            {
                CommitSha = sha,
                NodeLane = nodeLane,
                ColorIndex = Color(nodeLane),
                PassingLanes = segments
            });
        }

        return new GraphLayout { Rows = rows, LaneCount = Math.Max(maxLanes, 1) };
    }

    private int Color(int lane) => lane % _paletteSize;

    private static int IndexOf(List<string?> active, string sha)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i] == sha) return i;
        return -1;
    }

    /// <summary>Claims the leftmost free lane for <paramref name="target"/>, appending if none is free.</summary>
    private static int AllocateLane(List<string?> active, string? target)
    {
        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] is null)
            {
                active[i] = target;
                return i;
            }
        }
        active.Add(target);
        return active.Count - 1;
    }
}
