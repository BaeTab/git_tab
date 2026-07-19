using System.Diagnostics;

namespace GitTab.App.Services;

/// <summary>Launches external editors/tools (VS Code, the OS file handler). Best-effort — never throws.</summary>
public static class ExternalTools
{
    /// <summary>Open <paramref name="path"/> (a repo folder or a file) in VS Code, falling back to
    /// the OS default handler when <c>code</c> isn't on PATH.</summary>
    public static bool OpenInEditor(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            Process.Start(new ProcessStartInfo("code", $"\"{path}\"") { UseShellExecute = true });
            return true;
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return true;
            }
            catch { return false; }
        }
    }
}
