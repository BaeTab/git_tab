using System.IO;
using System.Linq;
using FluentAssertions;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
using GitTab.Core.Abstractions;
using GitTab.Core.Git;
using GitTab.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace GitTab.App.Tests;

/// <summary>
/// End-to-end integration tests for the P0.1 architecture: the app-shell <see cref="MainViewModel"/>
/// driving real <see cref="RepositorySessionViewModel"/> instances built by the real
/// <see cref="RepositorySessionFactory"/> over actual (temp) git repositories. These verify the
/// wiring that a headless UI screenshot can't: that each tab is a genuinely independent session, that
/// switching/closing tabs behaves, and that the shell's window-scoped pass-throughs track the active
/// session. Only read paths are exercised (LibGit2Sharp), so no git.exe is required.
/// </summary>
public sealed class MultiRepositorySessionTests
{
    private static MainViewModel BuildShell()
    {
        var runner = new GitCommandRunner(NullLogger<GitCommandRunner>.Instance);
        var discoverRepo = new RepositoryService(runner, NullLogger<RepositoryService>.Instance);
        var loc = new LocalizationService();
        var dialogs = Substitute.For<IDialogService>();
        var credentials = Substitute.For<ICredentialStore>();
        var hosting = Substitute.For<GitTab.App.Services.Hosting.IHostingClient>();
        var bookmarks = Substitute.For<IBookmarkStore>();
        bookmarks.Get(Arg.Any<string>()).Returns(System.Array.Empty<string>());

        var factory = new RepositorySessionFactory(
            runner, dialogs, loc, credentials, hosting, bookmarks, NullLoggerFactory.Instance);

        var recent = Substitute.For<IRecentRepositoriesStore>();
        recent.GetAll().Returns(System.Array.Empty<RecentRepository>());
        var theme = Substitute.For<IThemeService>();
        var updates = Substitute.For<IUpdateService>();
        var shell = Substitute.For<IShellIntegrationService>();
        var settings = Substitute.For<ISettingsService>();

        return new MainViewModel(
            discoverRepo, recent, dialogs, loc, theme, updates, shell,
            credentials, settings, factory, NullLogger<MainViewModel>.Instance);
    }

    [Fact]
    public async Task OpenPath_creates_a_session_with_live_data()
    {
        using var repo = TempRepo.CreateEmpty();
        repo.Commit("first", "a.txt", "1");
        repo.Commit("second", "a.txt", "2");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(repo.Path);

        vm.Sessions.Should().HaveCount(1);
        vm.IsRepositoryOpen.Should().BeTrue();
        vm.ActiveSession.Should().NotBeNull();
        vm.ActiveSession!.RepositoryName.Should().NotBeNullOrEmpty();
        vm.ActiveSession.Rows.Should().HaveCount(2);
        vm.ActiveSession.IsRepositoryOpen.Should().BeTrue();
        // Status-bar pass-through mirrors the active session.
        vm.RepositoryPath.Should().Be(vm.ActiveSession.RepositoryPath);
    }

    [Fact]
    public async Task Two_repositories_get_fully_independent_sessions()
    {
        using var r1 = TempRepo.CreateEmpty();
        r1.Commit("a", "f.txt", "1");
        r1.Commit("b", "f.txt", "2");
        r1.Commit("c", "f.txt", "3");
        using var r2 = TempRepo.CreateEmpty();
        r2.Commit("only", "f.txt", "1");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(r1.Path);
        var s1 = vm.ActiveSession!;
        await vm.OpenPathCommand.ExecuteAsync(r2.Path);
        var s2 = vm.ActiveSession!;

        vm.Sessions.Should().HaveCount(2);
        s2.Should().NotBeSameAs(s1);
        s1.Rows.Should().HaveCount(3);
        s2.Rows.Should().HaveCount(1);
        vm.ActiveSession.Should().BeSameAs(s2);

        // Filtering one session must not touch the other.
        s1.SearchText = "no-such-commit-zzz";
        s1.Rows.Should().BeEmpty();
        s2.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task Reopening_an_open_repository_focuses_its_existing_tab()
    {
        using var r1 = TempRepo.CreateEmpty();
        r1.Commit("a", "f.txt", "1");
        using var r2 = TempRepo.CreateEmpty();
        r2.Commit("b", "f.txt", "1");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(r1.Path);
        var s1 = vm.ActiveSession!;
        await vm.OpenPathCommand.ExecuteAsync(r2.Path);

        // Re-opening r1 should not create a third session — just re-activate the existing one.
        await vm.OpenPathCommand.ExecuteAsync(r1.Path);

        vm.Sessions.Should().HaveCount(2);
        vm.ActiveSession.Should().BeSameAs(s1);
    }

    [Fact]
    public async Task Switching_active_session_moves_the_shell_passthroughs()
    {
        using var r1 = TempRepo.CreateEmpty();
        r1.Commit("a", "f.txt", "1");
        using var r2 = TempRepo.CreateEmpty();
        r2.Commit("b", "f.txt", "1");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(r1.Path);
        var s1 = vm.ActiveSession!;
        await vm.OpenPathCommand.ExecuteAsync(r2.Path);
        var s2 = vm.ActiveSession!;

        vm.RepositoryPath.Should().Be(s2.RepositoryPath);
        vm.StashPushCommand.Should().BeSameAs(s2.StashPushCommand);

        vm.ActiveSession = s1;
        vm.RepositoryPath.Should().Be(s1.RepositoryPath);
        vm.StashPushCommand.Should().BeSameAs(s1.StashPushCommand);
    }

    [Fact]
    public async Task CloseTab_removes_the_session_and_falls_back_to_a_neighbour()
    {
        using var r1 = TempRepo.CreateEmpty();
        r1.Commit("a", "f.txt", "1");
        using var r2 = TempRepo.CreateEmpty();
        r2.Commit("b", "f.txt", "1");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(r1.Path);
        var s1 = vm.Sessions[0];
        await vm.OpenPathCommand.ExecuteAsync(r2.Path);
        var s2 = vm.Sessions[1];

        vm.CloseTabCommand.Execute(s2);
        vm.Sessions.Should().ContainSingle().Which.Should().BeSameAs(s1);
        vm.ActiveSession.Should().BeSameAs(s1);

        // Closing the last tab returns to the welcome (no-session) state.
        vm.CloseTabCommand.Execute(s1);
        vm.Sessions.Should().BeEmpty();
        vm.ActiveSession.Should().BeNull();
        vm.IsRepositoryOpen.Should().BeFalse();
        vm.StashPushCommand.Should().BeNull();   // pass-through is null with no active session
        vm.RepositoryPath.Should().BeNull();
    }

    [Fact]
    public async Task Opening_selects_the_newest_commit_and_wires_its_details()
    {
        using var repo = TempRepo.CreateEmpty();
        repo.Commit("older", "f.txt", "1");
        var newest = repo.Commit("newest", "f.txt", "2");

        var vm = BuildShell();
        await vm.OpenPathCommand.ExecuteAsync(repo.Path);
        var s = vm.ActiveSession!;

        s.SelectedCommitIndex.Should().Be(0);
        s.SelectedCommit.Should().NotBeNull();
        s.SelectedCommit!.Sha.Should().Be(newest);   // graph is newest-first
    }
}
