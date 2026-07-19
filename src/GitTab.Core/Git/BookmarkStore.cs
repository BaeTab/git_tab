using System.IO;
using System.Text.Json;
using GitTab.Core.Abstractions;

namespace GitTab.Core.Git;

/// <summary>Stores bookmarked commit SHAs per repository in %AppData%/GitTab/bookmarks.json.</summary>
public sealed class BookmarkStore : IBookmarkStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly object _sync = new();

    public BookmarkStore(string? overrideDirectory = null)
    {
        var dir = overrideDirectory
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitTab");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "bookmarks.json");
    }

    public IReadOnlyCollection<string> Get(string repoPath)
    {
        lock (_sync)
        {
            var all = Load();
            return all.TryGetValue(Normalize(repoPath), out var set) ? set.ToArray() : Array.Empty<string>();
        }
    }

    public bool Toggle(string repoPath, string sha)
    {
        lock (_sync)
        {
            var all = Load();
            var key = Normalize(repoPath);
            if (!all.TryGetValue(key, out var set)) { set = new HashSet<string>(StringComparer.Ordinal); all[key] = set; }

            bool nowOn = set.Add(sha);
            if (!nowOn) set.Remove(sha);
            if (set.Count == 0) all.Remove(key);

            Save(all);
            return nowOn;
        }
    }

    private Dictionary<string, HashSet<string>> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new(StringComparer.Ordinal);
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(_filePath));
            return raw?.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value, StringComparer.Ordinal), StringComparer.Ordinal)
                   ?? new(StringComparer.Ordinal);
        }
        catch { return new(StringComparer.Ordinal); }
    }

    private void Save(Dictionary<string, HashSet<string>> all)
    {
        try
        {
            var raw = all.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            File.WriteAllText(_filePath, JsonSerializer.Serialize(raw, JsonOptions));
        }
        catch { /* bookmarks are best-effort */ }
    }

    private static string Normalize(string p) => p.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
}
