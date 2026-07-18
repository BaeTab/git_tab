using System.Text;
using GitTab.Graph.Models;

namespace GitTab.Graph.Cli;

/// <summary>
/// Renders a <see cref="GraphLayout"/> as an ASCII/box-drawing graph for eyeball validation
/// against `git log --graph`. Each commit is one node line plus a connector line describing the
/// edges leaving it toward the next row.
/// </summary>
public static class AsciiGraphRenderer
{
    public static string Render(GraphLayout layout, IReadOnlyList<(string Sha, string Summary)> meta)
    {
        var sb = new StringBuilder();
        int cells = Math.Max(layout.LaneCount, 1) * 2;

        for (int i = 0; i < layout.Rows.Count; i++)
        {
            var row = layout.Rows[i];

            // --- node line
            var nodeLine = new char[cells];
            Array.Fill(nodeLine, ' ');
            foreach (var seg in row.PassingLanes)
                if (seg.Kind == LaneKind.Straight)
                    nodeLine[seg.FromLane * 2] = '│';
            nodeLine[row.NodeLane * 2] = '●';

            var (sha, summary) = i < meta.Count ? meta[i] : (row.CommitSha, string.Empty);
            var shortSha = sha.Length >= 7 ? sha[..7] : sha;
            sb.Append(new string(nodeLine).TrimEnd());
            sb.Append("  ").Append(shortSha).Append("  ").Append(summary).Append('\n');

            // --- connector line (edges leaving this row downward)
            var conn = new char[cells];
            Array.Fill(conn, ' ');
            foreach (var seg in row.PassingLanes)
            {
                if (seg.Kind == LaneKind.Straight)
                {
                    Put(conn, seg.FromLane * 2, '│');
                }
                else if (seg.Kind == LaneKind.Branch)
                {
                    int from = row.NodeLane * 2;
                    int to = seg.ToLane * 2;
                    if (to == from) Put(conn, from, '│');
                    else if (to > from) { Put(conn, from, '├'); for (int c = from + 1; c < to; c++) Put(conn, c, '─'); Put(conn, to, '╮'); }
                    else { Put(conn, to, '╭'); for (int c = to + 1; c < from; c++) Put(conn, c, '─'); Put(conn, from, '┤'); }
                }
            }
            var connStr = new string(conn).TrimEnd();
            if (connStr.Length > 0 && i < layout.Rows.Count - 1)
                sb.Append(connStr).Append('\n');
        }

        return sb.ToString();
    }

    // Prefer connective chars over plain bars when cells overlap.
    private static void Put(char[] buffer, int index, char ch)
    {
        if (index < 0 || index >= buffer.Length) return;
        char cur = buffer[index];
        if (cur == ' ')
        {
            buffer[index] = ch;
            return;
        }
        if (cur == '│' && ch is '─' or '├' or '┤' or '╮' or '╭') buffer[index] = '┼';
        else if (ch == '│' && cur is '─') buffer[index] = '┼';
        else buffer[index] = ch;
    }
}
