using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace GitTab.App.Services;

public enum AppTheme { Light, Dark, HighContrast, Midnight, Nord, Dracula, Solarized, Rose }

public interface IThemeService : INotifyPropertyChanged
{
    AppTheme Theme { get; set; }
    bool IsDark { get; }
    void Toggle();
    void Apply(AppTheme theme);

    /// <summary>Re-apply the custom accent color and fonts from settings without changing the theme.</summary>
    void RefreshPersonalization();
}

/// <summary>Swaps the merged theme <see cref="ResourceDictionary"/> at runtime, and layers the user's
/// custom accent color and fonts on top as direct app-resource entries (which win over the theme).</summary>
public sealed class ThemeService : IThemeService
{
    private readonly ISettingsService _settings;
    private ResourceDictionary? _current;
    private AppTheme _theme = AppTheme.Dark;

    public ThemeService(ISettingsService settings) => _settings = settings;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the theme/accent changes, so custom-drawn controls (which resolve brushes
    /// in OnRender rather than via DynamicResource) can invalidate and repaint.</summary>
    public static event EventHandler? ThemeChanged;

    public AppTheme Theme
    {
        get => _theme;
        set => Apply(value);
    }

    public bool IsDark => _theme == AppTheme.Dark;

    public void Toggle() => Apply(_theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public void Apply(AppTheme theme)
    {
        _theme = theme;
        var uri = new Uri($"pack://application:,,,/GitTab;component/Themes/{theme}.xaml", UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null) merged.Remove(_current);
        merged.Insert(0, dict); // themed brushes resolved by Common.xaml's DynamicResource lookups
        _current = dict;

        ApplyOverrides();

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDark)));
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshPersonalization()
    {
        ApplyOverrides();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    // Direct entries in Application.Resources win over the merged theme dictionary for both
    // DynamicResource lookups and TryFindResource, so this cleanly overrides the theme's accent/fonts.
    private void ApplyOverrides()
    {
        var res = Application.Current.Resources;
        var s = _settings.Current;

        if (s is not null && TryColor(s.AccentColor, out var accent))
        {
            res["Brush.Accent"] = Frozen(accent);
            res["Chip.Head.Bg"] = Frozen(accent);
            res["Brush.OnAccent"] = Frozen(Luminance(accent) > 0.6 ? Colors.Black : Colors.White);
        }
        else
        {
            res.Remove("Brush.Accent");
            res.Remove("Chip.Head.Bg");
            res.Remove("Brush.OnAccent");
        }

        res["Font.UI"] = new FontFamily(Nonempty(s?.UiFontFamily, "Segoe UI"));
        res["Font.Diff"] = new FontFamily(Nonempty(s?.DiffFontFamily, "Consolas"));
        res["Font.DiffSize"] = s?.DiffFontSize is > 0 ? s.DiffFontSize : 12.5;
    }

    private static string Nonempty(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v!;

    private static bool TryColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try { color = (Color)ColorConverter.ConvertFromString(hex.Trim()); return true; }
        catch { return false; }
    }

    private static double Luminance(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
