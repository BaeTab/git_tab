using System.IO;
using GitTab.Core.Merge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitTab.App.ViewModels;

/// <summary>
/// Visual 3-way merge editor: shows the common ancestor (base), ours, and theirs for reference, and
/// an editable "result" pane (starting from the working file with conflict markers) that the user
/// writes out. Quick actions fill the result with a whole side or strip markers keeping both.
/// </summary>
public sealed partial class ConflictResolverViewModel : ObservableObject
{
    private readonly string _fullPath;

    public ConflictResolverViewModel(string displayPath, string fullPath,
        string? baseText, string? ours, string? theirs)
    {
        DisplayPath = displayPath;
        _fullPath = fullPath;
        BaseText = baseText ?? string.Empty;
        Ours = ours ?? string.Empty;
        Theirs = theirs ?? string.Empty;
        HasBase = !string.IsNullOrEmpty(baseText);
        Result = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
    }

    public string DisplayPath { get; }
    public string BaseText { get; }
    public string Ours { get; }
    public string Theirs { get; }
    public bool HasBase { get; }

    [ObservableProperty] private string _result = string.Empty;
    public bool Written { get; private set; }

    [RelayCommand] private void UseOurs() => Result = Ours;
    [RelayCommand] private void UseTheirs() => Result = Theirs;

    /// <summary>Strip conflict markers keeping both sides, as a starting point to hand-edit.</summary>
    [RelayCommand]
    private void KeepBoth()
    {
        var parts = ConflictParser.Parse(Result);
        foreach (var p in parts)
            if (p.Conflict is { } c) c.Choice = ConflictChoice.Both;
        Result = ConflictParser.Resolve(parts);
    }

    public void Apply()
    {
        File.WriteAllText(_fullPath, Result);
        Written = true;
    }
}
