using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the changelog generator: builds a Conventional-Commits-grouped markdown
/// summary of the commits since a chosen tag (or the whole history).</summary>
public sealed partial class ChangelogViewModel : ObservableObject
{
    private static readonly Regex ConventionalCommitRegex =
        new(@"^(?<type>\w+)(\([^)]*\))?!?:\s*(?<desc>.+)$", RegexOptions.Compiled);

    // Canonical section key -> heading, in the order they should appear in the output.
    private static readonly (string Key, string Heading)[] SectionOrder =
    {
        ("feat", "✨ Features"),
        ("fix", "🐛 Fixes"),
        ("perf", "⚡ Performance"),
        ("refactor", "♻️ Refactoring"),
        ("docs", "📝 Docs"),
        ("test", "✅ Tests"),
        ("build/ci", "🛠️ Build/CI"),
        ("chore", "🧹 Chores"),
        ("other", "Other"),
    };

    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly string _allHistoryLabel;

    public ChangelogViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _allHistoryLabel = Loc.T("Changelog.AllHistory");

        FillFromRefs();
        GenerateInternal();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<string> FromRefs { get; } = new();

    [ObservableProperty]
    private string? _selectedFromRef;

    [ObservableProperty]
    private string _output = "";

    private void FillFromRefs()
    {
        FromRefs.Add(_allHistoryLabel);
        try
        {
            foreach (var tag in _repo.GetTags()) FromRefs.Add(tag.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read tags");
        }

        SelectedFromRef = FromRefs.Count > 1 ? FromRefs[1] : _allHistoryLabel;
    }

    [RelayCommand]
    private void Generate() => GenerateInternal();

    private void GenerateInternal()
    {
        try
        {
            var tags = _repo.GetTags();
            var tag = SelectedFromRef is not null ? tags.FirstOrDefault(t => t.Name == SelectedFromRef) : null;

            var commits = tag is not null
                ? _repo.GetCommitsBetween(tag.TargetSha, _repo.GetHead().TipSha ?? "HEAD")
                : _repo.GetCommits();

            Output = BuildMarkdown(commits, tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate changelog");
            _dialogs.Error(ex.Message, Loc.T("Changelog.Title"));
        }
    }

    private static string BuildMarkdown(IReadOnlyList<CommitInfo> commits, TagInfo? fromTag)
    {
        var groups = SectionOrder.ToDictionary(s => s.Key, _ => new List<(string Desc, string ShortSha)>());
        foreach (var c in commits)
        {
            var match = ConventionalCommitRegex.Match(c.Summary.Trim());
            var key = match.Success ? CanonicalType(match.Groups["type"].Value) : "other";
            var desc = match.Success ? match.Groups["desc"].Value : c.Summary;
            groups[key].Add((desc, c.ShortSha));
        }

        var sb = new StringBuilder();
        sb.AppendLine(fromTag is not null ? $"## {fromTag.Name} → HEAD" : "# Changelog");

        foreach (var (key, heading) in SectionOrder)
        {
            var entries = groups[key];
            if (entries.Count == 0) continue;

            sb.AppendLine();
            sb.AppendLine($"### {heading}");
            foreach (var (desc, shortSha) in entries)
                sb.AppendLine($"- {desc} ({shortSha})");
        }

        return sb.ToString().TrimEnd();
    }

    private static string CanonicalType(string type) => type.ToLowerInvariant() switch
    {
        "feat" => "feat",
        "fix" => "fix",
        "perf" => "perf",
        "refactor" => "refactor",
        "docs" => "docs",
        "test" => "test",
        "build" or "ci" => "build/ci",
        "chore" => "chore",
        _ => "other",
    };

    [RelayCommand]
    private void Copy()
    {
        try { System.Windows.Clipboard.SetText(Output); }
        catch (Exception ex) { _logger.LogDebug(ex, "Clipboard set failed"); }
    }

    [RelayCommand]
    private void Save()
    {
        var path = _dialogs.SaveFile(Loc.T("Changelog.SaveTitle"), "CHANGELOG.md", "md");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            System.IO.File.WriteAllText(path, Output);
            _dialogs.Info(path, Loc.T("Changelog.SaveTitle"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save changelog");
            _dialogs.Error(ex.Message, Loc.T("Changelog.SaveTitle"));
        }
    }
}
