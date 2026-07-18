using GitTab.Core.Git;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>
/// Integration tests that drive real git.exe through RepositoryService, validating the trickier
/// plumbing (interactive rebase sequence editor, conflict abort, stash, blame).
/// </summary>
public sealed class RepositoryServiceWriteTests
{
    private static RepositoryService NewService()
        => new(new GitCommandRunner(NullLogger<GitCommandRunner>.Instance), NullLogger<RepositoryService>.Instance);

    [Fact]
    public void Reads_commits_between_two_shas_newest_first()
    {
        using var repo = TestRepository.CreateEmpty();
        var a = repo.Commit("A", "f.txt", "1");
        var b = repo.Commit("B", "f.txt", "12");
        var c = repo.Commit("C", "f.txt", "123");

        using var svc = NewService();
        svc.Open(repo.Path);
        var between = svc.GetCommitsBetween(a, c);
        between.Select(x => x.Sha).Should().Equal(c, b);
    }

    [Fact]
    public async Task Interactive_rebase_squash_reduces_commit_count()
    {
        using var repo = TestRepository.CreateEmpty();
        var a = repo.Commit("A", "a.txt", "a");
        var b = repo.Commit("B", "b.txt", "b");
        var c = repo.Commit("C", "c.txt", "c");

        using var svc = NewService();
        svc.Open(repo.Path);

        // Replay B, C onto A, squashing C into B (plan is oldest-first).
        var plan = new List<RebaseTodoItem>
        {
            new() { Sha = b, Summary = "B", Action = RebaseAction.Pick },
            new() { Sha = c, Summary = "C", Action = RebaseAction.Squash },
        };
        var result = await svc.RebaseInteractiveAsync(a, plan);

        result.Success.Should().BeTrue(result.CombinedOutput);
        svc.Refresh();
        svc.GetCommits().Should().HaveCount(2); // A + (B+C squashed)
    }

    [Fact]
    public async Task Merge_conflict_is_detected_then_aborted()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "f.txt", "base\n");
        var main = repo.CurrentBranch;
        repo.CreateBranch("feature", checkout: true);
        repo.Commit("feature change", "f.txt", "feature\n");
        repo.Checkout(main);
        repo.Commit("main change", "f.txt", "main\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        var merge = await svc.MergeAsync("feature");
        merge.Success.Should().BeFalse(); // conflict

        svc.Refresh();
        var state = svc.GetState();
        state.Operation.Should().Be(RepositoryOperation.Merge);
        state.HasConflicts.Should().BeTrue();
        state.ConflictedPaths.Should().Contain("f.txt");

        var abort = await svc.AbortOperationAsync();
        abort.Success.Should().BeTrue(abort.CombinedOutput);
        svc.Refresh();
        svc.GetState().Operation.Should().Be(RepositoryOperation.None);
    }

    [Fact]
    public async Task Stash_push_then_pop_roundtrips_changes()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "f.txt", "one\n");
        repo.WriteFile("f.txt", "one-modified\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        var push = await svc.StashPushAsync("wip", includeUntracked: false);
        push.Success.Should().BeTrue(push.CombinedOutput);
        svc.Refresh();
        svc.GetStatus().IsClean.Should().BeTrue();
        svc.GetStashes().Should().HaveCount(1);

        var pop = await svc.StashApplyAsync(0, pop: true);
        pop.Success.Should().BeTrue(pop.CombinedOutput);
        svc.Refresh();
        svc.GetStatus().Unstaged.Should().Contain(f => f.Path == "f.txt");
        svc.GetStashes().Should().BeEmpty();
    }

    [Fact]
    public void Blame_attributes_each_line_to_its_commit()
    {
        using var repo = TestRepository.CreateEmpty();
        var c1 = repo.Commit("first", "f.txt", "alpha\nbeta\n");
        var c2 = repo.Commit("second", "f.txt", "alpha\nBETA2\n");

        using var svc = NewService();
        svc.Open(repo.Path);
        var blame = svc.GetBlame("f.txt");

        blame.Should().HaveCountGreaterThanOrEqualTo(2);
        blame[0].Sha.Should().Be(c1);   // line 1 unchanged since first commit
        blame[1].Sha.Should().Be(c2);   // line 2 changed in second commit
        blame[1].Content.Should().Contain("BETA2");
    }
}
