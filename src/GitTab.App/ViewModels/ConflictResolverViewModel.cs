using System.Collections.ObjectModel;
using System.IO;
using GitTab.Core.Merge;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitTab.App.ViewModels;

public sealed partial class ConflictBlockViewModel : ObservableObject
{
    private readonly ConflictBlock _block;

    public ConflictBlockViewModel(ConflictBlock block, int index)
    {
        _block = block;
        Index = index;
        _choice = block.Choice;
    }

    public int Index { get; }
    public string Ours => _block.Ours;
    public string Theirs => _block.Theirs;

    [ObservableProperty] private ConflictChoice _choice;

    partial void OnChoiceChanged(ConflictChoice value) => _block.Choice = value;
}

/// <summary>Backs the in-app conflict resolver: choose ours/theirs/both per block, then write the file.</summary>
public sealed partial class ConflictResolverViewModel : ObservableObject
{
    private readonly string _fullPath;
    private readonly IReadOnlyList<ConflictPart> _parts;

    public ConflictResolverViewModel(string displayPath, string fullPath)
    {
        DisplayPath = displayPath;
        _fullPath = fullPath;
        _parts = ConflictParser.Parse(File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty);

        int i = 1;
        Blocks = new ObservableCollection<ConflictBlockViewModel>(
            _parts.Where(p => p.IsConflict).Select(p => new ConflictBlockViewModel(p.Conflict!, i++)));
    }

    public string DisplayPath { get; }
    public ObservableCollection<ConflictBlockViewModel> Blocks { get; }
    public bool Written { get; private set; }

    public void Apply()
    {
        File.WriteAllText(_fullPath, ConflictParser.Resolve(_parts));
        Written = true;
    }
}
