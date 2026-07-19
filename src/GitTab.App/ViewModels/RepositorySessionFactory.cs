using System.IO;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Git;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// Builds a fully independent <see cref="RepositorySessionViewModel"/> — its own
/// <see cref="IRepositoryService"/>, commit-stats cache, and Working-copy/Branches/Details
/// sub-view-models — from the app-global singletons. One session per repository tab, so tabs never
/// share live state or a git handle.
/// </summary>
public sealed class RepositorySessionFactory
{
    private readonly IGitCommandRunner _git;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ICredentialStore _credentials;
    private readonly Services.Hosting.IHostingClient _hosting;
    private readonly IBookmarkStore _bookmarks;
    private readonly ILoggerFactory _loggerFactory;

    public RepositorySessionFactory(
        IGitCommandRunner git,
        IDialogService dialogs,
        ILocalizationService loc,
        ICredentialStore credentials,
        Services.Hosting.IHostingClient hosting,
        IBookmarkStore bookmarks,
        ILoggerFactory loggerFactory)
    {
        _git = git;
        _dialogs = dialogs;
        _loc = loc;
        _credentials = credentials;
        _hosting = hosting;
        _bookmarks = bookmarks;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Open the already-discovered repository at <paramref name="discoveredPath"/> in a brand-new
    /// session graph. The caller is responsible for calling <see cref="RepositorySessionViewModel.ReloadAllAsync"/>
    /// and, on tab close, <see cref="RepositorySessionViewModel.Dispose"/>.
    /// </summary>
    public RepositorySessionViewModel Create(string discoveredPath)
    {
        var repo = new RepositoryService(_git, _loggerFactory.CreateLogger<RepositoryService>());
        repo.Open(discoveredPath);

        var stats = new CommitStatsCache(repo);
        var workingCopy = new WorkingCopyViewModel(repo, _dialogs, _loc, _loggerFactory.CreateLogger<WorkingCopyViewModel>());
        var branches = new BranchesViewModel(repo, _dialogs, _loc, _loggerFactory.CreateLogger<BranchesViewModel>());
        var details = new CommitDetailsViewModel(repo, _hosting, _loggerFactory.CreateLogger<CommitDetailsViewModel>());

        var session = new RepositorySessionViewModel(
            repo, workingCopy, branches, details, stats,
            _dialogs, _loc, _credentials, _hosting, _bookmarks,
            _loggerFactory.CreateLogger<RepositorySessionViewModel>());

        session.Initialize(discoveredPath, new DirectoryInfo(discoveredPath).Name);
        return session;
    }
}
