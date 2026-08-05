using System.IO;
using FluentAssertions;
using GitTab.App.ViewModels;
using GitTab.Core.Merge;
using Xunit;

namespace GitTab.App.Tests;

/// <summary>
/// Per-block conflict resolution. The behaviour that matters is that choices are independent:
/// picking "theirs" for one region must not disturb the regions the user already settled.
/// </summary>
public sealed class ConflictResolverViewModelTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), "gittab-conflict-" + Guid.NewGuid().ToString("N") + ".txt");

    private const string TwoConflicts =
        "top\n" +
        "<<<<<<< HEAD\n" +
        "ours one\n" +
        "=======\n" +
        "theirs one\n" +
        ">>>>>>> feature\n" +
        "middle\n" +
        "<<<<<<< HEAD\n" +
        "ours two\n" +
        "=======\n" +
        "theirs two\n" +
        ">>>>>>> feature\n" +
        "bottom\n";

    private ConflictResolverViewModel NewVm(string content)
    {
        File.WriteAllText(_file, content);
        return new ConflictResolverViewModel("shared.txt", _file, "base", "ours whole file", "theirs whole file");
    }

    [Fact]
    public void Lists_every_conflicting_region()
    {
        var vm = NewVm(TwoConflicts);

        vm.HasBlocks.Should().BeTrue();
        vm.Blocks.Should().HaveCount(2);
        vm.Blocks[0].Number.Should().Be(1);
        vm.Blocks[0].OursPreview.Should().Be("ours one");
        vm.Blocks[0].TheirsPreview.Should().Be("theirs one");
        vm.Blocks[1].TheirsPreview.Should().Be("theirs two");
    }

    [Fact]
    public void Resolving_one_block_leaves_the_other_blocks_choice_intact()
    {
        var vm = NewVm(TwoConflicts);

        vm.Blocks[0].Choice = ConflictChoice.Theirs;
        vm.Blocks[1].Choice = ConflictChoice.Ours;

        vm.Result.Should().Contain("theirs one");
        vm.Result.Should().NotContain("ours one");
        vm.Result.Should().Contain("ours two");
        vm.Result.Should().NotContain("theirs two");
    }

    [Fact]
    public void Choosing_a_side_removes_the_conflict_markers()
    {
        var vm = NewVm(TwoConflicts);

        vm.Blocks[0].Choice = ConflictChoice.Theirs;
        vm.Blocks[1].Choice = ConflictChoice.Theirs;

        ConflictParser.HasConflictMarkers(vm.Result).Should().BeFalse();
        vm.Result.Should().StartWith("top\n");
        vm.Result.Should().Contain("middle\n");
        vm.Result.Should().Contain("bottom\n");
    }

    [Fact]
    public void Blocks_stay_switchable_after_the_markers_are_gone()
    {
        var vm = NewVm(TwoConflicts);

        vm.Blocks[0].Choice = ConflictChoice.Ours;      // result no longer has markers
        vm.Blocks[0].Choice = ConflictChoice.Theirs;    // ...but the user can still change their mind

        vm.Blocks.Should().HaveCount(2);
        vm.Result.Should().Contain("theirs one");
    }

    [Fact]
    public void Keep_both_applies_to_every_block()
    {
        var vm = NewVm(TwoConflicts);

        vm.KeepBothCommand.Execute(null);

        vm.Result.Should().Contain("ours one");
        vm.Result.Should().Contain("theirs one");
        vm.Result.Should().Contain("ours two");
        vm.Result.Should().Contain("theirs two");
        ConflictParser.HasConflictMarkers(vm.Result).Should().BeFalse();
    }

    [Fact]
    public void Use_ours_rebuilds_from_the_blocks_so_non_conflicting_edits_survive()
    {
        var vm = NewVm(TwoConflicts);

        vm.UseOursCommand.Execute(null);

        vm.Result.Should().Contain("ours one").And.Contain("ours two");
        vm.Result.Should().NotContain("theirs one");
        // The untouched surrounding text is still there — it would be lost if the whole file were
        // replaced by the index's "ours" side.
        vm.Result.Should().Contain("top").And.Contain("middle").And.Contain("bottom");
    }

    [Fact]
    public void A_file_without_markers_has_no_blocks()
    {
        var vm = NewVm("just some text\nwith no conflicts\n");

        vm.HasBlocks.Should().BeFalse();
        vm.Blocks.Should().BeEmpty();
    }

    [Fact]
    public void Falls_back_to_the_whole_ours_side_when_there_are_no_blocks()
    {
        var vm = NewVm("already resolved\n");

        vm.UseOursCommand.Execute(null);

        vm.Result.Should().Be("ours whole file");
    }

    [Fact]
    public void Apply_writes_the_resolved_text_to_disk()
    {
        var vm = NewVm(TwoConflicts);
        vm.Blocks[0].Choice = ConflictChoice.Theirs;
        vm.Blocks[1].Choice = ConflictChoice.Theirs;

        vm.Apply();

        vm.Written.Should().BeTrue();
        var written = File.ReadAllText(_file);
        written.Should().Contain("theirs one").And.NotContain("<<<<<<<");
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { /* temp file */ }
    }
}
