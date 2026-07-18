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

public sealed class CommandPaletteViewModelTests
{
    [Fact]
    public void Filters_items_by_the_search_text()
    {
        var items = new[]
        {
            new PaletteItem("Fetch", () => { }),
            new PaletteItem("Push", () => { }),
            new PaletteItem("Pull", () => { }),
        };
        var vm = new CommandPaletteViewModel(items);
        vm.Items.Should().HaveCount(3);

        vm.Search = "pu";
        vm.Items.Select(i => i.Title).Should().BeEquivalentTo("Push", "Pull");
        vm.Selected.Should().NotBeNull();
    }

    [Fact]
    public void Confirm_uses_the_selection_when_no_item_is_passed()
    {
        var target = new PaletteItem("Refresh", () => { });
        var vm = new CommandPaletteViewModel(new[] { target });
        vm.Confirm(null);
        vm.Chosen.Should().Be(target);
    }
}

public sealed class ContentSearchViewModelTests
{
    [Fact]
    public async Task Search_populates_results_from_the_repository()
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.SearchContentAsync("foo", false).Returns(Task.FromResult<IReadOnlyList<CommitInfo>>(new[]
        {
            new CommitInfo { Sha = "abc123def", ParentShas = System.Array.Empty<string>(),
                Summary = "add foo", MessageFull = "add foo", AuthorName = "A", AuthorEmail = "a@x",
                WhenUtc = System.DateTimeOffset.UtcNow }
        }));

        var vm = new ContentSearchViewModel(repo, new LocalizationService(), NullLogger.Instance) { Term = "foo" };
        await vm.SearchCommand.ExecuteAsync(null);

        vm.Results.Should().ContainSingle(r => r.Sha == "abc123def");
        vm.ShowEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Search_sets_ShowEmpty_when_there_are_no_matches()
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.SearchContentAsync(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromResult<IReadOnlyList<CommitInfo>>(System.Array.Empty<CommitInfo>()));

        var vm = new ContentSearchViewModel(repo, new LocalizationService(), NullLogger.Instance) { Term = "nope" };
        await vm.SearchCommand.ExecuteAsync(null);

        vm.Results.Should().BeEmpty();
        vm.ShowEmpty.Should().BeTrue();
    }
}

public sealed class CompareViewModelTests
{
    private static BranchInfo Branch(string name) =>
        new() { CanonicalName = "refs/heads/" + name, FriendlyName = name, IsRemote = false };

    [Fact]
    public void Loads_refs_and_changed_files_between_them()
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.GetBranches().Returns(new[] { Branch("main"), Branch("feature") });
        repo.GetTags().Returns(new[] { new TagInfo { Name = "v1.0", TargetSha = "abc" } });
        repo.GetChangesBetween("main", "feature").Returns(new[]
        {
            new FileChange { Path = "x.txt", Kind = FileChangeKind.Added }
        });

        var vm = new CompareViewModel(repo, new LocalizationService(), NullLogger.Instance, "main", "feature");

        vm.Refs.Should().Contain(new[] { "main", "feature", "v1.0" });
        vm.ChangedFiles.Should().ContainSingle(f => f.Path == "x.txt");
    }
}

public sealed class RemotesViewModelTests
{
    private static GitResult Ok() => new() { ExitCode = 0, StandardOutput = "", StandardError = "", CommandLine = "git" };

    [Fact]
    public async Task Add_prompts_then_adds_the_remote_and_marks_changed()
    {
        var repo = Substitute.For<IRepositoryService>();
        repo.GetRemotes().Returns(System.Array.Empty<RemoteInfo>());
        repo.AddRemoteAsync("origin", "https://x/y.git").Returns(Task.FromResult(Ok()));

        var dialogs = Substitute.For<IDialogService>();
        dialogs.Prompt(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
               .Returns("origin", "https://x/y.git");

        var vm = new RemotesViewModel(repo, dialogs, new LocalizationService(), NullLogger.Instance);
        await vm.AddCommand.ExecuteAsync(null);

        await repo.Received().AddRemoteAsync("origin", "https://x/y.git");
        vm.Changed.Should().BeTrue();
    }
}
