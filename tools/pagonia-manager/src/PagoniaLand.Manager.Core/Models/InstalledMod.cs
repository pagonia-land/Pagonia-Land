namespace PagoniaLand.Manager;

public sealed class InstalledMod
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string InstallPath { get; init; } = string.Empty;
    public string? InstalledAt { get; init; }
    public string? SourcePath { get; init; }
    public string? SourceType { get; init; }
    public string? ManifestName { get; init; }

    /// <summary>
    /// Transport-neutral provenance string from the install sidecar — e.g.
    /// <c>gh:owner/repo#&lt;sha&gt;/&lt;id&gt;</c> for GitHub installs,
    /// <c>url:&lt;url&gt;#&lt;sha256&gt;</c> for direct-URL installs. Empty
    /// for purely-local folder / zip installs. Used by the direct-URL
    /// fetcher's drift detection to compare a fresh install's archive hash
    /// against the previously-installed one when both name the same URL.
    /// </summary>
    public string? Source { get; init; }
}
