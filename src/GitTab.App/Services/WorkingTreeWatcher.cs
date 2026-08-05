using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

/// <summary>
/// Watches an open repository for on-disk changes so the UI refreshes itself instead of the user
/// pressing F5 after every save in their editor.
///
/// Two signals are raised, because they cost very different amounts to service:
/// <see cref="WorkingTreeChanged"/> for edits under the working directory (a status-only refresh is
/// enough) and <see cref="GitDirChanged"/> for HEAD/index/ref changes made outside the app — a
/// commit or checkout from a terminal — which move history and need a full reload.
///
/// Events are coalesced: bursts (a build, a branch switch touching thousands of files) collapse into
/// a single refresh, and the wait is capped so a long-running write storm still refreshes eventually.
/// </summary>
public sealed class WorkingTreeWatcher : IDisposable
{
    // Long enough that a save-all in an editor is one refresh, short enough to feel immediate.
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    // Upper bound on how long a continuous stream of events may postpone the refresh.
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(3);

    // How long to ignore the disk after the app's own git command, so our writes don't bounce back
    // as a second refresh. Must exceed Debounce.
    private static readonly TimeSpan SelfWriteGrace = TimeSpan.FromMilliseconds(1200);

    private readonly ILogger _logger;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly object _gate = new();

    private FileSystemWatcher? _workWatcher;
    private FileSystemWatcher? _gitWatcher;

    private string? _gitDirFullPath;
    private bool _workPending;
    private bool _gitPending;
    private DateTime _firstPendingUtc = DateTime.MinValue;
    private DateTime _ignoreUntilUtc = DateTime.MinValue;
    private bool _disposed;

