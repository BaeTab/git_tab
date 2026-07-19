using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using GitTab.Graph;
using GitTab.Graph.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// All live state and actions for a single open repository. One instance per repository tab, each
/// with its own <see cref="IRepositoryService"/> and Working-copy/Branches/Details sub-view-models,
/// so every tab keeps fully independent state (scroll position, selection, search, loaded pages,
/// in-flight network operations). The app-shell <see cref="MainViewModel"/> owns a collection of
/// these and swaps the active one.
/// </summary>
public sealed partial class RepositorySessionViewModel : ObservableObject, IDisposable
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ICredentialStore _credentials;
    private readonly Services.Hosting.IHostingClient _hosting;
    private readonly IBookmarkStore _bookmarkStore;
    private readonly ILogger<RepositorySessionViewModel> _logger;
    private readonly GraphLayoutEngine _engine = new();

    private HashSet<string> _bookmarks = new(StringComparer.Ordinal);

    private IReadOnlyList<CommitInfo> _allCommits = Array.Empty<CommitInfo>();
    private IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> _refsBySha =
        new Dictionary<string, IReadOnlyList<RefLabel>>();

    // Incremental loading: start with one page, load more as the user scrolls toward the bottom,
    // so opening a huge repository is fast.
    private const int CommitPageSize = 1500;
    private int _commitLimit = CommitPageSize;

    public ILocalizationService Loc { get; }
    public ICommitStatsSource Stats { get; }

    public WorkingCopyViewModel WorkingCopy { get; }
    public BranchesViewModel Branches { get; }
    public CommitDetailsViewModel Details { get; }

    public RepositorySessionViewModel(
        IRepositoryService repo,
        WorkingCopyViewModel workingCopy,
        BranchesViewModel branches,
        CommitDetailsViewModel details,
        ICommitStatsSource stats,
        IDialogService dialogs,
        ILocalizationService loc,
        ICredentialStore credentials,
        Services.Hosting.IHostingClient hosting,
        IBookmarkStore bookmarks,
        ILogger<RepositorySessionViewModel> logger)
    {
        _repo = repo;
        WorkingCopy = workingCopy;
        Branches = branches;
        Details = details;
        Stats = stats;
        _dialogs = dialogs;
        Loc = loc;
        _credentials = credentials;
        _hosting = hosting;
        _bookmarkStore = bookmarks;
        _logger = logger;

        // A single diff panel follows whichever file is selected (commit file or working file).
        ActiveDiff = Details.Diff;
        Details.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommitDetailsViewModel.SelectedFile) && Details.SelectedFile is not null)
                ActiveDiff = Details.Diff;
        };
        WorkingCopy.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkingCopyViewModel.SelectedFile) && WorkingCopy.SelectedFile is not null)
                ActiveDiff = WorkingCopy.Diff;
        };

        WorkingCopy.RepositoryChanged += ReloadAllAsync;
        Branches.RepositoryChanged += ReloadAllAsync;
    }

    /// <summary>Bind this session to an already-opened repository (see <see cref="RepositorySessionFactory"/>).</summary>
    public void Initialize(string path, string name)
    {
        RepositoryPath = path;
        RepositoryName = name;
        _bookmarks = new HashSet<string>(_bookmarkStore.Get(path), StringComparer.Ordinal);
        _commitLimit = CommitPageSize;
    }

    /// <summary>The remote URL of this repository (used by the app shell's credential actions).</summary>
    public string? GetRemoteUrl() => _repo.GetRemoteUrl();

    /// <summary>Wire up an "origin" remote right after creating the repository, then reload.</summary>
    public async Task AddOriginAndReloadAsync(string url)
    {
        if (await GitUi.RunAsync(() => _repo.AddRemoteAsync("origin", url), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    // A session only ever represents an open repository; the flag keeps the shared XAML (graph
    // visibility, etc.) readable and mirrors the old MainViewModel gate.
    public bool IsRepositoryOpen => true;

    [ObservableProperty] private DiffViewModel? _activeDiff;

    public ObservableCollection<StashInfo> Stashes { get; } = new();

    /// <summary>Bookmarked commits currently loaded (for the quick-jump list).</summary>
    public ObservableCollection<CommitRowViewModel> Bookmarks { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOperationInProgress))]
    [NotifyPropertyChangedFor(nameof(OperationName))]
    [NotifyPropertyChangedFor(nameof(IsBisecting))]
    private RepositoryStateInfo? _repoState;

    [ObservableProperty] private bool _hasSubmodules;
    [ObservableProperty] private string _bisectStatus = string.Empty;

    // Bisect has its own banner; the conflict banner (abort/continue) is only for merge/rebase/etc.
    public bool IsOperationInProgress =>
        RepoState?.IsInProgress == true && RepoState.Operation != RepositoryOperation.Bisect;
    public bool IsBisecting => RepoState?.Operation == RepositoryOperation.Bisect;
    public string OperationName => IsOperationInProgress ? Loc.T("State." + RepoState!.Operation) : string.Empty;

    [ObservableProperty] private string? _repositoryName;
    [ObservableProperty] private string? _repositoryPath;
    [ObservableProperty] private IReadOnlyList<CommitRowViewModel> _rows = Array.Empty<CommitRowViewModel>();
    [ObservableProperty] private int _laneCount = 1;
    [ObservableProperty] private CommitRowViewModel? _selectedCommit;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _currentBranchName;
    [ObservableProperty] private int _ahead;
    [ObservableProperty] private int _behind;

    /// <summary>The current branch is behind its upstream (there are commits to pull). Kept current
    /// by the background fetch so the toolbar can nudge the user without them fetching manually.</summary>
    [ObservableProperty] private bool _hasIncoming;

    /// <summary>Reflog message of the last undoable Git action (or null if there's nothing to undo);
    /// drives the toolbar "undo" button's tooltip and enabled state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    [NotifyCanExecuteChangedFor(nameof(UndoLastCommand))]
    private string? _undoDescription;

    public bool CanUndo => !string.IsNullOrEmpty(UndoDescription);

    [ObservableProperty] private int _selectedCommitIndex = -1;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private bool _hasMoreCommits;
    [ObservableProperty] private bool _isLoadingMore;

    partial void OnSelectedCommitIndexChanged(int value)
    {
        if (Rows.Count > 0 && value >= 0 && value < Rows.Count)
        {
            SelectedCommit = Rows[value];
            Details.Load(SelectedCommit.Commit);
        }
        else
        {
            SelectedCommit = null;
            Details.Load(null);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    // ---------------------------------------------------------------- reload / filter

    [RelayCommand]
    private Task Refresh() => ReloadAllAsync();

    public async Task ReloadAllAsync()
    {
        IsBusy = true;
        StatusText = Loc.T("Status.Loading");
        try
        {
            _repo.Refresh();
            var data = await Task.Run(() =>
            {
                var commits = _repo.GetCommits(_commitLimit);
                var branches = _repo.GetBranches();
                var refs = _repo.GetRefLabelsBySha();
                var head = _repo.GetHead();
                var tags = _repo.GetTags();
                var state = _repo.GetState();
                var stashes = _repo.GetStashes();
                var submodules = _repo.GetSubmodulePaths();
                return (commits, branches, refs, head, tags, state, stashes, submodules);
            });

            _allCommits = data.commits;
            _refsBySha = data.refs;
            HasMoreCommits = data.commits.Count >= _commitLimit;
            Branches.Refresh(data.branches, data.tags);
            WorkingCopy.Refresh();

            RepoState = data.state;
            HasSubmodules = data.submodules.Count > 0;
            Stashes.Clear();
            foreach (var s in data.stashes) Stashes.Add(s);

            var current = data.branches.FirstOrDefault(b => b.IsCurrent);
            CurrentBranchName = current?.FriendlyName ?? (data.head.IsDetached ? "(detached)" : data.head.BranchFriendlyName);
            Ahead = current?.Ahead ?? 0;
            Behind = current?.Behind ?? 0;
            HasIncoming = Behind > 0;

            ApplyFilter();
            UndoDescription = TryBuildUndo()?.Description;
            StatusText = RepoState.IsInProgress
                ? Loc.T("State." + RepoState.Operation)
                : Loc.T("Status.Ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reload failed");
            _dialogs.Error(ex.Message, Loc.T("Common.Error"));
            StatusText = Loc.T("Common.Error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Load the next page of older commits (triggered when scrolling toward the bottom).</summary>
    [RelayCommand]
    private async Task LoadMore()
    {
        if (IsLoadingMore || !HasMoreCommits) return;
        IsLoadingMore = true;
        try
        {
            _commitLimit += CommitPageSize;
            var loaded = await Task.Run(() =>
            {
                _repo.Refresh();
                return (commits: _repo.GetCommits(_commitLimit), refs: _repo.GetRefLabelsBySha());
            });
            _allCommits = loaded.commits;
            _refsBySha = loaded.refs;
            HasMoreCommits = loaded.commits.Count >= _commitLimit;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load more commits failed");
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void ApplyFilter()
    {
        var previousSha = SelectedCommit?.Sha;

        IEnumerable<CommitInfo> filtered = _allCommits;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            filtered = _allCommits.Where(c =>
                c.Summary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.AuthorName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Sha.StartsWith(q, StringComparison.OrdinalIgnoreCase) ||
                c.MessageFull.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        var list = filtered as IList<CommitInfo> ?? filtered.ToList();

        var graphCommits = list.Select(c => new GraphCommit { Sha = c.Sha, ParentShas = c.ParentShas }).ToList();
        var layout = _engine.Build(graphCommits);

        var rows = new List<CommitRowViewModel>(list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            rows.Add(new CommitRowViewModel
            {
                Commit = list[i],
                GraphRow = layout.Rows[i],
                Refs = _refsBySha.TryGetValue(list[i].Sha, out var refs) ? refs : Array.Empty<RefLabel>(),
                IsBookmarked = _bookmarks.Contains(list[i].Sha)
            });
        }

        Rows = rows;
        LaneCount = layout.LaneCount;

        Bookmarks.Clear();
        foreach (var r in rows) if (r.IsBookmarked) Bookmarks.Add(r);

        var restore = previousSha is null ? -1 : rows.FindIndex(r => r.Sha == previousSha);
        SelectedCommitIndex = restore >= 0 ? restore : (rows.Count > 0 ? 0 : -1);
        // Force detail refresh if index unchanged but content replaced.
        OnSelectedCommitIndexChanged(SelectedCommitIndex);
    }

    // ---------------------------------------------------------------- undo last action (safety net)

    /// <summary>
    /// One-click undo of the last HEAD-moving Git action, using the reflog to restore the previous
    /// position. The reset mode is chosen by what the action was: a commit/amend is undone with a
    /// <c>--soft</c> reset (the changes come back staged), a branch switch is reversed with
    /// <c>checkout -</c>, and everything else (reset/merge/pull/rebase/cherry-pick/revert) uses
    /// <c>--keep</c> so uncommitted local changes are preserved (it aborts rather than clobber them).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoLast()
    {
        if (TryBuildUndo() is not { } undo) return;
        if (!_dialogs.Confirm(Loc.T("Undo.Confirm", undo.Description), Loc.T("Undo.Title"))) return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(undo.Args), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    private (string Description, string[] Args)? TryBuildUndo()
    {
        IReadOnlyList<ReflogEntry> log;
        try { log = _repo.GetReflog(2); }
        catch { return null; }
        if (log.Count < 2) return null;

        var message = log[0].Message ?? string.Empty;
        var previous = log[1].Sha;
        return UndoPlan.Args(message, previous) is { } args ? (message, args) : null;
    }

    // ---------------------------------------------------------------- remote / network

    [RelayCommand]
    private Task Fetch() => RunNetworkAsync(ct => _repo.FetchAsync(ct: ct));

    [RelayCommand]
    private Task Pull() => RunNetworkAsync(ct => _repo.PullAsync(ct));

    [RelayCommand]
    private Task Push() => RunNetworkAsync(ct =>
    {
        var current = Branches.Local.FirstOrDefault(b => b.IsCurrent);
        var needsUpstream = current?.Model.UpstreamFriendlyName is null;
        return _repo.PushAsync(setUpstream: needsUpstream, ct: ct);
    });

    /// <summary>Force-push with lease (safe force — refuses if the remote moved under you).</summary>
    [RelayCommand]
    private Task ForcePush()
    {
        if (!_dialogs.Confirm(Loc.T("Push.ForceConfirm"), Loc.T("Push.Force"))) return Task.CompletedTask;
        return RunNetworkAsync(ct => _repo.PushAsync(forceWithLease: true, ct: ct));
    }

    private CancellationTokenSource? _networkCts;

    [ObservableProperty] private bool _canCancelNetwork;

    [RelayCommand]
    private void CancelNetwork() => _networkCts?.Cancel();

    private async Task RunNetworkAsync(Func<CancellationToken, Task<GitResult>> op)
    {
        using var cts = new CancellationTokenSource();
        _networkCts = cts;
        CanCancelNetwork = true;
        IsBusy = true;
        StatusText = Loc.T("Status.Working");
        var ok = await RunWithAuthRetryAsync(op, cts.Token);
        IsBusy = false;
        CanCancelNetwork = false;
        _networkCts = null;
        if (ok) await ReloadAllAsync();
        else StatusText = Loc.T("Status.Ready");
    }

    /// <summary>
    /// Run a network op; on an authentication failure, prompt for credentials in the GUI, store
    /// them (Windows Credential Manager), and retry once. This makes HTTPS auth fully GUI-driven.
    /// </summary>
    private async Task<bool> RunWithAuthRetryAsync(Func<CancellationToken, Task<GitResult>> op, CancellationToken ct)
    {
        GitResult result;
        try { result = await GitAuth.RunWithRetryAsync(op, _repo, _credentials, _dialogs, Loc, _logger, ct); }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "network op threw");
            _dialogs.Error(ex.Message, Loc.T("Error.GitFailed"));
            return false;
        }

        if (result.Success) return true;

        _logger.LogWarning("git failed ({Code}): {Cmd}\n{Out}", result.ExitCode, result.CommandLine, result.CombinedOutput);
        var body = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
        _dialogs.Error(body, Loc.T("Error.GitFailed"));
        return false;
    }

    private bool _polling;

    /// <summary>
    /// Quietly fetch this repository in the background and refresh the current branch's ahead/behind
    /// counters — no auth prompt, no busy spinner, no graph rebuild, and errors (offline, no
    /// credentials) are swallowed. Only the <see cref="Ahead"/>/<see cref="Behind"/>/<see cref="HasIncoming"/>
    /// indicators move, so the graph keeps showing exactly what the user was looking at.
    /// </summary>
    public async Task PollRemoteAsync(CancellationToken ct)
    {
        // Don't collide with a user-initiated fetch/pull/push, and don't overlap our own polls.
        if (_polling || CanCancelNetwork) return;
        _polling = true;
        try
        {
            GitResult result;
            try { result = await _repo.FetchAsync(ct: ct); }
            catch { return; }
            if (!result.Success) return;

            var counts = await Task.Run(() =>
            {
                _repo.Refresh();
                var cur = _repo.GetBranches().FirstOrDefault(b => b.IsCurrent);
                return (ahead: cur?.Ahead ?? Ahead, behind: cur?.Behind ?? Behind);
            }, ct);

            Ahead = counts.ahead;
            Behind = counts.behind;
            HasIncoming = counts.behind > 0;
        }
        catch { /* stay silent — background work must never surface errors */ }
        finally { _polling = false; }
    }

    [RelayCommand] private Task AbortOperation() => RunNetworkAsync(ct => _repo.AbortOperationAsync(ct));
    [RelayCommand] private Task ContinueOperation() => RunNetworkAsync(ct => _repo.ContinueOperationAsync(ct));
    [RelayCommand] private Task SubmoduleUpdate() => RunNetworkAsync(ct => _repo.SubmoduleUpdateAsync(ct));

    // ---------------------------------------------------------------- commit context-menu actions

    [RelayCommand] private void CopySha() => TryClipboard(SelectedCommit?.Sha);
    [RelayCommand] private void CopyMessage() => TryClipboard(SelectedCommit?.Commit.MessageFull);

    [RelayCommand]
    private async Task CheckoutCommit()
    {
        if (SelectedCommit is null) return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "checkout", SelectedCommit.Sha }), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task RewordCommit()
    {
        if (SelectedCommit is null) return;
        var current = SelectedCommit.Commit.MessageFull?.TrimEnd() ?? string.Empty;
        var message = _dialogs.PromptMultiline(Loc.T("Reword.Prompt"), Loc.T("Reword.Title"), current);
        if (string.IsNullOrWhiteSpace(message) || message.TrimEnd() == current) return;
        if (await GitUi.RunAsync(() => _repo.RewordAsync(SelectedCommit.Sha, message), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    /// <summary>Fix the author (name/email) of the latest commit.</summary>
    [RelayCommand]
    private async Task AmendAuthor()
    {
        var curName = await _repo.GetConfigAsync("user.name", global: false) ?? string.Empty;
        var curEmail = await _repo.GetConfigAsync("user.email", global: false) ?? string.Empty;
        var name = _dialogs.Prompt(Loc.T("Author.NamePrompt"), Loc.T("Author.Amend"), curName);
        if (string.IsNullOrWhiteSpace(name)) return;
        var email = _dialogs.Prompt(Loc.T("Author.EmailPrompt"), Loc.T("Author.Amend"), curEmail);
        if (string.IsNullOrWhiteSpace(email)) return;
        if (await GitUi.RunAsync(() => _repo.AmendAuthorAsync(name.Trim(), email.Trim()), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    /// <summary>Restore a file to its content at the selected commit.</summary>
    [RelayCommand]
    private async Task RestoreFile(FileChangeViewModel? file)
    {
        var path = file?.Path ?? Details.SelectedFile?.Path;
        if (path is null || SelectedCommit is null) return;
        if (!_dialogs.Confirm(Loc.T("Restore.Confirm", path, SelectedCommit.ShortSha), Loc.T("Restore.Title"))) return;
        if (await GitUi.RunAsync(() => _repo.RestoreFileAsync(SelectedCommit.Sha, path), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    /// <summary>Star/unstar a commit as a bookmark (persisted per repository).</summary>
    [RelayCommand]
    private void ToggleBookmark(CommitRowViewModel? row)
    {
        var target = row ?? SelectedCommit;
        if (target is null || RepositoryPath is null) return;
        var nowOn = _bookmarkStore.Toggle(RepositoryPath, target.Sha);
        if (nowOn) _bookmarks.Add(target.Sha); else _bookmarks.Remove(target.Sha);
        ApplyFilter();   // rebuild rows so the graph marker + Bookmarks list reflect the change
    }

    [RelayCommand]
    private void JumpToBookmark(CommitRowViewModel? row)
    {
        if (row is null) return;
        var idx = Rows is IList<CommitRowViewModel> list ? list.IndexOf(row) : -1;
        if (idx >= 0) SelectedCommitIndex = idx;
    }

    [RelayCommand]
    private async Task CreateBranchHere()
    {
        if (SelectedCommit is null) return;
        var name = _dialogs.Prompt(Loc.T("Branch.NewName.Prompt"), Loc.T("Branch.New"));
        if (string.IsNullOrWhiteSpace(name)) return;
        if (await GitUi.RunAsync(() => _repo.CreateBranchAsync(name, checkout: false, startPoint: SelectedCommit.Sha), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task CreateTagHere()
    {
        if (SelectedCommit is null) return;
        var name = _dialogs.Prompt(Loc.T("Branch.NewName.Prompt"), "Tag");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (await GitUi.RunAsync(() => _repo.CreateTagAsync(name, SelectedCommit.Sha), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand] private Task ResetSoft() => ResetTo("--soft", confirm: false);
    [RelayCommand] private Task ResetMixed() => ResetTo("--mixed", confirm: false);
    [RelayCommand] private Task ResetHard() => ResetTo("--hard", confirm: true);

    private async Task ResetTo(string mode, bool confirm)
    {
        if (SelectedCommit is null) return;
        if (confirm && !_dialogs.Confirm(
                $"git reset {mode} {SelectedCommit.ShortSha}", Loc.T("Common.Warning")))
            return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "reset", mode, SelectedCommit.Sha }), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task RevertCommit()
    {
        if (SelectedCommit is null) return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "revert", "--no-edit", SelectedCommit.Sha }), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task CherryPick()
    {
        if (SelectedCommit is null) return;
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "cherry-pick", SelectedCommit.Sha }), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ExportPatch()
    {
        if (SelectedCommit is null) return;
        var path = _dialogs.SaveFile(Loc.T("Patch.ExportTitle"), $"{SelectedCommit.ShortSha}.patch", "patch");
        if (string.IsNullOrWhiteSpace(path)) return;
        if (await GitUi.RunAsync(() => _repo.ExportCommitPatchAsync(SelectedCommit.Sha, path!), _dialogs, Loc, _logger))
            _dialogs.Info(path!, Loc.T("Patch.ExportTitle"));
    }

    [RelayCommand]
    private async Task ApplyPatch()
    {
        var path = _dialogs.OpenFile(Loc.T("Patch.ApplyTitle"), "patch");
        if (string.IsNullOrWhiteSpace(path)) return;
        var asCommits = _dialogs.Confirm(Loc.T("Patch.AsCommits"), Loc.T("Patch.ApplyTitle"));
        if (await GitUi.RunAsync(() => _repo.ApplyPatchAsync(path!, asCommits), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task InteractiveRebase()
    {
        if (SelectedCommit is null || RepositoryPath is null) return;
        var baseSha = SelectedCommit.Sha;

        IReadOnlyList<CommitInfo> above;
        try { above = _repo.GetCommitsBetween(baseSha, "HEAD"); }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); return; }

        if (above.Count == 0)
        {
            _dialogs.Info(Loc.T("Rebase.NothingAbove"), AppInfo.ProductName);
            return;
        }

        // Plan is oldest-first (git todo order).
        var items = above.Reverse()
            .Select(c => new RebaseTodoItem { Sha = c.Sha, Summary = c.Summary })
            .ToList();

        var plan = _dialogs.ShowInteractiveRebase(items);
        if (plan is null) return;

        if (await GitUi.RunAsync(() => _repo.RebaseInteractiveAsync(baseSha, plan), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    // ---- bisect ----

    [RelayCommand]
    private async Task StartBisect()
    {
        if (SelectedCommit is null) return;
        if (!_dialogs.Confirm(Loc.T("Bisect.StartConfirm"), Loc.T("Bisect.Title"))) return;
        await RunBisectAsync(() => _repo.BisectStartAsync(goodSha: SelectedCommit.Sha, badSha: "HEAD"));
    }

    [RelayCommand] private Task BisectGood() => RunBisectAsync(() => _repo.BisectMarkAsync("good"));
    [RelayCommand] private Task BisectBad() => RunBisectAsync(() => _repo.BisectMarkAsync("bad"));
    [RelayCommand] private Task BisectSkip() => RunBisectAsync(() => _repo.BisectMarkAsync("skip"));

    [RelayCommand]
    private async Task BisectReset()
    {
        if (await GitUi.RunAsync(() => _repo.BisectResetAsync(), _dialogs, Loc, _logger))
        {
            BisectStatus = string.Empty;
            await ReloadAllAsync();
        }
    }

    private async Task RunBisectAsync(Func<Task<GitResult>> op)
    {
        GitResult result;
        try { result = await op().ConfigureAwait(true); }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); return; }
        if (!result.Success)
        {
            var body = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
            _dialogs.Error(body, Loc.T("Error.GitFailed"));
            return;
        }
        BisectStatus = result.CombinedOutput.Trim();
        await ReloadAllAsync();
    }

    // ---------------------------------------------------------------- stash

    [RelayCommand]
    private async Task StashPush()
    {
        if (await GitUi.RunAsync(() => _repo.StashPushAsync(null, includeUntracked: true), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task StashPop(StashInfo? stash)
    {
        if (stash is null) return;
        if (await GitUi.RunAsync(() => _repo.StashApplyAsync(stash.Index, pop: true), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task StashApply(StashInfo? stash)
    {
        if (stash is null) return;
        if (await GitUi.RunAsync(() => _repo.StashApplyAsync(stash.Index, pop: false), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task StashDrop(StashInfo? stash)
    {
        if (stash is null) return;
        if (await GitUi.RunAsync(() => _repo.StashDropAsync(stash.Index), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task StashDiff(StashInfo? stash)
    {
        if (stash is null) return;
        try
        {
            var diff = await _repo.GetStashDiffAsync(stash.Index);
            _dialogs.ShowText(Loc.T("Stash.DiffTitle"), string.IsNullOrWhiteSpace(diff) ? "(empty)" : diff);
        }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); }
    }

    [RelayCommand]
    private async Task StashToBranch(StashInfo? stash)
    {
        if (stash is null) return;
        var branch = _dialogs.Prompt(Loc.T("Stash.ToBranchPrompt"), Loc.T("Ctx.StashToBranch"));
        if (string.IsNullOrWhiteSpace(branch)) return;
        if (await GitUi.RunAsync(() => _repo.StashToBranchAsync(stash.Index, branch.Trim()), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    // ---------------------------------------------------------------- files / conflicts

    [RelayCommand]
    private async Task ResolveConflict(FileChangeViewModel? file)
    {
        if (file is null || RepositoryPath is null) return;
        var (baseText, ours, theirs) = _repo.GetConflictVersions(file.Path);
        if (!_dialogs.ShowConflictResolver(RepositoryPath, file.Path, baseText, ours, theirs)) return;
        if (await GitUi.RunAsync(() => _repo.MarkResolvedAsync(file.Path), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private void FileHistory(FileChangeViewModel? file)
    {
        var path = file?.Path ?? Details.SelectedFile?.Path;
        if (path is null) return;
        var vm = new FileHistoryViewModel(_repo, Loc, _logger, path);
        _dialogs.ShowFileHistory(vm);
    }

    [RelayCommand]
    private void OpenFileInEditor(FileChangeViewModel? file)
    {
        var path = file?.Path ?? Details.SelectedFile?.Path;
        if (path is null || RepositoryPath is null) return;
        ExternalTools.OpenInEditor(Path.Combine(RepositoryPath, path));
    }

    /// <summary>Open a file in git's configured external diff tool (git difftool).</summary>
    [RelayCommand]
    private async Task ExternalDiff(FileChangeViewModel? file)
    {
        var path = file?.Path ?? Details.SelectedFile?.Path;
        if (path is null) return;
        var r = await _repo.RunRawAsync(new[] { "difftool", "-y", "--", path });
        if (!r.Success && !string.IsNullOrWhiteSpace(r.CombinedOutput))
            _dialogs.Error(r.CombinedOutput, Loc.T("Error.GitFailed"));
    }

    [RelayCommand]
    private void Blame(FileChangeViewModel? file)
    {
        var path = file?.Path ?? Details.SelectedFile?.Path;
        if (path is null || RepositoryPath is null) return;
        try
        {
            var lines = _repo.GetBlame(path);
            _dialogs.ShowBlame(path, lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blame failed for {Path}", path);
            _dialogs.Error(ex.Message, Loc.T("Common.Error"));
        }
    }

    /// <summary>Append a working-tree file to .gitignore.</summary>
    [RelayCommand]
    private async Task AddToGitignore(FileChangeViewModel? file)
    {
        if (file is null || RepositoryPath is null) return;
        try
        {
            var giPath = Path.Combine(RepositoryPath, ".gitignore");
            var line = "/" + file.Path.Replace('\\', '/');
            var existing = File.Exists(giPath) ? await File.ReadAllTextAsync(giPath) : string.Empty;
            if (!existing.Split('\n').Any(l => l.Trim() == line))
            {
                var prefix = existing.Length > 0 && !existing.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty;
                await File.AppendAllTextAsync(giPath, prefix + line + "\n");
            }
            await ReloadAllAsync();
        }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); }
    }

    [RelayCommand]
    private async Task GenerateGitignore()
    {
        if (RepositoryPath is null) return;
        if (_dialogs.ShowGitignoreGenerator(RepositoryPath))
            await ReloadAllAsync();
    }

    // ---------------------------------------------------------------- power tools / management dialogs

    [RelayCommand]
    private async Task ManageRemotes()
    {
        var vm = new RemotesViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowRemotes(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageWorktrees()
    {
        var vm = new WorktreeViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowWorktree(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageLfs()
    {
        var vm = new LfsViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowLfs(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageSubmodules()
    {
        var vm = new SubmoduleViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowSubmodule(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageSparseCheckout()
    {
        var vm = new SparseCheckoutViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowSparseCheckout(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private void ShowStatistics() => _dialogs.ShowStats(new StatsViewModel(_repo, Loc, _logger));

    [RelayCommand]
    private void GenerateChangelog() => _dialogs.ShowChangelog(new ChangelogViewModel(_repo, _dialogs, Loc, _logger));

    [RelayCommand]
    private async Task EditConfig()
    {
        var vm = new GitConfigViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowGitConfig(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ContentSearch()
    {
        var vm = new ContentSearchViewModel(_repo, Loc, _logger);
        if (!_dialogs.ShowContentSearch(vm) || vm.ChosenSha is not { } sha) return;

        int Find()
        {
            for (int i = 0; i < Rows.Count; i++)
                if (Rows[i].Sha == sha) return i;
            return -1;
        }

        var idx = Find();
        while (idx < 0 && HasMoreCommits) { await LoadMore(); idx = Find(); }
        if (idx >= 0) SelectedCommitIndex = idx;
        else _dialogs.Info(Loc.T("Search.NotInView"), AppInfo.ProductName);
    }

    [RelayCommand]
    private void Compare()
    {
        var current = CurrentBranchName;
        var names = _repo.GetBranches().Select(b => b.FriendlyName).ToList();
        var baseRef = names.Contains("main") ? "main"
            : names.Contains("master") ? "master"
            : names.FirstOrDefault(n => n != current) ?? current;
        var vm = new CompareViewModel(_repo, Loc, _logger, from: baseRef, to: current);
        _dialogs.ShowCompare(vm);
    }

    [RelayCommand]
    private async Task OpenReflog()
    {
        var vm = new ReflogViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowReflog(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private void OpenInEditor()
    {
        if (RepositoryPath is not null)
            ExternalTools.OpenInEditor(RepositoryPath);
    }

    // ---------------------------------------------------------------- hosting

    [RelayCommand]
    private void OpenHosting()
    {
        var remote = _repo.GetRemoteUrl();
        if (remote is null || !_hosting.IsSupported(remote))
        {
            _dialogs.Info(Loc.T("Hosting.Empty"), Loc.T("Hosting.Title"));
            return;
        }
        var vm = new HostingViewModel(_hosting, remote, Loc, _logger);
        _dialogs.ShowHosting(vm);
    }

    [RelayCommand]
    private void CreatePullRequest()
    {
        var url = RemoteWeb.PullRequestUrl(_repo.GetRemoteUrl(), CurrentBranchName);
        if (url is null) { _dialogs.Info(Loc.T("Hosting.Unsupported"), AppInfo.ProductName); return; }
        OpenBrowser(url);
    }

    [RelayCommand]
    private void OpenRemoteWeb()
    {
        var url = RemoteWeb.RepoUrl(_repo.GetRemoteUrl());
        if (url is null) { _dialogs.Info(Loc.T("Hosting.NoRemote"), AppInfo.ProductName); return; }
        OpenBrowser(url);
    }

    private void OpenBrowser(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _logger.LogWarning(ex, "Opening browser failed for {Url}", url); }
    }

    private void TryClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); }
        catch (Exception ex) { _logger.LogDebug(ex, "Clipboard set failed"); }
    }

    public void Dispose()
    {
        try { _repo.Close(); } catch (Exception ex) { _logger.LogWarning(ex, "Close failed"); }
        if (_repo is IDisposable d) d.Dispose();
    }
}
