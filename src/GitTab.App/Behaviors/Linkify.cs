using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using GitTab.App.Services;

namespace GitTab.App.Behaviors;

/// <summary>
/// Attached behavior that renders a commit message into a <see cref="TextBlock"/> with clickable
/// links: <c>http(s)://…</c> URLs, and <c>#123</c> issue/PR references resolved against the repo's
/// remote (GitHub/GitLab). Clicking opens the default browser.
/// </summary>
public static class Linkify
{
    private static readonly Regex LinkPattern =
        new(@"(?<url>https?://[^\s]+)|(?<issue>#(?<num>\d+))", RegexOptions.Compiled);

    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Linkify), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty RemoteUrlProperty = DependencyProperty.RegisterAttached(
        "RemoteUrl", typeof(string), typeof(Linkify), new PropertyMetadata(null, OnChanged));

    public static void SetText(DependencyObject o, string? v) => o.SetValue(TextProperty, v);
    public static string? GetText(DependencyObject o) => (string?)o.GetValue(TextProperty);
    public static void SetRemoteUrl(DependencyObject o, string? v) => o.SetValue(RemoteUrlProperty, v);
    public static string? GetRemoteUrl(DependencyObject o) => (string?)o.GetValue(RemoteUrlProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.Inlines.Clear();

        var text = GetText(tb) ?? string.Empty;
        var remote = GetRemoteUrl(tb);

        int pos = 0;
        foreach (Match m in LinkPattern.Matches(text))
        {
            if (m.Index > pos) tb.Inlines.Add(new Run(text[pos..m.Index]));

            string? target = m.Groups["url"].Success
                ? m.Groups["url"].Value
                : (int.TryParse(m.Groups["num"].Value, out var n) ? RemoteWeb.IssueUrl(remote, n) : null);

            if (target is not null)
            {
                var link = new Hyperlink(new Run(m.Value)) { NavigateUri = new Uri(target) };
                link.RequestNavigate += OnNavigate;
                tb.Inlines.Add(link);
            }
            else
            {
                tb.Inlines.Add(new Run(m.Value));   // e.g. #123 with no recognizable remote
            }
            pos = m.Index + m.Length;
        }
        if (pos < text.Length) tb.Inlines.Add(new Run(text[pos..]));
    }

    private static void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* no browser / blocked — ignore */ }
        e.Handled = true;
    }
}
