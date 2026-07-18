using GitTab.App.Localization;

namespace GitTab.App.ViewModels;

/// <summary>Formats a timestamp as a localized "N units ago" string.</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset when, ILocalizationService loc)
    {
        var delta = DateTimeOffset.UtcNow - when.ToUniversalTime();
        if (delta < TimeSpan.Zero) delta = TimeSpan.Zero;

        if (delta.TotalMinutes < 1) return loc.T("Time.JustNow");
        if (delta.TotalHours < 1) return loc.T("Time.MinutesAgo", (int)delta.TotalMinutes);
        if (delta.TotalDays < 1) return loc.T("Time.HoursAgo", (int)delta.TotalHours);
        if (delta.TotalDays < 30) return loc.T("Time.DaysAgo", (int)delta.TotalDays);
        if (delta.TotalDays < 365) return loc.T("Time.MonthsAgo", (int)(delta.TotalDays / 30));
        return loc.T("Time.YearsAgo", (int)(delta.TotalDays / 365));
    }
}
