using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// The application shell. Owns app-global state (theme, language, updates, recent list, Explorer
/// integration) and a collection of <see cref="RepositorySessionViewModel"/> — one per open
/// repository tab, each fully independent. The XAML binds repository content against
/// <see cref="ActiveSession"/>; a few context-menu and status-bar members are re-exposed here as
/// pass-throughs so the existing window-scoped bindings keep working.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    // The singleton repository service is used ONLY for path discovery here; every open repository
    // gets its own service instance inside its session (see RepositorySessionFactory).
    private readonly IRepositoryService _repo;
    private readonly IRecentRepositoriesStore _recent;
    private readonly IDialogService _dialogs;
    private readonly IThemeService _theme;
    private readonly IUpdateService _updates;
    private readonly IShellIntegrationService _shell;
    private readonly ICredentialStore _credentials;
    private readonly ISettingsService _settings;
    private readonly IKeybindingService _keybindings;
    private readonly RepositorySessionFactory _factory;
    private readonly ILogger<MainViewModel> _logger;

    // Periodically fetches every open repository and refreshes its "behind" count. Only created when a
    // WPF Application is running (never in headless tests), and it ticks on the UI thread.
    private System.Windows.Threading.DispatcherTimer? _fetchTimer;
    private bool _polling;

    /// <summary>Raised when a shell "commit" action asks the window to focus the staging panel.</summary>
    public event Action? CommitFocusRequested;

    /// <summary>Raised when the user presses Ctrl+F to jump to the graph search box.</summary>
    public event Action? SearchFocusRequested;

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
        IKeybindingService keybindings,
        RepositorySessionFactory factory,
        ILogger<MainViewModel> logger)
    {
        _repo = repo;
        _recent = recent;
        _dialogs = dialogs;
        Loc = loc;
        _theme = theme;
        _updates = updates;
        _shell = shell;
        _credentials = credentials;
        _settings = settings;
        _keybindings = keybindings;
        _factory = factory;
        _logger = logger;

        foreach (var r in _recent.GetAll()) RecentRepositories.Add(r);

        ApplyPersonalization();
        if (BackgroundFetchEnabled) EnsureFetchTimer()?.Start();
    }

    // ---- background fetch (periodic, per open repository) ----

    /// <summary>Periodically fetch every open repository and refresh its behind count. Persisted.</summary>
    public bool BackgroundFetchEnabled
    {
        get => _settings.Current?.BackgroundFetch ?? true;
        set
        {
            if (_settings.Current is { } s) { s.BackgroundFetch = value; _settings.Save(); }
            if (value) EnsureFetchTimer()?.Start(); else _fetchTimer?.Stop();
            OnPropertyChanged();
        }
    }

    private System.Windows.Threading.DispatcherTimer? EnsureFetchTimer()
    {
        if (_fetchTimer is not null) return _fetchTimer;
        // No WPF Application (unit tests / headless) → no timer; polling is UI-thread work.
        if (System.Windows.Application.Current is null) return null;
        _fetchTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Clamp(_settings.Current?.BackgroundFetchMinutes ?? 3, 1, 60))
        };
        _fetchTimer.Tick += async (_, _) => await PollAllSessionsAsync();
        return _fetchTimer;
    }

    private async Task PollAllSessionsAsync()
    {
        if (_polling || Sessions.Count == 0) return;
        _polling = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            foreach (var session in Sessions.ToArray())
            {
                try { await session.PollRemoteAsync(cts.Token); }
                catch (Exception ex) { _logger.LogDebug(ex, "Background fetch failed for a session"); }
            }
        }
        finally { _polling = false; }
    }

    public ILocalizationService Loc { get; }
    public IThemeService Theme => _theme;

    public ObservableCollection<RecentRepository> RecentRepositories { get; } = new();

    // ---- open repositories (tabs) ----
    public ObservableCollection<RepositorySessionViewModel> Sessions { get; } = new();

    [ObservableProperty] private RepositorySessionViewModel? _activeSession;

    /// <summary>True when at least one repository is open (drives the toolbar / welcome state).</summary>
    public bool IsRepositoryOpen => ActiveSession is not null;

    partial void OnActiveSessionChanged(RepositorySessionViewModel? oldValue, RepositorySessionViewModel? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnSessionPropertyChanged;
        if (newValue is not null) newValue.PropertyChanged += OnSessionPropertyChanged;
        // The active session changed wholesale — refresh every window-scoped pass-through binding.
        OnPropertyChanged(string.Empty);
    }

    // Mirror the live status-bar values of the active session so the (window-scoped) status bar
    // updates while a fetch/pull runs.
    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RepositorySessionViewModel.StatusText): OnPropertyChanged(nameof(StatusText)); break;
            case nameof(RepositorySessionViewModel.IsBusy): OnPropertyChanged(nameof(IsBusy)); break;
            case nameof(RepositorySessionViewModel.RepositoryPath): OnPropertyChanged(nameof(RepositoryPath)); break;
            case nameof(RepositorySessionViewModel.CanCancelNetwork): OnPropertyChanged(nameof(CanCancelNetwork)); break;
        }
    }

    // ---- status-bar pass-throughs (window-scoped bindings) ----
    public string StatusText => ActiveSession?.StatusText is { Length: > 0 } s ? s : Loc.T("Status.NoRepo");
    public bool IsBusy => ActiveSession?.IsBusy ?? false;
    public string? RepositoryPath => ActiveSession?.RepositoryPath;
    public bool CanCancelNetwork => ActiveSession?.CanCancelNetwork ?? false;
    public ICommand? CancelNetworkCommand => ActiveSession?.CancelNetworkCommand;

    // ---- context-menu pass-throughs (reached via PlacementTarget.Tag = window DataContext) ----
    public ObservableCollection<StashInfo>? Stashes => ActiveSession?.Stashes;
    public ICommand? StashPushCommand => ActiveSession?.StashPushCommand;
    public ICommand? StashPopCommand => ActiveSession?.StashPopCommand;
    public ICommand? StashApplyCommand => ActiveSession?.StashApplyCommand;
    public ICommand? StashDropCommand => ActiveSession?.StashDropCommand;
    public ICommand? StashDiffCommand => ActiveSession?.StashDiffCommand;
    public ICommand? StashToBranchCommand => ActiveSession?.StashToBranchCommand;
    public ICommand? ResolveConflictCommand => ActiveSession?.ResolveConflictCommand;
    public ICommand? FileHistoryCommand => ActiveSession?.FileHistoryCommand;
    public ICommand? BlameCommand => ActiveSession?.BlameCommand;
    public ICommand? OpenFileInEditorCommand => ActiveSession?.OpenFileInEditorCommand;
    public ICommand? ExternalDiffCommand => ActiveSession?.ExternalDiffCommand;
    public ICommand? AddToGitignoreCommand => ActiveSession?.AddToGitignoreCommand;
    public ICommand? RestoreFileCommand => ActiveSession?.RestoreFileCommand;

    // ---------------------------------------------------------------- open / create / clone

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

        // Optionally wire up the origin remote in the same step, on the newly created session.
        if (ActiveSession is { } session && !string.IsNullOrWhiteSpace(vm.RemoteUrl))
            await session.AddOriginAndReloadAsync(vm.RemoteUrl.Trim());
    }

    [RelayCommand]
    private Task Clone() => CloneInto(null);

    private bool _cloneBlobless;

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
        var result = await RunCloneWithAuthAsync(url, target, cts.Token);

        if (result is { Success: true })
        {
            await OpenPath(target);
        }
        else if (result is not null)
        {
            var body = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
            _dialogs.Error(body, Loc.T("Error.GitFailed"));
        }
    }

    // Clone can't use a session (no repo is open yet), so it keys credentials off the URL and runs
    // on the shared discovery-only repository service.
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
    private async Task OpenRecent(RecentRepository? repo)
    {
        if (repo is not null) await OpenPath(repo.Path);
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

        // Already open in a tab? Just focus it — its live state is preserved.
        var existing = Sessions.FirstOrDefault(s =>
            string.Equals(s.RepositoryPath, discovered, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { ActiveSession = existing; return; }

        RepositorySessionViewModel session;
        try { session = _factory.Create(discovered); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open repository {Path}", path);
            _dialogs.Error(ex.Message, Loc.T("Error.OpenFailed"));
            return;
        }

        Sessions.Add(session);
        ActiveSession = session;
        _recent.Add(discovered);
        SyncRecent();
        await session.ReloadAllAsync();
        _ = CheckForUpdatesAsync(silent: true);
    }

    private void SyncRecent()
    {
        RecentRepositories.Clear();
        foreach (var r in _recent.GetAll()) RecentRepositories.Add(r);
    }

    // ---------------------------------------------------------------- tabs

    [RelayCommand]
    private void CloseTab(RepositorySessionViewModel? session)
    {
        if (session is null) return;
        bool wasActive = ReferenceEquals(session, ActiveSession);
        int idx = Sessions.IndexOf(session);
        Sessions.Remove(session);
        session.Dispose();
        if (!wasActive) return;
        ActiveSession = Sessions.Count == 0 ? null : Sessions[Math.Min(idx, Sessions.Count - 1)];
    }

    [RelayCommand]
    private void NextTab()
    {
        if (Sessions.Count < 2 || ActiveSession is null) return;
        ActiveSession = Sessions[(Sessions.IndexOf(ActiveSession) + 1) % Sessions.Count];
    }

    [RelayCommand]
    private void PreviousTab()
    {
        if (Sessions.Count < 2 || ActiveSession is null) return;
        ActiveSession = Sessions[(Sessions.IndexOf(ActiveSession) - 1 + Sessions.Count) % Sessions.Count];
    }

    [RelayCommand]
    private void FocusSearch() => SearchFocusRequested?.Invoke();

    // ---------------------------------------------------------------- keybinding-stable command forwarders
    //
    // The window builds its InputBindings from these — not directly from ActiveSession's commands —
    // because ActiveSession changes every time the user switches tabs. A KeyBinding built against
    // "ActiveSession.RefreshCommand" today would keep pointing at *today's* session forever; routing
    // through a parameterless shell command that reads ActiveSession live means the same KeyBinding
    // instance keeps working no matter which tab is active.

    [RelayCommand]
    private async Task RefreshActive()
    {
        if (ActiveSession?.RefreshCommand is { } cmd && cmd.CanExecute(null))
            await cmd.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task FetchActive()
    {
        if (ActiveSession?.FetchCommand is { } cmd && cmd.CanExecute(null))
            await cmd.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task CommitActive()
    {
        if (ActiveSession?.WorkingCopy?.CommitCommand is { } cmd && cmd.CanExecute(null))
            await cmd.ExecuteAsync(null);
    }

    [RelayCommand]
    private void CloseActiveTab() => CloseTab(ActiveSession);

    // ---------------------------------------------------------------- theme / language / update

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(HighContrastEnabled));
        OnPropertyChanged(nameof(SelectedThemeOption));
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Loc.Toggle();
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(IsKoreanLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(DensityOptions));
        // Menu labels are baked into the registry — refresh them if the integration is installed.
        if (_shell.IsInstalled)
        {
            try { _shell.Install(); } catch (Exception ex) { _logger.LogDebug(ex, "Shell relabel skipped"); }
        }
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

    // ---------------------------------------------------------------- settings-dialog bindings

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

    /// <summary>A selectable built-in theme with its display name (brand names shown untranslated).</summary>
    public sealed record ThemeOption(AppTheme Value, string Name);

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new[]
    {
        new ThemeOption(AppTheme.Dark, "Dark"),
        new ThemeOption(AppTheme.Light, "Light"),
        new ThemeOption(AppTheme.Midnight, "Midnight"),
        new ThemeOption(AppTheme.Nord, "Nord"),
        new ThemeOption(AppTheme.Dracula, "Dracula"),
        new ThemeOption(AppTheme.Solarized, "Solarized"),
        new ThemeOption(AppTheme.Rose, "Rosé Pine"),
        new ThemeOption(AppTheme.HighContrast, "High Contrast"),
    };

    public ThemeOption? SelectedThemeOption
    {
        get => ThemeOptions.FirstOrDefault(o => o.Value == _theme.Theme);
        set { if (value is not null) SetTheme(value); }
    }

    [RelayCommand]
    private void SetTheme(ThemeOption? option)
    {
        if (option is null || _theme.Theme == option.Value) return;
        _theme.Apply(option.Value);
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(SelectedThemeOption));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(HighContrastEnabled));
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

    /// <summary>A selectable UI language with its own endonym (shown untranslated in the picker).</summary>
    public sealed record LanguageOption(AppLanguage Value, string Name);

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[]
    {
        new LanguageOption(AppLanguage.Korean, "한국어"),
        new LanguageOption(AppLanguage.English, "English"),
        new LanguageOption(AppLanguage.Japanese, "日本語"),
        new LanguageOption(AppLanguage.Chinese, "中文"),
        new LanguageOption(AppLanguage.Spanish, "Español"),
    };

    public LanguageOption? SelectedLanguageOption
    {
        get => LanguageOptions.FirstOrDefault(o => o.Value == Loc.Language);
        set { if (value is not null) SetLanguage(value); }
    }

    [RelayCommand]
    private void SetLanguage(LanguageOption? option)
    {
        if (option is null || Loc.Language == option.Value) return;
        Loc.Language = option.Value;
        _settings.Update(_theme.Theme, Loc.Language);
        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(IsKoreanLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
        OnPropertyChanged(nameof(DensityOptions));
        // Menu labels are baked into the registry — refresh them if the integration is installed.
        if (_shell.IsInstalled)
        {
            try { _shell.Install(); } catch (Exception ex) { _logger.LogDebug(ex, "Shell relabel skipped"); }
        }
    }

    /// <summary>High-contrast accessibility theme (overrides light/dark for low-vision use).</summary>
    public bool HighContrastEnabled
    {
        get => _theme.Theme == AppTheme.HighContrast;
        set
        {
            var target = value ? AppTheme.HighContrast : AppTheme.Dark;
            if (_theme.Theme == target) return;
            _theme.Apply(target);
            _settings.Update(_theme.Theme, Loc.Language);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(IsLightTheme));
        }
    }

    /// <summary>UI/font zoom in percent (100/115/130/150). Persisted; applied as a root LayoutTransform.</summary>
    public IReadOnlyList<int> UiScaleOptions { get; } = new[] { 100, 115, 130, 150 };

    public int UiScalePercent
    {
        get => _settings.Current?.UiScalePercent is int p and >= 100 ? p : 100;
        set
        {
            if (_settings.Current is { } s && s.UiScalePercent != value) { s.UiScalePercent = value; _settings.Save(); }
            OnPropertyChanged();
            OnPropertyChanged(nameof(UiScale));
        }
    }

    public double UiScale => UiScalePercent / 100.0;

    // ---------------------------------------------------------------- personalization (settings-dialog bindings)

    /// <summary>Custom accent color ("" = theme's own accent, else "#RRGGBB").</summary>
    public string AccentColor
    {
        get => _settings.Current?.AccentColor ?? "";
        set
        {
            if (_settings.Current is { } s) { s.AccentColor = value; _settings.Save(); }
            _theme.RefreshPersonalization();
            OnPropertyChanged();
        }
    }

    public string UiFontFamily
    {
        get => _settings.Current?.UiFontFamily ?? "Segoe UI";
        set
        {
            if (_settings.Current is { } s) { s.UiFontFamily = value; _settings.Save(); }
            _theme.RefreshPersonalization();
            OnPropertyChanged();
        }
    }

    public string DiffFontFamily
    {
        get => _settings.Current?.DiffFontFamily ?? "Consolas";
        set
        {
            if (_settings.Current is { } s) { s.DiffFontFamily = value; _settings.Save(); }
            _theme.RefreshPersonalization();
            OnPropertyChanged();
        }
    }

    public double DiffFontSize
    {
        get => _settings.Current?.DiffFontSize ?? 12.5;
        set
        {
            if (_settings.Current is { } s) { s.DiffFontSize = value; _settings.Save(); }
            _theme.RefreshPersonalization();
            OnPropertyChanged();
        }
    }

    /// <summary>Graph row height in pixels (compact 24 / normal 30 / comfortable 38).</summary>
    public int GraphRowHeight
    {
        get => _settings.Current?.GraphRowHeight ?? 30;
        set
        {
            if (_settings.Current is { } s) { s.GraphRowHeight = value; _settings.Save(); }
            GraphAppearance.RowHeight = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDensity));
        }
    }

    public bool GraphGlowEnabled
    {
        get => _settings.Current?.GraphGlow ?? true;
        set
        {
            if (_settings.Current is { } s) { s.GraphGlow = value; _settings.Save(); }
            GraphAppearance.Glow = value;
            OnPropertyChanged();
        }
    }

    public bool DiffSplitDefault
    {
        get => _settings.Current?.DiffSplitDefault ?? false;
        set
        {
            if (_settings.Current is { } s) { s.DiffSplitDefault = value; _settings.Save(); }
            DiffDefaults.Split = value;
            OnPropertyChanged();
        }
    }

    public int DiffContextDefault
    {
        get => _settings.Current?.DiffContextDefault ?? 3;
        set
        {
            if (_settings.Current is { } s) { s.DiffContextDefault = value; _settings.Save(); }
            DiffDefaults.Context = value;
            OnPropertyChanged();
        }
    }

    public bool DiffIgnoreWhitespaceDefault
    {
        get => _settings.Current?.DiffIgnoreWhitespaceDefault ?? false;
        set
        {
            if (_settings.Current is { } s) { s.DiffIgnoreWhitespaceDefault = value; _settings.Save(); }
            DiffDefaults.IgnoreWhitespace = value;
            OnPropertyChanged();
        }
    }

    public bool DiffWordWrap
    {
        get => _settings.Current?.DiffWordWrap ?? false;
        set
        {
            if (_settings.Current is { } s) { s.DiffWordWrap = value; _settings.Save(); }
            DiffDefaults.WordWrap = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Reopen the most-recently-open repository on the next startup.</summary>
    public bool ReopenLastRepo
    {
        get => _settings.Current?.ReopenLastRepo ?? true;
        set
        {
            if (_settings.Current is { } s) { s.ReopenLastRepo = value; _settings.Save(); }
            OnPropertyChanged();
        }
    }

    /// <summary>Show absolute timestamps instead of relative ("2h ago") ones throughout the app.</summary>
    public bool AbsoluteDates
    {
        get => _settings.Current?.AbsoluteDates ?? false;
        set
        {
            if (_settings.Current is { } s) { s.AbsoluteDates = value; _settings.Save(); }
            RelativeTime.UseAbsolute = value;
            _theme.RefreshPersonalization(); // forces a graph repaint so dates re-render
            OnPropertyChanged();
        }
    }

    /// <summary>Background-fetch interval in minutes; restarts the timer immediately when changed.</summary>
    public int BackgroundFetchMinutes
    {
        get => _settings.Current?.BackgroundFetchMinutes ?? 3;
        set
        {
            if (_settings.Current is { } s) { s.BackgroundFetchMinutes = value; _settings.Save(); }
            _fetchTimer?.Stop();
            if (_fetchTimer is { } t) t.Interval = TimeSpan.FromMinutes(Math.Clamp(value, 1, 60));
            if (BackgroundFetchEnabled) EnsureFetchTimer()?.Start();
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<string> UiFontOptions { get; } = new[]
    {
        "Segoe UI", "Segoe UI Variable Text", "Malgun Gothic", "Arial", "Calibri", "Verdana", "Tahoma"
    };

    public IReadOnlyList<string> DiffFontOptions { get; } = new[]
    {
        "Consolas", "Cascadia Mono", "Cascadia Code", "Courier New", "D2Coding", "JetBrains Mono", "Lucida Console"
    };

    public IReadOnlyList<double> DiffFontSizeOptions { get; } = new[] { 11, 11.5, 12, 12.5, 13, 14, 15, 16 };

    public IReadOnlyList<int> DiffContextOptions { get; } = new[] { 1, 3, 5, 10 };

    public IReadOnlyList<int> FetchIntervalOptions { get; } = new[] { 1, 3, 5, 10, 15, 30 };

    /// <summary>A selectable graph-row density with its localized display name.</summary>
    public sealed record DensityOption(int Value, string Name);

    public IReadOnlyList<DensityOption> DensityOptions => new[]
    {
        new DensityOption(24, Loc.T("Settings.Density.Compact")),
        new DensityOption(30, Loc.T("Settings.Density.Normal")),
        new DensityOption(38, Loc.T("Settings.Density.Comfortable")),
    };

    public DensityOption? SelectedDensity
    {
        get => DensityOptions.FirstOrDefault(o => o.Value == GraphRowHeight);
        set { if (value is not null) GraphRowHeight = value.Value; }
    }

    /// <summary>Quick-pick accent color swatches ("" = theme's own accent).</summary>
    public IReadOnlyList<string> AccentPresets { get; } = new[]
    {
        "", "#6B78E8", "#88C0D0", "#BD93F9", "#268BD2", "#C4A7E7", "#4FBF8B", "#E56B7B", "#FFB43C", "#FF6EC7"
    };

    [RelayCommand]
    private void SetAccent(string? hex) => AccentColor = hex ?? "";

    /// <summary>Push every persisted personalization setting into the live holders the UI reads from.
    /// Called once at startup so the graph/diff/date preferences are correct before the first paint.</summary>
    private void ApplyPersonalization()
    {
        if (_settings.Current is not { } s) return;
        GraphAppearance.RowHeight = s.GraphRowHeight;
        GraphAppearance.Glow = s.GraphGlow;
        DiffDefaults.Split = s.DiffSplitDefault;
        DiffDefaults.Context = s.DiffContextDefault;
        DiffDefaults.IgnoreWhitespace = s.DiffIgnoreWhitespaceDefault;
        DiffDefaults.WordWrap = s.DiffWordWrap;
        RelativeTime.UseAbsolute = s.AbsoluteDates;
    }

    public string GitPath => GitTab.Core.Git.GitExecutableLocator.Resolve();
    public string AppVersion => AppInfo.Version;

    public bool CrashReportsEnabled
    {
        get => _settings.Current.CrashReports;
        set { _settings.Current.CrashReports = value; _settings.Save(); OnPropertyChanged(); }
    }

    /// <summary>Beta update channel — include prereleases when checking for updates.</summary>
    public bool UpdateBetaChannelEnabled
    {
        get => string.Equals(_settings.Current?.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
        set
        {
            if (_settings.Current is { } s) { s.UpdateChannel = value ? "Beta" : "Stable"; _settings.Save(); }
            OnPropertyChanged();
        }
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
    private void OpenKeybindings() => _dialogs.ShowKeybindings(new KeybindingsViewModel(_keybindings, Loc));

    // ---------------------------------------------------------------- command palette

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
            P("Cred.Manage", ManageCredentialsCommand),
        };
        if (ActiveSession is { } s)
        {
            list.AddRange(new[]
            {
                P("Action.Refresh", s.RefreshCommand),
                P("Action.Fetch", s.FetchCommand),
                P("Action.Pull", s.PullCommand),
                P("Action.Push", s.PushCommand),
                P("Reflog.Title", s.OpenReflogCommand),
                P("Remote.Manage", s.ManageRemotesCommand),
                P("Worktree.Manage", s.ManageWorktreesCommand),
                P("Lfs.Manage", s.ManageLfsCommand),
                P("Submodule.Manage", s.ManageSubmodulesCommand),
                P("Sparse.Manage", s.ManageSparseCheckoutCommand),
                P("Menu.Hosting", s.OpenHostingCommand),
                P("Menu.OpenInEditor", s.OpenInEditorCommand),
                P("Menu.Statistics", s.ShowStatisticsCommand),
                P("Menu.Changelog", s.GenerateChangelogCommand),
                P("Menu.Config", s.EditConfigCommand),
                P("Menu.ForcePush", s.ForcePushCommand),
                P("Hosting.PR", s.CreatePullRequestCommand),
                P("Hosting.Web", s.OpenRemoteWebCommand),
                P("Gitignore.Title", s.GenerateGitignoreCommand),
                P("Stash.Push", s.StashPushCommand),
                P("Patch.Apply", s.ApplyPatchCommand),
                P("Compare.Title", s.CompareCommand),
                P("Search.Content", s.ContentSearchCommand),
            });
        }
        return list;
    }

    // ---------------------------------------------------------------- credentials (app-global)

    [RelayCommand]
    private void ManageCredentials()
    {
        var vm = new CredentialsViewModel(_credentials, _dialogs, Loc, _logger);
        _dialogs.ShowCredentials(vm);
    }

    [RelayCommand]
    private void ClearCredentials()
    {
        var url = ActiveSession?.GetRemoteUrl();
        var key = url is null ? null : CredentialKey.FromUrl(url);
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
    private static readonly string[] FileVerbs = { "filehistory", "blame", "revertfile", "gitignoreadd" };

    public async Task ExecuteShellCommandAsync(string verb, string? path)
    {
        // Clone doesn't open an existing repo — the path is the destination parent folder.
        if (verb == "clone") { await CloneInto(path); return; }

        // File-menu verbs: the path is a specific file, not a repository folder.
        if (Array.IndexOf(FileVerbs, verb) >= 0) { await ExecuteFileCommandAsync(verb, path); return; }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var target = _repo.Discover(path);
            if (target is null)
            {
                _dialogs.Error(Loc.T("Error.NotARepo"), Loc.T("Error.OpenFailed"));
                return;
            }
            var existing = Sessions.FirstOrDefault(s =>
                string.Equals(s.RepositoryPath, target, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) ActiveSession = existing;
            else await OpenPath(target);
        }

        if (ActiveSession is not { } session) return;

        switch (verb)
        {
            case "pull": await session.PullCommand.ExecuteAsync(null); break;
            case "push": await session.PushCommand.ExecuteAsync(null); break;
            case "fetch": await session.FetchCommand.ExecuteAsync(null); break;
            case "commit": CommitFocusRequested?.Invoke(); break;
                // "open" / "log": the repository is open and the graph (log) is already shown.
        }
    }

    // Explorer file-menu action: the argument is a file. Discover its repository, open/focus that tab,
    // then run the action against the file's repo-relative path.
    private async Task ExecuteFileCommandAsync(string verb, string? file)
    {
        if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file)) return;

        var dir = System.IO.Path.GetDirectoryName(file);
        var repoRoot = dir is null ? null : _repo.Discover(dir);
        if (repoRoot is null)
        {
            _dialogs.Error(Loc.T("Error.NotARepo"), Loc.T("Error.OpenFailed"));
            return;
        }

        var existing = Sessions.FirstOrDefault(s =>
            string.Equals(s.RepositoryPath, repoRoot, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) ActiveSession = existing;
        else await OpenPath(repoRoot);

        if (ActiveSession is not { } session) return;

        var relative = System.IO.Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        session.RunFileShellAction(verb, relative);
    }
}