    public WorkingTreeWatcher(ILogger logger)
    {
        _logger = logger;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = Debounce };
        _timer.Tick += (_, _) => Fire();
    }

    /// <summary>Files under the working directory changed; the staged/unstaged lists are stale.</summary>
    public event Action? WorkingTreeChanged;

    /// <summary>HEAD, the index or a ref changed (or the watcher lost events); reload everything.</summary>
    public event Action? GitDirChanged;

    /// <summary>Turns watching off without tearing the watchers down (Settings toggle).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Start watching a repository, replacing any previous one. A failure here is not fatal — the
    /// app simply keeps its manual refresh — so it is logged and swallowed.
    /// </summary>
    public void Watch(string workingDirectory, string? gitDirectory)
    {
        Stop();
        if (_disposed || string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return;

        try
        {
            _gitDirFullPath = ResolveGitDir(workingDirectory, gitDirectory);

            _workWatcher = Create(workingDirectory, OnWorkingTreeEvent);

            // A linked git dir (worktree/submodule) lives outside the working directory, so it needs
            // its own watcher; a normal .git is already covered but is filtered out of the working
            // watcher, so watch it separately either way.
            if (_gitDirFullPath is not null && Directory.Exists(_gitDirFullPath))
                _gitWatcher = Create(_gitDirFullPath, OnGitDirEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-refresh watcher could not start for {Path}", workingDirectory);
            Stop();
        }
    }

    /// <summary>
    /// Ignore the disk briefly. Called after the app runs its own git command so the writes it just
    /// made — which it has already refreshed for — don't trigger a redundant second refresh.
    /// </summary>
    public void IgnoreSelfWrites()
    {
        lock (_gate)
        {
            _ignoreUntilUtc = DateTime.UtcNow + SelfWriteGrace;
            _workPending = false;
            _gitPending = false;
            _firstPendingUtc = DateTime.MinValue;
        }
    }

    public void Stop()
    {
        _timer.Stop();
        Dispose(ref _workWatcher);
        Dispose(ref _gitWatcher);
        lock (_gate)
        {
            _workPending = false;
            _gitPending = false;
            _firstPendingUtc = DateTime.MinValue;
        }
    }

    private FileSystemWatcher Create(string path, FileSystemEventHandler handler)
    {
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            // The default 8 KB buffer overflows easily on a big checkout; overflow means lost events.
            InternalBufferSize = 64 * 1024,
        };
        watcher.Created += handler;
        watcher.Changed += handler;
        watcher.Deleted += handler;
        watcher.Renamed += (s, e) => handler(s, e);
        watcher.Error += OnWatcherError;
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private void OnWorkingTreeEvent(object sender, FileSystemEventArgs e)
    {
        // The git dir sits inside the working directory in the normal case; it has its own watcher
        // with its own filtering, so drop it here to avoid double-reporting object/lock churn.
        if (IsInsideGitDir(e.FullPath)) return;
        Signal(gitDir: false);
    }

    private void OnGitDirEvent(object sender, FileSystemEventArgs e)
    {
        if (IsNoisyGitPath(e.FullPath)) return;
        Signal(gitDir: true);
    }

    // A buffer overflow means changes were missed, so the safe response is a full reload.
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogDebug(e.GetException(), "Auto-refresh watcher dropped events; forcing a full reload");
        Signal(gitDir: true);
    }

    private void Signal(bool gitDir)
    {
        if (!Enabled) return;

        lock (_gate)
        {
            if (DateTime.UtcNow < _ignoreUntilUtc) return;
            if (gitDir) _gitPending = true; else _workPending = true;
            if (_firstPendingUtc == DateTime.MinValue) _firstPendingUtc = DateTime.UtcNow;
        }

        // Watcher callbacks arrive on a thread-pool thread; the timer and the handlers are UI-bound.
        _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Restart));
    }

    private void Restart()
    {
        DateTime first;
        lock (_gate) first = _firstPendingUtc;

        // Cap the debounce: a continuous stream of writes would otherwise keep pushing it out.
        if (first != DateTime.MinValue && DateTime.UtcNow - first >= MaxDelay)
        {
            Fire();
            return;
        }

        _timer.Stop();
        _timer.Start();
    }

    private void Fire()
    {
        _timer.Stop();

        bool work, git;
        lock (_gate)
        {
            if (DateTime.UtcNow < _ignoreUntilUtc) { _workPending = _gitPending = false; return; }
            work = _workPending;
            git = _gitPending;
            _workPending = _gitPending = false;
            _firstPendingUtc = DateTime.MinValue;
        }

        if (!Enabled) return;

        // A git-dir change subsumes a working-tree change: the full reload refreshes status too.
        if (git) GitDirChanged?.Invoke();
        else if (work) WorkingTreeChanged?.Invoke();
    }

    private bool IsInsideGitDir(string fullPath)
    {
        if (_gitDirFullPath is null) return false;
        return fullPath.StartsWith(_gitDirFullPath, StringComparison.OrdinalIgnoreCase);
    }

    // Object files, logs and lock files change constantly and never on their own mean the UI is
    // stale — the ref/HEAD/index write that follows them is what matters.
    private static bool IsNoisyGitPath(string fullPath)
    {
        if (fullPath.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)) return true;

        var name = Path.GetFileName(fullPath);
        if (name.Equals("COMMIT_EDITMSG", StringComparison.OrdinalIgnoreCase)) return true;

        var normalized = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.Contains($"{Path.DirectorySeparatorChar}objects{Path.DirectorySeparatorChar}",
                   StringComparison.OrdinalIgnoreCase)
            || normalized.Contains($"{Path.DirectorySeparatorChar}logs{Path.DirectorySeparatorChar}",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveGitDir(string workingDirectory, string? gitDirectory)
    {
        var candidate = string.IsNullOrWhiteSpace(gitDirectory)
            ? Path.Combine(workingDirectory, ".git")
            : gitDirectory;

        try { candidate = Path.GetFullPath(candidate); }
        catch (Exception) { return null; }

        return candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void Dispose(ref FileSystemWatcher? watcher)
    {
        if (watcher is null) return;
        try
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        catch (Exception)
        {
            // A watcher whose directory vanished throws on teardown; nothing useful to do.
        }
        watcher = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
