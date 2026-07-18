using System.Windows;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class OperationWindow : Window
{
    public OperationWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is OperationWindowViewModel vm)
                await vm.RunAsync();
        };
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
