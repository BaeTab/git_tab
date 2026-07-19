using GitTab.Core.Models;

namespace GitTab.App.ViewModels;

/// <summary>A changed file row (in a commit or the working tree), with a status glyph.</summary>
public sealed class FileChangeViewModel
{
    public required FileChange Model { get; init; }

    public string Path => Model.Path;
    public string DisplayPath => Model.DisplayPath;
    public bool IsStaged => Model.IsStaged;
    public FileChangeKind Kind => Model.Kind;

    /// <summary>The file's parent folder (for grouping), or "/" when at the repository root.</summary>
    public string Folder
    {
        get
        {
            var p = Model.Path.Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash <= 0 ? "/" : p[..slash];
        }
    }

    /// <summary>Just the file name, shown under a folder group.</summary>
    public string FileName
    {
        get
        {
            var p = Model.Path.Replace('\\', '/');
            int slash = p.LastIndexOf('/');
            return slash < 0 ? p : p[(slash + 1)..];
        }
    }

    public string StatusLetter => Kind switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Modified => "M",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R",
        FileChangeKind.Copied => "C",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.Untracked => "?",
        FileChangeKind.Conflicted => "!",
        _ => "•"
    };

    /// <summary>Theme resource key for the status color.</summary>
    public string StatusColorKey => Kind switch
    {
        FileChangeKind.Added or FileChangeKind.Untracked or FileChangeKind.Copied => "Brush.Success",
        FileChangeKind.Deleted => "Brush.Danger",
        FileChangeKind.Conflicted => "Brush.Danger",
        _ => "Brush.Accent"
    };
}
