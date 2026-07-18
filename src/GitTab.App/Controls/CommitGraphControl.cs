using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
using GitTab.Core.Models;
using GitTab.Graph.Models;

namespace GitTab.App.Controls;

/// <summary>
/// Custom-rendered, virtualized commit history in a GitLens-style columnar layout
/// (Branch/Tag · Graph · Message · Changes · Author · Date · SHA). Draws everything directly via
/// <see cref="OnRender"/> and implements <see cref="IScrollInfo"/> so only visible rows are drawn.
/// </summary>
public sealed class CommitGraphControl : FrameworkElement, IScrollInfo
{
    private const double RowHeight = 30;
    private const double LaneWidth = 18;
    private const double NodeRadius = 8;
    private const double LaneThickness = 2.4;
    private const double GraphLeftPad = 12;
    private const double ChipHeight = 17;

    private readonly Pen[] _lanePens;
    private readonly Brush[] _laneBrushes;
    private readonly Typeface _uiFont = new("Segoe UI");
    private readonly Typeface _uiBold = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
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
            var p = new Pen(new SolidColorBrush(c), LaneThickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
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
        // Re-resolve themed brushes and repaint when the theme is swapped at runtime.
        ThemeService.ThemeChanged += (_, _) => InvalidateVisual();
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

    public static readonly DependencyProperty StatsSourceProperty = DependencyProperty.Register(
        nameof(StatsSource), typeof(ICommitStatsSource), typeof(CommitGraphControl),
        new FrameworkPropertyMetadata(null, OnStatsSourceChanged));

    public ICommitStatsSource? StatsSource
    {
        get => (ICommitStatsSource?)GetValue(StatsSourceProperty);
        set => SetValue(StatsSourceProperty, value);
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
        c.AnnounceSelection();
    }

    // ------------------------------------------------------------ accessibility

    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        => new CommitGraphAutomationPeer(this);

    /// <summary>Text a screen reader should read for the current selection.</summary>
    internal string SelectionText
    {
        get
        {
            var rows = Rows;
            if (rows is { Count: > 0 } && SelectedIndex >= 0 && SelectedIndex < rows.Count)
            {
                var r = rows[SelectedIndex];
                return $"{r.Summary} — {r.AuthorName}, {r.ShortSha}";
            }
            return string.Empty;
        }
    }

    // Speak the newly selected commit when navigating by keyboard.
    private void AnnounceSelection()
    {
        if (System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this) is not { } peer) return;
        var text = SelectionText;
        if (text.Length == 0) return;
        try
        {
            peer.RaiseNotificationEvent(
                System.Windows.Automation.AutomationNotificationKind.ActionCompleted,
                System.Windows.Automation.AutomationNotificationProcessing.MostRecent,
                text, "commitSelected");
        }
        catch { /* older UIA hosts may not support notifications */ }
    }

    private sealed class CommitGraphAutomationPeer : System.Windows.Automation.Peers.FrameworkElementAutomationPeer
    {
        public CommitGraphAutomationPeer(CommitGraphControl owner) : base(owner) { }

        protected override System.Windows.Automation.Peers.AutomationControlType GetAutomationControlTypeCore()
            => System.Windows.Automation.Peers.AutomationControlType.List;

        protected override string GetNameCore()
        {
            var text = ((CommitGraphControl)Owner).SelectionText;
            return text.Length > 0 ? text : base.GetNameCore();
        }
    }

