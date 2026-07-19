using GitTab.Core.Models;

namespace GitTab.Core.Diff;

/// <summary>
/// Detects "moved" code inside a single file's diff — a run of removed lines whose exact text also
/// appears as a run of added lines elsewhere in the same diff (git's <c>--color-moved</c>). Such lines
/// weren't really deleted/created, just relocated, so the UI can tint them differently from genuine
/// additions/removals. Pure and side-effect free, so it's easy to unit-test.
/// </summary>
public static class MovedLineDetector
{
    /// <summary>
    /// Returns the set of indices (into the parallel <paramref name="kinds"/>/<paramref name="texts"/>
    /// lists, in document order) that belong to a moved block — both the removed side and the added
    /// side. Only contiguous runs of at least <paramref name="minBlock"/> identical, non-blank lines
    /// count, which keeps trivial matches (a lone <c>}</c>, blank lines) from lighting up.
    /// </summary>
    public static HashSet<int> Detect(
        IReadOnlyList<DiffLineKind> kinds,
        IReadOnlyList<string> texts,
        int minBlock = 3)
    {
        var moved = new HashSet<int>();
        if (kinds.Count != texts.Count) return moved;

        var removed = new List<int>();
        var added = new List<int>();
        for (int i = 0; i < kinds.Count; i++)
        {
            if (kinds[i] == DiffLineKind.Removed) removed.Add(i);
            else if (kinds[i] == DiffLineKind.Added) added.Add(i);
        }
        if (removed.Count < minBlock || added.Count < minBlock) return moved;

        // Index every non-blank added line by its text → its positions within `added`.
        var addedByText = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int j = 0; j < added.Count; j++)
        {
            var t = texts[added[j]];
            if (t.Trim().Length == 0) continue;
            if (!addedByText.TryGetValue(t, out var list)) addedByText[t] = list = new List<int>();
            list.Add(j);
        }

        var usedAdded = new bool[added.Count];
        int r = 0;
        while (r < removed.Count)
        {
            var startText = texts[removed[r]];
            var matched = false;
            if (startText.Trim().Length > 0 && addedByText.TryGetValue(startText, out var candidates))
            {
                foreach (var a in candidates)
                {
                    if (usedAdded[a]) continue;
                    int len = 0;
                    while (r + len < removed.Count && a + len < added.Count
                           && !usedAdded[a + len]
                           && texts[removed[r + len]] == texts[added[a + len]])
                        len++;

                    if (len >= minBlock)
                    {
                        for (int k = 0; k < len; k++)
                        {
                            moved.Add(removed[r + k]);
                            moved.Add(added[a + k]);
                            usedAdded[a + k] = true;
                        }
                        r += len;
                        matched = true;
                        break;
                    }
                }
            }
            if (!matched) r++;
        }

        return moved;
    }
}
