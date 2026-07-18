using LibGit2Sharp;

namespace Braid.Core.Tests;

/// <summary>
/// Builds a throwaway git repository on disk using LibGit2Sharp, so Core read adapters can be
/// exercised against real git data without shelling out. Best-effort cleanup on dispose.
/// </summary>
public sealed class TestRepository : IDisposable
{
    private static readonly Signature Sig = new("Braid Test", "test@braid.local", DateTimeOffset.UtcNow);

    public string Path { get; }

    private TestRepository(string path) => Path = path;

    public static TestRepository CreateEmpty()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "braid-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Repository.Init(root);
        return new TestRepository(root);
    }

    public string Commit(string message, string fileName, string content)
    {
        var full = System.IO.Path.Combine(Path, fileName);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        using var repo = new Repository(Path);
        Commands.Stage(repo, fileName);
        var c = repo.Commit(message, Sig, Sig);
        return c.Sha;
    }

    public void CreateBranch(string name, bool checkout = false)
    {
        using var repo = new Repository(Path);
        var branch = repo.CreateBranch(name);
        if (checkout) Commands.Checkout(repo, branch);
    }

    public void Checkout(string name)
    {
        using var repo = new Repository(Path);
        Commands.Checkout(repo, repo.Branches[name]);
    }

    /// <summary>Friendly name of the currently checked-out branch (e.g. "main" or "master").</summary>
    public string CurrentBranch
    {
        get { using var repo = new Repository(Path); return repo.Head.FriendlyName; }
    }

    public string Merge(string branchName, string mergedByMessage)
    {
        using var repo = new Repository(Path);
        var result = repo.Merge(repo.Branches[branchName], Sig,
            new MergeOptions { CommitOnSuccess = true, FastForwardStrategy = FastForwardStrategy.NoFastForward });
        return result.Commit?.Sha ?? repo.Head.Tip.Sha;
    }

    public void Tag(string name) { using var repo = new Repository(Path); repo.ApplyTag(name); }

    public void WriteFile(string fileName, string content)
        => File.WriteAllText(System.IO.Path.Combine(Path, fileName), content);

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
