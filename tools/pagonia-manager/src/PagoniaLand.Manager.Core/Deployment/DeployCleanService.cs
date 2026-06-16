using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public enum DeployCleanAction
{
    /// <summary>The timestamp directory was removed (or would have been on a
    /// dry-run).</summary>
    Removed,

    /// <summary>The timestamp was within the keep-window or protected as the
    /// active <c>state.yaml.lastDeploy</c> entry.</summary>
    Kept,

    /// <summary>The timestamp would have been removed by the keep-window
    /// rule but was the current <c>state.yaml.lastDeploy</c> entry — refusing
    /// to remove it preserves the "what status says is the latest" rollback
    /// path.</summary>
    RefusedLatest,
}

/// <summary>One audit row from <see cref="DeployCleanService.Clean"/>. Surfaces
/// every per-timestamp action the call took (or would have taken on dry-run)
/// so the CLI / wizard can render a precise log of what changed.</summary>
public sealed record DeployCleanEntry(
    string Fingerprint,
    string Timestamp,
    string Profile,
    DeployCleanAction Action,
    string Reason);

public sealed class DeployCleanResult
{
    public int RemovedCount { get; init; }
    public int KeptCount { get; init; }
    public int RefusedCount { get; init; }
    public bool DryRun { get; init; }
    public IReadOnlyList<DeployCleanEntry> Entries { get; init; } = Array.Empty<DeployCleanEntry>();
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>opt-in retention command for deploy backups.
/// Per-fingerprint keeps the N most recent timestamp directories and removes
/// older ones (manifests + backups). Refuses to delete the timestamp that
/// <c>state.yaml.lastDeploy</c> currently references — removing it would
/// orphan the user's "current deploy" with no rollback path.
/// <para>Default behaviour throughout the live-install path was "keep all backups". This
/// command is the user-opt-in to reclaim disk space without changing that
/// default. the current release does NOT auto-run it; the wizard surfaces a
/// <c>manager.deploysStorageHigh</c> hint when the total store/deploys/
/// size crosses ~15 GB (a single live deploy already backs up ~5 GB since
/// core.pak is that large), but the actual cleanup is always explicit.</para>
/// </summary>
public sealed class DeployCleanService
{
    private readonly DeployHistoryStore _historyStore = new();
    private readonly StoreStateReader _stateReader = new();

    // AOT pin so YamlDotNet can rewrite the trimmed history.yaml.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(DeployHistory))]
    [DynamicDependency(Shape, typeof(DeployHistoryEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployHistoryEntry>))]
    public DeployCleanService()
    {
    }

    /// <summary>Trim deploy timestamp directories per fingerprint to the
    /// <paramref name="keep"/> most recent entries.</summary>
    /// <param name="gameRoot">Optional scope — when non-null, only the
    /// fingerprint computed for this game install gets cleaned. When null,
    /// every fingerprint directory under <c>&lt;store&gt;/deploys/</c> is
    /// processed (each subject to its own keep-N rule).</param>
    /// <param name="dryRun">When true, no files are removed and history.yaml
    /// is not rewritten; the returned Entries log still shows what WOULD have
    /// happened. Counts reflect "would remove / would keep" tallies.</param>
    public DeployCleanResult Clean(StoreLayout layout, int keep, string? gameRoot, bool dryRun)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var entries = new List<DeployCleanEntry>();

