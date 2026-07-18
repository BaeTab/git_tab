using Braid.Core.Models;

namespace Braid.Core.Abstractions;

/// <summary>Persists the list of recently-opened repositories (JSON under %AppData%).</summary>
public interface IRecentRepositoriesStore
{
    IReadOnlyList<RecentRepository> GetAll();

    /// <summary>Records <paramref name="path"/> as most-recently-opened (dedupes, caps the list).</summary>
    void Add(string path);

    void Remove(string path);

    void Clear();
}
