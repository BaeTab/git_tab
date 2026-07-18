using System.IO;
using System.Text.Json;
using GitTab.App.Localization;
using Microsoft.Extensions.Logging;

namespace GitTab.App.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";       // "Dark" | "Light"
    public string Language { get; set; } = "Korean";  // "Korean" | "English"
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
