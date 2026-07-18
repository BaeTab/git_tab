using GitTab.App.Services;
using GitTab.Core.Models;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class GitAuthTests
{
    private static GitResult Fail(string stderr) => new()
    {
        ExitCode = 128,
        StandardOutput = string.Empty,
        StandardError = stderr,
        CommandLine = "git push"
    };

    [Theory]
    [InlineData("fatal: Authentication failed for 'https://github.com/x/y.git/'")]
    [InlineData("remote: Support for password authentication was removed on August 13, 2021.")]
    [InlineData("fatal: could not read Username for 'https://github.com': terminal prompts disabled")]
    [InlineData("remote: HTTP Basic: Access denied")]
    [InlineData("error: 403 Forbidden")]
    [InlineData("Permission denied (publickey).")]
    public void IsAuthFailure_detects_auth_errors(string stderr)
        => GitAuth.IsAuthFailure(Fail(stderr)).Should().BeTrue();

    [Theory]
    [InlineData("error: failed to push some refs (non-fast-forward)")]
    [InlineData("fatal: unable to access 'https://...': Could not resolve host")]
    [InlineData("Everything up-to-date")]
    [InlineData("")]
    public void IsAuthFailure_ignores_non_auth_errors(string stderr)
        => GitAuth.IsAuthFailure(Fail(stderr)).Should().BeFalse();
}
