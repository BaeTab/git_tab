using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace GitTab.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* no browser available — ignore */ }
        e.Handled = true;
    }
}
