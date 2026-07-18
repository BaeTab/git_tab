using GitTab.Core.Models;

namespace GitTab.App.ViewModels;

public sealed class BranchNodeViewModel
{
    public required BranchInfo Model { get; init; }

    public string FriendlyName => Model.FriendlyName;
    public string CanonicalName => Model.CanonicalName;
    public bool IsRemote => Model.IsRemote;
    public bool IsCurrent => Model.IsCurrent;
    public string? RemoteName => Model.RemoteName;

    /// <summary>Short name without the remote prefix (e.g. "origin/main" -> "main").</summary>
    public string ShortName
    {
        get
        {
            if (!IsRemote) return FriendlyName;
            var slash = FriendlyName.IndexOf('/');
            return slash >= 0 ? FriendlyName[(slash + 1)..] : FriendlyName;
        }
    }

    public string AheadBehindText
    {
        get
        {
            if (Model.Ahead is not { } a || Model.Behind is not { } b || (a == 0 && b == 0)) return string.Empty;
            var parts = new List<string>(2);
            if (a > 0) parts.Add($"↑{a}");
            if (b > 0) parts.Add($"↓{b}");
            return string.Join(" ", parts);
        }
    }
}
