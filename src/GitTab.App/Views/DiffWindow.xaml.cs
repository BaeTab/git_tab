using System.Windows;
using System.Windows.Input;

namespace GitTab.App.Views;

/// <summary>A standalone, resizable / fullscreen-capable window that shows one diff (shares the
/// <c>DiffViewModel</c> with the main window, so it stays live). F11 toggles borderless fullscreen.</summary>
public partial class DiffWindow : Window
{
    private WindowStyle _prevStyle;
    private WindowState _prevState;
    private bool _fullScreen;

    public DiffWindow()
    {
        InitializeComponent();
        // The pop-out button is meaningless here (we're already popped out).
        Loaded += (_, _) => Diff.HidePopOut();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullScreen(); e.Handled = true; }
        else if (e.Key == Key.Escape && _fullScreen) { ToggleFullScreen(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    private void OnToggleFull(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen()
    {
        if (!_fullScreen)
        {
            _prevStyle = WindowStyle;
            _prevState = WindowState;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // Toggle through Normal so a borderless Maximized reliably covers the work area.
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
            _fullScreen = true;
        }
        else
        {
            WindowStyle = _prevStyle;
            ResizeMode = ResizeMode.CanResize;
            WindowState = _prevState;
            _fullScreen = false;
        }
    }
}
