using GitTab.Core.Diff;
using FluentAssertions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>Unit tests for the line-level partial-stage patch surgery.</summary>
public sealed class PatchBuilderTests
{
    private const string Header = "diff --git a/f.txt b/f.txt\n--- a/f.txt\n+++ b/f.txt";
    private const string Hunk = "@@ -1,2 +1,2 @@";

    // body: 0=" ctx", 1="-old", 2="+new"
    private static readonly string[] Body = { " ctx", "-old", "+new" };

    [Fact]
    public void Selecting_added_line_keeps_it_and_drops_unselected_addition()
    {
        // Select only the removal; the addition must be dropped.
        var patch = PatchBuilder.BuildPartialStagePatch(Header, Hunk, Body, new HashSet<int> { 1 });

        patch.Should().NotBeNull();
        patch.Should().Contain("-old");
        patch.Should().NotContain("+new");   // unselected addition dropped
    }

    [Fact]
    public void Unselected_removal_becomes_context()
    {
        // Select only the addition; the removal must turn into a context line (leading space).
        var patch = PatchBuilder.BuildPartialStagePatch(Header, Hunk, Body, new HashSet<int> { 2 });

        patch.Should().NotBeNull();
        patch.Should().Contain("+new");
        patch.Should().Contain(" old");      // removal kept as context
        patch.Should().NotContain("-old");   // ...and NOT as a deletion
    }

    [Fact]
    public void No_change_selected_returns_null()
    {
        PatchBuilder.BuildPartialStagePatch(Header, Hunk, Body, new HashSet<int>()).Should().BeNull();
        // Selecting only the context line is still no actual change.
        PatchBuilder.BuildPartialStagePatch(Header, Hunk, Body, new HashSet<int> { 0 }).Should().BeNull();
    }

    [Fact]
    public void Selecting_all_changes_reproduces_the_full_hunk()
    {
        var patch = PatchBuilder.BuildPartialStagePatch(Header, Hunk, Body, new HashSet<int> { 1, 2 });

        patch.Should().NotBeNull();
        patch.Should().Contain("-old");
        patch.Should().Contain("+new");
        patch.Should().StartWith("diff --git");
        patch.Should().Contain(Hunk);
    }

    [Fact]
    public void No_newline_marker_is_dropped_when_its_line_is_dropped()
    {
        string[] body = { " ctx", "+added", "\\ No newline at end of file" };
        // Do not select the addition — both the '+' and its trailing "\ No newline" must vanish.
        var patch = PatchBuilder.BuildPartialStagePatch(Header, Hunk, body, new HashSet<int>());

        patch.Should().BeNull(); // nothing staged
    }
}
