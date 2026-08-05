using System.Text;
using System.Text.RegularExpressions;

namespace GitTab.Core.Git;

public enum DoctorFindingKind
{
    /// <summary>Content that looks like a credential.</summary>
    Secret,

    /// <summary>A blob big enough to bloat the repository permanently.</summary>
    LargeFile,

    /// <summary>A path that is almost always build output or a local secret file.</summary>
    ShouldBeIgnored
}

/// <summary>One thing worth telling the user about before they commit.</summary>
public sealed class DoctorFinding
{
    public required string Path { get; init; }
    public required DoctorFindingKind Kind { get; init; }

    /// <summary>Short human-readable reason ("AWS access key", "18.4 MB", ".env file").</summary>
    public required string Detail { get; init; }

    /// <summary>1-based line the match was found on, when the finding came from file content.</summary>
    public int? Line { get; init; }
}

/// <summary>
/// Pre-commit checks over the content that is about to be committed: credentials pasted into a
/// file, blobs large enough to bloat the repository forever, and paths that are nearly always build
/// output or local-only secrets.
///
/// Everything here is advisory. Git makes all three mistakes expensive to undo once they are in
/// history — rewriting history for a leaked key, or carrying a 200 MB blob in every clone — so the
/// point is to catch them at the one moment they are still cheap to fix.
/// </summary>
public static class RepoDoctor
{
    /// <summary>Warn above this size; git hosts start complaining well before their hard limits.</summary>
    public const long LargeFileBytes = 10L * 1024 * 1024;

    /// <summary>Never regex-scan more than this; big files are checked for size only.</summary>
    public const long MaxScanBytes = 2L * 1024 * 1024;

    // Bytes inspected when deciding whether a blob is binary.
    private const int BinarySniffBytes = 8000;

    private const RegexOptions Opts = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    // Ordered most-specific first: a hit on a vendor-shaped token is worth far more than the
    // generic "something called password is assigned a string" rule, which is the noisiest.
    private static readonly (Regex Pattern, string Detail)[] SecretRules =
    {
        (new Regex(@"-----BEGIN (?:RSA |EC |DSA |OPENSSH |PGP )?PRIVATE KEY-----", Opts), "private key"),
        (new Regex(@"\bAKIA[0-9A-Z]{16}\b", Opts), "AWS access key ID"),
        (new Regex(@"\bghp_[A-Za-z0-9]{36}\b", Opts), "GitHub personal access token"),
        (new Regex(@"\bgithub_pat_[A-Za-z0-9_]{22,}", Opts), "GitHub fine-grained token"),
        (new Regex(@"\bgh[osur]_[A-Za-z0-9]{36}\b", Opts), "GitHub token"),
        (new Regex(@"\bglpat-[A-Za-z0-9_\-]{20}\b", Opts), "GitLab personal access token"),
        (new Regex(@"\bxox[baprs]-[A-Za-z0-9\-]{10,}", Opts), "Slack token"),
        (new Regex(@"\bAIza[0-9A-Za-z_\-]{35}\b", Opts), "Google API key"),
        (new Regex(@"\bsk-[A-Za-z0-9]{32,}\b", Opts), "API secret key"),
        (new Regex(@"\beyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}", Opts), "JWT"),
        (new Regex(@"(?i)\baws_secret_access_key\b\s*[:=]\s*\S{20,}", Opts), "AWS secret access key"),
        (new Regex(@"(?i)\b(?:password|passwd|secret|api[_-]?key|auth[_-]?token|access[_-]?token)\b\s*[:=]\s*[""'][^""'\s]{8,}[""']",
            Opts), "hard-coded credential"),
    };

    // Extensions and file names that are local-only or build output essentially every time.
    private static readonly (string Suffix, string Detail)[] IgnorablePathRules =
    {
        (".pem", "private key file"),
        (".jks", "Java keystore"),
        (".keystore", "keystore"),
        (".pfx", "certificate with private key"),
        (".p12", "certificate with private key"),
        (".suo", "Visual Studio user state"),
        (".user", "IDE user settings"),
    };

    private static readonly (string Name, string Detail)[] IgnorableNameRules =
    {
        (".env", "environment file (usually holds secrets)"),
        (".env.local", "environment file (usually holds secrets)"),
        ("id_rsa", "SSH private key"),
        ("id_ed25519", "SSH private key"),
        ("keystore.properties", "signing credentials"),
        ("secrets.json", "secrets file"),
    };

    // Directory segments that mean build output or dependencies rather than source.
    private static readonly (string Segment, string Detail)[] IgnorableDirRules =
    {
        ("node_modules", "dependency directory"),
        (".vs", "Visual Studio cache"),
        ("__pycache__", "Python bytecode cache"),
    };

    /// <summary>
    /// Inspect one blob that is about to be committed. <paramref name="content"/> may be null when
    /// the blob was too big to read, in which case only the size and path rules apply.
    /// </summary>
    public static IReadOnlyList<DoctorFinding> Inspect(string path, long sizeBytes, byte[]? content)
    {
        var findings = new List<DoctorFinding>();

        if (sizeBytes >= LargeFileBytes)
        {
            findings.Add(new DoctorFinding
            {
                Path = path,
                Kind = DoctorFindingKind.LargeFile,
                Detail = FormatSize(sizeBytes)
            });
        }

        if (MatchPathRule(path) is { } pathDetail)
        {
            findings.Add(new DoctorFinding
            {
                Path = path,
                Kind = DoctorFindingKind.ShouldBeIgnored,
                Detail = pathDetail
            });
        }

        if (content is { Length: > 0 } && content.LongLength <= MaxScanBytes && !LooksBinary(content))
            findings.AddRange(ScanForSecrets(path, DecodeText(content)));

        return findings;
    }

    /// <summary>Credential-shaped content in a text blob, at most one finding per rule.</summary>
    public static IReadOnlyList<DoctorFinding> ScanForSecrets(string path, string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<DoctorFinding>();

        var findings = new List<DoctorFinding>();
        foreach (var (pattern, detail) in SecretRules)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;

            findings.Add(new DoctorFinding
            {
                Path = path,
                Kind = DoctorFindingKind.Secret,
                Detail = detail,
                Line = LineOf(text, match.Index)
            });
        }
        return findings;
    }

    /// <summary>The reason this path is usually not meant to be committed, or null.</summary>
    public static string? MatchPathRule(string path)
    {
        var normalized = path.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..];

        foreach (var (ruleName, detail) in IgnorableNameRules)
            if (name.Equals(ruleName, StringComparison.OrdinalIgnoreCase)) return detail;

        foreach (var (suffix, detail) in IgnorablePathRules)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return detail;

        // Only directory segments count — a source file named "node_modules.ts" is fine.
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
            foreach (var (ruleSegment, detail) in IgnorableDirRules)
                if (segments[i].Equals(ruleSegment, StringComparison.OrdinalIgnoreCase)) return detail;

        return null;
    }

    /// <summary>Git's own heuristic: a NUL byte near the start means binary.</summary>
    public static bool LooksBinary(byte[] content)
    {
        var limit = Math.Min(content.Length, BinarySniffBytes);
        for (var i = 0; i < limit; i++)
            if (content[i] == 0) return true;
        return false;
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (double)(1024L * 1024 * 1024):0.#} GB",
        >= 1024L * 1024 => $"{bytes / (double)(1024L * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B"
    };

    private static string DecodeText(byte[] content)
    {
        // Strip a UTF-8 BOM so an offset-0 match still reports line 1.
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);
        return Encoding.UTF8.GetString(content);
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
            if (text[i] == '\n') line++;
        return line;
    }
}
