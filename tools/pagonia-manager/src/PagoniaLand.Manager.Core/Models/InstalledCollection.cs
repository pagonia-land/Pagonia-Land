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

    /// <summary>
    /// Transport-neutral provenance string from the collection install sidecar —
    /// e.g. <c>gh:owner/repo#&lt;sha&gt;/&lt;collection-id&gt;</c> for a remote
    /// install. Empty / null for a local-file collection install. Used by the
    /// read-only update check to find the repo whose <c>index.yaml</c> advertises
    /// this collection's version.
    /// </summary>
    public string? Source { get; init; }
}
