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
    private readonly ILogger<MainViewModel> _logger;
    private readonly GraphLayoutEngine _engine = new();

    /// <summary>Raised when a shell "commit" action asks the window to focus the staging panel.</summary>
    public event Action? CommitFocusRequested;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOperationInProgress))]
    [NotifyPropertyChangedFor(nameof(OperationName))]
    private RepositoryStateInfo? _repoState;

    [ObservableProperty] private bool _hasSubmodules;

    public bool IsOperationInProgress => RepoState?.IsInProgress == true;
    public string OperationName => RepoState is { IsInProgress: true } s ? Loc.T("State." + s.Operation) : string.Empty;

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
    private async Task ManageRemotes()
    {
        if (!IsRepositoryOpen) return;
        var vm = new RemotesViewModel(_repo, _dialogs, Loc, _logger);
        _dialogs.ShowRemotes(vm);
        if (vm.Changed) await ReloadAllAsync();
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
                Refs = _refsBySha.TryGetValue(list[i].Sha, out var refs) ? refs : Array.Empty<RefLabel>()
            });
        }

        Rows = rows;
        LaneCount = layout.LaneCount;

        var restore = previousSha is null ? -1 : rows.FindIndex(r => r.Sha == previousSha);
        SelectedCommitIndex = restore >= 0 ? restore : (rows.Count > 0 ? 0 : -1);
        // Force detail refresh if index unchanged but content replaced.
        OnSelectedCommitIndexChanged(SelectedCommitIndex);
    }

    // ---------------------------------------------------------------- remote

    [RelayCommand]
    private Task Fetch() => RunNetworkAsync(() => _repo.FetchAsync());

    [RelayCommand]
    private Task Pull() => RunNetworkAsync(() => _repo.PullAsync());

    [RelayCommand]
    private Task Push() => RunNetworkAsync(() =>
    {
        var current = Branches.Local.FirstOrDefault(b => b.IsCurrent);
        var needsUpstream = current?.Model.UpstreamFriendlyName is null;
        return _repo.PushAsync(setUpstream: needsUpstream);
    });

    private async Task RunNetworkAsync(Func<Task<GitResult>> op)
    {
        if (!IsRepositoryOpen) return;
        IsBusy = true;
        StatusText = Loc.T("Status.Working");
        var ok = await RunWithAuthRetryAsync(op);
        IsBusy = false;
        if (ok) await ReloadAllAsync();
        else StatusText = Loc.T("Status.Ready");
    }

    /// <summary>
    /// Run a network op; on an authentication failure, prompt for credentials in the GUI, store
    /// them (Windows Credential Manager), and retry once. This makes HTTPS auth fully GUI-driven.
    /// </summary>
    private async Task<bool> RunWithAuthRetryAsync(Func<Task<GitResult>> op)
    {
        GitResult result;
        try { result = await GitAuth.RunWithRetryAsync(op, _repo, _credentials, _dialogs, Loc, _logger); }
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
        if (await GitUi.RunAsync(() => _repo.RunRawAsync(new[] { "tag", name, SelectedCommit.Sha }), _dialogs, Loc, _logger))
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

    [RelayCommand] private Task AbortOperation() => RunNetworkAsync(() => _repo.AbortOperationAsync());
    [RelayCommand] private Task ContinueOperation() => RunNetworkAsync(() => _repo.ContinueOperationAsync());

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

    [RelayCommand] private Task SubmoduleUpdate() => RunNetworkAsync(() => _repo.SubmoduleUpdateAsync());

    [RelayCommand]
    private async Task ResolveConflict(FileChangeViewModel? file)
    {
        if (file is null || RepositoryPath is null) return;
        if (!_dialogs.ShowConflictResolver(RepositoryPath, file.Path)) return;
        if (await GitUi.RunAsync(() => _repo.MarkResolvedAsync(file.Path), _dialogs, Loc, _logger))
            await ReloadAllAsync();
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
