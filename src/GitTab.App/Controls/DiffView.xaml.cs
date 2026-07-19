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

        // Only vertical is synced — horizontal is left independent, because the two panes can have
        // different line lengths and syncing it makes them fight. Skip when already aligned so we
        // don't reissue a scroll that bounces back.
        if (Math.Abs(to.VerticalOffset - from.VerticalOffset) > 0.5)
            to.ScrollToVerticalOffset(from.VerticalOffset);

        // The ScrollTo above can raise ScrollOffsetChanged on `to` *asynchronously* (after layout).
        // Reset the guard only once that has flushed, so the deferred event doesn't bounce back and
        // fight the pane the user is dragging — which is what showed up as scroll "stutter",
        // especially when one pane's horizontal scrollbar gives it a different max offset.
        Dispatcher.BeginInvoke(new Action(() => _syncing = false),
            System.Windows.Threading.DispatcherPriority.Background);
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

        // AvalonEdit's built-in highlighting definitions are tuned for a white editor — their keyword
        // blue etc. is a harsh, saturated color that's glaring/low-contrast on our dark surface. Tone
        // every syntax color down (cap saturation, normalize lightness for the current theme) so tokens
        // stay distinguishable but easy on the eyes. Idempotent, so re-applying per diff/theme is fine.
        if (definition is not null)
            SyntaxColorTuner.Soften(definition, IsDarkTheme());

        Editor.SyntaxHighlighting = definition;
        LeftEditor.SyntaxHighlighting = definition;
        RightEditor.SyntaxHighlighting = definition;
    }

    private bool IsDarkTheme()
    {
        if (TryFindResource("Brush.Window") is System.Windows.Media.SolidColorBrush b)
        {
            var c = b.Color;
            var luminance = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luminance < 0.5;
        }
        return true;
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
        _unifiedBg.MovedLines = MovedLineDetector.Detect(kinds, texts);

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

        // Moved-block detection runs on the flat hunk-order sequence, then maps each moved line to the
        // left (removed) or right (added) editor row it occupies.
        var movedSeq = MovedLineDetector.Detect(seqKinds, texts);
        var leftMoved = new HashSet<int>();
        var rightMoved = new HashSet<int>();
        foreach (var s in movedSeq)
        {
            if (seqKinds[s] == DiffLineKind.Removed && leftIndex[s] >= 0) leftMoved.Add(leftIndex[s]);
            else if (seqKinds[s] == DiffLineKind.Added && rightIndex[s] >= 0) rightMoved.Add(rightIndex[s]);
        }
        _leftBg.MovedLines = leftMoved;
        _rightBg.MovedLines = rightMoved;

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

    /// <summary>Line indices (0-based) whose add/remove is really a *move* (matched elsewhere in the
    /// diff), tinted differently from genuine additions/removals.</summary>
    public HashSet<int> MovedLines { get; set; } = new();

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
            bool moved = MovedLines.Contains(idx);
            var key = kind switch
            {
                DiffLineKind.Added => moved ? "Diff.MovedAddBg" : "Diff.AddBg",
                DiffLineKind.Removed => moved ? "Diff.MovedDelBg" : "Diff.DelBg",
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

/// <summary>
/// Tones down AvalonEdit's built-in syntax colors so they read comfortably on the app's dark (or
/// light) surface instead of the white background they were designed for. Caps saturation to kill the
/// glaring pure-blue keyword color and normalizes lightness into a readable band for the theme. Hue is
/// preserved, so keywords stay blue-ish, strings orange-ish, etc. — just softer. The transform is a
/// clamp, so applying it repeatedly (per diff / on theme switch) converges and never over-shoots.
/// </summary>
internal static class SyntaxColorTuner
{
    public static void Soften(ICSharpCode.AvalonEdit.Highlighting.IHighlightingDefinition definition, bool isDark)
    {
        foreach (var color in definition.NamedHighlightingColors)
        {
            if (color.Foreground is not { } fg) continue;
            System.Windows.Media.Color? rgb;
            try { rgb = fg.GetColor(null); } catch { continue; }
            if (rgb is not { } c) continue;
            color.Foreground = new ICSharpCode.AvalonEdit.Highlighting.SimpleHighlightingBrush(Soften(c, isDark));
        }
    }

    private static System.Windows.Media.Color Soften(System.Windows.Media.Color c, bool isDark)
    {
        RgbToHsl(c, out var h, out var s, out var l);
        s = Math.Min(s, 0.55);                       // cap saturation → no more "쨍한" glare
        l = isDark ? Clamp(l, 0.60, 0.80)            // readable-but-not-blinding on dark
                   : Clamp(l, 0.28, 0.48);           // dark-enough on light
        return HslToRgb(h, s, l);
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

    private static void RgbToHsl(System.Windows.Media.Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;
        if (Math.Abs(max - min) < 1e-9) { h = 0; s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        h /= 6.0;
    }

    private static System.Windows.Media.Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;
        if (s <= 0) { r = g = b = l; }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return System.Windows.Media.Color.FromRgb(To255(r), To255(g), To255(b));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static byte To255(double v) => (byte)Math.Round(Clamp(v, 0, 1) * 255);
}
