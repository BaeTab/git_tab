using System;
using System.Collections.Generic;
using System.Windows;
using GitTab.Core.Models;

namespace GitTab.App.Views;

/// <summary>
/// Read-only window that shows a per-line "git blame" annotation for a single file:
/// line number, commit, author, date and content, with consecutive lines from the
/// same commit visually grouped via an alternating row background.
/// </summary>
public partial class BlameView : Window
{
    public string FilePath { get; }

    public IReadOnlyList<BlameRow> Rows { get; }

    public BlameView(string filePath, IReadOnlyList<BlameLine> lines)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(lines);

        InitializeComponent();

        FilePath = filePath;
        Rows = BuildRows(lines);
        DataContext = this;

        Title = "Blame — " + filePath;
    }

    private static IReadOnlyList<BlameRow> BuildRows(IReadOnlyList<BlameLine> lines)
    {
        var rows = new List<BlameRow>(lines.Count);
        var band = false;
        string? previousSha = null;

        foreach (var line in lines)
        {
            if (previousSha is not null && !string.Equals(previousSha, line.Sha, StringComparison.Ordinal))
            {
                band = !band;
            }

            rows.Add(new BlameRow(line, band));
            previousSha = line.Sha;
        }

        return rows;
    }

    /// <summary>Row view-model: pairs a <see cref="BlameLine"/> with the alternating "same commit" band flag.</summary>
    public sealed class BlameRow
    {
        public BlameRow(BlameLine line, bool band)
        {
            Line = line;
            Band = band;
        }

        public BlameLine Line { get; }

        public bool Band { get; }
    }
}
