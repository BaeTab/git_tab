using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GitTab.App.Localization;

namespace GitTab.App.Controls;

/// <summary>Column header row for <see cref="CommitGraphControl"/>, using the same column geometry.</summary>
public sealed class CommitGraphHeader : FrameworkElement
{
    private const double LaneWidth = 18;
    private readonly Typeface _font = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    public CommitGraphHeader()
    {
        LocalizationService.Current.LanguageChanged += (_, _) => InvalidateVisual();
    }

    public static readonly DependencyProperty LaneCountProperty = DependencyProperty.Register(
        nameof(LaneCount), typeof(int), typeof(CommitGraphHeader),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public int LaneCount
    {
        get => (int)GetValue(LaneCountProperty);
        set => SetValue(LaneCountProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        return new Size(w, 28);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = RenderSize.Width;
        double h = RenderSize.Height;
        var surface = Res("Brush.Surface", Brushes.White);
        var border = Res("Brush.Border", Brushes.Gray);
        var muted = Res("Brush.TextMuted", Brushes.Gray);
        double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        dc.DrawRectangle(surface, null, new Rect(0, 0, w, h));
        dc.DrawLine(new Pen(border, 1), new Point(0, h - 0.5), new Point(w, h - 0.5));

        var cols = GraphColumns.Compute(w, LaneCount, LaneWidth);
        var loc = LocalizationService.Current;
        double y(FormattedText t) => (h - t.Height) / 2;

        void Label(string key, double x, double? max = null)
        {
            var t = new FormattedText(loc.T(key), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                _font, 10.5, muted, ppd) { Trimming = TextTrimming.CharacterEllipsis, MaxLineCount = 1 };
            if (max is { } m) t.MaxTextWidth = Math.Max(1, m);
            dc.DrawText(t, new Point(x, y(t)));
        }

        Label("Col.Refs", cols.RefsX);
        Label("Col.Graph", cols.GraphX + 6);
        Label("Col.Message", cols.MessageX, cols.MessageW);
        if (cols.ChangesW > 0) Label("Col.Changes", cols.ChangesX, cols.ChangesW);
        if (cols.AuthorW > 0) Label("Col.Author", cols.AuthorX, cols.AuthorW);
        if (cols.DateW > 0) Label("Col.Date", cols.DateX, cols.DateW);
        Label("Col.Sha", cols.ShaX, cols.ShaW);
    }

    private Brush Res(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
