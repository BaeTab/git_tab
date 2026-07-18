using System.Windows;
using GitTab.App.ViewModels;

namespace GitTab.App.Views;

public partial class HunkStageDialog : Window
{
    public HunkStageDialog()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is HunkStageViewModel vm)
                await vm.LoadAsync();
        };
    }
}
