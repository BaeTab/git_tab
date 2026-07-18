using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GitTab.App.Localization;
using GitTab.App.ViewModels;
using GitTab.Core.Models;
using GitTab.Graph.Models;

namespace GitTab.App.Controls;

/// <summary>
/// Custom-rendered, virtualized commit history. Draws lane edges, nodes, ref chips and commit
/// text directly via <see cref="OnRender"/>, and implements <see cref="IScrollInfo"/> so only the
/// rows inside the viewport are ever drawn — the key to smooth scrolling over huge histories.
/// </summary>
public sealed class CommitGraphControl : FrameworkElement, IScrollInfo
{
    private const double RowHeight = 26;
    private const double LaneWidth = 16;
    private const double NodeRadius = 5;
    private const double GraphLeftPad = 12;
    private const double ChipHeight = 16;

    private readonly Pen[] _lanePens;
    private readonly Brush[] _laneBrushes;
    private readonly Typeface _uiFont = new("Segoe UI");
    private readonly Typeface _monoFont = new("Consolas");

    private Vector _offset;
    private Size _extent;
    private Size _viewport;

    public CommitGraphControl()
    {
        Focusable = true;
        FocusVisualStyle = null;
        _lanePens = GraphPalette.Colors.Select(c =>
        {
            var p = new Pen(new SolidColorBrush(c), 2.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            return p;
        }).ToArray();
        _laneBrushes = GraphPalette.Colors.Select(c =>
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }).ToArray();

        LocalizationService.Current.LanguageChanged += (_, _) => InvalidateVisual();
    }

    // ------------------------------------------------------------ dependency properties

    public static readonly DependencyProperty RowsProperty = DependencyProperty.Register(
        nameof(Rows), typeof(IReadOnlyList<CommitRowViewModel>), typeof(CommitGraphControl),
        new FrameworkPropertyMetadata(null, OnRowsChanged));

