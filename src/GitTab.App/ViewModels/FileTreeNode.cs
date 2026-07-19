using System.Collections.ObjectModel;

namespace GitTab.App.ViewModels;

/// <summary>A node in the changed-files tree: either a folder (with children) or a file leaf.</summary>
public sealed class FileTreeNode
{
    public required string Name { get; init; }

    /// <summary>Non-null for file leaves; null for folders.</summary>
    public FileChangeViewModel? File { get; init; }

    public bool IsFolder => File is null;
    public ObservableCollection<FileTreeNode> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;

    /// <summary>Builds a folder tree from a flat list of changed files (grouped by path segments).</summary>
    public static ObservableCollection<FileTreeNode> Build(IEnumerable<FileChangeViewModel> files)
    {
        var root = new FileTreeNode { Name = string.Empty };
        foreach (var file in files)
        {
            var parts = file.Path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var name = parts[i];
                var folder = current.Children.FirstOrDefault(c => c.IsFolder && c.Name == name);
                if (folder is null) { folder = new FileTreeNode { Name = name }; current.Children.Add(folder); }
                current = folder;
            }
            current.Children.Add(new FileTreeNode { Name = parts.Length > 0 ? parts[^1] : file.Path, File = file });
        }
        Collapse(root);
        return root.Children;
    }

    // Merge single-child folder chains (e.g. "src/GitTab/App" → one node) so the tree isn't needlessly deep.
    private static void Collapse(FileTreeNode node)
    {
        foreach (var child in node.Children) Collapse(child);
        for (int i = 0; i < node.Children.Count; i++)
        {
            var c = node.Children[i];
            while (c.IsFolder && c.Children.Count == 1 && c.Children[0].IsFolder)
            {
                var only = c.Children[0];
                var merged = new FileTreeNode { Name = c.Name + "/" + only.Name };
                foreach (var g in only.Children) merged.Children.Add(g);
                node.Children[i] = merged;
                c = merged;
            }
        }
    }
}
