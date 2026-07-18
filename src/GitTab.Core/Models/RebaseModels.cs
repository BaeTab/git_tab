namespace GitTab.Core.Models;

public enum RebaseAction
{
    Pick,
    Reword,   // keeps the existing message (non-interactive editor)
    Squash,   // fold into previous, combined message kept
    Fixup,    // fold into previous, discard this message
    Drop
}

/// <summary>One line of an interactive-rebase plan (top = oldest applied first).</summary>
public sealed class RebaseTodoItem
{
    public required string Sha { get; init; }
    public required string Summary { get; init; }
    public RebaseAction Action { get; set; } = RebaseAction.Pick;
}
