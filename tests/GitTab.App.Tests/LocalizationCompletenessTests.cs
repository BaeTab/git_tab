using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using GitTab.App.Localization;
using FluentAssertions;
using Xunit;

namespace GitTab.App.Tests;

public sealed class LocalizationCompletenessTests
{
    private static Dictionary<string, string> Table(string name)
    {
        var field = typeof(LocalizationService).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        return (Dictionary<string, string>)field!.GetValue(null)!;
    }

    [Fact]
    public void Korean_and_English_tables_have_the_same_keys()
    {
        var ko = Table("Ko");
        var en = Table("En");

        ko.Keys.Except(en.Keys).Should().BeEmpty("every Korean key must have an English translation");
        en.Keys.Except(ko.Keys).Should().BeEmpty("every English key must have a Korean translation");
    }

    [Fact]
    public void Every_loc_Tr_key_used_in_XAML_is_defined()
    {
        var appDir = FindAppSourceDir();
        if (appDir is null) return; // source tree not available (e.g. packaged run) — nothing to scan

        var ko = Table("Ko");
        var pattern = new Regex(@"loc:Tr\s+([A-Za-z0-9_.]+)", RegexOptions.Compiled);
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var xaml in Directory.EnumerateFiles(appDir, "*.xaml", SearchOption.AllDirectories))
            foreach (Match m in pattern.Matches(File.ReadAllText(xaml)))
                if (!ko.ContainsKey(m.Groups[1].Value))
                    missing.Add(m.Groups[1].Value);

        missing.Should().BeEmpty("every {loc:Tr Key} used in XAML must be defined in the localization tables");
    }

    /// <summary>Walk up from the test binary to the repo root (GitTab.sln) and return src/GitTab.App.</summary>
    private static string? FindAppSourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var app = Path.Combine(dir.FullName, "src", "GitTab.App");
            if (File.Exists(Path.Combine(dir.FullName, "GitTab.sln")) && Directory.Exists(app))
                return app;
        }
        return null;
    }
}
