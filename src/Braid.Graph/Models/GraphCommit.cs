using System.Diagnostics.CodeAnalysis;

namespace Braid.Graph.Models;

/// <summary>
/// The minimal commit shape the layout engine needs. Deliberately independent of Braid.Core
/// so the engine stays a pure, self-contained, unit-testable component.
/// Commits must be supplied newest-first, topologically ordered (a parent never appears
/// above its child).
/// </summary>
public sealed class GraphCommit
{
    public required string Sha { get; init; }
    public required IReadOnlyList<string> ParentShas { get; init; }

    public GraphCommit() { }

    [SetsRequiredMembers]
    public GraphCommit(string sha, params string[] parents)
    {
        Sha = sha;
        ParentShas = parents;
    }
}
