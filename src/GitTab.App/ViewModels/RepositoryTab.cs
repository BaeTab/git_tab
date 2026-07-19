namespace GitTab.App.ViewModels;

/// <summary>One open repository shown as a tab. Identity is its working-directory path.</summary>
public sealed class RepositoryTab
{
    public RepositoryTab(string path, string name)
    {
        Path = path;
        Name = name;
    }

    public string Path { get; }
    public string Name { get; }
}
