using GitTab.Core.Diff;
using FluentAssertions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>Unit tests for the intra-line word diff that powers changed-word highlighting.</summary>
public sealed class WordDiffTests
{
    [Fact]
    public void Identical_lines_produce_no_segments()
    {
        var (oldSegs, newSegs) = WordDiff.Compute("the quick brown fox", "the quick brown fox");
        oldSegs.Should().BeEmpty();
        newSegs.Should().BeEmpty();
    }

    [Fact]
    public void Single_word_change_marks_only_that_word()
    {
        var (oldSegs, newSegs) = WordDiff.Compute("the quick brown fox", "the quick red fox");

        // "brown" replaced by "red"; the surrounding words stay unhighlighted.
        Text("the quick brown fox", oldSegs).Should().Be("brown");
        Text("the quick red fox", newSegs).Should().Be("red");
    }

    [Fact]
    public void Appended_words_are_marked_on_the_new_side_only()
    {
        var (oldSegs, newSegs) = WordDiff.Compute("int x = 1;", "int x = 1; // note");

        oldSegs.Should().BeEmpty();
        Text("int x = 1; // note", newSegs).Should().Contain("note");
    }

    [Fact]
    public void Removed_words_are_marked_on_the_old_side_only()
    {
        var (oldSegs, newSegs) = WordDiff.Compute("value = compute(a, b);", "value = compute(a);");

        Text("value = compute(a, b);", oldSegs).Should().Contain("b");
        newSegs.Should().BeEmpty();
    }

    [Fact]
    public void Very_long_lines_are_skipped_to_stay_cheap()
    {
        var longOld = string.Join(" ", Enumerable.Range(0, 1000).Select(i => $"w{i}"));
        var longNew = longOld + " extra";

        var (oldSegs, newSegs) = WordDiff.Compute(longOld, longNew);
        oldSegs.Should().BeEmpty();
        newSegs.Should().BeEmpty();
    }

    private static string Text(string line, IReadOnlyList<WordDiff.Segment> segs)
        => string.Concat(segs.Select(s => line.Substring(s.Start, s.Length)));
}
