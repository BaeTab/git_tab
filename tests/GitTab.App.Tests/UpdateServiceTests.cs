using System.IO;
using System.Security.Cryptography;
using GitTab.App.Services;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class UpdateServiceTests
{
    // Regression: the updater wrote the installer with File.Create (FileShare.None) and then hashed
    // it WHILE the write stream was still open, which threw an IOException and broke every real
    // update. WriteStreamToFileAsync must close the file so VerifyChecksumAsync can read it.
    [Fact]
    public async Task Written_file_can_be_checksum_verified_without_a_lock()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var expectedHex = Convert.ToHexString(SHA256.HashData(payload));
        var path = Path.Combine(Path.GetTempPath(), "gittab-updtest-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await using (var src = new MemoryStream(payload))
                await GitHubUpdateService.WriteStreamToFileAsync(src, path, payload.Length, null, default);

            // Must not throw (file is closed) and must match the published checksum.
            (await GitHubUpdateService.VerifyChecksumAsync(path, $"{expectedHex}  installer.exe")).Should().BeTrue();
            (await GitHubUpdateService.VerifyChecksumAsync(path, "deadbeef  installer.exe")).Should().BeFalse();
        }
        finally { try { File.Delete(path); } catch { /* best effort */ } }
    }


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
