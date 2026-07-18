using System.Text.Json;
using Braid.Core.Abstractions;
using Braid.Core.Models;
using Microsoft.Extensions.Logging;

namespace Braid.Core.Git;

/// <summary>Persists recently-opened repositories to %AppData%/Braid/recent-repos.json.</summary>
public sealed class RecentRepositoriesStore : IRecentRepositoriesStore
{
    private const int MaxEntries = 15;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<RecentRepositoriesStore> _logger;
    private readonly string _filePath;
    private readonly object _sync = new();

    public RecentRepositoriesStore(ILogger<RecentRepositoriesStore> logger, string? overrideDirectory = null)
    {
        _logger = logger;
        var dir = overrideDirectory
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Braid");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "recent-repos.json");
    }

    public IReadOnlyList<RecentRepository> GetAll()
    {
        lock (_sync)
        {
            return Load();
        }
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = Normalize(path);
        var name = SafeName(normalized);

        lock (_sync)
        {
            var list = Load()
                .Where(r => !PathEquals(r.Path, normalized))
                .ToList();

            list.Insert(0, new RecentRepository
            {
                Path = normalized,
                Name = name,
                LastOpenedUtc = DateTimeOffset.UtcNow
            });

            if (list.Count > MaxEntries)
                list = list.Take(MaxEntries).ToList();

            Save(list);
        }
    }

    public void Remove(string path)
    {
        var normalized = Normalize(path);
        lock (_sync)
        {
            var list = Load().Where(r => !PathEquals(r.Path, normalized)).ToList();
            Save(list);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            Save(new List<RecentRepository>());
        }
    }

    private List<RecentRepository> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new List<RecentRepository>();
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<RecentRepository>();
            return JsonSerializer.Deserialize<List<RecentRepository>>(json, JsonOptions)
                   ?? new List<RecentRepository>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read recent repositories from {Path}", _filePath);
            return new List<RecentRepository>();
        }
    }

    private void Save(List<RecentRepository> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write recent repositories to {Path}", _filePath);
        }
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string SafeName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private static bool PathEquals(string a, string b)
        => string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}
