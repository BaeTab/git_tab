using System.ComponentModel;

namespace GitTab.App.Localization;

public enum AppLanguage
{
    Korean,
    English,
    Japanese,
    Chinese,
    Spanish
}

/// <summary>
/// Runtime-switchable localization. XAML binds the indexer (via <c>{loc:Tr Key}</c>); changing
/// <see cref="Language"/> raises an indexer change so every bound string updates live.
/// </summary>
public interface ILocalizationService : INotifyPropertyChanged
{
    AppLanguage Language { get; set; }

    /// <summary>Indexer used by bindings. Returns the localized string, or the key if missing.</summary>
    string this[string key] { get; }

    string T(string key);
    string T(string key, params object[] args);

    void Toggle();

    event EventHandler? LanguageChanged;
}
