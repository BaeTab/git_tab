using System.Windows;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class GitignoreDialog : Window
{
    public GitignoreDialog()
    {
        InitializeComponent();
    }

    private GitignoreDialogViewModel? Vm => DataContext as GitignoreDialogViewModel;

    private void OnWrite(object sender, RoutedEventArgs e)
    {
        Vm?.Apply(append: false);
        DialogResult = true;
    }

    private void OnAppend(object sender, RoutedEventArgs e)
    {
        Vm?.Apply(append: true);
        DialogResult = true;
    }
}
