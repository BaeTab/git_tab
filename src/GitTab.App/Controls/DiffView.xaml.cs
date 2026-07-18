using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GitTab.App.ViewModels;
using GitTab.Core.Models;
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
            to.ScrollToVerticalOffset(from.VerticalOffset);
            to.ScrollToHorizontalOffset(from.HorizontalOffset);
        }
        finally { _syncing = false; }
    }

    private void RebuildAll()
    {
        BuildUnified();
        BuildSplit();
    }

    private void BuildUnified()
    {
        var diff = _vm?.Diff;
        if (diff is null || diff.IsBinary || diff.Hunks.Count == 0)
        {
            Editor.Text = string.Empty;
            _unifiedBg.Kinds = Array.Empty<DiffLineKind>();
            Editor.TextArea.TextView.Redraw();
            return;
        }

        var sb = new StringBuilder();
        var kinds = new List<DiffLineKind>();
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
            }
        }
        Editor.Text = sb.ToString().TrimEnd('\n');
        _unifiedBg.Kinds = kinds;
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
            return;
        }

        var lsb = new StringBuilder();
        var rsb = new StringBuilder();
        var lk = new List<DiffLineKind>();
        var rk = new List<DiffLineKind>();

        void AddL(string t, DiffLineKind k) { lsb.Append(t).Append('\n'); lk.Add(k); }
        void AddR(string t, DiffLineKind k) { rsb.Append(t).Append('\n'); rk.Add(k); }

        foreach (var hunk in diff.Hunks)
        {
            foreach (var line in hunk.Lines)
            {
                switch (line.Kind)
                {
                    case DiffLineKind.HunkHeader:
                        AddL(line.Text, DiffLineKind.HunkHeader);
                        AddR(line.Text, DiffLineKind.HunkHeader);
                        break;
                    case DiffLineKind.Context:
                        AddL(line.Text, DiffLineKind.Context);
                        AddR(line.Text, DiffLineKind.Context);
                        break;
                    case DiffLineKind.Removed:
                        AddL(line.Text, DiffLineKind.Removed);
                        AddR(string.Empty, DiffLineKind.Context); // blank filler (no color)
                        break;
                    case DiffLineKind.Added:
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
        LeftEditor.ScrollToHome();
        RightEditor.ScrollToHome();
        LeftEditor.TextArea.TextView.Redraw();
        RightEditor.TextArea.TextView.Redraw();
    }
}

/// <summary>Draws a full-width background behind each diff line according to its kind.</summary>
internal sealed class DiffBackgroundRenderer : IBackgroundRenderer
{
    private readonly FrameworkElement _resourceHost;

    public DiffBackgroundRenderer(FrameworkElement resourceHost) => _resourceHost = resourceHost;

    public IReadOnlyList<DiffLineKind> Kinds { get; set; } = Array.Empty<DiffLineKind>();

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Kinds.Count == 0 || !textView.VisualLinesValid) return;

        foreach (var vl in textView.VisualLines)
        {
            int lineNumber = vl.FirstDocumentLine.LineNumber;
            int idx = lineNumber - 1;
            if (idx < 0 || idx >= Kinds.Count) continue;

            var key = Kinds[idx] switch
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
        }
    }
}
