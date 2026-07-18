using System.IO;
using System.Text;

namespace GitTab.App.Services;

/// <summary>
/// Writes a self-contained crash report to <c>%AppData%/GitTab/crashes/</c> when an unhandled
/// exception occurs (opt-in). Reports stay on the machine — nothing is uploaded — so the user can
/// attach one to a bug report if they choose.
/// </summary>
public static class CrashReporter
{
    public static string CrashesDir(string appDataDir) => Path.Combine(appDataDir, "crashes");

    public static string? Write(string appDataDir, Exception ex, string context)
    {
        try
        {
            var dir = CrashesDir(appDataDir);
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.txt");

            var sb = new StringBuilder();
            sb.AppendLine($"Git Tab {AppInfo.Version} — {context}");
            sb.AppendLine(DateTimeOffset.Now.ToString("o"));
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($".NET: {Environment.Version}");
            sb.AppendLine(new string('-', 60));
            sb.AppendLine(ex.ToString());

            File.WriteAllText(file, sb.ToString());
            return file;
        }
        catch
        {
            return null; // never let crash reporting throw during a crash
        }
    }
}
