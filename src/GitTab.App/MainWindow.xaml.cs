using System.IO;
using System.Windows;
using GitTab.App.ViewModels;

namespace GitTab.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } paths) return;

        var path = paths[0];
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && _vm.OpenPathCommand.CanExecute(folder))
            _vm.OpenPathCommand.Execute(folder);
    }
}
