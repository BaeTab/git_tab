using System.IO;
using System.Runtime.InteropServices;
using GitTab.App.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace GitTab.App.Services;

public interface IShellIntegrationService
{
    /// <summary>Whether the Explorer right-click "Git Tab" submenu is currently registered.</summary>
    bool IsInstalled { get; }

    /// <summary>Register the cascading submenu under HKCU (no admin required).</summary>
    void Install();

    /// <summary>Remove the submenu.</summary>
    void Uninstall();
}

/// <summary>
/// Adds/removes a TortoiseGit-style cascading <b>Git Tab</b> submenu to the Windows Explorer
/// right-click menu — for a selected folder and for a folder's empty background — entirely under
/// <c>HKEY_CURRENT_USER</c> so no administrator rights are needed. Each entry launches this same
/// executable with a verb (<c>--pull</c>, <c>--commit</c>, …) plus the clicked folder path; a
/// single-instance named pipe then routes the action into the already-running window.
/// </summary>
public sealed class ShellIntegrationService : IShellIntegrationService
{
    // Parent flyout anchors: (Explorer shell key, the command-store key its items live in). We use
    // ExtendedSubCommandsKey (Microsoft's preferred cascade method) rather than the empty-SubCommands
    // trick, which produces an empty flyout on Windows 11.
    private static readonly (string ParentPath, string StoreKey)[] Parents =
    {
        (@"Software\Classes\Directory\shell\GitTab",             "GitTab.Menu.Dir"),
        (@"Software\Classes\Directory\Background\shell\GitTab",  "GitTab.Menu.Bg"),
    };

    // Command stores holding the actual items: (store key, path token). A *selected* folder passes
    // its own path via %1; a folder *background* must use %V (the folder being viewed).
    private static readonly (string StorePath, string Token)[] Stores =
    {
        (@"Software\Classes\GitTab.Menu.Dir", "%1"),
        (@"Software\Classes\GitTab.Menu.Bg",  "%V"),
    };

    private readonly ILocalizationService _loc;
    private readonly ILogger<ShellIntegrationService> _logger;

    public ShellIntegrationService(ILocalizationService loc, ILogger<ShellIntegrationService> logger)
    {
        _loc = loc;
        _logger = logger;
    }

    private static string ExePath =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "GitTab.exe");

    // Ordered so Explorer lists them predictably (subkeys sort alphabetically).
    private readonly record struct Item(string Order, string Verb, string LabelKey);

    private static readonly Item[] Items =
    {
        new("01", "open",   "Shell.Menu.Open"),
        new("02", "clone",  "Shell.Menu.Clone"),
        new("03", "commit", "Shell.Menu.Commit"),
        new("04", "pull",   "Shell.Menu.Pull"),
        new("05", "push",   "Shell.Menu.Push"),
        new("06", "fetch",  "Shell.Menu.Fetch"),
        new("07", "stash",  "Shell.Menu.Stash"),
        new("08", "log",    "Shell.Menu.Log"),
    };

    public bool IsInstalled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(Parents[0].ParentPath);
                return k is not null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Shell integration probe failed");
                return false;
            }
        }
    }

    public void Install()
    {
        var exe = ExePath;
        var icon = $"\"{exe}\",0";
        var rootLabel = _loc.T("Shell.Menu.Root");

        // Parent flyout anchors, each pointing at its command-store key.
        foreach (var (parentPath, storeKey) in Parents)
        {
            using var p = Registry.CurrentUser.CreateSubKey(parentPath);
            p.SetValue(null, rootLabel);
            p.SetValue("MUIVerb", rootLabel);
            p.SetValue("Icon", icon);
            p.SetValue("ExtendedSubCommandsKey", storeKey);
        }

        // The items themselves, one set per surface (different path token). Clear any previous set
        // first so re-registering after the item list changes doesn't leave stale entries.
        foreach (var (storePath, token) in Stores)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(storePath, throwOnMissingSubKey: false); }
            catch (Exception ex) { _logger.LogDebug(ex, "Clearing old menu store failed"); }

            using var shell = Registry.CurrentUser.CreateSubKey(storePath + @"\shell");
            foreach (var it in Items)
            {
                using var verb = shell.CreateSubKey(it.Order + it.Verb);
                var label = _loc.T(it.LabelKey);
                verb.SetValue(null, label);
                verb.SetValue("MUIVerb", label);
                verb.SetValue("Icon", icon);
                using var cmd = verb.CreateSubKey("command");
                cmd.SetValue(null, $"\"{exe}\" --{it.Verb} \"{token}\"");
            }
        }

        NotifyShell();
        _logger.LogInformation("Explorer shell integration installed → {Exe}", exe);
    }

    public void Uninstall()
    {
        foreach (var (parentPath, _) in Parents) TryDeleteTree(parentPath);
        foreach (var (storePath, _) in Stores) TryDeleteTree(storePath);
        NotifyShell();
        _logger.LogInformation("Explorer shell integration removed");
    }

    private void TryDeleteTree(string path)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed removing shell key {Path}", path); }
    }

    // Tell Explorer that file associations changed so the new/removed menu shows without a restart.
    private static void NotifyShell() =>
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
