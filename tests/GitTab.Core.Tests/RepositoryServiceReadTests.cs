using GitTab.Core.Git;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GitTab.Core.Tests;

public sealed class RepositoryServiceReadTests
{
    private static RepositoryService NewService()
        => new(new GitCommandRunner(NullLogger<GitCommandRunner>.Instance), NullLogger<RepositoryService>.Instance);

    [Fact]
    public void Reads_linear_history_in_newest_first_order()
    {
        using var repo = TestRepository.CreateEmpty();
        var c1 = repo.Commit("first", "a.txt", "1");
        var c2 = repo.Commit("second", "a.txt", "12");
        var c3 = repo.Commit("third", "a.txt", "123");

        using var svc = NewService();
        svc.Open(repo.Path);
        var commits = svc.GetCommits();

        commits.Should().HaveCount(3);
        commits[0].Sha.Should().Be(c3);
        commits[0].Summary.Should().Be("third");
        commits[2].Sha.Should().Be(c1);
        commits[0].ParentShas.Should().ContainSingle().Which.Should().Be(c2);
        commits[2].ParentShas.Should().BeEmpty(); // root
        commits[0].AuthorName.Should().Be("GitTab Test");
    }

    [Fact]
    public void Reads_branches_including_current()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        repo.CreateBranch("feature", checkout: true);
        repo.Commit("feature work", "b.txt", "x");

        using var svc = NewService();
        svc.Open(repo.Path);
        var branches = svc.GetBranches();

        branches.Select(b => b.FriendlyName).Should().Contain(new[] { "feature" });
        branches.Should().Contain(b => b.IsCurrent && b.FriendlyName == "feature");

        var head = svc.GetHead();
        head.IsDetached.Should().BeFalse();
        head.BranchFriendlyName.Should().Be("feature");
    }

    [Fact]
    public void Reads_merge_commit_with_two_parents()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "a.txt", "1");
        var mainBranch = repo.CurrentBranch; // "main" or "master" depending on git config
        repo.CreateBranch("feature", checkout: true);
        repo.Commit("feature", "b.txt", "x");
        repo.Checkout(mainBranch);
        repo.Commit("main work", "c.txt", "y");
        var mergeSha = repo.Merge("feature", "merge feature");

        using var svc = NewService();
        svc.Open(repo.Path);
        var commits = svc.GetCommits();

        var merge = commits.First(c => c.Sha == mergeSha);
        merge.ParentShas.Should().HaveCount(2);
        merge.IsMerge.Should().BeTrue();
    }

    [Fact]
    public void Reads_working_tree_status_staged_and_unstaged()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        repo.WriteFile("a.txt", "1-modified");   // unstaged modify
        repo.WriteFile("new.txt", "brand new");  // untracked

        using var svc = NewService();
        svc.Open(repo.Path);
        var status = svc.GetStatus();

        status.Unstaged.Should().Contain(c => c.Path == "a.txt" && c.Kind == FileChangeKind.Modified);
        status.Unstaged.Should().Contain(c => c.Path == "new.txt" && c.Kind == FileChangeKind.Untracked);
        status.IsClean.Should().BeFalse();
    }

    [Fact]
    public void Reads_commit_file_diff_with_added_lines()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "a.txt", "line1\n");
        var c2 = repo.Commit("second", "a.txt", "line1\nline2\n");

        using var svc = NewService();
        svc.Open(repo.Path);

        var changes = svc.GetCommitChanges(c2);
        changes.Should().ContainSingle(c => c.Path == "a.txt");

        var diff = svc.GetCommitFileDiff(c2, "a.txt");
        diff.IsBinary.Should().BeFalse();
        diff.Hunks.Should().NotBeEmpty();
        diff.AddedLines.Should().BeGreaterThan(0);
        diff.Hunks.SelectMany(h => h.Lines).Should().Contain(l => l.Kind == DiffLineKind.Added && l.Text.Contains("line2"));
    }

    [Fact]
    public void Discover_returns_working_dir_for_path_inside_repo()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("first", "sub/a.txt", "1");

        using var svc = NewService();
        var discovered = svc.Discover(Path.Combine(repo.Path, "sub"));
        discovered.Should().NotBeNull();
        // Windows paths are case-insensitive; TEMP env casing can differ from GetTempPath().
        Path.GetFullPath(discovered!).TrimEnd(Path.DirectorySeparatorChar)
            .Should().BeEquivalentTo(Path.GetFullPath(repo.Path).TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetFileHistory_returns_only_commits_touching_the_file()
    {
        using var repo = TestRepository.CreateEmpty();
        var a1 = repo.Commit("a1", "a.txt", "1");
        var b1 = repo.Commit("b1", "b.txt", "x");   // does not touch a.txt
        var a2 = repo.Commit("a2", "a.txt", "12");

        using var svc = NewService();
        svc.Open(repo.Path);

        var shas = svc.GetFileHistory("a.txt").Select(c => c.Sha).ToList();
        shas.Should().Contain(new[] { a1, a2 });
        shas.Should().NotContain(b1);   // a commit that didn't touch the file is excluded
    }

    [Fact]
    public void GetChangesBetween_lists_files_differing_between_refs()
    {
        using var repo = TestRepository.CreateEmpty();
        repo.Commit("base", "a.txt", "1");
        var main = repo.CurrentBranch;
        repo.CreateBranch("feature", checkout: true);
        repo.Commit("add b", "b.txt", "x");

        using var svc = NewService();
        svc.Open(repo.Path);

        var changes = svc.GetChangesBetween(main, "feature");
        changes.Should().Contain(c => c.Path == "b.txt" && c.Kind == FileChangeKind.Added);
    }
}
