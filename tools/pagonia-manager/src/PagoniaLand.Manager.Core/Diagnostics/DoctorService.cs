using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public enum DoctorStatus
{
    Ok,
    Warning,
    Error,
    Skipped,
}

/// <summary>One named health check + its findings.</summary>
public sealed record DoctorCheck(
    string Name,
    DoctorStatus Status,
    string Summary,
    IReadOnlyList<ManagerDiagnostic> Diagnostics);

/// <summary>The consolidated health report.</summary>
public sealed record DoctorReport(IReadOnlyList<DoctorCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.Status == DoctorStatus.Error);
    public bool HasWarnings => Checks.Any(check => check.Status == DoctorStatus.Warning);
}

/// <summary>
/// `pagonia-manager doctor` — a read-only health roll-up that bundles checks the
/// manager already implements behind one verb, so a confused user has a single
/// first-stop command. It writes nothing. Each check reuses an existing service;
/// `doctor` only orchestrates and grades them. The interactive StatusDashboard is
/// the visual equivalent; this is its scriptable form.
/// </summary>
public sealed class DoctorService
{
    // Mirror the StatusDashboard's deploy-storage nag threshold.
    private const long DeployStorageNagBytes = 15L * 1024 * 1024 * 1024;

    /// <param name="gameRoot">Resolved game root, or null — game-dependent checks are skipped when absent.</param>
    /// <param name="updateFetcher">
    /// Optional remote fetcher enabling the read-only "updates available" check. <c>doctor</c> is
    /// otherwise fully offline, so this network check is <b>opt-in</b>: pass a fetcher (the CLI does so
    /// only for <c>--check-updates</c>) to run it, leave it null to <see cref="DoctorStatus.Skipped"/>
    /// it. A network failure degrades to Skipped, never an error — so it stays CI-safe.
    /// </param>
    public DoctorReport Run(StoreLayout layout, string? gameRoot, IRemoteContentFetcher? updateFetcher = null)
    {
        var checks = new List<DoctorCheck>();

        // 1. Store initialised — a hard gate; nothing else is meaningful without it.
        var storeDiagnostics = new List<ManagerDiagnostic>();
        if (!ServicePreconditions.RequireInitialisedStore(layout, storeDiagnostics))
        {
            checks.Add(new DoctorCheck("Store", DoctorStatus.Error, "not initialised — run 'store init'", storeDiagnostics));
            return new DoctorReport(checks);
        }
        checks.Add(new DoctorCheck("Store", DoctorStatus.Ok, $"initialised at {layout.Root}", []));

        // 2. Active profile.
        var active = new ActiveProfileService().Show(layout);
        var profile = active.Profile;
        if (!active.Success || profile is null)
        {
            checks.Add(new DoctorCheck("Active profile", DoctorStatus.Error, "could not be read", active.Diagnostics));
            return new DoctorReport(checks);
        }
        var enabledCount = profile.EnabledMods.Count;
        checks.Add(new DoctorCheck(
            "Active profile",
            enabledCount == 0 ? DoctorStatus.Warning : DoctorStatus.Ok,
            $"'{active.ProfileName}' — {enabledCount} enabled mod{(enabledCount == 1 ? "" : "s")}",
            enabledCount == 0
                ? [new ManagerDiagnostic(ManagerDiagnosticSeverity.Warning, ManagerDiagnosticCodes.ProfileEmpty, $"Profile '{active.ProfileName}' has no enabled mods.")]
                : []));

        // 3. Enabled mods installed (store-only). Loads each enabled mod in load
        //    order; a missing install is an error, and the loaded set feeds check 4.
        var (loadedMods, missing) = LoadEnabledModsInOrder(layout, profile);
        checks.Add(new DoctorCheck(
            "Enabled mods installed",
            missing.Count > 0 ? DoctorStatus.Error : DoctorStatus.Ok,
            missing.Count > 0 ? $"{missing.Count} enabled mod(s) missing or unreadable" : $"all {loadedMods.Count} present",
            missing));

        // 4. Cross-mod overlay conflicts (store-only — reuses the plan-time detector).
        var conflicts = new CrossModOverlayConflictDetector().Detect(loadedMods);
        checks.Add(new DoctorCheck(
            "Cross-mod overlay conflicts",
            conflicts.Count > 0 ? DoctorStatus.Warning : DoctorStatus.Ok,
            conflicts.Count > 0 ? $"{conflicts.Count} entity(ies) destructively contested" : "none",
            conflicts));

        // 4b. Dependencies & incompatibilities across the enabled set (store-only).
        var installedIds = new HashSet<string>(
            new ModLister().List(layout).Select(m => m.Id), StringComparer.Ordinal);
        var relations = new ModDependencyDetector().Detect(loadedMods, installedIds);
        checks.Add(new DoctorCheck(
            "Dependencies & incompatibilities",
            relations.Count > 0 ? DoctorStatus.Warning : DoctorStatus.Ok,
            relations.Count > 0 ? $"{relations.Count} dependency/incompatibility issue(s)" : "none",
            relations));

        // 5. Orphaned deploys (deploy records whose game root vanished or changed).
        var orphans = new OrphanedDeployFinder().FindAll(layout);
        checks.Add(new DoctorCheck(
            "Orphaned deploys",
            orphans.Count > 0 ? DoctorStatus.Warning : DoctorStatus.Ok,
            orphans.Count > 0 ? $"{orphans.Count} orphan(s) — see 'deploys list-orphans'" : "none",
            orphans.Count > 0
                ? [new ManagerDiagnostic(ManagerDiagnosticSeverity.Warning, ManagerDiagnosticCodes.OrphanedDeploysPresent, $"{orphans.Count} orphaned deploy record(s); run 'pagonia-manager deploys clean' to reclaim space.")]
                : []));

        // 6. Deploy-backup storage (info, with a nag past the dashboard threshold).
        var deployBytes = DeployCleanService.ComputeDeploysSize(layout);
        var overNag = deployBytes >= DeployStorageNagBytes;
        checks.Add(new DoctorCheck(
            "Deploy-backup storage",
            overNag ? DoctorStatus.Warning : DoctorStatus.Ok,
            $"{FormatBytes(deployBytes)}{(overNag ? " — consider 'deploys clean'" : "")}",
            []));

        // 7. Expansion ownership (game-dependent — needs the install on disk).
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            checks.Add(new DoctorCheck("Expansion ownership", DoctorStatus.Skipped, "no game root resolved this run", []));
        }
        else
        {
            var expansions = new ExpansionOwnershipService().List(layout, gameRoot!);
            var hasError = expansions.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);
            checks.Add(new DoctorCheck(
                "Expansion ownership",
                hasError ? DoctorStatus.Error : DoctorStatus.Ok,
                hasError ? "could not resolve" : $"{expansions.Expansions.Count(e => e.Effective)} effective package(s)",
                expansions.Diagnostics));
        }

        // 8. Updates available (opt-in network check). doctor is offline by default, so this is
        //    Skipped unless the caller passed a fetcher (the CLI's --check-updates). Per-item
        //    unreachable repos are surfaced by UpdateDetectionService as warnings, not exceptions;
        //    a wholesale network failure degrades to Skipped rather than failing the roll-up.
        if (updateFetcher is null)
        {
            checks.Add(new DoctorCheck("Updates available", DoctorStatus.Skipped, "offline — pass --check-updates to check", []));
        }
        else
        {
            try
            {
                var updates = new UpdateDetectionService(updateFetcher).Check(layout);
                // Fold same-version content drift into the roll-up too, so doctor agrees with what
                // `outdated` reports (R5-007 — drift was surfaced by exactly one of the three update
                // surfaces). checkFailures are the warnings UpdateDetectionService already emitted, so
                // count them off the diagnostics list rather than re-counting drift there.
                var driftCount = updates.ContentDrifts.Count;
                var total = updates.Updates.Count + updates.CollectionUpdates.Count + driftCount;
                var checkFailures = updates.Diagnostics.Count(d => d.Severity == ManagerDiagnosticSeverity.Warning);

                DoctorStatus status;
                string summary;
                if (total > 0)
                {
                    status = DoctorStatus.Warning;
                    summary = $"{updates.Updates.Count} mod + {updates.CollectionUpdates.Count} collection update(s)"
                        + (driftCount > 0 ? $" + {driftCount} content drift(s)" : "")
                        + " — run 'outdated' to review";
                }
                else if (checkFailures > 0)
                {
                    status = DoctorStatus.Warning;
                    summary = $"couldn't check {checkFailures} item(s) (source unreachable)";
                }
                else
                {
                    status = DoctorStatus.Ok;
                    summary = $"{updates.CheckedCount + updates.CheckedCollectionCount} checked item(s) up to date";
                }

                checks.Add(new DoctorCheck("Updates available", status, summary, updates.Diagnostics));
            }
            catch (Exception ex)
            {
                checks.Add(new DoctorCheck("Updates available", DoctorStatus.Skipped, $"check skipped (network error: {ex.Message})", []));
            }
        }

        return new DoctorReport(checks);
    }

    private static (List<LoadedMod> Loaded, List<ManagerDiagnostic> Missing) LoadEnabledModsInOrder(StoreLayout layout, ProfileFile profile)
    {
        var loaded = new List<LoadedMod>();
        var missing = new List<ManagerDiagnostic>();
        var reader = new ManifestReader();

        // EnabledMods is the authority for "which mods are enabled"; LoadOrder only sets order.
        // Iterate EnabledMods (ordered by LoadOrder position where present) so an enabled mod
        // that's missing from LoadOrder isn't silently skipped from the health roll-up.
        var ordered = profile.EnabledMods.OrderBy(mod =>
        {
            var index = profile.LoadOrder.FindIndex(id => string.Equals(id, mod.Id, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        });

        foreach (var entry in ordered)
        {
            var directory = layout.ModVersionDirectory(entry.Id, entry.Version);
            if (!Directory.Exists(directory))
            {
                missing.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModInstallMissing,
                    $"Mod '{entry.Id}' version '{entry.Version}' is enabled but not installed at '{directory}'."));
                continue;
            }

            var read = reader.ReadMod(directory);
            if (read.Value is not null)
            {
                loaded.Add(read.Value);
            }
            else
            {
                // Installed but unreadable: surface it as an error instead of dropping it,
                // so the roll-up grades Error rather than claiming "all present".
                var detail = read.Diagnostics.FirstOrDefault(d => d.Severity == PatchDiagnosticSeverity.Error)?.Message;
                missing.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModManifestUnreadable,
                    $"Mod '{entry.Id}' version '{entry.Version}' is installed but its manifest could not be read{(string.IsNullOrEmpty(detail) ? "." : $": {detail}")}"));
            }
        }

        return (loaded, missing);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#} {units[unit]}";
    }
}