        if (keep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keep), keep, "keep must be >= 0");
        }

        if (!Directory.Exists(layout.DeploysDirectory))
        {
            return new DeployCleanResult { DryRun = dryRun };
        }

        var activeLastDeploy = ResolveActiveLastDeploy(layout);

        IEnumerable<string> targetFingerprints;
        if (!string.IsNullOrWhiteSpace(gameRoot))
        {
            var currentFingerprint = GameFingerprint.Compute(gameRoot);
            targetFingerprints = Directory.Exists(layout.DeployFingerprintDirectory(currentFingerprint))
                ? new[] { currentFingerprint }
                : Array.Empty<string>();
        }
        else
        {
            targetFingerprints = Directory.EnumerateDirectories(layout.DeploysDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>();
        }

        var removed = 0;
        var kept = 0;
        var refused = 0;

        foreach (var fingerprint in targetFingerprints)
        {
            if (!_historyStore.TryRead(layout, fingerprint, out var history, out _)) continue;
            if (history.Deploys.Count == 0) continue;

            // history.Deploys is newest-first by construction (DeployService
            // prepends the new entry). Take the first `keep` to preserve;
            // everything past that is a candidate for removal.
            //
            // Always retain the NEWEST deploy per fingerprint as the rollback anchor:
            // `rollback` reverts Deploys[0], so removing it (e.g. `--keep 0` on a
            // present but non-active install) would silently destroy that install's
            // only undo path. The lastDeploy guard below only protects the *active*
            // install; flooring the effective keep at 1 protects every fingerprint.
            var effectiveKeep = Math.Max(keep, 1);
            var toKeep = history.Deploys.Take(effectiveKeep).ToList();
            var toMaybeRemove = history.Deploys.Skip(effectiveKeep).ToList();

            foreach (var entry in toKeep)
            {
                kept++;
                entries.Add(new DeployCleanEntry(
                    Fingerprint: fingerprint,
                    Timestamp: entry.Timestamp,
                    Profile: entry.Profile,
                    Action: DeployCleanAction.Kept,
                    Reason: keep == 0 ? "newest deploy retained as the rollback anchor" : $"within keep-{keep} window"));
            }

            var keptAfterProtection = new List<DeployHistoryEntry>(toKeep);

            foreach (var entry in toMaybeRemove)
            {
                if (activeLastDeploy is not null
                    && string.Equals(activeLastDeploy.Value.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(activeLastDeploy.Value.Timestamp, entry.Timestamp, StringComparison.Ordinal))
                {
                    refused++;
                    keptAfterProtection.Add(entry);
                    entries.Add(new DeployCleanEntry(
                        Fingerprint: fingerprint,
                        Timestamp: entry.Timestamp,
                        Profile: entry.Profile,
                        Action: DeployCleanAction.RefusedLatest,
                        Reason: "current state.yaml.lastDeploy — removing would orphan the user's 'latest deploy'"));
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.DeployCleanRefusedLatest,
                        $"Refused to remove '{entry.Timestamp}' under fingerprint '{fingerprint}' — state.yaml.lastDeploy points at it."));
                    continue;
                }

                removed++;
                entries.Add(new DeployCleanEntry(
                    Fingerprint: fingerprint,
                    Timestamp: entry.Timestamp,
                    Profile: entry.Profile,
                    Action: DeployCleanAction.Removed,
                    Reason: dryRun ? "would remove (dry-run)" : "removed"));
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.DeployCleanRemoved,
                    dryRun
                        ? $"Dry-run: would remove '{entry.Timestamp}' under fingerprint '{fingerprint}'."
                        : $"Removed '{entry.Timestamp}' under fingerprint '{fingerprint}'."));

                if (!dryRun)
                {
                    var tsDir = layout.DeployTimestampDirectory(fingerprint, entry.Timestamp);
                    if (Directory.Exists(tsDir))
                    {
                        try { Directory.Delete(tsDir, recursive: true); }
                        catch (IOException) { /* best-effort */ }
                    }
                }
            }

            // Rewrite history.yaml with the truncated list (keep-window plus
            // any refused-latest entries that survived). On dry-run we skip
            // the write — the on-disk state is unchanged.
            if (!dryRun && keptAfterProtection.Count != history.Deploys.Count)
            {
                _historyStore.Write(layout, fingerprint, new DeployHistory
                {
                    DeployHistoryVersion = history.DeployHistoryVersion,
                    GameFingerprint = history.GameFingerprint,
                    GameRoot = history.GameRoot,
                    Deploys = keptAfterProtection,
                });
            }
        }

        return new DeployCleanResult
        {
            RemovedCount = removed,
            KeptCount = kept,
            RefusedCount = refused,
            DryRun = dryRun,
            Entries = entries,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Total bytes used under <c>&lt;store&gt;/deploys/</c>. Best-effort
    /// — broken symlinks / permission errors are ignored. Used by the status
    /// dashboard to surface a "deploys storage is growing, consider 'deploys
    /// clean'" hint when the total crosses a soft threshold.</summary>
    public static long ComputeDeploysSize(StoreLayout layout)
    {
        if (!Directory.Exists(layout.DeploysDirectory)) return 0;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(layout.DeploysDirectory, "*", SearchOption.AllDirectories))
        {
            try { total += new FileInfo(file).Length; }
            catch { /* best-effort */ }
        }
        return total;
    }

    /// <summary>Per-fingerprint byte usage under <c>&lt;store&gt;/deploys/</c>, largest
    /// first. Each immediate subdirectory of the deploys folder is one game-install
    /// fingerprint. Best-effort like <see cref="ComputeDeploysSize"/>; lets the status
    /// dashboard break the total down by which install's backups dominate.</summary>
    public static IReadOnlyList<(string Fingerprint, long Bytes)> ComputeDeploysSizeByFingerprint(StoreLayout layout)
    {
        if (!Directory.Exists(layout.DeploysDirectory)) return [];
        var result = new List<(string Fingerprint, long Bytes)>();
        foreach (var dir in Directory.EnumerateDirectories(layout.DeploysDirectory))
        {
            long bytes = 0;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { bytes += new FileInfo(file).Length; }
                catch { /* best-effort */ }
            }
            result.Add((Path.GetFileName(dir), bytes));
        }
        result.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
        return result;
    }

    private (string Fingerprint, string Timestamp)? ResolveActiveLastDeploy(StoreLayout layout)
    {
        if (!_stateReader.Exists(layout)) return null;
        var state = _stateReader.Read(layout);
        if (state.LastDeploy is null) return null;
        // A malformed lastDeploy with an empty GameRoot would throw in GameFingerprint.Compute and
        // crash `deploys clean`. Treat it as "no active deploy to protect" — the per-fingerprint
        // keep>=1 floor still preserves each fingerprint's newest backup as the rollback anchor.
        if (string.IsNullOrWhiteSpace(state.LastDeploy.GameRoot)) return null;
        var fp = GameFingerprint.Compute(state.LastDeploy.GameRoot);
        return (fp, state.LastDeploy.Timestamp);
    }
}
