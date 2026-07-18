using GitTab.App.Services;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("ABCD1234  GitTab-Setup-1.0.0.exe", "abcd1234")]   // sha256sum format, case-insensitive
    [InlineData("abcd1234", "ABCD1234")]                            // bare hash
    [InlineData("abcd1234 *GitTab-Setup.exe", "abcd1234")]          // binary-mode "*filename"
    public void HashesMatch_true_when_first_token_matches(string checksumFile, string actual)
        => GitHubUpdateService.HashesMatch(checksumFile, actual).Should().BeTrue();

    [Theory]
    [InlineData("abcd1234  file", "ffffffff")]
    [InlineData(null, "abcd")]
    [InlineData("abcd", null)]
    [InlineData("", "abcd")]
    public void HashesMatch_false_on_mismatch_or_missing(string? checksumFile, string? actual)
        => GitHubUpdateService.HashesMatch(checksumFile, actual).Should().BeFalse();
}
