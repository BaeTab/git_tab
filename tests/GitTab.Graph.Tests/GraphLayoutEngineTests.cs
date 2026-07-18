using GitTab.Graph;
using GitTab.Graph.Models;
using FluentAssertions;
using Xunit;

namespace GitTab.Graph.Tests;

public sealed class GraphLayoutEngineTests
{
    private static readonly GraphLayoutEngine Engine = new();

    private static GraphRow Row(GraphLayout layout, string sha)
        => layout.Rows.Single(r => r.CommitSha == sha);

    // ------------------------------------------------------------- linear

    [Fact]
    public void Linear_history_stays_in_lane_zero()
    {
        // D -> C -> B -> A(root), newest first.
        var commits = new[]
        {
            new GraphCommit("D", "C"),
            new GraphCommit("C", "B"),
            new GraphCommit("B", "A"),
            new GraphCommit("A"),
        };

        var layout = Engine.Build(commits);

        layout.LaneCount.Should().Be(1);
        layout.Rows.Should().OnlyContain(r => r.NodeLane == 0);
        layout.Rows.Should().OnlyContain(r => r.ColorIndex == 0);

        // Root has no outgoing branch; every other commit continues straight down.
        Row(layout, "A").PassingLanes.Should().NotContain(s => s.Kind == LaneKind.Branch);
        Row(layout, "D").PassingLanes.Should().Contain(s => s.Kind == LaneKind.Branch && s.ToLane == 0);
    }

    // ------------------------------------------------------------- branch + merge (diamond)

    [Fact]
    public void Branch_and_merge_places_feature_on_second_lane_and_merges_back()
    {
        //        M (merge of B[main], C[feature])
        //       / \
        //   B(main) C(feature)
        //       \ /
        //        A (root)
        var commits = new[]
        {
            new GraphCommit("M", "B", "C"),
            new GraphCommit("B", "A"),
            new GraphCommit("C", "A"),
            new GraphCommit("A"),
        };

        var layout = Engine.Build(commits);

        layout.LaneCount.Should().Be(2);
        Row(layout, "M").NodeLane.Should().Be(0);
        Row(layout, "B").NodeLane.Should().Be(0);
        Row(layout, "C").NodeLane.Should().Be(1);
        Row(layout, "A").NodeLane.Should().Be(0);

        // Merge commit fans a diagonal out to the feature lane (second parent).
        Row(layout, "M").PassingLanes.Should()
            .Contain(s => s.Kind == LaneKind.Branch && s.FromLane == 0 && s.ToLane == 1);

        // Feature commit merges back down into the main lane.
        Row(layout, "C").PassingLanes.Should()
            .Contain(s => s.Kind == LaneKind.Branch && s.FromLane == 1 && s.ToLane == 0);

        // The main lane passes straight through the feature commit's row.
        Row(layout, "C").PassingLanes.Should()
            .Contain(s => s.Kind == LaneKind.Straight && s.FromLane == 0 && s.ToLane == 0);
    }

    // ------------------------------------------------------------- octopus (3+ parents)

    [Fact]
    public void Octopus_merge_fans_out_to_all_parents()
    {
        //   O = merge(P1, P2, P3), each Pn child of root R
        var commits = new[]
        {
            new GraphCommit("O", "P1", "P2", "P3"),
            new GraphCommit("P1", "R"),
            new GraphCommit("P2", "R"),
            new GraphCommit("P3", "R"),
            new GraphCommit("R"),
        };

        var layout = Engine.Build(commits);

        var o = Row(layout, "O");
        var branches = o.PassingLanes.Where(s => s.Kind == LaneKind.Branch).ToList();
        branches.Should().HaveCount(3);
        branches.Select(b => b.ToLane).Should().BeEquivalentTo(new[] { 0, 1, 2 });

        layout.LaneCount.Should().BeGreaterThanOrEqualTo(3);
        Row(layout, "P2").NodeLane.Should().Be(1);
        Row(layout, "P3").NodeLane.Should().Be(2);

        // All three eventually reach the single root.
        Row(layout, "R").NodeLane.Should().Be(0);
    }

    // ------------------------------------------------------------- three parallel (disjoint) histories

