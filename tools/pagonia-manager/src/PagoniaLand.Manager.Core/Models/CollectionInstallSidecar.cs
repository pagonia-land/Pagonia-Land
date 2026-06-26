using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Provenance sidecar written beside an installed collection's manifest in
/// <c>&lt;store&gt;/collections/&lt;id&gt;/&lt;version&gt;/</c> when the
/// collection was fetched from a remote repo. The per-mod origins already live
/// in the lockfile; this records where the <em>collection itself</em> came from,
/// so a later read-only update check knows which repo's <c>index.yaml</c>
/// advertises this collection's <c>version</c>. The mod equivalent is
/// <see cref="InstallSidecar"/>.
/// </summary>
public sealed class CollectionInstallSidecar
{
    [YamlMember(Alias = "installedAt")]
    public string InstalledAt { get; init; } = string.Empty;

    /// <summary>
    /// Transport-neutral origin of the collection install — e.g.
    /// <c>gh:owner/repo#&lt;sha&gt;/&lt;collection-id&gt;</c>. The SHA is pinned
    /// at fetch time so the trail stays accurate after the branch moves on the
    /// remote. Empty for a local-file collection install (nothing to
    /// update-check against).
    /// </summary>
    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;
}
