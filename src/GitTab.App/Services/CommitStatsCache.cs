using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;

namespace GitTab.App.Services;

public interface ICommitStatsSource
{
    bool TryGet(string sha, out CommitStats stats);
    void Request(string sha);
    void Clear();
    event EventHandler? StatsUpdated;
}

/// <summary>Lazily computes per-commit change stats on background threads and caches them, so the
/// graph's Changes column stays responsive on huge histories (only visible rows are requested).</summary>
public sealed class CommitStatsCache : ICommitStatsSource
{
    private readonly IRepositoryService _repo;
    private readonly Dispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, CommitStats> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);
    private volatile bool _updateScheduled;

    public CommitStatsCache(IRepositoryService repo)
    {
        _repo = repo;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public event EventHandler? StatsUpdated;

    public bool TryGet(string sha, out CommitStats stats) => _cache.TryGetValue(sha, out stats!);

    public void Request(string sha)
    {
        if (string.IsNullOrEmpty(sha) || _cache.ContainsKey(sha) || !_pending.TryAdd(sha, 0)) return;
        Task.Run(() =>
        {
            try { _cache[sha] = _repo.GetCommitStats(sha); }
            catch { /* commit gone / repo closed — ignore */ }
            finally { _pending.TryRemove(sha, out _); ScheduleUpdate(); }
        });
    }

    public void Clear()
    {
        _cache.Clear();
        _pending.Clear();
    }

    private void ScheduleUpdate()
    {
        if (_updateScheduled) return;
        _updateScheduled = true;
        _dispatcher.BeginInvoke(() =>
        {
            _updateScheduled = false;
            StatsUpdated?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Background);
    }
}
