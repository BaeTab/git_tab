using GitTab.Core.Merge;
using FluentAssertions;
using Xunit;

namespace GitTab.Core.Tests;

public sealed class ConflictParserTests
{
    private const string Conflicted =
        "line one\n" +
        "<<<<<<< HEAD\n" +
        "our change\n" +
        "=======\n" +
        "their change\n" +
        ">>>>>>> feature\n" +
        "line last\n";

    [Fact]
    public void Detects_conflict_markers()
    {
        ConflictParser.HasConflictMarkers(Conflicted).Should().BeTrue();
        ConflictParser.HasConflictMarkers("no markers here").Should().BeFalse();
    }

    [Fact]
    public void Parses_literal_and_conflict_parts()
    {
        var parts = ConflictParser.Parse(Conflicted);
        parts.Should().HaveCount(3);
        parts[0].Text.Should().Contain("line one");
        parts[1].IsConflict.Should().BeTrue();
        parts[1].Conflict!.Ours.Should().Be("our change");
        parts[1].Conflict!.Theirs.Should().Be("their change");
        parts[2].Text.Should().Contain("line last");
    }

    [Fact]
    public void Resolves_choosing_ours_theirs_and_both()
    {
        var parts = ConflictParser.Parse(Conflicted);
        parts[1].Conflict!.Choice = ConflictChoice.Ours;
        ConflictParser.Resolve(parts).Should().Contain("our change").And.NotContain("their change");

        parts[1].Conflict!.Choice = ConflictChoice.Theirs;
        ConflictParser.Resolve(parts).Should().Contain("their change").And.NotContain("our change");

        parts[1].Conflict!.Choice = ConflictChoice.Both;
        var both = ConflictParser.Resolve(parts);
        both.Should().Contain("our change");
        both.Should().Contain("their change");
    }

    [Fact]
    public void Resolved_output_has_no_markers()
    {
        var parts = ConflictParser.Parse(Conflicted);
        parts[1].Conflict!.Choice = ConflictChoice.Ours;
        var resolved = ConflictParser.Resolve(parts);
        ConflictParser.HasConflictMarkers(resolved).Should().BeFalse();
    }

    [Fact]
    public void Handles_diff3_base_section()
    {
        const string diff3 =
            "<<<<<<< HEAD\n" + "ours\n" + "||||||| base\n" + "original\n" + "=======\n" + "theirs\n" + ">>>>>>> b\n";
        var parts = ConflictParser.Parse(diff3);
        parts.Should().ContainSingle(p => p.IsConflict);
        var c = parts.Single(p => p.IsConflict).Conflict!;
        c.Ours.Should().Be("ours");
        c.Theirs.Should().Be("theirs");
    }
}
