using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GitTab.App.Services;
using GitTab.App.ViewModels;

namespace GitTab.App;

public partial class MainWindow : Window
{
    // Segoe MDL2 Assets glyphs (E922 maximize, E923 restore).
    private static readonly string GlyphMaximize = char.ConvertFromUtf32(0xE922);
    private static readonly string GlyphRestore = char.ConvertFromUtf32(0xE923);

    private readonly MainViewModel _vm;
    private readonly IKeybindingService _keybindings;

    public MainWindow(MainViewModel vm, IKeybindingService keybindings)
    {
        InitializeComponent();
        _vm = vm;
        _keybindings = keybindings;
        DataContext = vm;
        StateChanged += (_, _) => UpdateMaxRestoreGlyph();
        // A shell "commit" action focuses the staging tab.
        vm.CommitFocusRequested += () => LeftTabs.SelectedIndex = 0;
        // Ctrl+F jumps to the graph search box.
        vm.SearchFocusRequested += () => { SearchBox.Focus(); SearchBox.SelectAll(); };
        // Incremental loading: pull the next page when the graph is scrolled near the bottom.
        // LoadMore lives on the active repository session (each tab loads independently).
        CommitGraph.NearEnd += (_, _) =>
        {
            if (_vm.ActiveSession?.LoadMoreCommand is { } cmd && cmd.CanExecute(null))
                cmd.Execute(null);
        };

        // Keyboard shortcuts are user-customizable (see the Keybindings dialog): build them from the
        // saved/default gestures now, and again whenever the user changes one.
        RebuildShortcuts();
        _keybindings.BindingsChanged += RebuildShortcuts;
        Closed += (_, _) => _keybindings.BindingsChanged -= RebuildShortcuts;
    }

    /// <summary>
    /// (Re)builds <see cref="Window.InputBindings"/> from the keybinding service's current gestures.
    /// Each action maps to a stable, parameterless command on <see cref="MainViewModel"/> so the
    /// binding keeps working regardless of which repository tab is active (see the
    /// "keybinding-stable command forwarders" region in <see cref="MainViewModel"/>).
    /// </summary>
    public void RebuildShortcuts()
    {
        InputBindings.Clear();

        var commands = new Dictionary<string, ICommand>(StringComparer.Ordinal)
        {
            ["OpenRepository"] = _vm.OpenRepositoryCommand,
            ["CreateRepository"] = _vm.CreateRepositoryCommand,
            ["CommandPalette"] = _vm.OpenCommandPaletteCommand,
            ["FocusSearch"] = _vm.FocusSearchCommand,
            ["CloseTab"] = _vm.CloseActiveTabCommand,
            ["NextTab"] = _vm.NextTabCommand,
            ["PreviousTab"] = _vm.PreviousTabCommand,
            ["Refresh"] = _vm.RefreshActiveCommand,
            ["Fetch"] = _vm.FetchActiveCommand,
            ["Commit"] = _vm.CommitActiveCommand,
        };

        var converter = new KeyGestureConverter();
        foreach (var action in _keybindings.Actions)
        {
            if (!commands.TryGetValue(action.Id, out var command)) continue;
            var gestureText = _keybindings.GetGesture(action.Id);
            if (string.IsNullOrWhiteSpace(gestureText)) continue;

            try
            {
                if (converter.ConvertFromString(gestureText) is KeyGesture gesture)
                    InputBindings.Add(new KeyBinding(command, gesture));
            }
            catch (Exception)
            {
                // Invalid/unparseable gesture text (corrupt settings file, etc.) — skip it rather
                // than crash the shell; the action just has no shortcut until reset in the dialog.
            }
        }
    }

    // Selecting a file leaf in the changed-files tree drives the diff panel.
    private void OnFileTreeSelected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ViewModels.FileTreeNode { File: { } file }
            && sender is System.Windows.FrameworkElement { DataContext: ViewModels.CommitDetailsViewModel details })
        {
            details.SelectedFile = file;
        }
    }

    /// <summary>Bring the window to the foreground when an Explorer right-click routes here.</summary>
    public void ActivateForShell()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        // Momentary topmost flip reliably raises the window above the caller (Explorer).
        var wasTopmost = Topmost;
        Topmost = true;
        Topmost = wasTopmost;
        Focus();
    }

    // ---- custom title-bar buttons ----
    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaxRestore(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseWindow(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreGlyph()
    {
        if (MaxRestoreButton is not null)
            MaxRestoreButton.Content = WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize;
    }

    // ---- drag & drop: open a dropped repo folder ----
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

    // ---- keep a custom-chrome window from covering the taskbar when maximized ----
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            AdjustMaximizedBounds(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void AdjustMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref info);
            var work = info.rcWork;
            var mon = info.rcMonitor;
            mmi.ptMaxPosition.X = Math.Abs(work.Left - mon.Left);
            mmi.ptMaxPosition.Y = Math.Abs(work.Top - mon.Top);
            mmi.ptMaxSize.X = Math.Abs(work.Right - work.Left);
            mmi.ptMaxSize.Y = Math.Abs(work.Bottom - work.Top);
            Marshal.StructureToPtr(mmi, lParam, true);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
