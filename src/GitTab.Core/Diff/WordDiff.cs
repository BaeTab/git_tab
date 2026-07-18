namespace GitTab.Core.Diff;

/// <summary>
/// Intra-line word-level diff. Given a removed line and the added line that replaced it, returns
/// the character ranges that actually changed on each side, so the UI can highlight the changed
/// words within a modified line instead of painting the whole row. Uses a token LCS so common
/// runs (identifiers, punctuation, whitespace) stay unhighlighted.
/// </summary>
public static class WordDiff
{
    /// <summary>A contiguous [Start, Start+Length) character range within a line.</summary>
    public readonly record struct Segment(int Start, int Length);

    /// <summary>Above this many tokens on either side we skip word diffing (the O(n*m) LCS would
    /// be too costly and the row background already signals the change).</summary>
    private const int MaxTokens = 600;

    /// <summary>
    /// Returns the changed ranges in <paramref name="oldText"/> (removed words) and in
    /// <paramref name="newText"/> (added words). Both empty when the lines are identical, or when
    /// either line is too long to diff cheaply.
    /// </summary>
    public static (IReadOnlyList<Segment> Old, IReadOnlyList<Segment> New) Compute(string oldText, string newText)
    {
        if (oldText == newText) return (Array.Empty<Segment>(), Array.Empty<Segment>());

        var a = Tokenize(oldText);
        var b = Tokenize(newText);
        if (a.Count == 0 || b.Count == 0 || a.Count > MaxTokens || b.Count > MaxTokens)
            return (Array.Empty<Segment>(), Array.Empty<Segment>());

        int n = a.Count, m = b.Count;

        // LCS length table (suffix form) so backtracking walks front-to-back.
        var dp = new int[n + 1, m + 1];
        for (int x = n - 1; x >= 0; x--)
            for (int y = m - 1; y >= 0; y--)
                dp[x, y] = a[x].Text == b[y].Text
                    ? dp[x + 1, y + 1] + 1
                    : Math.Max(dp[x + 1, y], dp[x, y + 1]);

        var matchedA = new bool[n];
        var matchedB = new bool[m];
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (a[i].Text == b[j].Text) { matchedA[i] = matchedB[j] = true; i++; j++; }
            else if (dp[i + 1, j] >= dp[i, j + 1]) i++;
            else j++;
        }

        var oldSegs = BuildSegments(a, matchedA);
        var newSegs = BuildSegments(b, matchedB);
        return (oldSegs, newSegs);
    }

    private readonly record struct Token(string Text, int Start);

    /// <summary>Word runs (letters/digits/underscore) and whitespace runs each collapse into one
    /// token; every other character is its own token, matching how developers read edits.</summary>
    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < s.Length)
        {
            int start = i;
            char c = s[i];
            if (IsWord(c))
                while (i < s.Length && IsWord(s[i])) i++;
            else if (char.IsWhiteSpace(c))
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            else
                i++;
            tokens.Add(new Token(s.Substring(start, i - start), start));
        }
        return tokens;
    }

    private static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static List<Segment> BuildSegments(List<Token> tokens, bool[] matched)
    {
        var segs = new List<Segment>();
        int k = 0;
        while (k < tokens.Count)
        {
            if (matched[k]) { k++; continue; }
            int startChar = tokens[k].Start;
            int endChar = startChar + tokens[k].Text.Length;
            k++;
            while (k < tokens.Count && !matched[k])
            {
                endChar = tokens[k].Start + tokens[k].Text.Length;
                k++;
            }
            segs.Add(new Segment(startChar, endChar - startChar));
        }
        return segs;
    }
}
