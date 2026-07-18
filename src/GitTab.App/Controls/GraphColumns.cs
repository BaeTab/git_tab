using System.Windows.Media;

namespace GitTab.App.Controls;

/// <summary>
/// Shared, <b>responsive</b> column geometry for the GitLens-style commit graph header + rows.
/// When the panel is narrow, optional columns (Changes, then Author, then Date) are dropped
/// (width 0) so columns never overlap; the Message column always keeps a readable minimum.
/// </summary>
internal readonly struct GraphColumns
{
    public double RefsX { get; init; }
    public double RefsW { get; init; }
    public double GraphX { get; init; }
    public double GraphW { get; init; }
    public double MessageX { get; init; }
    public double MessageW { get; init; }
    public double ChangesX { get; init; }
    public double ChangesW { get; init; }   // 0 = hidden
    public double AuthorX { get; init; }
    public double AuthorW { get; init; }    // 0 = hidden
    public double DateX { get; init; }
    public double DateW { get; init; }      // 0 = hidden
    public double ShaX { get; init; }
    public double ShaW { get; init; }

    public static GraphColumns Compute(double width, int laneCount, double laneWidth)
    {
        const double gap = 10;
        const double graphPad = 16;
        const double shaW = 66;
        const double minMessage = 150;

        double refsW = 150;
        double changesW = 96;
        double authorW = 140;
        double dateW = 90;

        double graphW = Math.Max(1, laneCount) * laneWidth + graphPad;
        graphW = Math.Min(graphW, Math.Max(48, width * 0.40));

        // Space the message column would get with the current optional-column set.
        double MessageWidth() =>
            width - refsW - graphW - shaW
            - (changesW > 0 ? changesW + gap : 0)
            - (authorW > 0 ? authorW + gap : 0)
            - (dateW > 0 ? dateW + gap : 0)
            - 3 * gap;

        // Drop optional columns in priority order until the message fits.
        if (MessageWidth() < minMessage) changesW = 0;
        if (MessageWidth() < minMessage) authorW = 0;
        if (MessageWidth() < minMessage) refsW = Math.Max(96, refsW - (minMessage - MessageWidth()));
        if (MessageWidth() < minMessage) dateW = 0;

        // Right-anchor the trailing columns.
        double right = width - gap;
        double shaX = right - shaW; right = shaX - gap;
        double dateX = 0, authorX = 0, changesX = 0;
        if (dateW > 0) { dateX = right - dateW; right = dateX - gap; }
        if (authorW > 0) { authorX = right - authorW; right = authorX - gap; }
        if (changesW > 0) { changesX = right - changesW; right = changesX - gap; }

        double graphX = refsW;
        double messageX = graphX + graphW + gap;
        double messageW = Math.Max(60, right - messageX);

        return new GraphColumns
        {
            RefsX = 8,
            RefsW = Math.Max(0, refsW - 8),
            GraphX = graphX,
            GraphW = graphW,
            MessageX = messageX,
            MessageW = messageW,
            ChangesX = changesX,
            ChangesW = changesW,
            AuthorX = authorX,
            AuthorW = authorW,
            DateX = dateX,
            DateW = dateW,
            ShaX = shaX,
            ShaW = shaW
        };
    }
}

/// <summary>Deterministic avatar color + initial from an author's name/email.</summary>
internal static class AuthorAvatar
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xE8,0x61,0x5F), Color.FromRgb(0xE0,0x91,0x3C), Color.FromRgb(0xD1,0xA5,0x34),
        Color.FromRgb(0x7C,0xB3,0x42), Color.FromRgb(0x26,0xA6,0x9A), Color.FromRgb(0x4F,0xA3,0xE3),
        Color.FromRgb(0x6C,0x79,0xE8), Color.FromRgb(0xA1,0x6A,0xE0), Color.FromRgb(0xE5,0x6A,0xB3),
        Color.FromRgb(0x8D,0x6E,0x63), Color.FromRgb(0x78,0x92,0x62), Color.FromRgb(0x5C,0x8A,0x9E),
    };

    public static Color ColorFor(string? key)
    {
        key ??= string.Empty;
        uint h = 2166136261;
        unchecked
        {
            foreach (var c in key) { h ^= c; h *= 16777619; }
        }
        return Palette[h % (uint)Palette.Length];
    }

    public static string Initial(string? name)
    {
        if (!string.IsNullOrEmpty(name))
            foreach (var ch in name)
                if (char.IsLetterOrDigit(ch)) return char.ToUpperInvariant(ch).ToString();
        return "?";
    }
}
