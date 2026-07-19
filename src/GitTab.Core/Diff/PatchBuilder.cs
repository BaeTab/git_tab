using System.Text;

namespace GitTab.Core.Diff;

/// <summary>
/// Builds a minimal, valid unified-diff patch that stages only a chosen subset of a hunk's
/// changed lines — the surgery behind line-level "stage these lines" (the GUI equivalent of
/// <c>git add -p</c>'s split/edit). Meant to be applied with <c>git apply --cached --recount</c>,
/// so hunk line counts don't need to be recomputed here.
///
/// Transform (staging the selected changes):
///   context ' '        → kept as context
///   '+' selected       → kept (the addition is staged)
///   '+' not selected   → dropped (that addition stays only in the working tree)
///   '-' selected       → kept (the removal is staged)
///   '-' not selected   → turned into context (the line is NOT removed from the index)
/// </summary>
public static class PatchBuilder
{
    /// <summary>
    /// Produces the patch for staging the body lines whose indices are in
    /// <paramref name="selected"/>. Returns null when the selection contains no actual change
    /// (nothing would be staged).
    /// </summary>
    public static string? BuildPartialStagePatch(
        string fileHeader,
        string hunkHeader,
        IReadOnlyList<string> body,
        ISet<int> selected)
    {
        var outLines = new List<string>();
        bool anyChange = false;
        bool prevKept = false;

        for (int i = 0; i < body.Count; i++)
        {
            var line = body[i];
            if (line.Length == 0) { outLines.Add(" "); prevKept = true; continue; }

            char c = line[0];
            switch (c)
            {
                case ' ':
                    outLines.Add(line);
                    prevKept = true;
                    break;
                case '+':
                    if (selected.Contains(i)) { outLines.Add(line); anyChange = true; prevKept = true; }
                    else { prevKept = false; }   // drop unselected addition
                    break;
                case '-':
                    if (selected.Contains(i)) { outLines.Add(line); anyChange = true; prevKept = true; }
                    else { outLines.Add(" " + line.Substring(1)); prevKept = true; } // keep as context
                    break;
                case '\\':
                    // "\ No newline at end of file" — attaches to the line above; keep only if that
                    // line survived.
                    if (prevKept) outLines.Add(line);
                    break;
                default:
                    outLines.Add(line);
                    prevKept = true;
                    break;
            }
        }

        if (!anyChange) return null;

        var sb = new StringBuilder();
        sb.Append(fileHeader.TrimEnd('\n')).Append('\n');
        sb.Append(hunkHeader.TrimEnd('\n')).Append('\n');
        foreach (var l in outLines) sb.Append(l).Append('\n');
        return sb.ToString();
    }
}
