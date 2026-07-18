using GitTab.Core.Models;

namespace GitTab.Core.Abstractions;

/// <summary>Persists the list of recently-opened repositories (JSON under %AppData%).</summary>
public interface IRecentRepositoriesStore
{
    IReadOnlyList<RecentRepository> GetAll();

    /// <summary>Records <paramref name="path"/> as most-recently-opened (dedupes, caps the list).</summary>
    void Add(string path);
}
