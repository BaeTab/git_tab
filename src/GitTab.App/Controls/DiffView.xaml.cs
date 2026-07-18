using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GitTab.App.ViewModels;
using GitTab.Core.Models;
using ICSharpCode.AvalonEdit.Rendering;

namespace GitTab.App.Controls;

/// <summary>Colored unified-diff viewer built on AvalonEdit.</summary>
public partial class DiffView : UserControl
{
    private readonly DiffBackgroundRenderer _background;
    private DiffViewModel? _vm;

    public DiffView()
    {
        InitializeComponent();
        _background = new DiffBackgroundRenderer(this);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_background);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as DiffViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        Rebuild();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DiffViewModel.Diff)) Rebuild();
    }

    private void Rebuild()
    {
        var diff = _vm?.Diff;
        if (diff is null || diff.IsBinary || diff.Hunks.Count == 0)
        {
            Editor.Text = string.Empty;
            _background.Kinds = Array.Empty<DiffLineKind>();
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
        _background.Kinds = kinds;
        Editor.ScrollToHome();
        Editor.TextArea.TextView.Redraw();
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
            var rect = new Rect(0, top, width, vl.Height);
            drawingContext.DrawRectangle(brush, null, rect);
        }
    }
}
