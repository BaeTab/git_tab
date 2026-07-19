using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitTab.App.Localization;
using GitTab.App.Services;

namespace GitTab.App.ViewModels;

/// <summary>One row in the keybindings dialog: an action's display name, its current gesture text,
/// and the flags that drive the row's visual state (customized / conflicting / capturing input).</summary>
public sealed partial class KeybindingRowViewModel : ObservableObject
{
    public KeybindingRowViewModel(string actionId, string displayName)
    {
        ActionId = actionId;
        DisplayName = displayName;
    }

    public string ActionId { get; }
    public string DisplayName { get; }

    [ObservableProperty] private string _gestureText = string.Empty;

    /// <summary>True if this row has a saved override (differs from the app default).</summary>
    [ObservableProperty] private bool _isCustomized;

    /// <summary>True if another row currently shares the same gesture text.</summary>
    [ObservableProperty] private bool _isConflicted;

    /// <summary>True while this row's "Rebind" button is waiting for the next key chord.</summary>
    [ObservableProperty] private bool _isCapturing;
}

/// <summary>
/// Backs the keyboard-shortcuts dialog: lists every rebindable action with its current gesture,
/// flags conflicts (two actions sharing one gesture), and applies rebind/reset actions straight to
/// <see cref="IKeybindingService"/>, which persists them and notifies the shell window to rebuild its
/// <see cref="System.Windows.Input.InputBinding"/>s.
/// </summary>
public sealed partial class KeybindingsViewModel : ObservableObject
{
    private readonly IKeybindingService _keybindings;

    public KeybindingsViewModel(IKeybindingService keybindings, ILocalizationService loc)
    {
        _keybindings = keybindings;
        Loc = loc;

        foreach (var action in _keybindings.Actions)
            Rows.Add(new KeybindingRowViewModel(action.Id, Loc.T(action.DisplayNameKey)));

        Reload();
    }

    public ILocalizationService Loc { get; }
    public ObservableCollection<KeybindingRowViewModel> Rows { get; } = new();

    /// <summary>True while any two rows share a gesture — drives the dialog's warning banner.</summary>
    [ObservableProperty] private bool _hasConflicts;

    /// <summary>True once anything changed in this dialog session, so the caller (the window) knows
    /// whether it needs to rebuild its InputBindings after the dialog closes.</summary>
    public bool Changed { get; private set; }

    /// <summary>Called by the dialog's code-behind once it has turned a raw key-down into a
    /// (modifiers, key) chord worth keeping (not a lone modifier, not a bare letter/digit).</summary>
    public void ApplyCapturedGesture(KeybindingRowViewModel row, ModifierKeys modifiers, Key key)
    {
        _keybindings.SetGesture(row.ActionId, GestureFormatting.Format(modifiers, key));
        Changed = true;
        Reload();
    }

    [RelayCommand]
    private void ResetRow(KeybindingRowViewModel? row)
    {
        if (row is null) return;
        _keybindings.Reset(row.ActionId);
        Changed = true;
        Reload();
    }

    [RelayCommand]
    private void ResetAllBindings()
    {
        _keybindings.ResetAll();
        Changed = true;
        Reload();
    }

    private void Reload()
    {
        foreach (var row in Rows)
        {
            row.GestureText = _keybindings.GetGesture(row.ActionId);
            row.IsCustomized = _keybindings.IsCustomized(row.ActionId);
        }

        var conflicted = new HashSet<KeybindingRowViewModel>();
        foreach (var group in Rows.Where(r => !string.IsNullOrWhiteSpace(r.GestureText))
                                   .GroupBy(r => r.GestureText, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1) continue;
            foreach (var row in group) conflicted.Add(row);
        }
        foreach (var row in Rows) row.IsConflicted = conflicted.Contains(row);
        HasConflicts = conflicted.Count > 0;
    }
}
