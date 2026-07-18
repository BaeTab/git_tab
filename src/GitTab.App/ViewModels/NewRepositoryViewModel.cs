using GitTab.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitTab.App.ViewModels;

/// <summary>Backs the "create a new repository" dialog (folder, initial branch, optional remote URL).</summary>
public sealed partial class NewRepositoryViewModel : ObservableObject
{
    public NewRepositoryViewModel(ILocalizationService loc) => Loc = loc;

    public ILocalizationService Loc { get; }

    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private string _initialBranch = "main";
    [ObservableProperty] private string _remoteUrl = string.Empty;
}
