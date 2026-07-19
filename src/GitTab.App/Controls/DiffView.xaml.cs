using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GitTab.App.ViewModels;
using GitTab.Core.Diff;
using GitTab.Core.Models;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace GitTab.App.Controls;

/// <summary>Colored unified + side-by-side diff viewer built on AvalonEdit.</summary>
public partial class DiffView : UserControl
{
    private readonly DiffBackgroundRenderer _unifiedBg;
    private readonly DiffBackgroundRenderer _leftBg;
    private readonly DiffBackgroundRenderer _rightBg;
    private DiffViewModel? _vm;
    private bool _syncing;

    public DiffView()
    {
        InitializeComponent();
        _unifiedBg = new DiffBackgroundRenderer(this);
        _leftBg = new DiffBackgroundRenderer(this);
        _rightBg = new DiffBackgroundRenderer(this);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_unifiedBg);
        LeftEditor.TextArea.TextView.BackgroundRenderers.Add(_leftBg);
        RightEditor.TextArea.TextView.BackgroundRenderers.Add(_rightBg);

        LeftEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Sync(LeftEditor, RightEditor);
        RightEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Sync(RightEditor, LeftEditor);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as DiffViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        RebuildAll();
        UpdateMode();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DiffViewModel.Diff)) { RebuildAll(); UpdateMode(); }
        else if (e.PropertyName is nameof(DiffViewModel.IsSplit)) UpdateMode();
    }

    private void UpdateMode()
    {
        bool split = _vm?.IsSplit == true;
        Editor.Visibility = split ? Visibility.Collapsed : Visibility.Visible;
        SplitRoot.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Sync(ICSharpCode.AvalonEdit.TextEditor from, ICSharpCode.AvalonEdit.TextEditor to)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            // Only scroll when the target is actually off — reissuing ScrollTo to an already-clamped
            // offset (e.g. when one pane is longer and the other bottoms out) makes AvalonEdit raise
            // ScrollOffsetChanged again, which oscillates and shows up as scroll "stutter".
            if (Math.Abs(to.VerticalOffset - from.VerticalOffset) > 0.5)
                to.ScrollToVerticalOffset(from.VerticalOffset);
            if (Math.Abs(to.HorizontalOffset - from.HorizontalOffset) > 0.5)
                to.ScrollToHorizontalOffset(from.HorizontalOffset);
        }
        finally { _syncing = false; }
    }

    private void RebuildAll()
    {
        ApplyHighlighting();
        BuildUnified();
        BuildSplit();
    }

    /// <summary>Picks a language definition from the diffed file's extension and applies it to all
    /// three editors so tokens are colored under the existing diff line backgrounds.</summary>
    private void ApplyHighlighting()
    {
        IHighlightingDefinition? definition = null;
        var diff = _vm?.Diff;
        if (diff is { IsBinary: false, IsTooLarge: false } && diff.Hunks.Count > 0)
        {
            var ext = System.IO.Path.GetExtension(diff.Path);
            if (!string.IsNullOrEmpty(ext))
            {
                try { definition = HighlightingManager.Instance.GetDefinitionByExtension(ext); }
                catch { definition = null; }
            }
        }

        Editor.SyntaxHighlighting = definition;
        LeftEditor.SyntaxHighlighting = definition;
        RightEditor.SyntaxHighlighting = definition;
    }

    private void BuildUnified()
    {
        var diff = _vm?.Diff;
        if (diff is null || diff.IsBinary || diff.Hunks.Count == 0)
        {
            Editor.Text = string.Empty;
            _unifiedBg.Kinds = Array.Empty<DiffLineKind>();
            _unifiedBg.WordSegments = EmptyWords;
            Editor.TextArea.TextView.Redraw();
            return;
        }

        var sb = new StringBuilder();
        var kinds = new List<DiffLineKind>();
        var texts = new List<string>();
        foreach (var hunk in diff.Hunks)
        {
            foreach (var line in hunk.Lines)
            {
                var text = line.Kind switch
                {
                    DiffLineKind.Added => "+" + line.Text,
                    DiffLineKind.Removed => "-" + line.Text,
                    DiffLineKind.Context => " " + line.Text,
                    _ => line.Text
                };
                sb.Append(text).Append('\n');
                kinds.Add(line.Kind);
                texts.Add(line.Text);
            }
        }
        Editor.Text = sb.ToString().TrimEnd('\n');
        _unifiedBg.Kinds = kinds;

        // Word-level highlight: pair each removed line with the added line replacing it and mark
        // only the ranges that changed. Prefix +1 accounts for the "+"/"-" column.
        var words = new Dictionary<int, IReadOnlyList<WordDiff.Segment>>();
        foreach (var (rIdx, aIdx) in PairModified(kinds))
        {
            var (oldSegs, newSegs) = WordDiff.Compute(texts[rIdx], texts[aIdx]);
            if (oldSegs.Count > 0) words[rIdx] = Shift(oldSegs, 1);
            if (newSegs.Count > 0) words[aIdx] = Shift(newSegs, 1);
        }
        _unifiedBg.WordSegments = words;

        Editor.ScrollToHome();
        Editor.TextArea.TextView.Redraw();
    }

    private void BuildSplit()
    {
        var diff = _vm?.Diff;
        if (diff is null || diff.IsBinary || diff.Hunks.Count == 0)
        {
            LeftEditor.Text = RightEditor.Text = string.Empty;
            _leftBg.Kinds = _rightBg.Kinds = Array.Empty<DiffLineKind>();
            _leftBg.WordSegments = _rightBg.WordSegments = EmptyWords;
            return;
        }

        var lsb = new StringBuilder();
        var rsb = new StringBuilder();
        var lk = new List<DiffLineKind>();
        var rk = new List<DiffLineKind>();

        // Flat hunk-order sequence so we can pair a removed line with the added line replacing it;
        // each entry remembers which left/right editor line it occupies (-1 = blank filler).
        var seqKinds = new List<DiffLineKind>();
        var leftIndex = new List<int>();
        var rightIndex = new List<int>();
        var texts = new List<string>();

        void AddL(string t, DiffLineKind k) { lsb.Append(t).Append('\n'); lk.Add(k); }
        void AddR(string t, DiffLineKind k) { rsb.Append(t).Append('\n'); rk.Add(k); }

        foreach (var hunk in diff.Hunks)
        {
            foreach (var line in hunk.Lines)
            {
                switch (line.Kind)
                {
                    case DiffLineKind.HunkHeader:
                        seqKinds.Add(DiffLineKind.HunkHeader); leftIndex.Add(lk.Count); rightIndex.Add(rk.Count); texts.Add(line.Text);
                        AddL(line.Text, DiffLineKind.HunkHeader);
                        AddR(line.Text, DiffLineKind.HunkHeader);
                        break;
                    case DiffLineKind.Context:
                        seqKinds.Add(DiffLineKind.Context); leftIndex.Add(lk.Count); rightIndex.Add(rk.Count); texts.Add(line.Text);
                        AddL(line.Text, DiffLineKind.Context);
                        AddR(line.Text, DiffLineKind.Context);
                        break;
                    case DiffLineKind.Removed:
                        seqKinds.Add(DiffLineKind.Removed); leftIndex.Add(lk.Count); rightIndex.Add(-1); texts.Add(line.Text);
                        AddL(line.Text, DiffLineKind.Removed);
                        AddR(string.Empty, DiffLineKind.Context); // blank filler (no color)
                        break;
                    case DiffLineKind.Added:
                        seqKinds.Add(DiffLineKind.Added); leftIndex.Add(-1); rightIndex.Add(rk.Count); texts.Add(line.Text);
                        AddL(string.Empty, DiffLineKind.Context);
                        AddR(line.Text, DiffLineKind.Added);
                        break;
                }
            }
        }

        LeftEditor.Text = lsb.ToString().TrimEnd('\n');
        RightEditor.Text = rsb.ToString().TrimEnd('\n');
        _leftBg.Kinds = lk;
        _rightBg.Kinds = rk;

        var leftWords = new Dictionary<int, IReadOnlyList<WordDiff.Segment>>();
        var rightWords = new Dictionary<int, IReadOnlyList<WordDiff.Segment>>();
        foreach (var (rSeq, aSeq) in PairModified(seqKinds))
        {
            var (oldSegs, newSegs) = WordDiff.Compute(texts[rSeq], texts[aSeq]);
            if (oldSegs.Count > 0) leftWords[leftIndex[rSeq]] = Shift(oldSegs, 0);
            if (newSegs.Count > 0) rightWords[rightIndex[aSeq]] = Shift(newSegs, 0);
        }
        _leftBg.WordSegments = leftWords;
        _rightBg.WordSegments = rightWords;

        LeftEditor.ScrollToHome();
        RightEditor.ScrollToHome();
        LeftEditor.TextArea.TextView.Redraw();
        RightEditor.TextArea.TextView.Redraw();
    }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<WordDiff.Segment>> EmptyWords
        = new Dictionary<int, IReadOnlyList<WordDiff.Segment>>();

    /// <summary>Pairs each removed line with the added line that replaced it: within a run of
    /// removed lines immediately followed by a run of added lines, the p-th removed maps to the
    /// p-th added. Returns indices into the supplied kinds list.</summary>
    private static IEnumerable<(int RemovedIdx, int AddedIdx)> PairModified(IReadOnlyList<DiffLineKind> kinds)
    {
        int i = 0;
        while (i < kinds.Count)
        {
            if (kinds[i] != DiffLineKind.Removed) { i++; continue; }

            int rStart = i;
            while (i < kinds.Count && kinds[i] == DiffLineKind.Removed) i++;
            int rEnd = i;

            if (i >= kinds.Count || kinds[i] != DiffLineKind.Added) continue;

            int aStart = i;
            while (i < kinds.Count && kinds[i] == DiffLineKind.Added) i++;
            int aEnd = i;

            int pairs = Math.Min(rEnd - rStart, aEnd - aStart);
            for (int p = 0; p < pairs; p++)
                yield return (rStart + p, aStart + p);
        }
    }

    private static IReadOnlyList<WordDiff.Segment> Shift(IReadOnlyList<WordDiff.Segment> segs, int by)
    {
        if (by == 0) return segs;
        var shifted = new List<WordDiff.Segment>(segs.Count);
        foreach (var s in segs) shifted.Add(new WordDiff.Segment(s.Start + by, s.Length));
        return shifted;
    }
}

