using System.Reflection;

namespace GitTab.App;

/// <summary>App identity/version helpers, read from assembly metadata.</summary>
public static class AppInfo
{
    public const string ProductName = "Git Tab";
    public const string RepoOwner = "BaeTab";
    public const string RepoName = "git_tab";

    /// <summary>Three-part semantic version string, e.g. "0.1.0".</summary>
    public static string Version
    {
        get
        {
            var info = typeof(AppInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // Strip any "+<sha>" build metadata.
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }
            return typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public static Version SemVer =>
        System.Version.TryParse(Version, out var v) ? v : new Version(0, 0, 0);
}
