using GitTab.App.Localization;
using GitTab.App.Services;
using GitTab.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the Git config editor: view/edit <c>user.name</c>/<c>user.email</c> and set arbitrary keys, local or global.</summary>
public sealed partial class GitConfigViewModel : ObservableObject
{
    private readonly IRepositoryService _repo;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;
    private bool _loading;

    public GitConfigViewModel(IRepositoryService repo, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _repo = repo;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        _ = LoadAsync();
    }

    public ILocalizationService Loc { get; }

    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private string _userEmail = "";
    [ObservableProperty] private bool _global;

    /// <summary>True if any config value was written, so the host can reload if needed.</summary>
    public bool Changed { get; private set; }

    partial void OnGlobalChanged(bool value) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            UserName = await _repo.GetConfigAsync("user.name", Global) ?? "";
            UserEmail = await _repo.GetConfigAsync("user.email", Global) ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read git config");
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_loading) return;
        if (!string.IsNullOrWhiteSpace(UserName) &&
            await GitUi.RunAsync(() => _repo.SetConfigAsync("user.name", UserName.Trim(), Global), _dialogs, Loc, _logger))
        {
            Changed = true;
        }
        if (!string.IsNullOrWhiteSpace(UserEmail) &&
            await GitUi.RunAsync(() => _repo.SetConfigAsync("user.email", UserEmail.Trim(), Global), _dialogs, Loc, _logger))
        {
            Changed = true;
        }
    }

    [RelayCommand]
    private async Task SetCustom()
    {
        var key = _dialogs.Prompt(Loc.T("Config.KeyPrompt"), Loc.T("Config.SetCustom"));
        if (string.IsNullOrWhiteSpace(key)) return;
        var value = _dialogs.Prompt(Loc.T("Config.ValuePrompt"), Loc.T("Config.SetCustom"));
        if (value is null) return;
        if (await GitUi.RunAsync(() => _repo.SetConfigAsync(key.Trim(), value.Trim(), Global), _dialogs, Loc, _logger))
        {
            Changed = true;
        }
    }
}
