using System.Windows;

namespace GitTab.App.Views;

public partial class InteractiveRebaseDialog : Window
{
    public InteractiveRebaseDialog()
    {
        InitializeComponent();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