    private static void OnStatsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (CommitGraphControl)d;
        if (e.OldValue is ICommitStatsSource oldSrc) oldSrc.StatsUpdated -= c.OnStatsUpdated;
        if (e.NewValue is ICommitStatsSource newSrc) newSrc.StatsUpdated += c.OnStatsUpdated;
    }

    private void OnStatsUpdated(object? sender, EventArgs e) => InvalidateVisual();

    // ------------------------------------------------------------ layout / scrolling

    private double GraphWidthEstimate => Math.Max(1, LaneCount) * LaneWidth + 200;

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
        _extent = new Size(Math.Max(vw, GraphWidthEstimate), count * RowHeight);
        _offset.Y = Math.Max(0, Math.Min(_offset.Y, Math.Max(0, _extent.Height - _viewport.Height)));
    }

    // ------------------------------------------------------------ rendering

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));

        var rows = Rows;
        if (rows is null || rows.Count == 0) return;

        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var textBrush = Res("Brush.Text", Brushes.Black);
        var mutedBrush = Res("Brush.TextMuted", Brushes.Gray);
        var selBrush = Res("Brush.Selection", Brushes.LightBlue);
        var accentBrush = Res("Brush.Accent", Brushes.DodgerBlue);
        var surfaceBrush = Res("Brush.Surface", Brushes.White);
        var trackBrush = Res("Brush.SurfaceAlt", Brushes.LightGray);
        var greenBrush = Res("Brush.Success", Brushes.Green);
        var redBrush = Res("Brush.Danger", Brushes.Red);
        var nodeStroke = new Pen(surfaceBrush, 1.5);

        double vh = _viewport.Height > 0 ? _viewport.Height : RenderSize.Height;
        double vw = _viewport.Width > 0 ? _viewport.Width : RenderSize.Width;
        var cols = GraphColumns.Compute(vw, LaneCount, LaneWidth);

        int first = Math.Max(0, (int)Math.Floor(_offset.Y / RowHeight));
        int last = Math.Min(rows.Count - 1, (int)Math.Ceiling((_offset.Y + vh) / RowHeight));

        double LaneX(int lane) => cols.GraphX + GraphLeftPad + lane * LaneWidth + LaneWidth / 2;

        dc.PushTransform(new TranslateTransform(-_offset.X, -_offset.Y));

        for (int i = first; i <= last; i++)
        {
            var row = rows[i];
            double top = i * RowHeight;
            double centerY = top + RowHeight / 2;
            double bottom = top + RowHeight;

            if (i == SelectedIndex)
                dc.DrawRectangle(selBrush, null, new Rect(_offset.X, top, vw, RowHeight));

            // ----- edges -----
            foreach (var seg in row.GraphRow.PassingLanes)
            {
                var pen = _lanePens[Mod(seg.ColorIndex)];
                switch (seg.Kind)
                {
                    case LaneKind.Straight:
                        dc.DrawLine(pen, new Point(LaneX(seg.FromLane), top), new Point(LaneX(seg.ToLane), bottom));
                        break;
                    case LaneKind.Merge:
                        DrawConnector(dc, pen, LaneX(seg.FromLane), top, LaneX(row.GraphRow.NodeLane), centerY);
                        break;
                    case LaneKind.Branch:
                        DrawConnector(dc, pen, LaneX(row.GraphRow.NodeLane), centerY, LaneX(seg.ToLane), bottom);
                        break;
                }
            }

            // ----- avatar node -----
            double nodeX = LaneX(row.GraphRow.NodeLane);
            var lanePen = _lanePens[Mod(row.GraphRow.ColorIndex)];
            var avColor = AuthorAvatar.ColorFor(string.IsNullOrEmpty(row.Commit.AuthorEmail) ? row.AuthorName : row.Commit.AuthorEmail);
            var avBrush = new SolidColorBrush(avColor);
            if (row.IsHead)
                dc.DrawEllipse(null, new Pen(accentBrush, 2.2), new Point(nodeX, centerY), NodeRadius + 3, NodeRadius + 3);
            dc.DrawEllipse(avBrush, nodeStroke, new Point(nodeX, centerY), NodeRadius, NodeRadius);
            dc.DrawEllipse(null, lanePen, new Point(nodeX, centerY), NodeRadius, NodeRadius); // lane-colored ring
            var initial = Ft(AuthorAvatar.Initial(row.AuthorName), Brushes.White, 9.5, _uiBold, ppd);
            dc.DrawText(initial, new Point(nodeX - initial.Width / 2, centerY - initial.Height / 2));

            // ----- refs (right-aligned, ending just left of the graph) -----
            DrawRefs(dc, row.Refs, cols.RefsX, cols.GraphX - 6, centerY, ppd);

            // ----- message -----
            var message = Ft(row.Summary, textBrush, 12.5, _uiFont, ppd, cols.MessageW);
            dc.DrawText(message, new Point(cols.MessageX, centerY - message.Height / 2));

            // ----- changes (optional) -----
            if (cols.ChangesW > 0)
                DrawChanges(dc, row.Sha, cols.ChangesX, cols.ChangesW, centerY, ppd, mutedBrush, trackBrush, greenBrush, redBrush);

            // ----- author / date (optional) / sha -----
            if (cols.AuthorW > 0)
            {
                var author = Ft(row.AuthorName, mutedBrush, 12, _uiFont, ppd, cols.AuthorW);
                dc.DrawText(author, new Point(cols.AuthorX, centerY - author.Height / 2));
            }

            if (cols.DateW > 0)
            {
                var date = Ft(RelativeTime.Format(row.WhenUtc, LocalizationService.Current), mutedBrush, 11.5, _uiFont, ppd, cols.DateW);
                dc.DrawText(date, new Point(cols.DateX, centerY - date.Height / 2));
            }

            var sha = Ft(row.ShortSha, mutedBrush, 11.5, _monoFont, ppd, cols.ShaW);
            dc.DrawText(sha, new Point(cols.ShaX, centerY - sha.Height / 2));
        }

        dc.Pop();
    }

    private void DrawRefs(DrawingContext dc, IReadOnlyList<RefLabel> refs, double leftLimit, double endX, double centerY, double ppd)
    {
        double x = endX;
        foreach (var chip in refs)
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
            var label = Ft(chip.Name, fg, 10.5, _uiFont, ppd, 120);
            double w = label.Width + 12;
            double chipX = x - w;
            if (chipX < leftLimit) break;
            var rect = new Rect(chipX, centerY - ChipHeight / 2, w, ChipHeight);
            dc.DrawRoundedRectangle(bg, null, rect, 7, 7);
            dc.DrawText(label, new Point(chipX + 6, centerY - label.Height / 2));
            x = chipX - 4;
        }
    }

    private void DrawChanges(DrawingContext dc, string sha, double x, double w, double centerY, double ppd,
        Brush muted, Brush track, Brush green, Brush red)
    {
        var source = StatsSource;
        if (source is null) return;
        if (!source.TryGet(sha, out var st)) { source.Request(sha); return; }

        var count = Ft(st.FilesChanged.ToString(), muted, 11, _uiFont, ppd);
        dc.DrawText(count, new Point(x, centerY - count.Height / 2));

        double barX = x + 24;
        double barW = w - 28;
        double barH = 6;
        double barY = centerY - barH / 2;
        if (barW < 8) return;
        dc.DrawRoundedRectangle(track, null, new Rect(barX, barY, barW, barH), 3, 3);

        int total = st.Total;
        if (total <= 0) return;
        double addW = Math.Round(barW * st.Additions / (double)total);
        if (addW > 0) dc.DrawRoundedRectangle(green, null, new Rect(barX, barY, addW, barH), 3, 3);
        double delW = barW - addW;
        if (delW > 0) dc.DrawRoundedRectangle(red, null, new Rect(barX + addW, barY, delW, barH), 3, 3);
    }

    private static void DrawConnector(DrawingContext dc, Pen pen, double x1, double y1, double x2, double y2)
    {
        if (Math.Abs(x1 - x2) < 0.1)
        {
            dc.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));
            return;
        }
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

    // ------------------------------------------------------------ input

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        SelectRowAt(e.GetPosition(this));
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Focus();
        SelectRowAt(e.GetPosition(this));
    }

    private void SelectRowAt(Point p)
    {
        int index = (int)((p.Y + _offset.Y) / RowHeight);
        if (Rows is { } rows && index >= 0 && index < rows.Count)
            SelectedIndex = index;
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
