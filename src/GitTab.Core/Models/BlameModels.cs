namespace GitTab.Core.Models;

/// <summary>One line of a file annotated with the commit that last changed it.</summary>
public sealed class BlameLine
{
    public required int LineNumber { get; init; }
    public required string Sha { get; init; }
    public required string Author { get; init; }
    public required DateTimeOffset When { get; init; }
    public required string Summary { get; init; }
    public required string Content { get; init; }

    public string ShortSha => Sha.Length >= 8 ? Sha[..8] : Sha;
}
