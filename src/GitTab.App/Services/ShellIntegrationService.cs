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
    // A cascade surface: the Explorer anchor key, its command-store key (ExtendedSubCommandsKey —
    // Microsoft's preferred cascade, vs. the empty-SubCommands trick that shows an empty flyout on
    // Windows 11), the store path, the path token, and which items belong to it. A *selected* folder
    // or file passes its own path via %1; a folder *background* uses %V (the folder being viewed).
    private readonly record struct Surface(string ParentPath, string StoreKey, string StorePath, string Token, Item[] Items);

    private static Surface[] Surfaces =>
    [
        new(@"Software\Classes\Directory\shell\GitTab",            "GitTab.Menu.Dir",  @"Software\Classes\GitTab.Menu.Dir",  "%1", FolderItems),
        new(@"Software\Classes\Directory\Background\shell\GitTab", "GitTab.Menu.Bg",   @"Software\Classes\GitTab.Menu.Bg",   "%V", FolderItems),
        new(@"Software\Classes\*\shell\GitTab",                    "GitTab.Menu.File", @"Software\Classes\GitTab.Menu.File", "%1", FileItems),
    ];

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

    // Folder / background surface verbs.
    private static readonly Item[] FolderItems =
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

    // File surface verbs (right-click a specific file). Each opens the file's repository and runs the
    // action on that file; the app tells the user if the file isn't inside a git repository.
    private static readonly Item[] FileItems =
    {
        new("01", "filehistory", "Shell.Menu.FileHistory"),
        new("02", "blame",       "Shell.Menu.Blame"),
        new("03", "revertfile",  "Shell.Menu.RevertFile"),
        new("04", "gitignoreadd", "Shell.Menu.Gitignore"),
    };

    public bool IsInstalled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(Surfaces[0].ParentPath);
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

        foreach (var surface in Surfaces)
        {
            // Parent flyout anchor → its command-store key.
            using (var p = Registry.CurrentUser.CreateSubKey(surface.ParentPath))
            {
                p.SetValue(null, rootLabel);
                p.SetValue("MUIVerb", rootLabel);
                p.SetValue("Icon", icon);
                p.SetValue("ExtendedSubCommandsKey", surface.StoreKey);
            }

            // The items. Clear any previous set first so re-registering after the item list changes
            // doesn't leave stale entries.
            try { Registry.CurrentUser.DeleteSubKeyTree(surface.StorePath, throwOnMissingSubKey: false); }
            catch (Exception ex) { _logger.LogDebug(ex, "Clearing old menu store failed"); }

            using var shell = Registry.CurrentUser.CreateSubKey(surface.StorePath + @"\shell");
            foreach (var it in surface.Items)
            {
                using var verb = shell.CreateSubKey(it.Order + it.Verb);
                var label = _loc.T(it.LabelKey);
                verb.SetValue(null, label);
                verb.SetValue("MUIVerb", label);
                verb.SetValue("Icon", icon);
                using var cmd = verb.CreateSubKey("command");
                cmd.SetValue(null, $"\"{exe}\" --{it.Verb} \"{surface.Token}\"");
            }
        }

        NotifyShell();
        _logger.LogInformation("Explorer shell integration installed → {Exe}", exe);
    }

    public void Uninstall()
    {
        foreach (var surface in Surfaces)
        {
            TryDeleteTree(surface.ParentPath);
            TryDeleteTree(surface.StorePath);
        }
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
