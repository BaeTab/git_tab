using GitTab.Core.Gitignore;
using FluentAssertions;
using Xunit;

namespace GitTab.Core.Tests;

public sealed class GitignoreServiceTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gittab-ign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Detects_dotnet_stack_from_csproj_and_always_suggests_global()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "Thing.csproj"), "<Project/>");
            var svc = new GitignoreService();
            var detected = svc.Detect(dir);
            detected.Should().Contain("dotnet");
            detected.Should().Contain("global");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Detects_node_stack_from_package_json()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "package.json"), "{}");
            new GitignoreService().Detect(dir).Should().Contain("node");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Build_concatenates_selected_templates()
    {
        var svc = new GitignoreService();
        var content = svc.Build(new[] { "dotnet", "node" });
        content.Should().Contain("bin/");
        content.Should().Contain("node_modules/");
        content.Should().Contain(".NET / Visual Studio");
    }

    [Fact]
    public void Write_creates_then_appends_gitignore()
    {
        var dir = NewTempDir();
        try
        {
            var svc = new GitignoreService();
            svc.Write(dir, "first-block\n", append: false);
            var path = Path.Combine(dir, ".gitignore");
            File.Exists(path).Should().BeTrue();

            svc.Write(dir, "second-block\n", append: true);
            var text = File.ReadAllText(path);
            text.Should().Contain("first-block");
            text.Should().Contain("second-block");
        }
        finally { Directory.Delete(dir, true); }
    }
}
