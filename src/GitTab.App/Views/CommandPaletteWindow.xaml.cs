using System.Windows;
using System.Windows.Input;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class CommandPaletteWindow : Window
{
    public CommandPaletteWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
        // Close if the palette loses focus (click elsewhere).
        Deactivated += (_, _) => { if (IsLoaded) Close(); };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private CommandPaletteViewModel? Vm => DataContext as CommandPaletteViewModel;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                DialogResult = false;
                e.Handled = true;
                break;
            case Key.Enter:
                Vm?.Confirm(null);
                DialogResult = true;
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (ItemList.Items.Count == 0) return;
        var i = ItemList.SelectedIndex + delta;
        ItemList.SelectedIndex = Math.Max(0, Math.Min(ItemList.Items.Count - 1, i));
        ItemList.ScrollIntoView(ItemList.SelectedItem);
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Vm is null) return;
        Vm.Confirm(null);
        DialogResult = true;
    }
}
