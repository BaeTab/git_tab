using System.ComponentModel;
using System.Windows;

namespace GitTab.App.Services;

public enum AppTheme { Light, Dark }

public interface IThemeService : INotifyPropertyChanged
{
    AppTheme Theme { get; set; }
    bool IsDark { get; }
    void Toggle();
    void Apply(AppTheme theme);
}

/// <summary>Swaps the merged theme <see cref="ResourceDictionary"/> at runtime.</summary>
public sealed class ThemeService : IThemeService
{
    private ResourceDictionary? _current;
    private AppTheme _theme = AppTheme.Dark;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the theme dictionary is swapped, so custom-drawn controls (which
    /// resolve brushes in OnRender rather than via DynamicResource) can invalidate and repaint.</summary>
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

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDark)));
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
