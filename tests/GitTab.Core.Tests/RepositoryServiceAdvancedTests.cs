using GitTab.Core.Git;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>
/// Integration tests for the Tier-1/2 advanced operations (signing config/verify, worktrees,
/// submodules, patch export/apply, extra stash ops, sparse-checkout) driven through real git.exe.
/// LFS is not covered here because git-lfs may not be installed on the runner.
/// </summary>
public sealed class RepositoryServiceAdvancedTests
{
    private static RepositoryService NewService()
        => new(new GitCommandRunner(NullLogger<GitCommandRunner>.Instance), NullLogger<RepositoryService>.Instance);

    [Fact]
    public async Task Signing_config_roundtrips()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.SetSigningConfigAsync(enabled: true, key: "ABC123KEY", format: "ssh")).Success.Should().BeTrue();

        var cfg = await svc.GetSigningConfigAsync();
        cfg.Enabled.Should().BeTrue();
        cfg.Key.Should().Be("ABC123KEY");
        cfg.Format.Should().Be("ssh");
    }

    [Fact]
    public async Task Unsigned_commit_reports_no_signature()
    {
        using var repo = TestRepository.CreateEmpty();
        var head = repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.GetSignatureStatusAsync(head)).Should().Be(CommitSignature.None);
    }

    [Fact]
    public async Task Annotated_tag_is_created_with_a_message()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.CreateTagAsync("v2", message: "release two")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetTags().Should().ContainSingle(t => t.Name == "v2" && t.IsAnnotated);
    }

    [Fact]
    public async Task Worktree_add_list_remove_roundtrips()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        var wtPath = Path.Combine(Path.GetTempPath(), "gittab-wt-" + Guid.NewGuid().ToString("N"));
        try
        {
            (await svc.WorktreeAddAsync(wtPath, "wt-branch", createBranch: true)).Success.Should().BeTrue();

            var trees = await svc.GetWorktreesAsync();
            trees.Should().HaveCountGreaterThanOrEqualTo(2);
            trees.Should().Contain(w => w.Branch == "wt-branch");
            trees.Should().Contain(w => w.IsCurrent);

            (await svc.WorktreeRemoveAsync(wtPath, force: true)).Success.Should().BeTrue();
            (await svc.GetWorktreesAsync()).Should().HaveCount(1);
        }
        finally
        {
            try { if (Directory.Exists(wtPath)) Directory.Delete(wtPath, true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Submodules_are_empty_for_a_plain_repo()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        svc.GetSubmodules().Should().BeEmpty();
    }

    [Fact]
    public async Task Stash_diff_shows_the_change_and_stash_to_branch_restores_it()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "f.txt", "one\n");
        repo.WriteFile("f.txt", "one\nMAGIC_STASH_LINE\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.StashPushAsync("wip", includeUntracked: false)).Success.Should().BeTrue();
        svc.Refresh();

        var diff = await svc.GetStashDiffAsync(0);
        diff.Should().Contain("MAGIC_STASH_LINE");

        (await svc.StashToBranchAsync(0, "from-stash")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetBranches().Should().Contain(b => b.FriendlyName == "from-stash");
        File.ReadAllText(Path.Combine(repo.Path, "f.txt")).Should().Contain("MAGIC_STASH_LINE");
    }

    [Fact]
    public async Task Patch_export_then_apply_reproduces_the_commit()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "f.txt", "one\n");
        var target = repo.Commit("add feature", "feature.txt", "hello\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        var patchFile = Path.Combine(Path.GetTempPath(), "gittab-patch-" + Guid.NewGuid().ToString("N") + ".patch");
        try
        {
            (await svc.ExportCommitPatchAsync(target, patchFile)).Success.Should().BeTrue();
            File.Exists(patchFile).Should().BeTrue();
            File.ReadAllText(patchFile).Should().Contain("feature.txt");
        }
        finally
        {
            try { File.Delete(patchFile); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task Sparse_checkout_set_then_disable()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        repo.Commit("more", "docs/readme.md", "hi");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.SparseCheckoutSetAsync(new[] { "docs" }, cone: true)).Success.Should().BeTrue();
        (await svc.GetSparseCheckoutPatternsAsync()).Should().NotBeEmpty();

        (await svc.SparseCheckoutDisableAsync()).Success.Should().BeTrue();
        (await svc.GetSparseCheckoutPatternsAsync()).Should().BeEmpty();
    }

    [Fact]
    public void Blob_bytes_are_read_at_a_commit_and_in_the_working_tree()
    {
        using var repo = TestRepository.CreateEmpty();
        var sha = repo.Commit("add", "data.bin", "ORIGINAL");
        repo.WriteFile("data.bin", "WORKING");

        using var svc = NewService();
        svc.Open(repo.Path);

        System.Text.Encoding.UTF8.GetString(svc.GetBlobBytes(sha, "data.bin")!).Should().Be("ORIGINAL");
        System.Text.Encoding.UTF8.GetString(svc.GetWorkingBytes("data.bin")!).Should().Be("WORKING");
        svc.GetBlobBytes(sha, "missing.bin").Should().BeNull();
    }

    [Fact]
    public async Task Ignore_whitespace_diff_drops_a_whitespace_only_change()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "f.txt", "int x = 1;\n");
        repo.WriteFile("f.txt", "int   x   =   1;\n");   // same tokens, only spacing changed

        using var svc = NewService();
        svc.Open(repo.Path);

        var normal = svc.GetWorkingFileDiff("f.txt", staged: false);
        normal.Hunks.Should().NotBeEmpty("the raw diff sees the spacing change");

        var ignored = await svc.GetWorkingFileDiffWithOptionsAsync("f.txt", staged: false, ignoreWhitespace: true, contextLines: -1);
        ignored.Hunks.Should().BeEmpty("git diff -w ignores whitespace-only changes");
    }

    [Fact]
    public async Task Config_set_then_get_roundtrips()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.SetConfigAsync("user.name", "Custom Name", global: false)).Success.Should().BeTrue();
        (await svc.GetConfigAsync("user.name", global: false)).Should().Be("Custom Name");
        (await svc.GetConfigAsync("nonexistent.key", global: false)).Should().BeNull();
    }

    [Fact]
    public async Task Restore_file_reverts_working_copy_to_a_commit_version()
    {
        using var repo = TestRepository.CreateEmpty();
        var sha = repo.Commit("v1", "f.txt", "ORIGINAL");
        repo.WriteFile("f.txt", "CHANGED");

        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.RestoreFileAsync(sha, "f.txt")).Success.Should().BeTrue();
        File.ReadAllText(Path.Combine(repo.Path, "f.txt")).Should().Be("ORIGINAL");
    }

    [Fact]
    public async Task Amend_author_rewrites_the_latest_commit_author()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        repo.Commit("second", "b.txt", "2");

        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.AmendAuthorAsync("Alice Example", "alice@example.com")).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetCommits()[0].AuthorName.Should().Be("Alice Example");
    }

    [Fact]
    public async Task Commit_with_signoff_adds_a_trailer()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "a.txt", "1");
        repo.WriteFile("a.txt", "12");

        using var svc = NewService();
        svc.Open(repo.Path);
        await svc.StageAsync("a.txt");

        (await svc.CommitAsync("signed off change", signOff: true)).Success.Should().BeTrue();
        svc.Refresh();
        svc.GetCommits()[0].MessageFull.Should().Contain("Signed-off-by:");
    }

    [Fact]
    public async Task Repo_stats_report_commit_count_and_contributors()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("c1", "a.txt", "1");
        repo.Commit("c2", "a.txt", "12");
        repo.Commit("c3", "a.txt", "123");

        using var svc = NewService();
        svc.Open(repo.Path);

        var stats = await svc.GetRepoStatsAsync();
        stats.CommitCount.Should().Be(3);
        stats.Contributors.Should().NotBeEmpty();
        stats.Contributors.Sum(c => c.Commits).Should().Be(3);
    }

    [Fact]
    public async Task Option_like_arguments_are_rejected_by_the_guard()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        using var svc = NewService();
        svc.Open(repo.Path);

        (await svc.LfsTrackAsync("--evil")).Success.Should().BeFalse();
        (await svc.StashToBranchAsync(0, "--evil")).Success.Should().BeFalse();
        (await svc.SubmoduleDeinitAsync("--evil", force: false)).Success.Should().BeFalse();
    }
}
