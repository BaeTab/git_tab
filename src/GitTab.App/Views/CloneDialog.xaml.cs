using System.IO;
using System.Windows;
using GitTab.App.ViewModels;
using Microsoft.Win32;

namespace GitTab.App.Views;

public partial class CloneDialog : Window
{
    public CloneDialog()
    {
        InitializeComponent();
    }

    private CloneViewModel? Vm => DataContext as CloneViewModel;

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Multiselect = false };
        if (Vm is { ParentFolder: { Length: > 0 } p } && Directory.Exists(p)) dlg.InitialDirectory = p;
        if (dlg.ShowDialog() == true && Vm is not null)
            Vm.ParentFolder = dlg.FolderName;
    }

    private void OnClone(object sender, RoutedEventArgs e)
    {
        if (Vm is null || string.IsNullOrWhiteSpace(Vm.Url) || string.IsNullOrWhiteSpace(Vm.TargetPath)) return;
        DialogResult = true;
    }
}
