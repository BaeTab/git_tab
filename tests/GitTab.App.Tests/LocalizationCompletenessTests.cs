using System.Reflection;
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
}
