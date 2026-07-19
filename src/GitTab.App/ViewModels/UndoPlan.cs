namespace GitTab.App.ViewModels;

/// <summary>
/// Chooses the git command that undoes the last HEAD-moving action, from its reflog message and the
/// previous HEAD sha (HEAD@{1}). Pure, so the classification is unit-testable without a real repo.
/// </summary>
public static class UndoPlan
{
    /// <summary>The git args that undo <paramref name="reflogMessage"/>, or null if it can't be undone.</summary>
    public static string[]? Args(string reflogMessage, string previousSha)
    {
        if (string.IsNullOrEmpty(reflogMessage) || string.IsNullOrEmpty(previousSha)) return null;

        // A branch switch is reversed by switching back, not by moving the branch.
        if (reflogMessage.StartsWith("checkout: moving from", StringComparison.Ordinal))
            return new[] { "checkout", "-" };

        // A commit/amend is undone softly so the changes come back staged rather than being discarded.
        if (reflogMessage.StartsWith("commit:", StringComparison.Ordinal) ||
            reflogMessage.StartsWith("commit (amend):", StringComparison.Ordinal))
            return new[] { "reset", "--soft", previousSha };

        // reset / merge / pull / rebase / cherry-pick / revert / … — restore the previous position but
        // keep (don't clobber) any uncommitted local edits; --keep aborts instead of losing them.
        return new[] { "reset", "--keep", previousSha };
    }
}
