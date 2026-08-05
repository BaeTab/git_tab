using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
using GitTab.Core.Abstractions;
using GitTab.Core.Git;
using GitTab.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GitTab.App.Tests;

/// <summary>ViewModel-level tests using a mocked repository/dialog (NSubstitute).</summary>
public sealed class WorkingCopyViewModelTests
{
    private static WorkingCopyViewModel NewVm(
        IRepositoryService? repo = null,
        IDialogService? dialogs = null,
        ISettingsService? settings = null)
    {
        if (settings is null)
        {
            settings = Substitute.For<ISettingsService>();
            settings.Current.Returns(new AppSettings());
        }

        return new WorkingCopyViewModel(
            repo ?? Substitute.For<IRepositoryService>(),
            dialogs ?? Substitute.For<IDialogService>(),
            new LocalizationService(),
            settings,
            NullLogger<WorkingCopyViewModel>.Instance);
    }

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

    // ---- pre-commit check (Repo Doctor) ----

    private static DoctorFinding Leak() => new()
    {
        Path = ".env",
        Kind = DoctorFindingKind.ShouldBeIgnored,
        Detail = "environment file (usually holds secrets)"
    };

    private static IRepositoryService RepoWithFindings(params DoctorFinding[] findings)
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.InspectStagedChanges().Returns(findings);
        repo.CommitAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new GitResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
        return repo;
    }

    [Fact]
    public async Task Declining_the_pre_commit_warning_abandons_the_commit()
    {
        var repo = RepoWithFindings(Leak());
        var dialogs = Substitute.For<IDialogService>();
        dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var vm = NewVm(repo, dialogs);
        vm.CommitMessage = "msg";
        vm.Amend = true;

        await vm.CommitCommand.ExecuteAsync(null);

        await repo.DidNotReceive().CommitAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
        vm.CommitMessage.Should().Be("msg");   // the message survives so the user can fix and retry
    }

    [Fact]
    public async Task Accepting_the_pre_commit_warning_still_commits()
    {
        var repo = RepoWithFindings(Leak());
        var dialogs = Substitute.For<IDialogService>();
        dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var vm = NewVm(repo, dialogs);
        vm.CommitMessage = "msg";
        vm.Amend = true;

        await vm.CommitCommand.ExecuteAsync(null);

        await repo.Received(1).CommitAsync("msg", true, false, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clean_staging_area_commits_without_asking()
    {
        var repo = RepoWithFindings();   // nothing flagged
        var dialogs = Substitute.For<IDialogService>();

        var vm = NewVm(repo, dialogs);
        vm.CommitMessage = "msg";
        vm.Amend = true;

        await vm.CommitCommand.ExecuteAsync(null);

        dialogs.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<string>());
        await repo.Received(1).CommitAsync("msg", true, false, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_check_can_be_turned_off_in_settings()
    {
        var repo = RepoWithFindings(Leak());
        var dialogs = Substitute.For<IDialogService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { RepoDoctor = false });

        var vm = NewVm(repo, dialogs, settings);
        vm.CommitMessage = "msg";
        vm.Amend = true;

        await vm.CommitCommand.ExecuteAsync(null);

        repo.DidNotReceive().InspectStagedChanges();
        dialogs.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<string>());
        await repo.Received(1).CommitAsync("msg", true, false, false, Arg.Any<CancellationToken>());
    }
}
