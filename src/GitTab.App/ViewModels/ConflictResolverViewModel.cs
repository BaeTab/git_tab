using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using GitTab.Core.Merge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitTab.App.ViewModels;

/// <summary>
/// Visual 3-way merge editor: shows the common ancestor (base), ours, and theirs for reference, and
/// an editable "result" pane (starting from the working file with conflict markers) that the user
/// writes out. Each conflicting region is also listed separately so a side can be picked
/// <em>per block</em>; the quick actions apply one choice to every block at once.
/// </summary>
public sealed partial class ConflictResolverViewModel : ObservableObject
{
    private readonly string _fullPath;

    // The parsed form of the conflicted file. It stays the source of truth once the user starts
    // picking sides, so choosing a side for one block never loses the choices made for the others.
    private IReadOnlyList<ConflictPart> _parts = Array.Empty<ConflictPart>();

    // True while we rewrite Result ourselves, so the round-trip doesn't re-parse (and discard) the
    // very block list the user is clicking through.
    private bool _syncing;

    public ConflictResolverViewModel(string displayPath, string fullPath,
        string? baseText, string? ours, string? theirs)
    {
        DisplayPath = displayPath;
        _fullPath = fullPath;
        BaseText = baseText ?? string.Empty;
        Ours = ours ?? string.Empty;
        Theirs = theirs ?? string.Empty;
        HasBase = !string.IsNullOrEmpty(baseText);
        Result = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
    }

    public string DisplayPath { get; }
    public string BaseText { get; }
    public string Ours { get; }
    public string Theirs { get; }
    public bool HasBase { get; }

    [ObservableProperty] private string _result = string.Empty;
    public bool Written { get; private set; }

    /// <summary>The file's conflicting regions, each independently resolvable.</summary>
    public ObservableCollection<ConflictBlockViewModel> Blocks { get; } = new();

    public bool HasBlocks => Blocks.Count > 0;

    partial void OnResultChanged(string value)
    {
        if (_syncing) return;
        // Re-derive the blocks while the text still carries markers (initial load, or the user
        // hand-edited them). Once the text is resolved we keep the existing list so the per-block
        // buttons stay usable and the user can still switch a block to the other side.
        if (ConflictParser.HasConflictMarkers(value) || Blocks.Count == 0) ReparseBlocks(value);
    }

    private void ReparseBlocks(string content)
    {
        Blocks.Clear();
        _parts = ConflictParser.HasConflictMarkers(content)
            ? ConflictParser.Parse(content)
            : Array.Empty<ConflictPart>();

        var number = 0;
        foreach (var part in _parts)
            if (part.Conflict is { } conflict)
                Blocks.Add(new ConflictBlockViewModel(++number, conflict, RebuildResult));

        OnPropertyChanged(nameof(HasBlocks));
    }

    /// <summary>Regenerate the result text from the per-block choices.</summary>
    private void RebuildResult()
    {
        if (_parts.Count == 0) return;
        _syncing = true;
        try { Result = ConflictParser.Resolve(_parts); }
        finally { _syncing = false; }
    }

    /// <summary>Apply one choice to every block. Returns false when the file has no parsed blocks
    /// (already resolved, or markers stripped), so callers can fall back to whole-file behavior.</summary>
    private bool SetAllBlocks(ConflictChoice choice)
    {
        if (Blocks.Count == 0) return false;
        foreach (var block in Blocks) block.SetChoiceSilently(choice);
        RebuildResult();
        return true;
    }

    [RelayCommand]
    private void UseOurs()
    {
        // Prefer rebuilding from the blocks: that keeps any non-conflicting edits already made to
        // the working file, which replacing the whole text with the index's "ours" side would drop.
        if (!SetAllBlocks(ConflictChoice.Ours)) Result = Ours;
    }

    [RelayCommand]
    private void UseTheirs()
    {
        if (!SetAllBlocks(ConflictChoice.Theirs)) Result = Theirs;
    }

    /// <summary>Strip conflict markers keeping both sides, as a starting point to hand-edit.</summary>
    [RelayCommand]
    private void KeepBoth()
    {
        if (SetAllBlocks(ConflictChoice.Both)) return;

        var parts = ConflictParser.Parse(Result);
        foreach (var p in parts)
            if (p.Conflict is { } c) c.Choice = ConflictChoice.Both;
        Result = ConflictParser.Resolve(parts);
    }

    public void Apply()
    {
        File.WriteAllText(_fullPath, Result);
        Written = true;
    }
}

/// <summary>One conflicting region in the resolver's block list.</summary>
public sealed partial class ConflictBlockViewModel : ObservableObject
{
    private const int PreviewLines = 3;

    private readonly ConflictBlock _model;
    private readonly Action _changed;
    private bool _suppress;

    public ConflictBlockViewModel(int number, ConflictBlock model, Action changed)
    {
        _model = model;
        _changed = changed;
        Number = number;
        _choice = model.Choice;
    }

    public int Number { get; }
    public string OursPreview => Preview(_model.Ours);
    public string TheirsPreview => Preview(_model.Theirs);

    [ObservableProperty] private ConflictChoice _choice;

    partial void OnChoiceChanged(ConflictChoice value)
    {
        _model.Choice = value;
        if (!_suppress) _changed();
    }

    /// <summary>Set the choice without rebuilding the result text (used for bulk apply).</summary>
    public void SetChoiceSilently(ConflictChoice choice)
    {
        _suppress = true;
        try { Choice = choice; }
        finally { _suppress = false; }
    }

    // A couple of lines is enough to tell the two sides apart in the list; the full text stays
    // visible in the reference panes above.
    private static string Preview(string text)
    {
        if (string.IsNullOrEmpty(text)) return "(empty)";
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        for (var i = 0; i < Math.Min(PreviewLines, lines.Length); i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(lines[i]);
        }
        if (lines.Length > PreviewLines) sb.Append("\n…");
        return sb.ToString();
    }
}
