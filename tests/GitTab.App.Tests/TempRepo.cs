using System;
using System.IO;
using LibGit2Sharp;

namespace GitTab.App.Tests;

/// <summary>
/// Builds a throwaway on-disk git repository (via LibGit2Sharp) so the app-layer session
/// integration tests can exercise the real <c>RepositorySessionFactory</c> / <c>RepositoryService</c>
/// against actual git data. Best-effort cleanup on dispose; a session that still holds the repo open
/// may keep the folder locked, which is harmless for temp dirs.
/// </summary>
public sealed class TempRepo : IDisposable
{
    private static readonly Signature Sig = new("GitTab AppTest", "apptest@gittab.local", DateTimeOffset.UtcNow);

    public string Path { get; }

    private TempRepo(string path) => Path = path;

    public static TempRepo CreateEmpty()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gittab-apptest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Repository.Init(root);
        // Repo-local identity so git operations work even on machines with no global user.name/email.
        using var repo = new Repository(root);
        repo.Config.Set("user.name", "GitTab AppTest", ConfigurationLevel.Local);
        repo.Config.Set("user.email", "apptest@gittab.local", ConfigurationLevel.Local);
        return new TempRepo(root);
    }

    /// <summary>Stage <paramref name="fileName"/> with <paramref name="content"/> and commit it.</summary>
    public string Commit(string message, string fileName, string content)
    {
        var full = System.IO.Path.Combine(Path, fileName);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        using var repo = new Repository(Path);
        Commands.Stage(repo, fileName);
        return repo.Commit(message, Sig, Sig).Sha;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best effort — leftover temp dirs are harmless.
        }
    }
}
