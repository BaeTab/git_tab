using System.IO;
using System.Windows;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
using GitTab.App.Views;
using GitTab.Core.Abstractions;
using GitTab.Core.Git;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GitTab.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitTab");

    private ILogger<App>? _logger;
    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // GIT_ASKPASS mode: git launched us to answer a credential prompt. Print the stored value
        // to stdout and exit immediately — no logging, DI, or UI.
        if (Environment.GetEnvironmentVariable("GITTAB_ASKPASS") == "1")
        {
            try { AskPassResponder.Respond(e.Args, Console.Out); } catch { /* never block git */ }
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        Directory.CreateDirectory(Path.Combine(AppDataDir, "logs"));

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppDataDir, "logs", "braid-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Services = BuildServiceProvider();
        _logger = Services.GetRequiredService<ILogger<App>>();

        // Restore the saved language early — the Explorer menu labels are localized at register time.
        var settings = Services.GetRequiredService<ISettingsService>();
        var loc = Services.GetRequiredService<ILocalizationService>(); // sets LocalizationService.Current
        if (Enum.TryParse<AppLanguage>(settings.Current.Language, out var savedLang))
            loc.Language = savedLang;

        // Headless: (un)register the Explorer right-click integration and exit. Used by the in-app
        // toggle so the registry write happens in the real user's HKCU, and available to scripts.
        if (e.Args.Contains("--register-shell") || e.Args.Contains("--unregister-shell"))
        {
            var shell = Services.GetRequiredService<IShellIntegrationService>();
            if (e.Args.Contains("--register-shell")) shell.Install(); else shell.Uninstall();
            Shutdown(0);
            return;
        }

        _logger.LogInformation("GitTab starting up. Version {Version}", AppInfo.Version);
        RegisterGlobalExceptionHandlers();

        var cmd = ParseShellCommand(e.Args);

        // Explorer right-click Pull/Push/Fetch/Commit/Stash open a dedicated dialog (TortoiseGit-style)
        // as their own short-lived process, instead of routing into the full application window.
        // (Clone/Open/Log open the main window instead.)
        if (cmd.Path is not null && cmd.Verb is "pull" or "push" or "fetch" or "commit" or "stash")
        {
            ShowStandaloneDialog(cmd.Verb!, cmd.Path, e.Args);
            return;
        }

        // Single instance: if Git Tab is already running, forward open/log to it and exit so the
        // action lands in the open window instead of starting a second copy.
        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            SingleInstance.TrySend((cmd.Verb ?? string.Empty) + "\n" + (cmd.Path ?? string.Empty));
            _singleInstance.Dispose();
            Shutdown(0);
            return;
        }
        _singleInstance.StartServer(OnShellMessage);

        ApplyTheme(e.Args);

        var window = Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // --topmost keeps the window above others (handy for screenshots / kiosk use).
        if (e.Args.Contains("--topmost"))
        {
            window.Topmost = true;
            window.Activate();
        }

        // If no git.exe is anywhere (bundled / installed / PATH), reads still work via LibGit2Sharp
        // but writes/network don't — tell the user once, with a link, instead of failing silently.
        _ = Task.Run(async () =>
        {
            var runner = Services.GetRequiredService<IGitCommandRunner>();
            if (await runner.IsGitAvailableAsync())
                return;
            window.Dispatcher.Invoke(() =>
                Services.GetRequiredService<IDialogService>().Info(loc.T("Git.NotFound"), loc.T("App.Title")));
        });

        var mainVm = Services.GetRequiredService<ViewModels.MainViewModel>();
        if (cmd.Verb is not null && cmd.Path is not null)
        {
            // A repository path + verb was passed on the command line (shell action / drag-open).
            _ = window.Dispatcher.InvokeAsync(() => _ = mainVm.ExecuteShellCommandAsync(cmd.Verb, cmd.Path));
        }
        else if (settings.Current.ReopenLastRepo)
        {
            // Otherwise reopen the most recently used repository so the app resumes where you left off.
            var toOpen = mainVm.RecentRepositories.FirstOrDefault(r => Directory.Exists(r.Path))?.Path;
            if (toOpen is not null)
                _ = window.Dispatcher.InvokeAsync(() => mainVm.OpenPathCommand.Execute(toOpen));
        }
    }

    /// <summary>Parse a shell/CLI command into a (verb, folder-path) pair. A bare path implies "open".</summary>
    private static (string? Verb, string? Path) ParseShellCommand(string[] args)
    {
        string? verb = null, path = null;
        foreach (var a in args)
        {
            switch (a)
            {
                case "--open": verb = "open"; break;
                case "--log": verb = "log"; break;
                case "--commit": verb = "commit"; break;
                case "--pull": verb = "pull"; break;
                case "--push": verb = "push"; break;
                case "--fetch": verb = "fetch"; break;
                case "--stash": verb = "stash"; break;
                case "--clone": verb = "clone"; break;
                // File-menu verbs — the path argument is a specific file, not a folder.
                case "--filehistory": verb = "filehistory"; break;
                case "--blame": verb = "blame"; break;
                case "--revertfile": verb = "revertfile"; break;
                case "--gitignoreadd": verb = "gitignoreadd"; break;
                default:
                    if (!a.StartsWith("--", StringComparison.Ordinal) && path is null && (Directory.Exists(a) || File.Exists(a)))
                        path = a;
                    break;
            }
        }
        if (path is not null && verb is null) verb = "open";
        return (verb, path);
    }

    private void ApplyTheme(string[] args)
    {
        var settings = Services.GetRequiredService<ISettingsService>();
        // --light / --dark / --theme <Name> force a theme (handy for screenshots); otherwise use the
        // saved preference. --theme accepts any AppTheme name (Dark/Light/Nord/Dracula/…).
        string? themeArg = null;
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--theme") themeArg = args[i + 1];

        var theme = args.Contains("--light") ? AppTheme.Light
            : args.Contains("--dark") ? AppTheme.Dark
            : themeArg is not null && Enum.TryParse<AppTheme>(themeArg, ignoreCase: true, out var ta) ? ta
            : Enum.TryParse<AppTheme>(settings.Current.Theme, out var t) ? t : AppTheme.Dark;
        Services.GetRequiredService<IThemeService>().Apply(theme);
    }

    // Open a standalone TortoiseGit-style dialog for a shell verb, without the main window.
    private void ShowStandaloneDialog(string verb, string path, string[] args)
    {
        ApplyTheme(args);

        var repo = Services.GetRequiredService<IRepositoryService>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        var loc = Services.GetRequiredService<ILocalizationService>();

        var discovered = repo.Discover(path);
        if (discovered is null)
        {
            dialogs.Error(loc.T("Error.NotARepo"), loc.T("Error.OpenFailed"));
            Shutdown(1);
            return;
        }
        repo.Open(discovered);
        var name = new DirectoryInfo(discovered).Name;

        Window win;
        if (verb == "commit")
        {
            var wc = Services.GetRequiredService<WorkingCopyViewModel>();
            wc.Refresh();
            win = new CommitWindow(name) { DataContext = wc };
        }
        else
        {
            var vm = new OperationWindowViewModel(verb, repo,
                Services.GetRequiredService<ICredentialStore>(), dialogs, loc,
                Services.GetRequiredService<ILogger<OperationWindowViewModel>>());
            win = new OperationWindow { DataContext = vm };
        }

        MainWindow = win;
        win.Show();
        win.Activate();
    }

    // A forwarded command line arrived from a second instance (background thread) → run it here.
    private void OnShellMessage(string msg)
    {
        int nl = msg.IndexOf('\n');
        string verb = (nl >= 0 ? msg[..nl] : msg).Trim();
        string path = (nl >= 0 ? msg[(nl + 1)..] : string.Empty).Trim();
        if (verb.Length == 0) verb = "open";

        _ = Dispatcher.InvokeAsync(async () =>
        {
            (MainWindow as MainWindow)?.ActivateForShell();
            var vm = Services.GetRequiredService<ViewModels.MainViewModel>();
            await vm.ExecuteShellCommandAsync(verb, string.IsNullOrEmpty(path) ? null : path);
        });
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog(dispose: true);
            b.SetMinimumLevel(LogLevel.Debug);
        });

        // Core — git.exe resolved from a bundled copy / install / PATH, with this exe wired as the
        // GIT_ASKPASS credential provider so HTTPS auth works with no credential helper installed.
        services.AddSingleton<IGitCommandRunner>(sp =>
            new GitCommandRunner(
                sp.GetRequiredService<ILogger<GitCommandRunner>>(),
                gitExecutable: GitExecutableLocator.Resolve(),
                askPassExe: Environment.ProcessPath));
        services.AddSingleton<IRepositoryService, RepositoryService>();
        services.AddSingleton<IRecentRepositoriesStore>(sp =>
            new RecentRepositoriesStore(sp.GetRequiredService<ILogger<RecentRepositoriesStore>>()));
        services.AddSingleton<GitTab.Core.Gitignore.IGitignoreService, GitTab.Core.Gitignore.GitignoreService>();
        services.AddSingleton<ICommitStatsSource>(sp => new CommitStatsCache(sp.GetRequiredService<IRepositoryService>()));

        // App services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddSingleton<IShellIntegrationService, ShellIntegrationService>();
        services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
        services.AddSingleton<IKeybindingService, KeybindingService>();
        services.AddSingleton<IBookmarkStore>(_ => new GitTab.Core.Git.BookmarkStore());
        services.AddSingleton<GitTab.App.Services.Hosting.IHostingClient, GitTab.App.Services.Hosting.HostingClient>();
        services.AddSingleton<GitTab.App.Services.Hosting.GitHubDeviceFlow>();

        // ViewModels. WorkingCopy/Branches/CommitDetails + the commit-stats source stay registered as
        // singletons for the standalone Explorer "commit" dialog path (ShowStandaloneDialog). The main
        // window instead gets one independent set per repository tab via RepositorySessionFactory.
        services.AddSingleton<WorkingCopyViewModel>();
        services.AddSingleton<BranchesViewModel>();
        services.AddSingleton<CommitDetailsViewModel>();
        services.AddSingleton<RepositorySessionFactory>();
        services.AddSingleton<MainViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unhandled UI exception");
            TryWriteCrash(args.Exception, "UI thread");
            ShowFatal(args.Exception);
            args.Handled = true; // keep the app alive
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _logger?.LogCritical(ex, "Unhandled non-UI exception (terminating={Terminating})", args.IsTerminating);
                TryWriteCrash(ex, "background thread");
            }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void TryWriteCrash(Exception ex, string context)
    {
        try
        {
            if (Services?.GetService<ISettingsService>()?.Current.CrashReports == true)
                CrashReporter.Write(AppDataDir, ex, context);
        }
        catch { /* never throw from the crash path */ }
    }

    private void ShowFatal(Exception ex)
    {
        try
        {
            var loc = Services.GetRequiredService<ILocalizationService>();
            MessageBox.Show(
                $"{ex.Message}\n\n{loc.T("Common.Error")}: {ex.GetType().Name}",
                loc.T("Common.Error"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("GitTab exiting.");
        _singleInstance?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
