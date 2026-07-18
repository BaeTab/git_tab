using System.Globalization;
using System.Text.RegularExpressions;
using GitTab.Core.Models;

namespace GitTab.Core.Diff;

/// <summary>
/// Parses a single-file unified diff (as emitted by git / libgit2) into a structured
/// <see cref="FileDiff"/> with per-line kinds and line numbers, for colored display.
/// We never compute diffs ourselves — we only parse git's canonical output.
/// </summary>
public static partial class UnifiedDiffParser
{
    [GeneratedRegex(@"^@@ -(?<os>\d+)(?:,(?<oc>\d+))? \+(?<ns>\d+)(?:,(?<nc>\d+))? @@(?<ctx>.*)$")]
    private static partial Regex HunkHeaderRegex();

    public static FileDiff Parse(string path, string? oldPath, string rawPatch, bool isBinary)
    {
        if (isBinary)
        {
            return new FileDiff
            {
                Path = path,
                OldPath = oldPath,
                IsBinary = true,
                RawPatch = rawPatch,
                Hunks = Array.Empty<DiffHunk>()
            };
        }

        var hunks = new List<DiffHunk>();
        List<DiffLine>? currentLines = null;
        DiffHunk? pendingHeader = null;
        int oldLine = 0, newLine = 0;
        int added = 0, removed = 0;
        bool binaryDetected = false;

        // Normalize line endings for splitting; keep content as-is otherwise.
        var lines = rawPatch.Replace("\r\n", "\n").Split('\n');

        void FlushHunk()
        {
            if (pendingHeader is not null && currentLines is not null)
            {
                hunks.Add(new DiffHunk
                {
                    Header = pendingHeader.Header,
                    OldStart = pendingHeader.OldStart,
                    OldCount = pendingHeader.OldCount,
                    NewStart = pendingHeader.NewStart,
                    NewCount = pendingHeader.NewCount,
                    Lines = currentLines
                });
            }
            pendingHeader = null;
            currentLines = null;
        }

        foreach (var raw in lines)
        {
            // Unified-diff content lines always carry a leading marker (' ', '+', '-'),
            // so a zero-length line is only the trailing artifact of splitting on '\n'.
            if (raw.Length == 0)
                continue;

            if (raw.StartsWith("@@", StringComparison.Ordinal))
            {
                FlushHunk();
                var m = HunkHeaderRegex().Match(raw);
                int os = 0, oc = 1, ns = 0, nc = 1;
                if (m.Success)
                {
                    os = ParseInt(m.Groups["os"].Value);
                    oc = m.Groups["oc"].Success ? ParseInt(m.Groups["oc"].Value) : 1;
                    ns = ParseInt(m.Groups["ns"].Value);
                    nc = m.Groups["nc"].Success ? ParseInt(m.Groups["nc"].Value) : 1;
                }
                oldLine = os;
                newLine = ns;
                currentLines = new List<DiffLine>
                {
                    new() { Kind = DiffLineKind.HunkHeader, Text = raw }
                };
                pendingHeader = new DiffHunk { Header = raw, OldStart = os, OldCount = oc, NewStart = ns, NewCount = nc };
                continue;
            }

            if (pendingHeader is null || currentLines is null)
            {
                // Still in the file header region.
                if (raw.StartsWith("Binary files", StringComparison.Ordinal) ||
                    raw.StartsWith("GIT binary patch", StringComparison.Ordinal))
                {
                    binaryDetected = true;
                }
                continue;
            }

            char c = raw[0];
            switch (c)
            {
                case '+':
                    currentLines.Add(new DiffLine { Kind = DiffLineKind.Added, Text = raw[1..], NewLineNumber = newLine++ });
                    added++;
                    break;
                case '-':
                    currentLines.Add(new DiffLine { Kind = DiffLineKind.Removed, Text = raw[1..], OldLineNumber = oldLine++ });
                    removed++;
                    break;
                case ' ':
                    currentLines.Add(new DiffLine { Kind = DiffLineKind.Context, Text = raw[1..], OldLineNumber = oldLine++, NewLineNumber = newLine++ });
                    break;
                case '\\':
                    // "\ No newline at end of file"
                    currentLines.Add(new DiffLine { Kind = DiffLineKind.NoNewline, Text = raw });
                    break;
                default:
                    // Defensive: treat unknown leading char as context.
                    currentLines.Add(new DiffLine { Kind = DiffLineKind.Context, Text = raw });
                    break;
            }
        }

        FlushHunk();

        return new FileDiff
        {
            Path = path,
            OldPath = oldPath,
            IsBinary = binaryDetected,
            RawPatch = rawPatch,
            Hunks = hunks,
            AddedLines = added,
            RemovedLines = removed
        };
    }

    private static int ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
