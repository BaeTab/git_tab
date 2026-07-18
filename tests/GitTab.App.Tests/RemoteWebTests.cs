using GitTab.App.Services;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class RemoteWebTests
{
    [Theory]
    [InlineData("https://github.com/BaeTab/git_tab.git", "github.com", "BaeTab", "git_tab")]
    [InlineData("https://github.com/BaeTab/git_tab", "github.com", "BaeTab", "git_tab")]
    [InlineData("git@github.com:BaeTab/git_tab.git", "github.com", "BaeTab", "git_tab")]
    [InlineData("https://token@github.com/BaeTab/git_tab.git", "github.com", "BaeTab", "git_tab")]
    [InlineData("https://gitlab.com/group/sub/proj.git", "gitlab.com", "group", "proj")]
    public void Parse_extracts_host_owner_repo(string url, string host, string owner, string repo)
    {
        var p = RemoteWeb.Parse(url);
        p.Should().NotBeNull();
        p!.Value.Host.Should().Be(host);
        p.Value.Owner.Should().Be(owner);
        p.Value.Repo.Should().Be(repo);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-a-url")]
    [InlineData("https://github.com/onlyowner")]
    public void Parse_returns_null_for_bad_input(string? url)
        => RemoteWeb.Parse(url).Should().BeNull();

    [Fact]
    public void PullRequestUrl_github_uses_compare()
        => RemoteWeb.PullRequestUrl("https://github.com/BaeTab/git_tab.git", "feature/x")
            .Should().Be("https://github.com/BaeTab/git_tab/compare/feature%2Fx?expand=1");

    [Fact]
    public void PullRequestUrl_gitlab_uses_merge_requests_new()
        => RemoteWeb.PullRequestUrl("https://gitlab.com/g/p.git", "feat")
            .Should().Be("https://gitlab.com/g/p/-/merge_requests/new?merge_request%5Bsource_branch%5D=feat");

    [Fact]
    public void PullRequestUrl_null_for_unknown_host_or_missing_branch()
    {
        RemoteWeb.PullRequestUrl("https://bitbucket.org/a/b.git", "x").Should().BeNull();
        RemoteWeb.PullRequestUrl("https://github.com/a/b.git", "").Should().BeNull();
    }

    [Fact]
    public void RepoUrl_normalizes_to_https_home()
        => RemoteWeb.RepoUrl("git@github.com:BaeTab/git_tab.git").Should().Be("https://github.com/BaeTab/git_tab");
}
