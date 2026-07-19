using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitTab.App.Localization;
using GitTab.Core.Models;

namespace GitTab.App.ViewModels;

/// <summary>Holds the currently-displayed file diff for the AvalonEdit-based diff view.</summary>
public sealed partial class DiffViewModel : ObservableObject
{
    private bool _suppressWs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContent))]
    [NotifyPropertyChangedFor(nameof(IsBinary))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private FileDiff? _diff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    private string _placeholderKey = "Diff.SelectFile";

    [ObservableProperty] private bool _isSplit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    private bool _isImage;

    [ObservableProperty] private ImageSource? _oldImage;
    [ObservableProperty] private ImageSource? _newImage;

    /// <summary>When on, the diff is re-fetched with whitespace changes ignored (git diff -w).</summary>
    [ObservableProperty] private bool _ignoreWhitespace;

    /// <summary>git's default number of unified-diff context lines.</summary>
    public const int DefaultContext = 3;

    /// <summary>Unified-diff context lines to show; the "expand context" control raises this.</summary>
    [ObservableProperty] private int _contextLines = DefaultContext;

    public bool CanShrinkContext => ContextLines > DefaultContext;

    /// <summary>Set by the caller: re-produce the current file's diff honoring the current
    /// ignore-whitespace + context-lines options (reads them off this VM).</summary>
    public Func<Task<FileDiff>>? Refetch { get; set; }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff"
    };

    public static bool IsImagePath(string path)
        => ImageExtensions.Contains(Path.GetExtension(path));

    public bool HasContent => Diff is { IsBinary: false } d && d.Hunks.Count > 0;
    public bool IsBinary => Diff?.IsBinary == true;
    public bool ShowPlaceholder => !HasContent && !IsImage;
    public string PlaceholderText => LocalizationService.Current.T(PlaceholderKey);

    public void Show(FileDiff diff)
    {
        IsImage = false;
        OldImage = NewImage = null;
        ResetOptions();
        Diff = diff;
        if (diff.IsTooLarge) PlaceholderKey = "Diff.TooLarge";
        else if (diff.IsBinary) PlaceholderKey = "Diff.Binary";
        else if (diff.Hunks.Count == 0) PlaceholderKey = "Diff.NoChanges";
    }

    /// <summary>Show a before/after image comparison instead of a text diff.</summary>
    public void ShowImage(byte[]? oldBytes, byte[]? newBytes, string path)
    {
        ResetOptions();
        Diff = null;
        OldImage = ToImage(oldBytes);
        NewImage = ToImage(newBytes);
        IsImage = true;
    }

    public void Clear()
    {
        IsImage = false;
        OldImage = NewImage = null;
        Diff = null;
        PlaceholderKey = "Diff.SelectFile";
    }

    /// <summary>Show more surrounding (context) lines around each change.</summary>
    [RelayCommand]
    private void ExpandContext() => ContextLines += 10;

    /// <summary>Back to fewer context lines (down to git's default).</summary>
    [RelayCommand]
    private void ShrinkContext() => ContextLines = System.Math.Max(DefaultContext, ContextLines - 10);

    partial void OnIgnoreWhitespaceChanged(bool value) => TriggerRefetch();

    partial void OnContextLinesChanged(int value)
    {
        OnPropertyChanged(nameof(CanShrinkContext));
        TriggerRefetch();
    }

    private void TriggerRefetch()
    {
        if (_suppressWs || Refetch is null) return;
        _ = ApplyOptionsAsync();
    }

    private async Task ApplyOptionsAsync()
    {
        try
        {
            var d = await Refetch!.Invoke();
            IsImage = false;
            OldImage = NewImage = null;
            Diff = d;
            if (d.Hunks.Count == 0) PlaceholderKey = "Diff.NoChanges";
        }
        catch { /* leave the current diff shown */ }
    }

    private void ResetOptions()
    {
        _suppressWs = true;
        IgnoreWhitespace = false;
        ContextLines = DefaultContext;
        _suppressWs = false;
    }

    private static ImageSource? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // don't keep the stream/file locked
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
