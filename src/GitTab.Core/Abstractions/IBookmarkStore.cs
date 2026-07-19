namespace GitTab.Core.Abstractions;

/// <summary>Persists per-repository "bookmarked" commit SHAs (starred commits you jump back to).</summary>
public interface IBookmarkStore
{
    /// <summary>The bookmarked commit SHAs for <paramref name="repoPath"/>.</summary>
    IReadOnlyCollection<string> Get(string repoPath);

    /// <summary>Toggle a commit's bookmark; returns true if it is now bookmarked.</summary>
    bool Toggle(string repoPath, string sha);
}
