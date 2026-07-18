using System.IO;
using System.Windows;
using GitTab.App.ViewModels;
using Microsoft.Win32;

namespace GitTab.App.Views;

public partial class NewRepositoryDialog : Window
{
    public NewRepositoryDialog()
    {
        InitializeComponent();
    }

    private NewRepositoryViewModel? Vm => DataContext as NewRepositoryViewModel;

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Multiselect = false };
        if (Vm is { FolderPath: { Length: > 0 } p } && Directory.Exists(p)) dlg.InitialDirectory = p;
        if (dlg.ShowDialog() == true && Vm is not null)
            Vm.FolderPath = dlg.FolderName;
    }

    private void OnCreate(object sender, RoutedEventArgs e)
    {
        if (Vm is null || string.IsNullOrWhiteSpace(Vm.FolderPath)) return;
        DialogResult = true;
    }
}
