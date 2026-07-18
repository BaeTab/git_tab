using System.IO;
using System.Windows;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.App.ViewModels;
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

    protected override void OnStartup(StartupEventArgs e)
    {
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
        _logger.LogInformation("GitTab starting up. Version {Version}", AppInfo.Version);

        RegisterGlobalExceptionHandlers();

        // Theme + language must be applied before any window is shown.
        Services.GetRequiredService<IThemeService>().Apply(AppTheme.Dark);
        _ = Services.GetRequiredService<ILocalizationService>(); // sets LocalizationService.Current

        var window = Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // Auto-open: a repository passed on the command line (e.g. an "Open in Git Tab" shell action),
        // otherwise reopen the most recently used repository so the app starts where you left off.
        var mainVm = Services.GetRequiredService<ViewModels.MainViewModel>();
        string? toOpen = e.Args.Length > 0 && Directory.Exists(e.Args[0])
            ? e.Args[0]
            : mainVm.RecentRepositories.FirstOrDefault(r => Directory.Exists(r.Path))?.Path;
        if (toOpen is not null)
            _ = window.Dispatcher.InvokeAsync(() => mainVm.OpenPathCommand.Execute(toOpen));
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

        // Core
        services.AddSingleton<IGitCommandRunner>(sp =>
            new GitCommandRunner(sp.GetRequiredService<ILogger<GitCommandRunner>>()));
        services.AddSingleton<IRepositoryService, RepositoryService>();
        services.AddSingleton<IRecentRepositoriesStore>(sp =>
            new RecentRepositoriesStore(sp.GetRequiredService<ILogger<RecentRepositoriesStore>>()));

        // App services
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        // ViewModels
        services.AddSingleton<WorkingCopyViewModel>();
        services.AddSingleton<BranchesViewModel>();
        services.AddSingleton<CommitDetailsViewModel>();
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
            ShowFatal(args.Exception);
            args.Handled = true; // keep the app alive
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                _logger?.LogCritical(ex, "Unhandled non-UI exception (terminating={Terminating})", args.IsTerminating);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
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
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
