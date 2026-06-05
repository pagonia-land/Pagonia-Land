namespace PagoniaLand.Manager;

public sealed class InstalledCollection
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Author { get; init; }
    public string? GameDatabaseVersion { get; init; }
    public string? Description { get; init; }
    public int ResolvedModCount { get; init; }
    public string ManifestPath { get; init; } = string.Empty;
    public string? LockfilePath { get; init; }
    public string? GeneratedAt { get; init; }
}
