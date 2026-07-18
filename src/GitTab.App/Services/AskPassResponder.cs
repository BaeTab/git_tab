using System.IO;

namespace GitTab.App.Services;

/// <summary>
/// Runs when git invokes this executable as <c>GIT_ASKPASS</c> (marked by the environment variable
/// <c>GITTAB_ASKPASS=1</c>). git passes the prompt — e.g. <c>Username for 'https://github.com': </c>
/// — as the single argument; we look up the GUI-managed credential store and print the matching
/// username or secret to stdout so HTTPS authentication proceeds with no console interaction.
/// </summary>
internal static class AskPassResponder
{
    public static void Respond(string[] args, TextWriter output)
    {
        var prompt = args.Length > 0 ? args[0] : string.Empty;
        var key = CredentialKey.FromUrl(ExtractUrl(prompt));
        if (key is null) return;

        var cred = new WindowsCredentialStore().Get(key);
        if (cred is null) return;

        var wantPassword = prompt.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase);
        output.Write(wantPassword ? cred.Value.Secret : cred.Value.User);
        output.Write('\n');
        output.Flush();
    }

    // Pull the URL out from between the single quotes git puts around it.
    private static string? ExtractUrl(string prompt)
    {
        int a = prompt.IndexOf('\'');
        int b = a >= 0 ? prompt.IndexOf('\'', a + 1) : -1;
        return a >= 0 && b > a ? prompt.Substring(a + 1, b - a - 1) : null;
    }
}
