using System.Collections.ObjectModel;
using GitTab.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GitTab.App.ViewModels;

/// <summary>Backs the interactive-rebase planning dialog: reorder rows and pick an action per commit.</summary>
public sealed partial class InteractiveRebaseViewModel : ObservableObject
{
    /// <summary>Selectable actions for the dialog. Reword is excluded — the backend can't edit
    /// commit messages non-interactively.</summary>
    private static readonly RebaseAction[] AvailableActions =
    {
        RebaseAction.Pick,
        RebaseAction.Squash,
        RebaseAction.Fixup,
        RebaseAction.Drop
    };

    public InteractiveRebaseViewModel(IReadOnlyList<RebaseTodoItem> items)
    {
        Rows = new ObservableCollection<RebaseRowViewModel>(
            items.Select(i => new RebaseRowViewModel(i)));
    }

    /// <summary>Rows in current plan order, top = oldest / applied first.</summary>
    public ObservableCollection<RebaseRowViewModel> Rows { get; }

    /// <summary>Actions offered by the row ComboBoxes.</summary>
    public IReadOnlyList<RebaseAction> Actions => AvailableActions;

    [ObservableProperty] private RebaseRowViewModel? _selectedRow;

    /// <summary>Rebuilds the plan from the rows in their current order and chosen actions.</summary>
    public IReadOnlyList<RebaseTodoItem> Plan =>
        Rows.Select(r => new RebaseTodoItem { Sha = r.Sha, Summary = r.Summary, Action = r.Action }).ToList();

    [RelayCommand]
    private void MoveUp(RebaseRowViewModel? row)
    {
        if (row is null) return;
        var index = Rows.IndexOf(row);
        if (index <= 0) return;
        Rows.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown(RebaseRowViewModel? row)
    {
        if (row is null) return;
        var index = Rows.IndexOf(row);
        if (index < 0 || index >= Rows.Count - 1) return;
        Rows.Move(index, index + 1);
    }

    /// <summary>One editable row in the rebase plan.</summary>
    public sealed partial class RebaseRowViewModel : ObservableObject
    {
        public RebaseRowViewModel(RebaseTodoItem item)
        {
            Sha = item.Sha;
            Summary = item.Summary;
            Action = item.Action;
        }

        public string Sha { get; }
        public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
        public string Summary { get; }

        [ObservableProperty] private RebaseAction _action;
    }
}
