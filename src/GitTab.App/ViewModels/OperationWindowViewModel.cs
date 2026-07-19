using System.IO;
using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>
/// Drives a standalone Pull/Push/Fetch dialog launched from the Explorer right-click menu — runs
/// the operation (with GUI auth retry) and surfaces its output, TortoiseGit-style, without opening
/// the full application window.
/// </summary>
public sealed partial class OperationWindowViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly ICredentialStore _credentials;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogger<OperationWindowViewModel> _logger;
    private readonly string _verb;

    public OperationWindowViewModel(string verb, IRepositoryService repo, ICredentialStore credentials,
        IDialogService dialogs, ILocalizationService loc, ILogger<OperationWindowViewModel> logger)
    {
        _verb = verb;
        _repo = repo;
        _credentials = credentials;
        _dialogs = dialogs;
        _loc = loc;
        _logger = logger;

        Loc = loc;
        Title = loc.T(verb switch { "pull" => "Action.Pull", "push" => "Action.Push", "stash" => "Stash.Push", _ => "Action.Fetch" });
        RepositoryName = new DirectoryInfo(repo.CurrentRepositoryPath ?? ".").Name;
        Status = loc.T("Status.Working");
    }

    public ILocalizationService Loc { get; }
    public string Title { get; }
    public string RepositoryName { get; }

    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private bool _isRunning = true;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private string _status;

    private readonly CancellationTokenSource _cts = new();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Cancel() => _cts.Cancel();

    public async Task RunAsync()
    {
        Func<CancellationToken, Task<GitResult>> op = _verb switch
        {
            "pull" => ct => _repo.PullAsync(ct),
            "push" => PushAsync,
            "stash" => ct => _repo.StashPushAsync(null, includeUntracked: true, ct: ct),
            _ => ct => _repo.FetchAsync(ct: ct)
        };

        try
        {
            var result = await GitAuth.RunWithRetryAsync(op, _repo, _credentials, _dialogs, _loc, _logger, _cts.Token);
            IsSuccess = result.Success;
            var text = string.IsNullOrWhiteSpace(result.CombinedOutput) ? result.CommandLine : result.CombinedOutput;
            Output = text.Trim();
            Status = _loc.T(result.Success ? "Common.Success" : "Error.GitFailed");
        }
        catch (Exception ex)
        {
            IsSuccess = false;
            Output = ex.Message;
            Status = _loc.T("Common.Error");
            _logger.LogError(ex, "standalone {Verb} failed", _verb);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private Task<GitResult> PushAsync(CancellationToken ct)
    {
        var current = _repo.GetBranches().FirstOrDefault(b => b.IsCurrent);
        var needsUpstream = current?.UpstreamFriendlyName is null;
        return _repo.PushAsync(setUpstream: needsUpstream, ct: ct);
    }
}
