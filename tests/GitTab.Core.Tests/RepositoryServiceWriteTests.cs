using GitTab.Core.Git;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>
/// Integration tests that drive real git.exe through RepositoryService, validating the trickier
/// plumbing (interactive rebase sequence editor, conflict abort, stash, blame, init, remotes,
/// reflog) plus argument-injection hardening.
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

        var plan = new List<RebaseTodoItem>
        {
            new() { Sha = b, Summary = "B", Action = RebaseAction.Pick },
            new() { Sha = c, Summary = "C", Action = RebaseAction.Squash },
        };
        var result = await svc.RebaseInteractiveAsync(a, plan);

        result.Success.Should().BeTrue(result.CombinedOutput);
        svc.Refresh();
        svc.GetCommits().Should().HaveCount(2);
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
        merge.Success.Should().BeFalse();

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
        blame[0].Sha.Should().Be(c1);
        blame[1].Sha.Should().Be(c2);
        blame[1].Content.Should().Contain("BETA2");
    }

    // ---------------------------------------------------------------- init / remotes / reflog

    [Fact]
    public async Task Init_creates_a_new_repository()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gittab-init-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var svc = NewService();
            svc.Discover(dir).Should().BeNull("the folder is not a repository yet");

            var result = await svc.InitAsync(dir, "main");
            result.Success.Should().BeTrue(result.CombinedOutput);

            Directory.Exists(Path.Combine(dir, ".git")).Should().BeTrue();
            svc.Discover(dir).Should().NotBeNull("it is a repository after init");
        }
        finally
        {
            TryDelete(dir);
        }
    }

    [Fact]
    public async Task Remotes_roundtrip_add_get_set_remove()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        svc.GetRemotes().Should().BeEmpty();
        svc.GetRemoteUrl().Should().BeNull();

        (await svc.AddRemoteAsync("origin", "https://example.com/foo.git")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetRemotes().Should().ContainSingle(r => r.Name == "origin" && r.Url == "https://example.com/foo.git");
        svc.GetRemoteUrl().Should().Be("https://example.com/foo.git");

        (await svc.SetRemoteUrlAsync("origin", "https://example.com/bar.git")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetRemoteUrl("origin").Should().Be("https://example.com/bar.git");

        (await svc.RemoveRemoteAsync("origin")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetRemotes().Should().BeEmpty();
        svc.GetRemoteUrl().Should().BeNull();
    }

    [Fact]
    public void Reflog_lists_recent_head_positions_newest_first()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        var latest = repo.Commit("second", "a.txt", "12");

        using var svc = NewService();
        svc.Open(repo.Path);

        var log = svc.GetReflog(50);
        log.Should().NotBeEmpty();
        log[0].Index.Should().Be(0);
        log[0].Sha.Should().Be(latest, "the most recent reflog entry is the latest commit");
        log.Should().OnlyContain(e => e.Sha.Length > 0);
    }

    [Fact]
    public void GetState_reports_clean_repository()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        svc.GetState().Operation.Should().Be(RepositoryOperation.None);
        svc.GetState().IsInProgress.Should().BeFalse();
        svc.GetSubmodulePaths().Should().BeEmpty();
    }

    // ---------------------------------------------------------------- argument-injection hardening

    [Theory]
    [InlineData("main")]
    [InlineData("feature/x")]
    [InlineData("release-1.0")]
    public void GitArg_accepts_normal_names(string name)
        => GitArg.IsSafe(name).Should().BeTrue();

    [Theory]
    [InlineData("--upload-pack=evil")]
    [InlineData("-D")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("-ctrl")]
    public void GitArg_rejects_option_like_or_control(string? name)
        => GitArg.IsSafe(name).Should().BeFalse();

    [Fact]
    public async Task Checkout_rejects_option_like_ref_without_running_git()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        var result = await svc.CheckoutAsync("--evil");
        result.Success.Should().BeFalse();
        result.CombinedOutput.Should().Contain("Unsafe");
    }

    [Fact]
    public async Task CreateTag_rejects_option_like_name()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.CreateTagAsync("-x")).Success.Should().BeFalse();
        (await svc.CreateTagAsync("v1.0")).Success.Should().BeTrue();
    }

    [Fact]
    public async Task SearchContent_finds_commits_that_added_the_term()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("c1", "a.txt", "hello world\n");
        var c2 = repo.Commit("c2", "a.txt", "hello world\nMAGIC_TOKEN_XYZ\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        var hits = await svc.SearchContentAsync("MAGIC_TOKEN_XYZ", useRegex: false);
        hits.Select(c => c.Sha).Should().Contain(c2);
    }

    [Fact]
    public async Task Reword_head_amends_the_message()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("a", "a.txt", "1");
        var head = repo.Commit("original", "b.txt", "2");

        using var svc = NewService();
        svc.Open(repo.Path);

        var r = await svc.RewordAsync(head, "reworded message");
        r.Success.Should().BeTrue(r.CombinedOutput);
        svc.Refresh();
        svc.GetCommits()[0].Summary.Should().Be("reworded message");
    }

    [Fact]
    public async Task Reword_older_commit_rewrites_via_rebase()
    {
        using var repo = TestRepository.CreateEmpty();
        var target = repo.Commit("original first", "a.txt", "1");
        repo.Commit("second", "b.txt", "2");

        using var svc = NewService();
        svc.Open(repo.Path);

        var r = await svc.RewordAsync(target, "new first message");
        r.Success.Should().BeTrue(r.CombinedOutput);
        svc.Refresh();
        var summaries = svc.GetCommits().Select(c => c.Summary).ToList();
        summaries.Should().Contain("new first message");
        summaries.Should().Contain("second");    // later commit preserved
        summaries.Should().NotContain("original first");
    }

    [Fact]
    public async Task Bisect_start_marks_bisecting_then_reset_clears_it()
    {
        using var repo = TestRepository.CreateEmpty();
        var good = repo.Commit("c1", "a.txt", "1");
        repo.Commit("c2", "a.txt", "12");
        repo.Commit("c3", "a.txt", "123");
        repo.Commit("c4", "a.txt", "1234");

        using var svc = NewService();
        svc.Open(repo.Path);
        svc.IsBisecting().Should().BeFalse();

        var start = await svc.BisectStartAsync(goodSha: good, badSha: "HEAD");
        start.Success.Should().BeTrue(start.CombinedOutput);
        svc.IsBisecting().Should().BeTrue();

        var reset = await svc.BisectResetAsync();
        reset.Success.Should().BeTrue(reset.CombinedOutput);
        svc.IsBisecting().Should().BeFalse();
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch { /* leftover temp dirs are harmless */ }
    }
}