    [Fact]
    public void Three_parallel_histories_use_three_lanes()
    {
        var commits = new[]
        {
            new GraphCommit("X2", "X1"),
            new GraphCommit("Y2", "Y1"),
            new GraphCommit("Z2", "Z1"),
            new GraphCommit("X1"),
            new GraphCommit("Y1"),
            new GraphCommit("Z1"),
        };

        var layout = Engine.Build(commits);

        layout.LaneCount.Should().Be(3);
        Row(layout, "X2").NodeLane.Should().Be(0);
        Row(layout, "Y2").NodeLane.Should().Be(1);
        Row(layout, "Z2").NodeLane.Should().Be(2);
        // Distinct roots keep their lanes.
        Row(layout, "X1").NodeLane.Should().Be(0);
        Row(layout, "Y1").NodeLane.Should().Be(1);
        Row(layout, "Z1").NodeLane.Should().Be(2);
    }

    // ------------------------------------------------------------- convergence (multiple children, one parent)

    [Fact]
    public void Multiple_children_converge_into_one_parent()
    {
        //   B(main) and C(side) both have parent A; no merge commit.
        var commits = new[]
        {
            new GraphCommit("B", "A"),
            new GraphCommit("C", "A"),
            new GraphCommit("A"),
        };

        var layout = Engine.Build(commits);

        // B holds lane 0 down to A; C (the later-seen child) takes lane 1 and its edge bends
        // back into lane 0 to converge on the shared parent A — the standard rendering.
        Row(layout, "A").NodeLane.Should().Be(0);
        Row(layout, "C").NodeLane.Should().Be(1);
        Row(layout, "C").PassingLanes.Should()
            .Contain(s => s.Kind == LaneKind.Branch && s.FromLane == 1 && s.ToLane == 0);
        // A receives a single incoming edge on its own lane (no dangling diagonal).
        Row(layout, "A").PassingLanes.Should()
            .Contain(s => s.Kind == LaneKind.Merge && s.FromLane == 0 && s.ToLane == 0);
    }

    // ------------------------------------------------------------- edge cases

    [Fact]
    public void Empty_input_returns_empty_layout()
    {
        Engine.Build(Array.Empty<GraphCommit>()).Should().BeSameAs(GraphLayout.Empty);
    }

    [Fact]
    public void Root_commit_has_no_outgoing_edge()
    {
        var layout = Engine.Build(new[] { new GraphCommit("A") });
        var a = Row(layout, "A");
        a.NodeLane.Should().Be(0);
        a.PassingLanes.Should().NotContain(s => s.Kind == LaneKind.Branch);
    }

    [Fact]
    public void Two_orphan_roots_do_not_crash_and_get_distinct_lanes()
    {
        // Disconnected histories: (M1->M0) and standalone N0.
        var commits = new[]
        {
            new GraphCommit("M1", "M0"),
            new GraphCommit("N0"),
            new GraphCommit("M0"),
        };

        var layout = Engine.Build(commits);
        layout.Rows.Should().HaveCount(3);
        Row(layout, "M1").NodeLane.Should().Be(0);
        Row(layout, "N0").NodeLane.Should().Be(1);
    }

    [Fact]
    public void Color_index_wraps_around_palette()
    {
        var engine = new GraphLayoutEngine(paletteSize: 3);
        // Five parallel 2-commit chains => five concurrent lanes 0..4 => colors 0,1,2,0,1.
        var commits = new[]
        {
            new GraphCommit("A2", "A1"), new GraphCommit("B2", "B1"), new GraphCommit("C2", "C1"),
            new GraphCommit("D2", "D1"), new GraphCommit("E2", "E1"),
            new GraphCommit("A1"), new GraphCommit("B1"), new GraphCommit("C1"),
            new GraphCommit("D1"), new GraphCommit("E1"),
        };
        var layout = engine.Build(commits);
        layout.LaneCount.Should().Be(5);
        layout.Rows.Take(5).Select(r => r.ColorIndex).Should().Equal(0, 1, 2, 0, 1);
    }

    [Fact]
    public void Every_segment_references_a_valid_lane()
    {
        var commits = new[]
        {
            new GraphCommit("M", "B", "C"),
            new GraphCommit("B", "A"),
            new GraphCommit("C", "A"),
            new GraphCommit("A"),
        };
        var layout = Engine.Build(commits);
        foreach (var row in layout.Rows)
        foreach (var seg in row.PassingLanes)
        {
            seg.FromLane.Should().BeInRange(0, layout.LaneCount - 1);
            seg.ToLane.Should().BeInRange(0, layout.LaneCount - 1);
            seg.ColorIndex.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}
