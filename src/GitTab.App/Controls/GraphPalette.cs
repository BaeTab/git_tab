using System.Windows.Media;

namespace GitTab.App.Controls;

/// <summary>Lane colors for the commit graph. Chosen to read well on both light and dark themes.</summary>
public static class GraphPalette
{
    public static readonly Color[] Colors =
    {
        Color.FromRgb(0x5A, 0x8C, 0xFF), // blue
        Color.FromRgb(0x1F, 0xD1, 0xC6), // teal
        Color.FromRgb(0xB8, 0x8A, 0xFF), // violet
        Color.FromRgb(0xFF, 0xB4, 0x3C), // amber
        Color.FromRgb(0xFF, 0x6E, 0xC7), // pink
        Color.FromRgb(0x4F, 0xD3, 0x7B), // green
        Color.FromRgb(0xFF, 0x8A, 0x5B), // orange
        Color.FromRgb(0x7C, 0x88, 0xFF), // indigo
        Color.FromRgb(0xFF, 0x6B, 0x6B), // red
        Color.FromRgb(0xA6, 0xD8, 0x4A), // lime
    };

    public static int Size => Colors.Length;
}
