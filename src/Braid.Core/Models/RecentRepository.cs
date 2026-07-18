namespace Braid.Core.Models;

/// <summary>A previously-opened repository, persisted to %AppData% as JSON.</summary>
public sealed class RecentRepository
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset LastOpenedUtc { get; init; }
}
