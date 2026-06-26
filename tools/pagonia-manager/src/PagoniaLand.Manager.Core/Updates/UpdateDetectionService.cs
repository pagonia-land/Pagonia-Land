namespace PagoniaLand.Manager;

/// <summary>One installed mod that a newer version is published for.</summary>
public sealed record ModUpdate(
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string GameDatabaseVersion);

/// <summary>One installed collection that a newer version is published for.</summary>
public sealed record CollectionUpdate(
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string GameDatabaseVersion);

/// <summary>One installed mod whose source re-published the <b>same version</b> with different content
/// — caught by the advertised <c>contentHash</c> differing from the installed payload's. The version
/// string can't signal this; the hash can.</summary>
public sealed record ModContentDrift(
    string Id,
    string Version,
    string AdvertisedHash,
    string InstalledHash);

/// <summary>Outcome of a read-only update check across a store's installed mods.</summary>
public sealed class UpdateCheckResult
{
    /// <summary>Mods whose source repo advertises a strictly-newer version.</summary>
    public IReadOnlyList<ModUpdate> Updates { get; init; } = Array.Empty<ModUpdate>();

    /// <summary>Collections whose source repo advertises a strictly-newer version.</summary>
    public IReadOnlyList<CollectionUpdate> CollectionUpdates { get; init; } = Array.Empty<CollectionUpdate>();

    /// <summary>Mods whose source re-published the same version with changed content (hash drift).</summary>
    public IReadOnlyList<ModContentDrift> ContentDrifts { get; init; } = Array.Empty<ModContentDrift>();

    /// <summary>One info per available update + one warning per mod / collection that couldn't be checked.</summary>
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    /// <summary>Installed mods that carry a remote (gh:) source, so an update check was attempted.</summary>
    public int CheckedCount { get; init; }

    /// <summary>Installed mods with no remote source (local folder / zip) — nothing to compare against.</summary>
    public int SkippedLocalCount { get; init; }

    /// <summary>Installed collections that carry a remote (gh:) source, so an update check was attempted.</summary>
    public int CheckedCollectionCount { get; init; }

    /// <summary>Installed collections with no remote source (local-file install) — nothing to compare against.</summary>
    public int SkippedLocalCollectionCount { get; init; }
}

/// <summary>
/// Read-only update detection for both mods and collections. For each installed mod / collection that
/// came from a <c>gh:</c> repo, it re-fetches that repo's <c>index.yaml</c> at the default branch and
/// compares the catalog-advertised <c>version</c> to what's installed — <b>mirror-first</b> (the index
/// is the cheap, curated source of truth; the patcher's <c>index-check</c> keeps it honest against each
/// <c>mod.yaml</c>). It never changes anything: it only surfaces "an update is available" so the user
/// can decide.
///
/// <para>
/// Local-only installs (folder / zip mods, local-file collections — no <c>source</c>) are skipped —
/// there's nothing to check against. A repo that's unreachable, ships no index, or no longer lists the
/// mod / collection yields a per-item warning rather than failing the whole check, so one dead repo
/// doesn't hide the rest. The collection half compares the repo index's <c>collections[].version</c>
/// against the highest installed version of each collection (the same shape as the mod half, reading
/// the collection provenance sidecar via <see cref="InstalledCollection.Source"/>).
/// </para>
/// </summary>
public sealed class UpdateDetectionService
{
    private readonly IRemoteContentFetcher _fetcher;

