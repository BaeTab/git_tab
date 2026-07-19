using System.Collections.ObjectModel;
using GitTab.App.Localization;
using GitTab.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.ViewModels;

/// <summary>Backs the credential manager: view and delete the PATs Git Tab stored in Windows Credential Manager.</summary>
public sealed partial class CredentialsViewModel : ObservableObject
{
    private readonly ICredentialStore _credentials;
    private readonly IDialogService _dialogs;
    private readonly ILogger _logger;

    public CredentialsViewModel(ICredentialStore credentials, IDialogService dialogs, ILocalizationService loc, ILogger logger)
    {
        _credentials = credentials;
        _dialogs = dialogs;
        Loc = loc;
        _logger = logger;
        Refresh();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<string> Credentials { get; } = new();
    public bool IsEmpty => Credentials.Count == 0;

    private void Refresh()
    {
        Credentials.Clear();
        try { foreach (var target in _credentials.List()) Credentials.Add(target); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read stored credentials"); }
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Delete(string? target)
    {
        if (target is null) return;
        if (!_dialogs.Confirm(Loc.T("Cred.DeleteConfirm", target), Loc.T("Cred.Manage"))) return;
        _credentials.Delete(target);
        Refresh();
    }
}
