using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class KeybindingsDialog : Window
{
    // The row currently waiting for a key chord (only one at a time — starting a new capture, or
    // losing focus, cancels whichever row was previously listening).
    private KeybindingRowViewModel? _capturingRow;

    public KeybindingsDialog()
    {
        InitializeComponent();
    }

    private KeybindingsViewModel? Vm => DataContext as KeybindingsViewModel;

    private void OnRebindClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: KeybindingRowViewModel row } button) return;

        if (_capturingRow is { } previous) previous.IsCapturing = false;
        row.IsCapturing = true;
        _capturingRow = row;
        button.Focus();
    }

    private void OnRebindKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Button { DataContext: KeybindingRowViewModel row } || !ReferenceEquals(row, _capturingRow))
            return;

        // Key.System covers chords WPF routes specially (e.g. Alt+letter) — the real key is SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;

        if (key == Key.Escape) { EndCapture(row); return; }

        // A lone modifier press isn't a chord yet — keep listening for the real key.
        if (IsModifierOnly(key)) return;

        // Letter/digit/numpad keys need a modifier so a shortcut can never collide with normal
        // typing (e.g. in a text box elsewhere in the app); function keys, Tab, arrows, Delete, etc.
        // are fine bare, matching the app's existing defaults (F5, F7).
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None && RequiresModifier(key)) return;

        Vm?.ApplyCapturedGesture(row, modifiers, key);
        EndCapture(row);
    }

    private void OnRebindLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: KeybindingRowViewModel row } && ReferenceEquals(row, _capturingRow))
            EndCapture(row);
    }

    private void EndCapture(KeybindingRowViewModel row)
    {
        row.IsCapturing = false;
        if (ReferenceEquals(_capturingRow, row)) _capturingRow = null;
    }

    private static bool IsModifierOnly(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System;

    private static bool RequiresModifier(Key key) =>
        (key >= Key.A && key <= Key.Z) ||
        (key >= Key.D0 && key <= Key.D9) ||
        (key >= Key.NumPad0 && key <= Key.NumPad9);
}
