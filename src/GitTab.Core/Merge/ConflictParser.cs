using System.Text;

namespace GitTab.Core.Merge;

public enum ConflictChoice { Ours, Theirs, Both, None }

/// <summary>One conflicting region of a file: the "ours" and "theirs" sides, plus the chosen resolution.</summary>
public sealed class ConflictBlock
{
    public required string Ours { get; init; }
    public required string Theirs { get; init; }
    public ConflictChoice Choice { get; set; } = ConflictChoice.Ours;
}

/// <summary>A part of a conflicted file: either literal text or a <see cref="ConflictBlock"/>.</summary>
public sealed class ConflictPart
{
    public string? Text { get; init; }
    public ConflictBlock? Conflict { get; init; }
    public bool IsConflict => Conflict is not null;
}

/// <summary>
/// Parses a file that contains git conflict markers (<c>&lt;&lt;&lt;&lt;&lt;&lt;&lt;</c> /
/// <c>=======</c> / <c>&gt;&gt;&gt;&gt;&gt;&gt;&gt;</c>, and the diff3 <c>|||||||</c> base section)
/// into literal + conflict parts, and rebuilds a resolved file from per-block choices.
/// </summary>
public static class ConflictParser
{
    public static bool HasConflictMarkers(string content)
        => content.Contains("<<<<<<<", StringComparison.Ordinal) &&
           content.Contains(">>>>>>>", StringComparison.Ordinal);

    public static IReadOnlyList<ConflictPart> Parse(string content)
    {
        var parts = new List<ConflictPart>();
        var lines = content.Replace("\r\n", "\n").Split('\n');

        var literal = new StringBuilder();
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.StartsWith("<<<<<<<", StringComparison.Ordinal))
            {
                if (literal.Length > 0) { parts.Add(new ConflictPart { Text = literal.ToString() }); literal.Clear(); }

                var ours = new StringBuilder();
                var theirs = new StringBuilder();
                i++;
                // ours: up to "|||||||" (diff3 base) or "======="
                while (i < lines.Length && !lines[i].StartsWith("|||||||", StringComparison.Ordinal)
                                        && !lines[i].StartsWith("=======", StringComparison.Ordinal))
                    ours.Append(lines[i++]).Append('\n');
                // optional base section: skip up to "======="
                if (i < lines.Length && lines[i].StartsWith("|||||||", StringComparison.Ordinal))
                {
                    i++;
                    while (i < lines.Length && !lines[i].StartsWith("=======", StringComparison.Ordinal)) i++;
                }
                // separator
                if (i < lines.Length && lines[i].StartsWith("=======", StringComparison.Ordinal)) i++;
                // theirs: up to ">>>>>>>"
                while (i < lines.Length && !lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal))
                    theirs.Append(lines[i++]).Append('\n');
                if (i < lines.Length && lines[i].StartsWith(">>>>>>>", StringComparison.Ordinal)) i++;

                parts.Add(new ConflictPart
                {
                    Conflict = new ConflictBlock
                    {
                        Ours = TrimTrailingNewline(ours.ToString()),
                        Theirs = TrimTrailingNewline(theirs.ToString())
                    }
                });
            }
            else
            {
                literal.Append(line);
                if (i < lines.Length - 1) literal.Append('\n');
                i++;
            }
        }
        if (literal.Length > 0) parts.Add(new ConflictPart { Text = literal.ToString() });
        return parts;
    }

    public static string Resolve(IReadOnlyList<ConflictPart> parts)
    {
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Text is not null)
            {
                sb.Append(part.Text);
            }
            else if (part.Conflict is { } c)
            {
                var chosen = c.Choice switch
                {
                    ConflictChoice.Ours => c.Ours,
                    ConflictChoice.Theirs => c.Theirs,
                    ConflictChoice.Both => c.Ours + "\n" + c.Theirs,
                    _ => c.Ours
                };
                sb.Append(chosen).Append('\n');
            }
        }
        return sb.ToString();
    }

    private static string TrimTrailingNewline(string s) => s.EndsWith('\n') ? s[..^1] : s;
}
