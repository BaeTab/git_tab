using FluentAssertions;
using GitTab.Core.Diff;
using GitTab.Core.Models;
using Xunit;

namespace GitTab.Core.Tests;

public sealed class MovedLineDetectorTests
{
    private static (DiffLineKind[] kinds, string[] texts) Build(params (DiffLineKind kind, string text)[] lines)
        => (lines.Select(l => l.kind).ToArray(), lines.Select(l => l.text).ToArray());

    [Fact]
    public void Detects_a_block_of_three_or_more_lines_moved_elsewhere()
    {
        // A 3-line block removed here and added later is "moved".
        var (kinds, texts) = Build(
            (DiffLineKind.Removed, "alpha"),
            (DiffLineKind.Removed, "beta"),
            (DiffLineKind.Removed, "gamma"),
            (DiffLineKind.Context, "middle"),
            (DiffLineKind.Added, "alpha"),
            (DiffLineKind.Added, "beta"),
            (DiffLineKind.Added, "gamma"));

        var moved = MovedLineDetector.Detect(kinds, texts);

        moved.Should().BeEquivalentTo(new[] { 0, 1, 2, 4, 5, 6 });
    }

    [Fact]
    public void Does_not_flag_blocks_shorter_than_the_minimum()
    {
        var (kinds, texts) = Build(
            (DiffLineKind.Removed, "one"),
            (DiffLineKind.Removed, "two"),
            (DiffLineKind.Added, "one"),
            (DiffLineKind.Added, "two"));

        MovedLineDetector.Detect(kinds, texts, minBlock: 3).Should().BeEmpty();
    }

    [Fact]
    public void Genuine_additions_and_removals_are_not_moved()
    {
        var (kinds, texts) = Build(
            (DiffLineKind.Removed, "old one"),
            (DiffLineKind.Removed, "old two"),
            (DiffLineKind.Removed, "old three"),
            (DiffLineKind.Added, "new one"),
            (DiffLineKind.Added, "new two"),
            (DiffLineKind.Added, "new three"));

        MovedLineDetector.Detect(kinds, texts).Should().BeEmpty();
    }

    [Fact]
    public void Blank_lines_do_not_anchor_a_moved_block()
    {
        // Three blank removed lines that also appear as blank added lines must NOT count as moved.
        var (kinds, texts) = Build(
            (DiffLineKind.Removed, "   "),
            (DiffLineKind.Removed, ""),
            (DiffLineKind.Removed, "\t"),
            (DiffLineKind.Added, "   "),
            (DiffLineKind.Added, ""),
            (DiffLineKind.Added, "\t"));

        MovedLineDetector.Detect(kinds, texts).Should().BeEmpty();
    }

    [Fact]
    public void A_longer_moved_run_is_detected_whole()
    {
        var (kinds, texts) = Build(
            (DiffLineKind.Context, "keep"),
            (DiffLineKind.Removed, "L1"),
            (DiffLineKind.Removed, "L2"),
            (DiffLineKind.Removed, "L3"),
            (DiffLineKind.Removed, "L4"),
            (DiffLineKind.Added, "L1"),
            (DiffLineKind.Added, "L2"),
            (DiffLineKind.Added, "L3"),
            (DiffLineKind.Added, "L4"));

        var moved = MovedLineDetector.Detect(kinds, texts);

        moved.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    }
}
