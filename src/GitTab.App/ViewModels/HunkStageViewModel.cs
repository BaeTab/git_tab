using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Diff;
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
                var header = current[0];
                var body = current.Skip(1).ToList();
                var block = string.Join("\n", current).TrimEnd('\n');
                var item = new HunkItem
                {
                    HunkHeader = header,
                    Text = block,
                    Patch = _header + "\n" + block + "\n",
                    BodyLines = body
                };
                for (int bi = 0; bi < body.Count; bi++)
                {
                    var t = body[bi];
                    bool isChange = t.Length > 0 && (t[0] == '+' || t[0] == '-');
                    item.Lines.Add(new HunkLineVm { BodyIndex = bi, Text = t, IsChange = isChange });
                }
                Hunks.Add(item);
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
    private Task Stage(HunkItem? hunk)
        => hunk is null ? Task.CompletedTask : ApplyPatchTextAsync(hunk.Patch);

    /// <summary>Stage only the checked +/- lines of a hunk (git add -p line-level).</summary>
    [RelayCommand]
    private Task StageLines(HunkItem? hunk)
    {
        if (hunk is null) return Task.CompletedTask;
        var selected = new HashSet<int>();
        foreach (var l in hunk.Lines)
            if (l.IsChange && l.IsSelected) selected.Add(l.BodyIndex);

        var patch = PatchBuilder.BuildPartialStagePatch(_header, hunk.HunkHeader, hunk.BodyLines, selected);
        if (patch is null)
        {
            _dialogs.Info(Loc.T("HunkStage.LineHint"), Loc.T("WC.StagePartial"));
            return Task.CompletedTask;
        }
        return ApplyPatchTextAsync(patch);
    }

    private async Task ApplyPatchTextAsync(string patch)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "gittab-hunk-" + Guid.NewGuid().ToString("N") + ".patch");
        try
        {
            File.WriteAllText(tmp, patch, new UTF8Encoding(false));
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
    public required string HunkHeader { get; init; }
    public required string Text { get; init; }
    public required string Patch { get; init; }

    /// <summary>The hunk's body lines (after the @@ header), each starting with ' ', '+', or '-'.</summary>
    public required IReadOnlyList<string> BodyLines { get; init; }

    /// <summary>Per-line view models for line-level staging.</summary>
    public ObservableCollection<HunkLineVm> Lines { get; } = new();
}

/// <summary>A single diff line inside a hunk, selectable when it is an addition/removal.</summary>
public sealed partial class HunkLineVm : ObservableObject
{
    public required int BodyIndex { get; init; }
    public required string Text { get; init; }
    public required bool IsChange { get; init; }

    /// <summary>Whether this changed line is included when staging selected lines (default on).</summary>
    [ObservableProperty]
    private bool _isSelected = true;
}
