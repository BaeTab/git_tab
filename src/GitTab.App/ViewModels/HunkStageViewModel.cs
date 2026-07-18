using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// Backs the partial-staging dialog: shows the individual hunks of a file's unstaged diff and stages
/// them one at a time (the GUI equivalent of <c>git add -p</c>) by applying a per-hunk patch to the
/// index with <c>git apply --cached</c>.
/// </summary>
public sealed partial class HunkStageViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private readonly string _path;
    private string _header = string.Empty;

    public HunkStageViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc,
        ILogger logger, string path)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _path = path;
        FilePath = path;
    }

    public ILocalizationService Loc { get; }
    public string FilePath { get; }
    public ObservableCollection<HunkItem> Hunks { get; } = new();
    public bool IsEmpty => Hunks.Count == 0;

    /// <summary>True if any hunk was staged, so the host can refresh.</summary>
    public bool Changed { get; private set; }

    public async Task LoadAsync()
    {
        Hunks.Clear();
        try
        {
            var r = await _repo.RunRawAsync(new[] { "diff", "--", _path });
            if (r.Success) Parse(r.StandardOutput);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read diff for partial staging"); }
        OnPropertyChanged(nameof(IsEmpty));
    }

    // A unified diff is: file header (up to the first "@@") followed by one or more @@ hunks.
    private void Parse(string patch)
    {
        var lines = patch.Replace("\r\n", "\n").Split('\n');
        int firstHunk = Array.FindIndex(lines, l => l.StartsWith("@@", StringComparison.Ordinal));
        if (firstHunk < 0) return;

        _header = string.Join("\n", lines.Take(firstHunk));
        List<string>? current = null;
        void Flush()
        {
            if (current is { Count: > 0 })
            {
                var block = string.Join("\n", current).TrimEnd('\n');
                Hunks.Add(new HunkItem
                {
                    Index = Hunks.Count + 1,
                    HunkHeader = current[0],
                    Text = block,
                    Patch = _header + "\n" + block + "\n"
                });
            }
        }
        for (int i = firstHunk; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("@@", StringComparison.Ordinal)) { Flush(); current = new List<string>(); }
            current?.Add(lines[i]);
        }
        Flush();
    }

    [RelayCommand]
    private async Task Stage(HunkItem? hunk)
    {
        if (hunk is null) return;
        var tmp = Path.Combine(Path.GetTempPath(), "gittab-hunk-" + Guid.NewGuid().ToString("N") + ".patch");
        try
        {
            File.WriteAllText(tmp, hunk.Patch, new UTF8Encoding(false));
            var r = await _repo.RunRawAsync(new[] { "apply", "--cached", "--recount", tmp });
            if (r.Success)
            {
                Changed = true;
                await LoadAsync();   // re-read so the remaining hunks reflect the new index state
            }
            else
            {
                var body = string.IsNullOrWhiteSpace(r.CombinedOutput) ? "git apply failed" : r.CombinedOutput;
                _dialogs.Error(body, Loc.T("Error.GitFailed"));
            }
        }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); }
        finally { try { File.Delete(tmp); } catch { /* temp cleanup best effort */ } }
    }
}

/// <summary>One hunk of a file's diff, plus the self-contained patch that stages just it.</summary>
public sealed class HunkItem
{
    public required int Index { get; init; }
    public required string HunkHeader { get; init; }
    public required string Text { get; init; }
    public required string Patch { get; init; }
}
