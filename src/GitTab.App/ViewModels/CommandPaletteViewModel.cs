using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitTab.App.ViewModels;

/// <summary>One entry in the command palette: a display title and the action to run.</summary>
public sealed record PaletteItem(string Title, Action Execute);

/// <summary>Backs the Ctrl+P command palette: a filterable list of actions.</summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly IReadOnlyList<PaletteItem> _all;

    public CommandPaletteViewModel(IReadOnlyList<PaletteItem> all)
    {
        _all = all;
        Filter();
    }

    public ObservableCollection<PaletteItem> Items { get; } = new();

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private PaletteItem? _selected;

    /// <summary>The item the user confirmed (Enter / click), or null if cancelled.</summary>
    public PaletteItem? Chosen { get; private set; }

    partial void OnSearchChanged(string value) => Filter();

    private void Filter()
    {
        var q = Search.Trim();
        Items.Clear();
        foreach (var it in _all)
            if (q.Length == 0 || it.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                Items.Add(it);
        Selected = Items.FirstOrDefault();
    }

    public void Confirm(PaletteItem? item) => Chosen = item ?? Selected;
}
