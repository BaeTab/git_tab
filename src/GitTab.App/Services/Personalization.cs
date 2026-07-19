namespace GitTab.App.Services;

/// <summary>
/// Live graph-appearance preferences, read by the (non-DI) <c>CommitGraphControl</c>. The app shell
/// pushes the user's settings in here; the control redraws on <see cref="Changed"/>.
/// </summary>
public static class GraphAppearance
{
    private static double _rowHeight = 30;
    private static bool _glow = true;

    public static double RowHeight
    {
        get => _rowHeight;
        set { if (Math.Abs(_rowHeight - value) > 0.01) { _rowHeight = value; Changed?.Invoke(null, EventArgs.Empty); } }
    }

    public static bool Glow
    {
        get => _glow;
        set { if (_glow != value) { _glow = value; Changed?.Invoke(null, EventArgs.Empty); } }
    }

    public static event EventHandler? Changed;
}

/// <summary>
/// The diff options a freshly-created <c>DiffViewModel</c> starts with (Split/Context/IgnoreWhitespace),
/// plus a live word-wrap flag the diff view honors on each render.
/// </summary>
public static class DiffDefaults
{
    public static bool Split { get; set; }
    public static int Context { get; set; } = 3;
    public static bool IgnoreWhitespace { get; set; }

    private static bool _wordWrap;
    public static bool WordWrap
    {
        get => _wordWrap;
        set { if (_wordWrap != value) { _wordWrap = value; Changed?.Invoke(null, EventArgs.Empty); } }
    }

    public static event EventHandler? Changed;
}
