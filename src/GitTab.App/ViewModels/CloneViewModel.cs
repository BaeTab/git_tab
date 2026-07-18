using System.IO;
using GitTab.App.Localization;
using GitTab.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitTab.App.ViewModels;

/// <summary>Backs the "clone repository" dialog: a URL, a destination parent folder, and a folder
/// name (auto-derived from the URL) that combine into the target path.</summary>
public sealed partial class CloneViewModel : ObservableObject
{
    public CloneViewModel(ILocalizationService loc, string? initialParent = null)
    {
        Loc = loc;
        _parentFolder = initialParent ?? string.Empty;
    }

    public ILocalizationService Loc { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetPath))]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetPath))]
    private string _parentFolder = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetPath))]
    private string _folderName = string.Empty;

    /// <summary>The full destination path (parent folder + folder name), or empty if incomplete.</summary>
    public string TargetPath =>
        string.IsNullOrWhiteSpace(ParentFolder) || string.IsNullOrWhiteSpace(FolderName)
            ? string.Empty
            : Path.Combine(ParentFolder.Trim(), FolderName.Trim());

    // Derive the folder name from the URL (e.g. https://…/git_tab.git → "git_tab").
    partial void OnUrlChanged(string value)
    {
        var name = RemoteWeb.Parse(value)?.Repo;
        if (!string.IsNullOrEmpty(name)) FolderName = name;
    }
}
