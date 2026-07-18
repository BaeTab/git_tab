using CommunityToolkit.Mvvm.ComponentModel;
using GitTab.App.Localization;
using GitTab.Core.Models;

namespace GitTab.App.ViewModels;

/// <summary>Holds the currently-displayed file diff for the AvalonEdit-based diff view.</summary>
public sealed partial class DiffViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    [NotifyPropertyChangedFor(nameof(IsBinary))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private FileDiff? _diff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    private string _placeholderKey = "Diff.SelectFile";

    public bool HasContent => Diff is { IsBinary: false } d && d.Hunks.Count > 0;
    public bool IsBinary => Diff?.IsBinary == true;
    public bool ShowPlaceholder => !HasContent;
    public string PlaceholderText => LocalizationService.Current.T(PlaceholderKey);

    public void Show(FileDiff diff)
    {
        Diff = diff;
        if (diff.IsBinary) PlaceholderKey = "Diff.Binary";
        else if (diff.Hunks.Count == 0) PlaceholderKey = "Diff.NoChanges";
    }

    public void Clear()
    {
        Diff = null;
        PlaceholderKey = "Diff.SelectFile";
    }
}
