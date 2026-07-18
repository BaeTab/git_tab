using System.Windows.Media;

namespace GitTab.App.Controls;

/// <summary>Lane colors for the commit graph. Chosen to read well on both light and dark themes.</summary>
public static class GraphPalette
{
    public static readonly Color[] Colors =
    {
        Color.FromRgb(0x4C, 0x8D, 0xFF), // blue
        Color.FromRgb(0xE5, 0x53, 0x4B), // red
        Color.FromRgb(0x57, 0xC9, 0x73), // green
        Color.FromRgb(0xE0, 0xA0, 0x30), // amber
        Color.FromRgb(0xB5, 0x7B, 0xEE), // purple
        Color.FromRgb(0x24, 0xC4, 0xC4), // teal
        Color.FromRgb(0xEC, 0x6C, 0xB9), // pink
        Color.FromRgb(0x8C, 0xB3, 0x3A), // olive
        Color.FromRgb(0xF0, 0x88, 0x3E), // orange
        Color.FromRgb(0x6E, 0x7F, 0xE0), // indigo
    };

    public static int Size => Colors.Length;

    public static Color At(int colorIndex) => Colors[((colorIndex % Size) + Size) % Size];
}
