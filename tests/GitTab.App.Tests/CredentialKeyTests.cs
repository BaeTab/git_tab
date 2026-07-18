using GitTab.App.Services;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class CredentialKeyTests
{
    [Theory]
    [InlineData("https://github.com/user/repo.git", "GitTab:https://github.com")]
    [InlineData("https://github.com/user/repo", "GitTab:https://github.com")]
    [InlineData("https://token@github.com/user/repo.git", "GitTab:https://github.com")]
    [InlineData("https://gitlab.com/group/proj.git", "GitTab:https://gitlab.com")]
    [InlineData("http://internal.example.com/x.git", "GitTab:http://internal.example.com")]
    public void FromUrl_keys_by_scheme_and_host(string url, string expected)
        => CredentialKey.FromUrl(url).Should().Be(expected);

    [Theory]
    [InlineData("git@github.com:user/repo.git")] // ssh
    [InlineData("ssh://git@github.com/user/repo.git")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not a url")]
    public void FromUrl_returns_null_for_non_http(string? url)
        => CredentialKey.FromUrl(url).Should().BeNull();

    [Fact]
    public void HostLabel_extracts_host()
    {
        CredentialKey.HostLabel("https://github.com/user/repo.git").Should().Be("github.com");
        CredentialKey.HostLabel(null).Should().Be("remote");
    }
}
