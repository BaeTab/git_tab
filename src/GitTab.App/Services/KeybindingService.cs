using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

/// <summary>Describes one rebindable shell action: a stable id used as the persistence key, its
/// default gesture text (e.g. "Ctrl+O", "F5", "Ctrl+Shift+Tab" — parseable by
/// <see cref="System.Windows.Input.KeyGestureConverter"/>), and a localization key for the
/// friendly name shown in the keybindings dialog.</summary>
public sealed record KeybindingAction(string Id, string DefaultGesture, string DisplayNameKey);

public interface IKeybindingService
{
    /// <summary>Every rebindable action, in display order.</summary>
    IReadOnlyList<KeybindingAction> Actions { get; }

    /// <summary>The gesture text currently in effect for <paramref name="actionId"/> — the user's
    /// saved override if one exists, otherwise the action's default.</summary>
    string GetGesture(string actionId);

    /// <summary>True if this action has a saved override (regardless of whether it happens to
    /// equal the default text).</summary>
    bool IsCustomized(string actionId);

    void SetGesture(string actionId, string gesture);
    void Reset(string actionId);
    void ResetAll();

    /// <summary>Raised after <see cref="SetGesture"/>, <see cref="Reset"/>, or <see cref="ResetAll"/>
    /// so the shell window can rebuild its <see cref="System.Windows.Input.InputBinding"/>s.</summary>
    event Action? BindingsChanged;
}

/// <summary>Holds the fixed list of rebindable shell actions and persists user overrides as JSON to
/// %AppData%/GitTab/keybindings.json (a simple actionId → gesture-text dictionary).</summary>
public sealed class KeybindingService : IKeybindingService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly ILogger<KeybindingService> _logger;
    private readonly string _path;
    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);

    public event Action? BindingsChanged;

    public IReadOnlyList<KeybindingAction> Actions { get; } = new List<KeybindingAction>
    {
        new("OpenRepository",   "Ctrl+O",         "Keybind.Action.OpenRepository"),
        new("CreateRepository", "Ctrl+N",         "Keybind.Action.CreateRepository"),
        new("CommandPalette",   "Ctrl+P",         "Keybind.Action.CommandPalette"),
        new("FocusSearch",      "Ctrl+F",         "Keybind.Action.FocusSearch"),
        new("CloseTab",         "Ctrl+W",         "Keybind.Action.CloseTab"),
        new("NextTab",          "Ctrl+Tab",       "Keybind.Action.NextTab"),
        new("PreviousTab",      "Ctrl+Shift+Tab", "Keybind.Action.PreviousTab"),
        new("Refresh",          "F5",             "Keybind.Action.Refresh"),
        new("Fetch",            "F7",             "Keybind.Action.Fetch"),
        new("Commit",           "Ctrl+Enter",     "Keybind.Action.Commit"),
    };

    public KeybindingService(ILogger<KeybindingService> logger)
    {
        _logger = logger;
        _path = Path.Combine(App.AppDataDir, "keybindings.json");
        Load();
    }

    public string GetGesture(string actionId) =>
        _overrides.TryGetValue(actionId, out var gesture) ? gesture : DefaultOf(actionId);

    public bool IsCustomized(string actionId) => _overrides.ContainsKey(actionId);

    public void SetGesture(string actionId, string gesture)
    {
        // Storing the default verbatim is harmless but keeps the override file (and "customized"
        // flag) meaningful — collapse a rebind back to the default into "no override".
        if (string.Equals(gesture, DefaultOf(actionId), StringComparison.OrdinalIgnoreCase))
            _overrides.Remove(actionId);
        else
            _overrides[actionId] = gesture;
        Save();
        BindingsChanged?.Invoke();
    }

    public void Reset(string actionId)
    {
        if (!_overrides.Remove(actionId)) return;
        Save();
        BindingsChanged?.Invoke();
    }

    public void ResetAll()
    {
        if (_overrides.Count == 0) return;
        _overrides.Clear();
        Save();
        BindingsChanged?.Invoke();
    }

    private string DefaultOf(string actionId)
    {
        foreach (var action in Actions)
            if (action.Id == actionId) return action.DefaultGesture;
        return string.Empty;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
            if (loaded is null) return;
            foreach (var (id, gesture) in loaded)
                if (!string.IsNullOrWhiteSpace(gesture)) _overrides[id] = gesture;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read keybindings.json — using defaults"); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(App.AppDataDir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_overrides, Json));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to save keybindings.json"); }
    }
}

/// <summary>Formats a captured (modifiers, key) chord into the same "Ctrl+Shift+O"-style text that
/// <see cref="KeyGestureConverter"/> parses back — the round-trip format used for both the built-in
/// defaults and user-saved overrides.</summary>
public static class GestureFormatting
{
    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Windows");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
