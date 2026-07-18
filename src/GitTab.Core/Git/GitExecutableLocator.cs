using System.IO;

namespace GitTab.Core.Git;

/// <summary>
/// Finds a usable git.exe so Git Tab is a self-contained, "no CLI required" tool. A copy of
/// portable git (MinGit) shipped next to the app (<c>&lt;app&gt;\git\cmd\git.exe</c>) is preferred,
/// then well-known install locations, then bare <c>"git"</c> from PATH as a last resort.
/// </summary>
public static class GitExecutableLocator
{
    /// <summary>Resolve the best git executable path. Never null — falls back to "git" (PATH).</summary>
    public static string Resolve()
    {
        foreach (var candidate in Candidates())
        {
            if (File.Exists(candidate)) return candidate;
        }
        return "git";
    }

    private static IEnumerable<string> Candidates()
    {
        var baseDir = AppContext.BaseDirectory;
        // Bundled portable git (MinGit) shipped by the installer.
        yield return Path.Combine(baseDir, "git", "cmd", "git.exe");
        yield return Path.Combine(baseDir, "git", "bin", "git.exe");
        yield return Path.Combine(baseDir, "MinGit", "cmd", "git.exe");

        // Common Git-for-Windows install locations.
        foreach (var env in new[] { "ProgramFiles", "ProgramW6432", "ProgramFiles(x86)", "LOCALAPPDATA" })
        {
            var root = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrEmpty(root)) continue;
            yield return Path.Combine(root, "Git", "cmd", "git.exe");
            yield return Path.Combine(root, "Git", "bin", "git.exe");
            yield return Path.Combine(root, "Programs", "Git", "cmd", "git.exe");
        }
    }
}
