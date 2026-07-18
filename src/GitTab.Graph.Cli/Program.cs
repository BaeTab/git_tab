using System.Text;
using GitTab.Core.Git;
using GitTab.Graph;
using GitTab.Graph.Cli;
using GitTab.Graph.Models;
using Microsoft.Extensions.Logging.Abstractions;

try { Console.OutputEncoding = Encoding.UTF8; } catch { /* redirected / unsupported console */ }

// Throwaway validation harness: reads a real repo and prints an ASCII commit graph so a human
// can confirm the lane layout matches `git log --graph`.
//   usage: GitTab.Graph.Cli [repoPath] [maxCommits]

string repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
int max = args.Length > 1 && int.TryParse(args[1], out var m) ? m : 40;

using var svc = new RepositoryService(
    new GitCommandRunner(NullLogger<GitCommandRunner>.Instance),
    NullLogger<RepositoryService>.Instance);

var discovered = svc.Discover(repoPath);
if (discovered is null)
{
    Console.Error.WriteLine($"No git repository found at: {repoPath}");
    return 1;
}

svc.Open(repoPath);
var commits = svc.GetCommits(max);
if (commits.Count == 0)
{
    Console.Error.WriteLine("Repository has no commits.");
    return 1;
}

var graphCommits = commits
    .Select(c => new GraphCommit { Sha = c.Sha, ParentShas = c.ParentShas })
    .ToArray();

var layout = new GraphLayoutEngine().Build(graphCommits);
var meta = commits.Select(c => (c.Sha, Summary: c.Summary)).ToArray();

Console.WriteLine($"# GitTab GraphLayoutEngine — {discovered}");
Console.WriteLine($"# {commits.Count} commits, {layout.LaneCount} lane(s)\n");
Console.Write(AsciiGraphRenderer.Render(layout, meta));
return 0;