    public IReadOnlyList<CommitRowViewModel>? Rows
    {
        get => (IReadOnlyList<CommitRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public static readonly DependencyProperty LaneCountProperty = DependencyProperty.Register(
        nameof(LaneCount), typeof(int), typeof(CommitGraphControl),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public int LaneCount
    {
        get => (int)GetValue(LaneCountProperty);
        set => SetValue(LaneCountProperty, value);
    }

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(CommitGraphControl),
        new FrameworkPropertyMetadata(-1,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectedIndexChanged));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private static void OnRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CommitGraphControl)d;
        c._offset = new Vector(0, 0);
        c.InvalidateMeasure();
        c.InvalidateVisual();
        c.ScrollOwner?.InvalidateScrollInfo();
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CommitGraphControl)d;
        c.BringIndexIntoView((int)e.NewValue);
    }

    // ------------------------------------------------------------ layout

    private double LaneX(int lane) => GraphLeftPad + lane * LaneWidth + LaneWidth / 2;
    private double GraphWidth => GraphLeftPad + Math.Max(LaneCount, 1) * LaneWidth + 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateExtent(availableSize);
        double w = double.IsInfinity(availableSize.Width) ? _extent.Width : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? _extent.Height : availableSize.Height;
        return new Size(w, h);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _viewport = finalSize;
        UpdateExtent(finalSize);
        ScrollOwner?.InvalidateScrollInfo();
        return finalSize;
    }

    private void UpdateExtent(Size viewport)
    {
        int count = Rows?.Count ?? 0;
        double vw = double.IsInfinity(viewport.Width) ? _viewport.Width : viewport.Width;
        _extent = new Size(Math.Max(vw, GraphWidth), count * RowHeight);
        // Clamp offset if content shrank.
        _offset.Y = Math.Max(0, Math.Min(_offset.Y, Math.Max(0, _extent.Height - _viewport.Height)));
    }

    // ------------------------------------------------------------ rendering

    protected override void OnRender(DrawingContext dc)
    {
        // Whole-viewport transparent fill so clicks anywhere hit the control.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));

        var rows = Rows;
        if (rows is null || rows.Count == 0) return;

        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var textBrush = Res("Brush.Text", Brushes.Black);
        var mutedBrush = Res("Brush.TextMuted", Brushes.Gray);
        var selBrush = Res("Brush.Selection", Brushes.LightBlue);
        var accentBrush = Res("Brush.Accent", Brushes.DodgerBlue);
        var surfaceBrush = Res("Brush.Surface", Brushes.White);
        var nodeStroke = new Pen(surfaceBrush, 1.5);

        double vh = _viewport.Height > 0 ? _viewport.Height : RenderSize.Height;
        double vw = _viewport.Width > 0 ? _viewport.Width : RenderSize.Width;
        int first = Math.Max(0, (int)Math.Floor(_offset.Y / RowHeight));
        int last = Math.Min(rows.Count - 1, (int)Math.Ceiling((_offset.Y + vh) / RowHeight));

        dc.PushTransform(new TranslateTransform(-_offset.X, -_offset.Y));

        for (int i = first; i <= last; i++)
        {
            var row = rows[i];
            double top = i * RowHeight;
            double centerY = top + RowHeight / 2;
            double bottom = top + RowHeight;

            // selection background across the viewport width
            if (i == SelectedIndex)
                dc.DrawRectangle(selBrush, null, new Rect(_offset.X, top, vw, RowHeight));

            // edges
            foreach (var seg in row.GraphRow.PassingLanes)
            {
                var pen = _lanePens[Mod(seg.ColorIndex)];
                switch (seg.Kind)
                {
                    case LaneKind.Straight:
                        dc.DrawLine(pen, new Point(LaneX(seg.FromLane), top), new Point(LaneX(seg.ToLane), bottom));
                        break;
                    case LaneKind.Merge: // top edge -> node center
                        DrawConnector(dc, pen, LaneX(seg.FromLane), top, LaneX(row.GraphRow.NodeLane), centerY);
                        break;
                    case LaneKind.Branch: // node center -> bottom edge
                        DrawConnector(dc, pen, LaneX(row.GraphRow.NodeLane), centerY, LaneX(seg.ToLane), bottom);
                        break;
                }
            }

            // node
            double nodeX = LaneX(row.GraphRow.NodeLane);
            var nodeBrush = _laneBrushes[Mod(row.GraphRow.ColorIndex)];
            if (row.IsHead)
                dc.DrawEllipse(null, new Pen(accentBrush, 2.0), new Point(nodeX, centerY), NodeRadius + 2.5, NodeRadius + 2.5);
            dc.DrawEllipse(nodeBrush, nodeStroke, new Point(nodeX, centerY), NodeRadius, NodeRadius);

            // text area
            double x = GraphWidth + 6;
            double rightEdge = _offset.X + vw - 10;

            // right-aligned: time, then author, then short sha (all muted)
            var timeText = Ft(RelativeTime.Format(row.WhenUtc, LocalizationService.Current), mutedBrush, 11.5, _uiFont, pixelsPerDip);
            var authorText = Ft(Truncate(row.AuthorName, 22), mutedBrush, 11.5, _uiFont, pixelsPerDip);
            var shaText = Ft(row.ShortSha, mutedBrush, 11.5, _monoFont, pixelsPerDip);

            double ty = centerY - timeText.Height / 2;
            double timeX = rightEdge - timeText.Width;
            double authorX = timeX - 12 - authorText.Width;
            double shaX = authorX - 12 - shaText.Width;
            dc.DrawText(timeText, new Point(timeX, ty));
            dc.DrawText(authorText, new Point(authorX, ty));
            dc.DrawText(shaText, new Point(shaX, ty));

            // chips
            foreach (var chip in row.Refs)
            {
                double w = DrawChip(dc, chip, x, centerY, pixelsPerDip);
                x += w + 4;
                if (x > shaX - 40) break; // don't overflow into the right block
            }

            // summary (fills remaining space, ellipsized)
            double summaryMax = Math.Max(40, shaX - 16 - x);
            var summary = Ft(row.Summary, textBrush, 12.5, _uiFont, pixelsPerDip, summaryMax);
            dc.DrawText(summary, new Point(x, centerY - summary.Height / 2));
        }

        dc.Pop();
    }

    private static void DrawConnector(DrawingContext dc, Pen pen, double x1, double y1, double x2, double y2)
    {
        if (Math.Abs(x1 - x2) < 0.1)
        {
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
            return;
        }
        // Smooth S-curve between lanes.
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x1, y1), false, false);
            double midY = (y1 + y2) / 2;
            ctx.BezierTo(new Point(x1, midY), new Point(x2, midY), new Point(x2, y2), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private double DrawChip(DrawingContext dc, RefLabel chip, double x, double centerY, double ppd)
    {
        var (bgKey, fgKey) = chip.Kind switch
        {
            RefKind.Tag => ("Chip.Tag.Bg", "Chip.Tag.Text"),
            RefKind.RemoteBranch => ("Chip.Remote.Bg", "Chip.Remote.Text"),
            RefKind.Head => ("Chip.Head.Bg", "Chip.Head.Text"),
            _ => chip.IsCurrent ? ("Chip.Head.Bg", "Chip.Head.Text") : ("Chip.Local.Bg", "Chip.Local.Text")
        };
        var bg = Res(bgKey, Brushes.LightGray);
        var fg = Res(fgKey, Brushes.Black);

        var label = Ft(chip.Name, fg, 10.5, _uiFont, ppd);
        double w = label.Width + 12;
        var rect = new Rect(x, centerY - ChipHeight / 2, w, ChipHeight);
        dc.DrawRoundedRectangle(bg, null, rect, 8, 8);
        dc.DrawText(label, new Point(x + 6, centerY - label.Height / 2));
        return w;
    }

    private FormattedText Ft(string text, Brush brush, double size, Typeface face, double ppd, double? maxWidth = null)
    {
        var ft = new FormattedText(text ?? string.Empty, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, face, size, brush, ppd)
        {
            Trimming = TextTrimming.CharacterEllipsis,
            MaxLineCount = 1
        };
        if (maxWidth is { } mw) ft.MaxTextWidth = Math.Max(1, mw);
        return ft;
    }

    private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static int Mod(int i) => ((i % GraphPalette.Size) + GraphPalette.Size) % GraphPalette.Size;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    // ------------------------------------------------------------ input

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var p = e.GetPosition(this);
        int index = (int)((p.Y + _offset.Y) / RowHeight);
        if (Rows is { } rows && index >= 0 && index < rows.Count)
            SelectedIndex = index;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Focus();
        var p = e.GetPosition(this);
        int index = (int)((p.Y + _offset.Y) / RowHeight);
        if (Rows is { } rows && index >= 0 && index < rows.Count)
            SelectedIndex = index; // select before the ContextMenu opens
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var rows = Rows;
        if (rows is null || rows.Count == 0) return;
        switch (e.Key)
        {
            case Key.Down: SelectedIndex = Math.Min(rows.Count - 1, Math.Max(0, SelectedIndex + 1)); e.Handled = true; break;
            case Key.Up: SelectedIndex = Math.Max(0, SelectedIndex - 1); e.Handled = true; break;
            case Key.PageDown: SelectedIndex = Math.Min(rows.Count - 1, SelectedIndex + VisibleRows); e.Handled = true; break;
            case Key.PageUp: SelectedIndex = Math.Max(0, SelectedIndex - VisibleRows); e.Handled = true; break;
        }
    }

    private int VisibleRows => Math.Max(1, (int)(_viewport.Height / RowHeight));

    private void BringIndexIntoView(int index)
    {
        if (index < 0) return;
        double top = index * RowHeight;
        double bottom = top + RowHeight;
        if (top < _offset.Y) SetVerticalOffset(top);
        else if (bottom > _offset.Y + _viewport.Height) SetVerticalOffset(bottom - _viewport.Height);
        InvalidateVisual();
    }

    // ------------------------------------------------------------ IScrollInfo

    public ScrollViewer? ScrollOwner { get; set; }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;

    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;

    public void LineUp() => SetVerticalOffset(_offset.Y - RowHeight);
    public void LineDown() => SetVerticalOffset(_offset.Y + RowHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageUp() => SetVerticalOffset(_offset.Y - _viewport.Height);
    public void PageDown() => SetVerticalOffset(_offset.Y + _viewport.Height);
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelUp() => SetVerticalOffset(_offset.Y - 3 * RowHeight);
    public void MouseWheelDown() => SetVerticalOffset(_offset.Y + 3 * RowHeight);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        double max = Math.Max(0, _extent.Height - _viewport.Height);
        double clamped = Math.Max(0, Math.Min(offset, max));
        if (Math.Abs(clamped - _offset.Y) < 0.001) return;
        _offset.Y = clamped;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateVisual();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;
}
