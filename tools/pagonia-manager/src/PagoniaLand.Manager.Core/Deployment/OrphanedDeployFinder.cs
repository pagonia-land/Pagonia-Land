namespace PagoniaLand.Manager;

/// <summary>Why a deploy directory under <c>&lt;store&gt;/deploys/</c> no longer
/// applies to a live install. <see cref="OrphanedDeploy"/> carries one of these
/// as its <see cref="OrphanedDeploy.Reason"/>.</summary>
public enum OrphanReason
{
    /// <summary>The recorded gameRoot path no longer exists on disk — the
    /// user moved or deleted the game install. Backups under this deploy dir
    /// are no longer restorable (no target install to restore to).</summary>
    GameRootGone,

    /// <summary>The recorded gameRoot path still exists, but its current
    /// fingerprint differs from this deploy's. Most common cause: a Steam
    /// game update touched <c>system.json</c> between deploy and now.
    /// Restoring the backup would put pre-update pak bytes over a post-update
    /// install — likely broken.</summary>
    GameUpdated,
}

/// <summary>One orphaned deploy directory. The fingerprint + timestamp +
/// recorded gameRoot identify the deploy; <see cref="LatestProfile"/> +
/// <see cref="LatestModCount"/> + <see cref="LatestFileCount"/> summarise its
/// most recent entry for a humane "what was this" line.</summary>
public sealed record OrphanedDeploy(
    string Fingerprint,
    string RecordedGameRoot,
    OrphanReason Reason,
    string? LatestTimestamp,
    string? LatestProfile,
    int LatestModCount,
    int LatestFileCount,
    int TotalDeployCount);

/// <summary>finds <c>&lt;store&gt;/deploys/&lt;fp&gt;/</c>
/// directories that no longer apply to a live install, either because the
/// recorded gameRoot is gone or because its fingerprint has drifted (Steam
/// update being the typical case). Used by:
/// <list type="bullet">
/// <item><description>The <c>deploys list-orphans</c> CLI command + the
/// <see cref="StatusDashboard"/> warning panel — surface stale deploys
/// that consume disk space but can no longer be rolled back to.</description></item>
/// <item><description>The deploy preflight in <see cref="DeployService"/> —
/// emit <c>manager.gameUpdatedSinceLastDeploy</c> when re-deploying to a
/// fingerprint-changed install, so the user knows their old backups
/// aren't going to help.</description></item>
/// </list></summary>
public sealed class OrphanedDeployFinder
{
    private readonly DeployHistoryStore _historyStore = new();

    /// <summary>Enumerate every fingerprint directory under
    /// <c>&lt;store&gt;/deploys/</c>, return the ones whose recorded gameRoot
    /// is gone or whose current fingerprint no longer matches. Best-effort —
    /// directories that can't be read are silently skipped.</summary>
    public IReadOnlyList<OrphanedDeploy> FindAll(StoreLayout layout)
    {
        var deploysDir = layout.DeploysDirectory;
        if (!Directory.Exists(deploysDir)) return Array.Empty<OrphanedDeploy>();

        var orphans = new List<OrphanedDeploy>();
        foreach (var fpDir in Directory.EnumerateDirectories(deploysDir))
        {
            var fingerprint = Path.GetFileName(fpDir);
            if (string.IsNullOrEmpty(fingerprint)) continue;

            if (!_historyStore.TryRead(layout, fingerprint, out var history, out _)) continue;
            if (history.Deploys.Count == 0) continue;

            var recordedGameRoot = history.GameRoot;
            var reason = ClassifyOrphan(fingerprint, recordedGameRoot);
            if (reason is null) continue;

            var latest = history.Deploys[0];
            orphans.Add(new OrphanedDeploy(
                Fingerprint: fingerprint,
                RecordedGameRoot: recordedGameRoot,
                Reason: reason.Value,
                LatestTimestamp: latest.Timestamp,
                LatestProfile: latest.Profile,
                LatestModCount: latest.ModCount,
                LatestFileCount: latest.FileCount,
                TotalDeployCount: history.Deploys.Count));
        }
        return orphans;
    }

    /// <summary>Deploy-preflight helper: given a current gameRoot + its
    /// freshly-computed fingerprint, return true if any prior deploy in the
    /// store recorded the SAME gameRoot path but a DIFFERENT fingerprint
    /// (game update happened between then and now). The wizard surfaces
    /// <c>manager.gameUpdatedSinceLastDeploy</c> when this is true so the
    /// user knows their previous backups won't roll back cleanly.
    /// <paramref name="priorGameProductVersion"/> carries the version that prior
    /// deploy's latest manifest recorded (null if unrecorded / unreadable), so the
    /// warning can name the old version when both ends are known.</summary>
    public bool AnyPriorDeployForGameRootHasDifferentFingerprint(
        StoreLayout layout,
        string currentGameRoot,
        string currentFingerprint,
        out string? priorFingerprint,
        out string? priorGameProductVersion)
    {
        priorFingerprint = null;
        priorGameProductVersion = null;
        var deploysDir = layout.DeploysDirectory;
        if (!Directory.Exists(deploysDir)) return false;

        var normalisedGameRoot = Path.GetFullPath(currentGameRoot);
        foreach (var fpDir in Directory.EnumerateDirectories(deploysDir))
        {
            var fingerprint = Path.GetFileName(fpDir);
            if (string.IsNullOrEmpty(fingerprint)) continue;
            if (string.Equals(fingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_historyStore.TryRead(layout, fingerprint, out var history, out _)) continue;
            if (history.Deploys.Count == 0) continue;

            if (PathsEqual(history.GameRoot, normalisedGameRoot))
            {
                priorFingerprint = fingerprint;
                priorGameProductVersion = TryReadManifestProductVersion(layout, fingerprint, history);
                return true;
            }
        }
        return false;
    }

    /// <summary>Best-effort read of the <c>gameProductVersion</c> the prior
    /// deploy's latest manifest recorded. Returns null when the manifest is
    /// missing, unreadable, or predates the field (older deploys never stored it).</summary>
    private static string? TryReadManifestProductVersion(
        StoreLayout layout, string fingerprint, DeployHistory history)
    {
        if (history.Deploys.Count == 0) return null;
        var manifestPath = layout.DeployManifestFile(fingerprint, history.Deploys[0].Timestamp);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var manifest = ManagerYaml.CreateDeserializer()
                .Deserialize<DeployManifest>(File.ReadAllText(manifestPath));
            return string.IsNullOrWhiteSpace(manifest?.GameProductVersion)
                ? null
                : manifest!.GameProductVersion;
        }
        catch
        {
            return null;
        }
    }

    private OrphanReason? ClassifyOrphan(string fingerprint, string recordedGameRoot)
    {
        if (string.IsNullOrWhiteSpace(recordedGameRoot)) return null;
        if (!Directory.Exists(recordedGameRoot)) return OrphanReason.GameRootGone;

        var currentFingerprint = GameFingerprint.Compute(recordedGameRoot);
        return string.Equals(currentFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)
            ? null
            : OrphanReason.GameUpdated;
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