/// <summary>Draws a full-width background behind each diff line according to its kind.</summary>
internal sealed class DiffBackgroundRenderer : IBackgroundRenderer
{
    private readonly FrameworkElement _resourceHost;

    public DiffBackgroundRenderer(FrameworkElement resourceHost) => _resourceHost = resourceHost;

    public IReadOnlyList<DiffLineKind> Kinds { get; set; } = Array.Empty<DiffLineKind>();

    /// <summary>Per-line (0-based) changed-word ranges, offset to document columns, painted with a
    /// stronger brush on top of the row background.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<WordDiff.Segment>> WordSegments { get; set; }
        = new Dictionary<int, IReadOnlyList<WordDiff.Segment>>();

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Kinds.Count == 0 || !textView.VisualLinesValid) return;

        foreach (var vl in textView.VisualLines)
        {
            var docLine = vl.FirstDocumentLine;
            int idx = docLine.LineNumber - 1;
            if (idx < 0 || idx >= Kinds.Count) continue;

            var kind = Kinds[idx];
            var key = kind switch
            {
                DiffLineKind.Added => "Diff.AddBg",
                DiffLineKind.Removed => "Diff.DelBg",
                DiffLineKind.HunkHeader => "Diff.HunkBg",
                _ => null
            };
            if (key is null) continue;
            if (_resourceHost.TryFindResource(key) is not Brush brush) continue;

            double top = vl.VisualTop - textView.VerticalOffset;
            double width = textView.ActualWidth + textView.HorizontalOffset;
            drawingContext.DrawRectangle(brush, null, new Rect(0, top, width, vl.Height));

            // Overlay the changed words within this modified line with a stronger tint.
            if (!WordSegments.TryGetValue(idx, out var segments)) continue;
            var wordKey = kind == DiffLineKind.Added ? "Diff.AddWordBg" : "Diff.DelWordBg";
            if (_resourceHost.TryFindResource(wordKey) is not Brush wordBrush) continue;

            foreach (var seg in segments)
            {
                int start = docLine.Offset + seg.Start;
                int len = Math.Min(seg.Length, Math.Max(0, docLine.EndOffset - start));
                if (len <= 0) continue;
                var textSeg = new TextSegment { StartOffset = start, Length = len };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, textSeg))
                    drawingContext.DrawRectangle(wordBrush, null, rect);
            }
        }
    }
}
