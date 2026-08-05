using FluentAssertions;
using GitTab.Core.Git;
using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>
/// Predicting a merge before running it. The point of the feature is that the answer costs nothing
/// to get wrong-way-round: the working tree and index must be exactly as they were afterwards, even
/// when the prediction is "this conflicts".
/// </summary>
public sealed class MergePreviewTests
{
    private static RepositoryService NewService()
        => new(new GitCommandRunner(NullLogger<GitCommandRunner>.Instance), NullLogger<RepositoryService>.Instance);

    [Fact]
    public void Predicts_a_clean_merge_when_the_branches_touch_different_files()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "a.txt", "a\n");
        var main = repo.CurrentBranch;

        repo.CreateBranch("feature", checkout: true);
        repo.Commit("feature work", "b.txt", "b\n");

        repo.Checkout(main);
        repo.Commit("main work", "c.txt", "c\n");

        using var svc = NewService();
        svc.Open(repo.Path);
        var preview = svc.PreviewMerge("feature");

        preview.Should().NotBeNull();
        preview!.HasConflicts.Should().BeFalse();
        preview.AlreadyUpToDate.Should().BeFalse();
        preview.CanFastForward.Should().BeFalse();
    }

    [Fact]
    public void Predicts_the_conflicting_files_when_both_sides_edited_the_same_lines()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "shared.txt", "original\n");
        var main = repo.CurrentBranch;

        repo.CreateBranch("feature", checkout: true);
        repo.Commit("their change", "shared.txt", "theirs\n");

        repo.Checkout(main);
        repo.Commit("our change", "shared.txt", "ours\n");

        using var svc = NewService();
        svc.Open(repo.Path);
        var preview = svc.PreviewMerge("feature");

        preview.Should().NotBeNull();
        preview!.HasConflicts.Should().BeTrue();
        preview.ConflictingPaths.Should().ContainSingle().Which.Should().Be("shared.txt");
    }

    [Fact]
    public void Predicting_a_conflict_leaves_the_working_tree_and_index_untouched()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "shared.txt", "original\n");
        var main = repo.CurrentBranch;

        repo.CreateBranch("feature", checkout: true);
        repo.Commit("their change", "shared.txt", "theirs\n");

        repo.Checkout(main);
        repo.Commit("our change", "shared.txt", "ours\n");

        using var svc = NewService();
        svc.Open(repo.Path);
        svc.PreviewMerge("feature")!.HasConflicts.Should().BeTrue();

        // No merge was started: no MERGE_HEAD, no conflicted index entries, and the file on disk
        // still holds our side with no conflict markers written into it.
        using var check = new Repository(repo.Path);
        check.Index.Conflicts.Should().BeEmpty();
        check.Info.CurrentOperation.Should().Be(CurrentOperation.None);
        File.ReadAllText(Path.Combine(repo.Path, "shared.txt")).Should().Be("ours\n");
        svc.GetStatus().IsClean.Should().BeTrue();
    }

    [Fact]
    public void Reports_a_fast_forward_when_head_is_an_ancestor()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "a.txt", "a\n");
        var main = repo.CurrentBranch;

        repo.CreateBranch("feature", checkout: true);
        repo.Commit("ahead", "a.txt", "a\nb\n");

        repo.Checkout(main);

        using var svc = NewService();
        svc.Open(repo.Path);
        var preview = svc.PreviewMerge("feature");

        preview!.CanFastForward.Should().BeTrue();
        preview.HasConflicts.Should().BeFalse();
    }

    [Fact]
    public void Reports_up_to_date_when_the_branch_is_already_contained()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "a.txt", "a\n");
        var main = repo.CurrentBranch;
        repo.CreateBranch("feature");           // points at the same commit, stays behind
        repo.Commit("main moves on", "a.txt", "a\nb\n");

        using var svc = NewService();
        svc.Open(repo.Path);
        var preview = svc.PreviewMerge("feature");

        preview!.AlreadyUpToDate.Should().BeTrue();
        preview.HasConflicts.Should().BeFalse();
    }

    [Fact]
    public void Returns_null_for_an_unknown_branch()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("root", "a.txt", "a\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        svc.PreviewMerge("no-such-branch").Should().BeNull();
    }
}
