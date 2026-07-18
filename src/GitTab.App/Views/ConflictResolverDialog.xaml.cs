using System.Windows;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class ConflictResolverDialog : Window
{
    public ConflictResolverDialog()
    {
        InitializeComponent();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        (DataContext as ConflictResolverViewModel)?.Apply();
        DialogResult = true;
    }
}