    public UpdateDetectionService(IRemoteContentFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public UpdateCheckResult Check(StoreLayout layout, CancellationToken cancellationToken = default)
    {
        // One row per installed id, at its highest installed version — "is something newer published
        // than the best copy I already have?" (an older version kept around for rollback isn't drift).
        var latestById = new ModLister().List(layout)
            .GroupBy(m => m.Id, StringComparer.Ordinal)
            .Select(group => group.Aggregate(static (a, b) => ModVersion.IsNewer(b.Version, a.Version) ? b : a))
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList();

        var updates = new List<ModUpdate>();
        var drifts = new List<ModContentDrift>();
        var diagnostics = new List<ManagerDiagnostic>();
        var indexFetcher = new RepoIndexFetcher(_fetcher);
        var checkable = 0;
        var skippedLocal = 0;

        foreach (var mod in latestById)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(mod.Source)
                || !RemoteSourceParser.TryParse(mod.Source, out var parsed)
                || parsed is not GitHubSource source)
            {
                // Local folder / zip / non-gh provenance — no index mirror to compare against.
                skippedLocal++;
                continue;
            }

            checkable++;

            // Fetch the index at the default branch (the pinned sha in the provenance is the *installed*
            // commit; "latest" lives at HEAD). Owner/repo/base come from the provenance.
            var fetch = indexFetcher.Fetch(source with { Ref = "HEAD" }, cancellationToken);
            if (!fetch.Success || !fetch.HasIndex || fetch.Index is null)
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                    $"Could not check '{mod.Id}' for updates: its source repo {source.Owner}/{source.Repo} didn't return a readable index.yaml."));
                continue;
            }

            var entry = fetch.Index.Mods.FirstOrDefault(e => string.Equals(e.Id, mod.Id, StringComparison.Ordinal));
            if (entry is null || string.IsNullOrWhiteSpace(entry.Version))
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                    $"Could not check '{mod.Id}' for updates: the repo {source.Owner}/{source.Repo} no longer lists it with a version in index.yaml."));
                continue;
            }

            if (ModVersion.IsNewer(entry.Version, mod.Version))
            {
                updates.Add(new ModUpdate(mod.Id, mod.Version, entry.Version, entry.GameDatabaseVersion));
                diagnostics.Add(Info(ManagerDiagnosticCodes.ModUpdateAvailable,
                    $"Update available: {mod.Id} {mod.Version} -> {entry.Version}"
                    + (string.IsNullOrWhiteSpace(entry.GameDatabaseVersion) ? "." : $" (gameDatabaseVersion {entry.GameDatabaseVersion}).")));
            }
            else if (string.Equals(entry.Version, mod.Version, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(entry.ContentHash))
            {
                // Same version string, but did the source re-publish different content? Re-hash the
                // installed payload (mod.yaml + patches; the install sidecar isn't part of the payload)
                // and compare to the advertised contentHash. A difference is drift the version can't show.
                var installedHash = PagoniaLand.Patcher.ContentHash.OfModPayload(mod.InstallPath);
                if (installedHash is not null
                    && !string.Equals(installedHash, entry.ContentHash, StringComparison.OrdinalIgnoreCase))
                {
                    drifts.Add(new ModContentDrift(mod.Id, mod.Version, entry.ContentHash, installedHash));
                    diagnostics.Add(Info(ManagerDiagnosticCodes.ModContentDriftAvailable,
                        $"Content changed for {mod.Id} {mod.Version} (same version, different content) — re-install to refresh."));
                }
            }
            else if (!string.Equals(entry.Version, mod.Version, StringComparison.Ordinal)
                && ModVersion.TryParse(entry.Version, out _) != ModVersion.TryParse(mod.Version, out _))
            {
                // The two versions differ but exactly one is a parseable semver, so IsNewer can't compare
                // them. Don't silently bucket the mod as up-to-date — surface that the check couldn't run,
                // or a real update hides behind an unparseable version string.
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                    $"Could not compare versions for '{mod.Id}': installed '{mod.Version}' vs advertised '{entry.Version}' (one isn't a parseable version)."));
            }
        }

        var (collectionUpdates, checkableCollections, skippedLocalCollections) =
            CheckCollections(layout, indexFetcher, diagnostics, cancellationToken);

        return new UpdateCheckResult
        {
            Updates = updates,
            CollectionUpdates = collectionUpdates,
            ContentDrifts = drifts,
            Diagnostics = diagnostics,
            CheckedCount = checkable,
            SkippedLocalCount = skippedLocal,
            CheckedCollectionCount = checkableCollections,
            SkippedLocalCollectionCount = skippedLocalCollections,
        };
    }

    /// <summary>
    /// The collection half of the check: one row per installed collection id at its highest installed
    /// version, compared against the source repo's <c>index.yaml</c> <c>collections[].version</c> at
    /// HEAD. Mirrors the mod loop above — local-file collections (no provenance sidecar) are skipped,
    /// an unreachable repo / dropped entry warns per-collection.
    /// </summary>
    private static (List<CollectionUpdate> Updates, int Checkable, int SkippedLocal) CheckCollections(
        StoreLayout layout,
        RepoIndexFetcher indexFetcher,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var latestById = new CollectionLister().List(layout)
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .Select(group => group.Aggregate(static (a, b) => ModVersion.IsNewer(b.Version, a.Version) ? b : a))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();

        var updates = new List<CollectionUpdate>();
        var checkable = 0;
        var skippedLocal = 0;

        foreach (var collection in latestById)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(collection.Source)
                || !RemoteSourceParser.TryParse(collection.Source, out var parsed)
                || parsed is not GitHubSource source)
            {
                // Local-file collection install (no provenance sidecar) — nothing to compare against.
                skippedLocal++;
                continue;
            }

            checkable++;

            var fetch = indexFetcher.Fetch(source with { Ref = "HEAD" }, cancellationToken);
            if (!fetch.Success || !fetch.HasIndex || fetch.Index is null)
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                    $"Could not check collection '{collection.Id}' for updates: its source repo {source.Owner}/{source.Repo} didn't return a readable index.yaml."));
                continue;
            }

            var entry = fetch.Index.Collections.FirstOrDefault(e => string.Equals(e.Id, collection.Id, StringComparison.Ordinal));
            if (entry is null || string.IsNullOrWhiteSpace(entry.Version))
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                    $"Could not check collection '{collection.Id}' for updates: the repo {source.Owner}/{source.Repo} no longer lists it with a version in index.yaml."));
                continue;
            }

            if (ModVersion.IsNewer(entry.Version, collection.Version))
            {
                updates.Add(new CollectionUpdate(collection.Id, collection.Version, entry.Version, entry.GameDatabaseVersion));
                diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionUpdateAvailable,
                    $"Collection update available: {collection.Id} {collection.Version} -> {entry.Version}"
                    + (string.IsNullOrWhiteSpace(entry.GameDatabaseVersion) ? "." : $" (gameDatabaseVersion {entry.GameDatabaseVersion}).")));
            }
        }

        return (updates, checkable, skippedLocal);
    }

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);
}
