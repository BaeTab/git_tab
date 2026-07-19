using System.IO;
using System.Text.Json;
using GitTab.App.Localization;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";       // "Dark" | "Light"
    public string Language { get; set; } = "Korean";  // "Korean" | "English"
    public bool CrashReports { get; set; } = true;     // write a local crash report on unhandled errors
    public bool BackgroundFetch { get; set; } = true;  // periodically fetch open repos and update "behind"
    public string UpdateChannel { get; set; } = "Stable"; // "Stable" | "Beta" (beta includes prereleases)
    public int UiScalePercent { get; set; } = 100;     // UI/font zoom: 100 / 115 / 130 / 150

    // ---- personalization ----
    public string AccentColor { get; set; } = "";        // "" = theme's own accent, else "#RRGGBB"
    public string UiFontFamily { get; set; } = "Segoe UI";
    public string DiffFontFamily { get; set; } = "Consolas";
    public double DiffFontSize { get; set; } = 12.5;
    public int GraphRowHeight { get; set; } = 30;        // compact 24 / normal 30 / comfortable 38
    public bool GraphGlow { get; set; } = true;          // luminous graph lanes/nodes
    public bool DiffSplitDefault { get; set; }           // default diff view: false=unified, true=split
    public int DiffContextDefault { get; set; } = 3;     // default surrounding context lines
    public bool DiffIgnoreWhitespaceDefault { get; set; }
    public bool DiffWordWrap { get; set; }               // wrap long lines in the diff
    public int BackgroundFetchMinutes { get; set; } = 3; // 0 unused (toggle is BackgroundFetch); 1..30
    public bool ReopenLastRepo { get; set; } = true;     // reopen the most-recent repo on startup
    public bool AbsoluteDates { get; set; }              // false = relative ("2h ago"), true = absolute
}

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void Update(AppTheme theme, AppLanguage language);
}

/// <summary>Persists UI preferences to %AppData%/GitTab/settings.json.</summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly ILogger<SettingsService> _logger;
    private readonly string _path;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitTab");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    public void Update(AppTheme theme, AppLanguage language)
    {
        Current.Theme = theme.ToString();
        Current.Language = language.ToString();
        Save();
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Current, Json)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to save settings"); }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read settings"); }
        return new AppSettings();
    }
}
