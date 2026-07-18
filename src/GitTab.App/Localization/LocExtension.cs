using System.Windows.Data;
using System.Windows.Markup;

namespace GitTab.App.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Tr Panel.History}"</c> binds to the localization
/// service's indexer so the text follows the current language automatically.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = BindingMode.OneWay,
            FallbackValue = Key
        };
        return binding.ProvideValue(serviceProvider);
    }
}
