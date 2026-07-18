namespace GitTab.Core.Git;

/// <summary>
/// Validates user-provided positional git arguments — ref/branch/tag/remote names and URLs — so a
/// value that begins with <c>-</c> cannot be parsed by git as an option (argument injection). Path
/// arguments are already separated with <c>--</c> at the call sites; this guards the ref/name
/// positionals that a trailing <c>--</c> does not protect.
/// </summary>
public static class GitArg
{
    /// <summary>True if <paramref name="value"/> is safe to pass as a positional git argument.</summary>
    public static bool IsSafe(string? value)
        => !string.IsNullOrEmpty(value)
           && value[0] != '-'
           && !value.Any(char.IsControl);
}
