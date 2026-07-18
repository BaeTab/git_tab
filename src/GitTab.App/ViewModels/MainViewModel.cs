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
    private readonly ILogger<MainViewModel> _logger;
    private readonly GraphLayoutEngine _engine = new();

    private IReadOnlyList<CommitInfo> _allCommits = Array.Empty<CommitInfo>();
    private IReadOnlyDictionary<string, IReadOnlyList<RefLabel>> _refsBySha =
        new Dictionary<string, IReadOnlyList<RefLabel>>();
    private bool _childrenWired;

    public MainViewModel(
        IRepositoryService repo,
        IRecentRepositoriesStore recent,
        IDialogService dialogs,
        ILocalizationService loc,
        IThemeService theme,
        IUpdateService updates,
        WorkingCopyViewModel workingCopy,
        BranchesViewModel branches,
        CommitDetailsViewModel details,
        ILogger<MainViewModel> logger)
    {
        _repo = repo;
        _recent = recent;
        _dialogs = dialogs;
        Loc = loc;
        _theme = theme;
        _updates = updates;
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
                var commits = _repo.GetCommits(5000);
                var branches = _repo.GetBranches();
                var refs = _repo.GetRefLabelsBySha();
                var head = _repo.GetHead();
                return (commits, branches, refs, head);
            });

            _allCommits = data.commits;
            _refsBySha = data.refs;
            Branches.Refresh(data.branches);
            WorkingCopy.Refresh();

            var current = data.branches.FirstOrDefault(b => b.IsCurrent);
            CurrentBranchName = current?.FriendlyName ?? (data.head.IsDetached ? "(detached)" : data.head.BranchFriendlyName);
            Ahead = current?.Ahead ?? 0;
            Behind = current?.Behind ?? 0;

            ApplyFilter();
            StatusText = Loc.T("Status.Ready");
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
        var ok = await GitUi.RunAsync(op, _dialogs, Loc, _logger);
        IsBusy = false;
        if (ok) await ReloadAllAsync();
        else StatusText = Loc.T("Status.Ready");
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

    [RelayCommand] private void ToggleTheme() => _theme.Toggle();
    [RelayCommand] private void ToggleLanguage() => Loc.Toggle();

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
