using System.Text;
using FluentAssertions;
using GitTab.Core.Git;
using Xunit;

namespace GitTab.Core.Tests;

/// <summary>
/// Rules behind the pre-commit warning. The bar for a finding is "a real mistake that is expensive
/// to undo once it is in history"; the bar for staying silent is "ordinary source code", because a
/// warning the user learns to click through is worse than no warning at all.
/// </summary>
public sealed class RepoDoctorTests
{
    private static byte[] Text(string s) => Encoding.UTF8.GetBytes(s);

    // The vendor prefixes are split with `" + "` on purpose. Written as one literal, these fixtures
    // are indistinguishable from real credentials to a scanner reading the file — GitHub's push
    // protection rejected this very file for the GitLab one. Splitting keeps the compiled string
    // (what the rules see) intact while leaving nothing credential-shaped in the source text.
    private const string Aws = "AKIA" + "IOSFODNN7EXAMPLE";
    private const string GitHubPat = "ghp" + "_1234567890abcdefghijklmnopqrstuvwxyz";
    private const string GitLabPat = "glpat" + "-AAAAAAAAAABBBBBBBBBB";
    private const string GoogleKey = "AIza" + "SyD-1234567890abcdefghijklmnopqrstu";
    private const string SlackToken = "xoxb" + "-1234567890-abcdefghij";

    [Theory]
    [InlineData("const key = \"" + Aws + "\";", "AWS access key ID")]
    [InlineData("token: " + GitHubPat, "GitHub personal access token")]
    [InlineData("gitlab = " + GitLabPat, "GitLab personal access token")]
    [InlineData("-----BEGIN RSA PRIVATE KEY-----", "private key")]
    [InlineData("-----BEGIN OPENSSH PRIVATE KEY-----", "private key")]
    [InlineData("google = \"" + GoogleKey + "\"", "Google API key")]
    [InlineData("slack_hook = " + SlackToken, "Slack token")]
    public void Detects_credential_shaped_content(string line, string expectedDetail)
    {
        var findings = RepoDoctor.ScanForSecrets("config.cs", line);

        findings.Should().Contain(f => f.Kind == DoctorFindingKind.Secret && f.Detail == expectedDetail);
    }

    [Fact]
    public void Reports_the_line_the_secret_is_on()
    {
        var text = "line one\nline two\nkey = \"" + Aws + "\"\n";

        var finding = RepoDoctor.ScanForSecrets("a.cs", text).Single(f => f.Detail == "AWS access key ID");

        finding.Line.Should().Be(3);
    }

    [Theory]
    [InlineData("var passwordLabel = \"Password\";")]
    [InlineData("// TODO: read the api key from configuration")]
    [InlineData("public string Token { get; set; }")]
    [InlineData("password = GetFromVault()")]
    public void Leaves_ordinary_source_code_alone(string line)
    {
        RepoDoctor.ScanForSecrets("a.cs", line).Should().BeEmpty();
    }

    [Fact]
    public void Flags_a_hard_coded_credential_assignment()
    {
        var findings = RepoDoctor.ScanForSecrets("a.cs", "password = \"hunter2hunter2\"");

        findings.Should().ContainSingle(f => f.Detail == "hard-coded credential");
    }

    [Fact]
    public void Flags_blobs_over_the_size_threshold()
    {
        var findings = RepoDoctor.Inspect("assets/video.mp4", RepoDoctor.LargeFileBytes + 1, content: null);

        findings.Should().ContainSingle(f => f.Kind == DoctorFindingKind.LargeFile);
    }

    [Fact]
    public void Leaves_normal_sized_files_alone()
    {
        RepoDoctor.Inspect("src/a.cs", 4096, Text("var x = 1;"))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(".env", "environment file (usually holds secrets)")]
    [InlineData("config/.env", "environment file (usually holds secrets)")]
    [InlineData("certs/server.pem", "private key file")]
    [InlineData("keystore.properties", "signing credentials")]
    [InlineData("app/node_modules/left-pad/index.js", "dependency directory")]
    [InlineData(".ssh/id_rsa", "SSH private key")]
    public void Flags_paths_that_are_normally_not_committed(string path, string expectedDetail)
    {
        RepoDoctor.MatchPathRule(path).Should().Be(expectedDetail);
    }

    [Theory]
    [InlineData("src/Environment.cs")]
    [InlineData("src/node_modules.ts")]        // a source file, not the dependency directory
    [InlineData("docs/keystore-guide.md")]
    public void Leaves_ordinary_paths_alone(string path)
    {
        RepoDoctor.MatchPathRule(path).Should().BeNull();
    }

    [Fact]
    public void Does_not_scan_binary_content()
    {
        // The key pattern is present, but a NUL byte marks the blob binary — scanning it would only
        // produce noise from compiled output that happens to contain matching bytes.
        var binary = Text(Aws).Prepend((byte)0).ToArray();

        RepoDoctor.Inspect("bin/app.dll", binary.Length, binary)
            .Should().NotContain(f => f.Kind == DoctorFindingKind.Secret);
    }

    [Fact]
    public void Skips_content_scanning_for_files_over_the_scan_limit()
    {
        var findings = RepoDoctor.Inspect("huge.log", RepoDoctor.MaxScanBytes + 1, content: null);

        findings.Should().NotContain(f => f.Kind == DoctorFindingKind.Secret);
    }

    [Fact]
    public void Reports_size_and_path_problems_together()
    {
        var findings = RepoDoctor.Inspect("secrets/prod.pem", RepoDoctor.LargeFileBytes + 1, content: null);

        findings.Select(f => f.Kind).Should()
            .Contain(new[] { DoctorFindingKind.LargeFile, DoctorFindingKind.ShouldBeIgnored });
    }

    [Theory]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KB")]
    [InlineData(10L * 1024 * 1024, "10 MB")]
    public void Formats_sizes_readably(long bytes, string expected)
    {
        RepoDoctor.FormatSize(bytes).Should().Be(expected);
    }
}
