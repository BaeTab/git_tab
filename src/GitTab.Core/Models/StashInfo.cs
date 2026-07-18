namespace GitTab.Core.Models;

/// <summary>An entry in the stash stack.</summary>
public sealed class StashInfo
{
    public required int Index { get; init; }
    public required string Message { get; init; }
    public required string Sha { get; init; }
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
}
