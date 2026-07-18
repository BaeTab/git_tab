using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GitTab.App.Tests;

/// <summary>ViewModel-level tests using a mocked repository/dialog (NSubstitute).</summary>
public sealed class WorkingCopyViewModelTests
{
    private static WorkingCopyViewModel NewVm(IRepositoryService? repo = null)
        => new(repo ?? Substitute.For<IRepositoryService>(),
               Substitute.For<IDialogService>(),
               new LocalizationService(),
               NullLogger<WorkingCopyViewModel>.Instance);

    [Fact]
    public void Selecting_commit_type_prefixes_the_message()
    {
        var vm = NewVm();
        vm.CommitMessage = "add login";
        vm.SelectedCommitType = "feat";
        vm.CommitMessage.Should().Be("feat: add login");
    }

    [Fact]
    public void Selecting_a_new_type_replaces_the_existing_prefix()
    {
        var vm = NewVm();
        vm.CommitMessage = "feat: add login";
        vm.SelectedCommitType = "fix";
        vm.CommitMessage.Should().Be("fix: add login");
    }

    [Fact]
    public void CanCommit_requires_a_message_and_staged_or_amend()
    {
        var vm = NewVm();
        vm.CanCommit.Should().BeFalse();     // no message, nothing staged

        vm.CommitMessage = "msg";
        vm.CanCommit.Should().BeFalse();     // message but nothing staged and not amending

        vm.Amend = true;
        vm.CanCommit.Should().BeTrue();      // amend permits a commit with no staged files
    }

    [Fact]
    public void Refresh_populates_staged_and_unstaged_from_status()
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.GetStatus().Returns(new WorkingTreeStatus
        {
            Staged = new[] { new FileChange { Path = "a.txt", Kind = FileChangeKind.Modified, IsStaged = true } },
            Unstaged = new[] { new FileChange { Path = "b.txt", Kind = FileChangeKind.Untracked, IsStaged = false } },
        });

        var vm = NewVm(repo);
        vm.Refresh();

        vm.Staged.Should().ContainSingle(f => f.Path == "a.txt");
        vm.Unstaged.Should().ContainSingle(f => f.Path == "b.txt");
        vm.IsClean.Should().BeFalse();
    }
}
