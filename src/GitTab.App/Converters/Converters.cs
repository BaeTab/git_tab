using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GitTab.App.Converters;

/// <summary>Resolves a theme resource key (string) to its Brush.</summary>
public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key && Application.Current.TryFindResource(key) is Brush b ? b : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>bool -> Visibility. ConverterParameter "invert" flips the mapping.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase)) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility vis && vis == Visibility.Visible;
}

/// <summary>Non-empty string / non-null / non-zero-count -> Visible.</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool has = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            System.Collections.ICollection c => c.Count > 0,
            _ => true
        };
        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase)) has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Two-way enum ⇄ bool for radio buttons: checked when the value equals the parameter.</summary>
public sealed class EnumBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is string name && value.ToString() == name;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is string name && Enum.TryParse(targetType, name, out var e)
            ? e!
            : Binding.DoNothing;
}
