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

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IRecentRepositoriesStore _recent;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly IUpdateService _updates;
    private readonly IShellIntegrationService _shell;
    private readonly ICredentialStore _credentials;
    private readonly Services.Hosting.IHostingClient _hosting;
    private readonly IBookmarkStore _bookmarkStore;
    private HashSet<string> _bookmarks = new(StringComparer.Ordinal);
    private readonly ILogger<MainViewModel> _logger;
    private readonly GraphLayoutEngine _engine = new();

    /// <summary>Raised when a shell "commit" action asks the window to focus the staging panel.</summary>
    public event Action? CommitFocusRequested;

    /// <summary>Raised when the user presses Ctrl+F to jump to the graph search box.</summary>
    public event Action? SearchFocusRequested;

    private IReadOnlyList<CommitInfo> _allCommits = Array.Empty<CommitInfo>();
    private IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> _refsBySha =
        new Dictionary<string, IReadOnlyList<RefLabel>>();
    private bool _childrenWired;

    // Incremental loading: start with one page, load more as the user scrolls toward the bottom,
    // so opening a huge repository is fast.
    private const int CommitPageSize = 1500;
    private int _commitLimit = CommitPageSize;

    private readonly ISettingsService _settings;

    public ICommitStatsSource Stats { get; }

    public MainViewModel(
        IRepositoryService repo,
        IRecentRepositoriesStore recent,
        IDialogService dialogs,
        ILocalizationService loc,
        IThemeService theme,
        IUpdateService updates,
        IShellIntegrationService shell,
        ICredentialStore credentials,
        Services.Hosting.IHostingClient hosting,
        IBookmarkStore bookmarks,
        ISettingsService settings,
        ICommitStatsSource stats,
        WorkingCopyViewModel workingCopy,
        BranchesViewModel branches,
        CommitDetailsViewModel details,
        ILogger<MainViewModel> logger)
    {
        Stats = stats;
        _repo = repo;
        _recent = recent;
        _dialogs = dialogs;
        Loc = loc;
        _theme = theme;
        _updates = updates;
        _shell = shell;
        _credentials = credentials;
        _hosting = hosting;
        _bookmarkStore = bookmarks;
        _settings = settings;
        WorkingCopy = workingCopy;
        Branches = branches;
        Details = details;
        _logger = logger;

        StatusText = loc.T("Status.NoRepo");
        foreach (var r in _recent.GetAll()) RecentRepositories.Add(r);

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
    }

    [ObservableProperty] private DiffViewModel? _activeDiff;

    public ILocalizationService Loc { get; }
    public IThemeService Theme => _theme;

    public WorkingCopyViewModel WorkingCopy { get; }
    public BranchesViewModel Branches { get; }
    public CommitDetailsViewModel Details { get; }

    public ObservableCollection<RecentRepository> RecentRepositories { get; } = new();
    public ObservableCollection<StashInfo> Stashes { get; } = new();

    /// <summary>Bookmarked commits currently loaded (for the quick-jump list).</summary>
    public ObservableCollection<CommitRowViewModel> Bookmarks { get; } = new();

    // ---- multi-repository tabs ----
    public ObservableCollection<RepositoryTab> Tabs { get; } = new();

    /// <summary>Guards re-entrancy while we programmatically change the active tab.</summary>
    private bool _switchingTab;

    [ObservableProperty] private RepositoryTab? _activeTab;

    partial void OnActiveTabChanged(RepositoryTab? value)
    {
        if (_switchingTab || value is null) return;
        if (string.Equals(value.Path, RepositoryPath, StringComparison.OrdinalIgnoreCase)) return;
        _ = OpenPath(value.Path);
    }

    /// <summary>Add (or focus) the tab for <paramref name="path"/> without re-triggering an open.</summary>
    private void TrackTab(string path, string name)
    {
        var tab = Tabs.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        if (tab is null) { tab = new RepositoryTab(path, name); Tabs.Add(tab); }
        _switchingTab = true;
        ActiveTab = tab;
        _switchingTab = false;
    }

    [RelayCommand]
    private async Task CloseTab(RepositoryTab? tab)
    {
        if (tab is null) return;
        bool wasActive = ReferenceEquals(tab, ActiveTab);
        int idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (!wasActive) return;

        if (Tabs.Count == 0)
        {
            _switchingTab = true;
            ActiveTab = null;
            _switchingTab = false;
            CloseActiveRepository();
        }
        else
        {
            // Activating a neighbour re-opens it through OnActiveTabChanged.
            ActiveTab = Tabs[Math.Min(idx, Tabs.Count - 1)];
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void NextTab()
    {
        if (Tabs.Count < 2 || ActiveTab is null) return;
        ActiveTab = Tabs[(Tabs.IndexOf(ActiveTab) + 1) % Tabs.Count];
    }

    [RelayCommand]
    private void PreviousTab()
    {
        if (Tabs.Count < 2 || ActiveTab is null) return;
        ActiveTab = Tabs[(Tabs.IndexOf(ActiveTab) - 1 + Tabs.Count) % Tabs.Count];
    }

    [RelayCommand]
    private void FocusSearch() => SearchFocusRequested?.Invoke();

    private void CloseActiveRepository()
    {
        try { _repo.Close(); } catch (Exception ex) { _logger.LogWarning(ex, "Close failed"); }
        IsRepositoryOpen = false;
        RepositoryPath = null;
        RepositoryName = null;
        Rows = Array.Empty<CommitRowViewModel>();
        _allCommits = Array.Empty<CommitInfo>();
        SelectedCommitIndex = -1;
        Stashes.Clear();
        RepoState = null;
        StatusText = Loc.T("Status.NoRepo");
    }

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
    [ObservableProperty] private bool _isRepositoryOpen;
    [ObservableProperty] private IReadOnlyList<CommitRowViewModel> _rows = Array.Empty<CommitRowViewModel>();
    [ObservableProperty] private int _laneCount = 1;
    [ObservableProperty] private CommitRowViewModel? _selectedCommit;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _currentBranchName;
    [ObservableProperty] private int _ahead;
    [ObservableProperty] private int _behind;

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

    // ---------------------------------------------------------------- open / reload

    [RelayCommand]
    private async Task OpenRepository()
    {
        var folder = _dialogs.PickFolder(Loc.T("Action.Open"));
        if (folder is not null) await OpenPath(folder);
    }

    [RelayCommand]
    private async Task CreateRepository()
    {
        var vm = new NewRepositoryViewModel(Loc);
        if (!_dialogs.ShowNewRepository(vm)) return;

        var path = vm.FolderPath.Trim();
        if (string.IsNullOrWhiteSpace(path)) return;
        var branch = string.IsNullOrWhiteSpace(vm.InitialBranch) ? "main" : vm.InitialBranch.Trim();

        if (!await GitUi.RunAsync(() => _repo.InitAsync(path, branch), _dialogs, Loc, _logger)) return;
        await OpenPath(path);

        // Optionally wire up the origin remote in the same step.
        if (IsRepositoryOpen && !string.IsNullOrWhiteSpace(vm.RemoteUrl))
        {
            if (await GitUi.RunAsync(() => _repo.AddRemoteAsync("origin", vm.RemoteUrl.Trim()), _dialogs, Loc, _logger))
                await ReloadAllAsync();
        }
    }

    [RelayCommand]
    private Task Clone() => CloneInto(null);

    /// <summary>Clone flow; <paramref name="parentFolder"/> pre-fills the destination (shell "clone here").</summary>
    public async Task CloneInto(string? parentFolder)
    {
        var vm = new CloneViewModel(Loc, parentFolder);
        if (!_dialogs.ShowClone(vm)) return;
        var url = vm.Url.Trim();
        var target = vm.TargetPath;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(target)) return;
        _cloneBlobless = vm.Blobless;

        using var cts = new CancellationTokenSource();
        _networkCts = cts;
        CanCancelNetwork = true;
        IsBusy = true;
        StatusText = Loc.T("Status.Working");
        var result = await RunCloneWithAuthAsync(url, target, cts.Token);
        IsBusy = false;
        CanCancelNetwork = false;
        _networkCts = null;

        if (result is { Success: true })
        {
            await OpenPath(target);
        }
        else
        {
            if (result is not null)
            {
                var body = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
                _dialogs.Error(body, Loc.T("Error.GitFailed"));
            }
            StatusText = Loc.T("Status.Ready");
        }
    }

    // Clone can't use RunWithAuthRetryAsync (no repo is open yet), so it keys credentials off the URL.
    private async Task<GitResult?> RunCloneWithAuthAsync(string url, string target, CancellationToken ct)
    {
        try
        {
            var result = await _repo.CloneAsync(url, target, _cloneBlobless, ct);
            if (result.Success || !GitAuth.IsAuthFailure(result)) return result;

            var input = _dialogs.PromptCredentials(CredentialKey.HostLabel(url));
            if (input is null) return result;
            var key = CredentialKey.FromUrl(url);
            if (key is not null)
            {
                try { _credentials.Save(key, input.User, input.Secret); }
                catch (Exception ex) { _logger.LogWarning(ex, "Saving credentials failed"); }
            }
            return await _repo.CloneAsync(url, target, _cloneBlobless, ct);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "clone threw");
            _dialogs.Error(ex.Message, Loc.T("Error.GitFailed"));
            return null;
        }
    }

    [RelayCommand]
    private async Task ManageRemotes()
    {
        if (!IsRepositoryOpen) return;
        var vm = new RemotesViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowRemotes(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageWorktrees()
    {
        if (!IsRepositoryOpen) return;
        var vm = new WorktreeViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowWorktree(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageLfs()
    {
        if (!IsRepositoryOpen) return;
        var vm = new LfsViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowLfs(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageSubmodules()
    {
        if (!IsRepositoryOpen) return;
        var vm = new SubmoduleViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowSubmodule(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task ManageSparseCheckout()
    {
        if (!IsRepositoryOpen) return;
        var vm = new SparseCheckoutViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowSparseCheckout(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private void ManageCredentials()
    {
        var vm = new CredentialsViewModel(_credentials, _dialogs, Loc, _logger);
        _dialogs.ShowCredentials(vm);
    }

    [RelayCommand]
    private void OpenInEditor()
    {
        if (IsRepositoryOpen && RepositoryPath is not null)
            ExternalTools.OpenInEditor(RepositoryPath);
    }

    /// <summary>Force-push with lease (safe force — refuses if the remote moved under you).</summary>
    [RelayCommand]
    private Task ForcePush()
    {
        if (!IsRepositoryOpen) return Task.CompletedTask;
        if (!_dialogs.Confirm(Loc.T("Push.ForceConfirm"), Loc.T("Push.Force"))) return Task.CompletedTask;
        return RunNetworkAsync(ct => _repo.PushAsync(forceWithLease: true, ct: ct));
    }

    /// <summary>Fix the author (name/email) of the latest commit.</summary>
    [RelayCommand]
    private async Task AmendAuthor()
    {
        if (!IsRepositoryOpen) return;
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
        if (path is null || SelectedCommit is null || !IsRepositoryOpen) return;
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
    private void ShowStatistics()
    {
        if (!IsRepositoryOpen) return;
        _dialogs.ShowStats(new StatsViewModel(_repo, Loc, _logger));
    }

    [RelayCommand]
    private void GenerateChangelog()
    {
        if (!IsRepositoryOpen) return;
        _dialogs.ShowChangelog(new ChangelogViewModel(_repo, _dialogs, Loc, _logger));
    }

    [RelayCommand]
    private async Task EditConfig()
    {
        if (!IsRepositoryOpen) return;
        var vm = new GitConfigViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowGitConfig(vm);
        if (vm.Changed) await ReloadAllAsync();
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
    private void OpenHosting()
    {
        if (!IsRepositoryOpen) return;
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
    private async Task ExportPatch()
    {
        if (!IsRepositoryOpen || SelectedCommit is null) return;
        var path = _dialogs.SaveFile(Loc.T("Patch.ExportTitle"), $"{SelectedCommit.ShortSha}.patch", "patch");
        if (string.IsNullOrWhiteSpace(path)) return;
        if (await GitUi.RunAsync(() => _repo.ExportCommitPatchAsync(SelectedCommit.Sha, path!), _dialogs, Loc, _logger))
            _dialogs.Info(path!, Loc.T("Patch.ExportTitle"));
    }

    [RelayCommand]
    private async Task ApplyPatch()
    {
        if (!IsRepositoryOpen) return;
        var path = _dialogs.OpenFile(Loc.T("Patch.ApplyTitle"), "patch");
        if (string.IsNullOrWhiteSpace(path)) return;
        var asCommits = _dialogs.Confirm(Loc.T("Patch.AsCommits"), Loc.T("Patch.ApplyTitle"));
        if (await GitUi.RunAsync(() => _repo.ApplyPatchAsync(path!, asCommits), _dialogs, Loc, _logger))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private void CreatePullRequest()
    {
        if (!IsRepositoryOpen) return;
        var url = RemoteWeb.PullRequestUrl(_repo.GetRemoteUrl(), CurrentBranchName);
        if (url is null) { _dialogs.Info(Loc.T("Hosting.Unsupported"), AppInfo.ProductName); return; }
        OpenBrowser(url);
    }

    [RelayCommand]
    private void OpenRemoteWeb()
    {
        if (!IsRepositoryOpen) return;
        var url = RemoteWeb.RepoUrl(_repo.GetRemoteUrl());
        if (url is null) { _dialogs.Info(Loc.T("Hosting.NoRemote"), AppInfo.ProductName); return; }
        OpenBrowser(url);
    }

    private void OpenBrowser(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { _logger.LogWarning(ex, "Opening browser failed for {Url}", url); }
    }

    [RelayCommand]
    private async Task ContentSearch()
    {
        if (!IsRepositoryOpen) return;
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
        if (!IsRepositoryOpen) return;
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
        if (!IsRepositoryOpen) return;
        var vm = new ReflogViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowReflog(vm);
        if (vm.Changed) await ReloadAllAsync();
    }

    [RelayCommand]
    private async Task OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var discovered = _repo.Discover(path);
        if (discovered is null)
        {
            _dialogs.Error(Loc.T("Error.NotARepo"), Loc.T("Error.OpenFailed"));
            return;
        }

        try
        {
            _repo.Open(discovered);
            _commitLimit = CommitPageSize;   // start each repo at the first page
            Stats.Clear();
            _recent.Add(discovered);
            SyncRecent();
            RepositoryPath = discovered;
            RepositoryName = new DirectoryInfo(discovered).Name;
            IsRepositoryOpen = true;
            _bookmarks = new HashSet<string>(_bookmarkStore.Get(discovered), StringComparer.Ordinal);
            TrackTab(discovered, RepositoryName);
            WireChildren();
            await ReloadAllAsync();
            _ = CheckForUpdatesAsync(silent: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open repository {Path}", path);
            _dialogs.Error(ex.Message, Loc.T("Error.OpenFailed"));
        }
    }

    [RelayCommand]
    private async Task OpenRecent(RecentRepository? repo)
    {
        if (repo is not null) await OpenPath(repo.Path);
    }

    private void WireChildren()
    {
        if (_childrenWired) return;
        _childrenWired = true;
        WorkingCopy.RepositoryChanged += ReloadAllAsync;
        Branches.RepositoryChanged += ReloadAllAsync;
    }

    [RelayCommand]
    private Task Refresh() => ReloadAllAsync();

    private async Task ReloadAllAsync()
    {
        if (!IsRepositoryOpen) return;
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

            ApplyFilter();
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
        if (IsLoadingMore || !HasMoreCommits || !IsRepositoryOpen) return;
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

    // ---------------------------------------------------------------- remote

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

    private CancellationTokenSource? _networkCts;
    private bool _cloneBlobless;

    [ObservableProperty] private bool _canCancelNetwork;

    [RelayCommand]
    private void CancelNetwork() => _networkCts?.Cancel();

    private async Task RunNetworkAsync(Func<CancellationToken, Task<GitResult>> op)
    {
        if (!IsRepositoryOpen) return;
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

    // ---------------------------------------------------------------- theme / language / update

    // ---------------------------------------------------------------- conflicts / stash / submodule / blame / rebase

    // ---- bisect ----

    [RelayCommand]
    private async Task StartBisect()
    {
        if (SelectedCommit is null || !IsRepositoryOpen) return;
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

    [RelayCommand] private Task AbortOperation() => RunNetworkAsync(ct => _repo.AbortOperationAsync(ct));
    [RelayCommand] private Task ContinueOperation() => RunNetworkAsync(ct => _repo.ContinueOperationAsync(ct));

    [RelayCommand]
    private async Task StashPush()
    {
        if (!IsRepositoryOpen) return;
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

    [RelayCommand] private Task SubmoduleUpdate() => RunNetworkAsync(ct => _repo.SubmoduleUpdateAsync(ct));

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
        if (path is null || !IsRepositoryOpen) return;
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
        if (path is null || !IsRepositoryOpen) return;
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

    // ---------------------------------------------------------------- theme / language / update

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Loc.Toggle();
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(IsKoreanLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        // Menu labels are baked into the registry — refresh them if the integration is installed.
        if (_shell.IsInstalled)
        {
            try { _shell.Install(); } catch (Exception ex) { _logger.LogDebug(ex, "Shell relabel skipped"); }
        }
    }

    // ---------------------------------------------------------------- settings dialog bindings

    public bool IsDarkTheme
    {
        get => _theme.Theme == AppTheme.Dark;
        set { if (value && _theme.Theme != AppTheme.Dark) ToggleTheme(); }
    }

    public bool IsLightTheme
    {
        get => _theme.Theme == AppTheme.Light;
        set { if (value && _theme.Theme != AppTheme.Light) ToggleTheme(); }
    }

    public bool IsKoreanLanguage
    {
        get => Loc.Language == AppLanguage.Korean;
        set { if (value && Loc.Language != AppLanguage.Korean) ToggleLanguage(); }
    }

    public bool IsEnglishLanguage
    {
        get => Loc.Language == AppLanguage.English;
        set { if (value && Loc.Language != AppLanguage.English) ToggleLanguage(); }
    }

    public string GitPath => GitTab.Core.Git.GitExecutableLocator.Resolve();
    public string AppVersion => AppInfo.Version;

    public bool CrashReportsEnabled
    {
        get => _settings.Current.CrashReports;
        set { _settings.Current.CrashReports = value; _settings.Save(); OnPropertyChanged(); }
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(App.AppDataDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(App.AppDataDir) { UseShellExecute = true });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Opening logs folder failed"); }
    }

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettings(this);

    [RelayCommand]
    private void OpenCommandPalette()
    {
        var vm = new CommandPaletteViewModel(BuildPalette());
        if (_dialogs.ShowCommandPalette(vm) && vm.Chosen is { } chosen)
            chosen.Execute();
    }

    private IReadOnlyList<PaletteItem> BuildPalette()
    {
        PaletteItem P(string key, IRelayCommand cmd) => new(Loc.T(key), () =>
        {
            if (cmd.CanExecute(null)) cmd.Execute(null);
        });

        var list = new List<PaletteItem>
        {
            P("Action.Open", OpenRepositoryCommand),
            P("Repo.Create", CreateRepositoryCommand),
            P("Clone.Action", CloneCommand),
            P("Settings.Title", OpenSettingsCommand),
            P("Theme.Toggle", ToggleThemeCommand),
            P("Language", ToggleLanguageCommand),
            P("Update.Checking", CheckUpdatesCommand),
        };
        if (IsRepositoryOpen)
        {
            list.AddRange(new[]
            {
                P("Action.Refresh", RefreshCommand),
                P("Action.Fetch", FetchCommand),
                P("Action.Pull", PullCommand),
                P("Action.Push", PushCommand),
                P("Reflog.Title", OpenReflogCommand),
                P("Remote.Manage", ManageRemotesCommand),
                P("Worktree.Manage", ManageWorktreesCommand),
                P("Lfs.Manage", ManageLfsCommand),
                P("Submodule.Manage", ManageSubmodulesCommand),
                P("Sparse.Manage", ManageSparseCheckoutCommand),
                P("Cred.Manage", ManageCredentialsCommand),
                P("Menu.Hosting", OpenHostingCommand),
                P("Menu.OpenInEditor", OpenInEditorCommand),
                P("Menu.Statistics", ShowStatisticsCommand),
                P("Menu.Changelog", GenerateChangelogCommand),
                P("Menu.Config", EditConfigCommand),
                P("Menu.ForcePush", ForcePushCommand),
                P("Hosting.PR", CreatePullRequestCommand),
                P("Hosting.Web", OpenRemoteWebCommand),
                P("Gitignore.Title", GenerateGitignoreCommand),
                P("Stash.Push", StashPushCommand),
                P("Patch.Apply", ApplyPatchCommand),
                P("Compare.Title", CompareCommand),
                P("Search.Content", ContentSearchCommand),
            });
        }
        return list;
    }

    [RelayCommand]
    private void ClearCredentials()
    {
        if (!IsRepositoryOpen) { _dialogs.Info(Loc.T("Settings.NoRemote"), Loc.T("Settings.Title")); return; }
        var key = CredentialKey.FromUrl(_repo.GetRemoteUrl());
        if (key is null) { _dialogs.Info(Loc.T("Settings.NoRemote"), Loc.T("Settings.Title")); return; }
        try
        {
            _credentials.Delete(key);
            _dialogs.Info(Loc.T("Settings.CredCleared"), Loc.T("Settings.Title"));
        }
        catch (Exception ex) { _dialogs.Error(ex.Message, Loc.T("Common.Error")); }
    }

    // ---------------------------------------------------------------- Explorer shell integration

    public bool IsShellIntegrationInstalled => _shell.IsInstalled;

    [RelayCommand]
    private void ToggleShellIntegration()
    {
        try
        {
            if (_shell.IsInstalled) _shell.Uninstall();
            else _shell.Install();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Toggling Explorer shell integration failed");
            _dialogs.Error(ex.Message, Loc.T("Common.Error"));
        }
        OnPropertyChanged(nameof(IsShellIntegrationInstalled));
    }

    /// <summary>
    /// Run a command that arrived from the command line or an Explorer right-click: open (or switch
    /// to) the given repository, then perform the verb (pull / push / fetch / commit / log).
    /// </summary>
    public async Task ExecuteShellCommandAsync(string verb, string? path)
    {
        // Clone doesn't open an existing repo — the path is the destination parent folder.
        if (verb == "clone") { await CloneInto(path); return; }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var target = _repo.Discover(path);
            if (target is null)
            {
                _dialogs.Error(Loc.T("Error.NotARepo"), Loc.T("Error.OpenFailed"));
                return;
            }
            if (!IsRepositoryOpen || !string.Equals(RepositoryPath, target, StringComparison.OrdinalIgnoreCase))
                await OpenPath(target);
        }

        if (!IsRepositoryOpen) return;

        switch (verb)
        {
            case "pull": await Pull(); break;
            case "push": await Push(); break;
            case "fetch": await Fetch(); break;
            case "commit": CommitFocusRequested?.Invoke(); break;
                // "open" / "log": the repository is open and the graph (log) is already shown.
        }
    }

    [RelayCommand]
    private async Task GenerateGitignore()
    {
        if (RepositoryPath is null) return;
        if (_dialogs.ShowGitignoreGenerator(RepositoryPath))
            await ReloadAllAsync();
    }

    [RelayCommand]
    private Task CheckUpdates() => CheckForUpdatesAsync(silent: false);

    private async Task CheckForUpdatesAsync(bool silent)
    {
        try
        {
            var update = await _updates.CheckForUpdateAsync();
            if (update is null)
            {
                if (!silent) _dialogs.Info(Loc.T("Update.UpToDate"), AppInfo.ProductName);
                return;
            }
            var msg = Loc.T("Update.Available", update.TagName);
            if (_dialogs.Confirm(msg + "\n\n" + (update.Notes ?? string.Empty), AppInfo.ProductName))
            {
                if (update.InstallerUrl is null)
                {
                    _dialogs.Info(update.ReleaseUrl, AppInfo.ProductName);
                    return;
                }
                var path = await _updates.DownloadInstallerAsync(update);
                if (path is not null) _updates.LaunchInstallerAndExit(path);
                else _dialogs.Error(Loc.T("Update.CheckFailed"), AppInfo.ProductName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
            if (!silent) _dialogs.Error(Loc.T("Update.CheckFailed"), AppInfo.ProductName);
        }
    }

    private void TryClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { Clipboard.SetText(text); }
        catch (Exception ex) { _logger.LogDebug(ex, "Clipboard set failed"); }
    }

    private void SyncRecent()
    {
        RecentRepositories.Clear();
        foreach (var r in _recent.GetAll()) RecentRepositories.Add(r);
    }
}
