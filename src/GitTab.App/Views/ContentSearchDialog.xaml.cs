using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class ContentSearchDialog : Window
{
    public ContentSearchDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TermBox.Focus();
    }

    private ContentSearchViewModel? Vm => DataContext as ContentSearchViewModel;

    private void OnTermKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Vm?.SearchCommand.CanExecute(null) == true)
        {
            Vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: ContentResult r } && Vm is not null)
        {
            Vm.Choose(r.Sha);
            DialogResult = true;
        }
    }
}
