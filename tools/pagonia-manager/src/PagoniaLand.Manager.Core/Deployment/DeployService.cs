using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using PagoniaLand.Paker;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public enum DeployOutcome
{
    Failed,
    Completed,
    DryRun,
}

public sealed class DeployResult
{
    public DeployOutcome Outcome { get; init; } = DeployOutcome.Failed;
    public string? GameFingerprint { get; init; }
    public string? Timestamp { get; init; }
    public string? ProfileName { get; init; }
    public int ModifiedFileCount { get; init; }
    public int AddedFileCount { get; init; }

    /// <summary>Paks rebuilt on a live-install deploy. Zero on extracted-layout
    /// deploys (which use <see cref="ModifiedFileCount"/> instead).</summary>
    public int RebuiltPakCount { get; init; }

    public string? ManifestPath { get; init; }
    public string? BackupDirectory { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class DeployService
{
    private readonly PlanProfileService _planService = new();
    private readonly PatchApplier _applier = new();
    private readonly DeployHistoryStore _historyStore = new();
    private readonly StoreStateReader _stateReader = new();
    private readonly StoreStateWriter _stateWriter = new();

    // AOT: DeployService WRITES the deploy manifest via YamlDotNet. RollbackService
    // has the matching DynamicDependency for the READ path; pinning here keeps the
    // write path safe independently of reader-side coverage. Same Shape constant
    // as RollbackService.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(DeployManifest))]
    [DynamicDependency(Shape, typeof(DeployedMod))]
    [DynamicDependency(Shape, typeof(DeployFileEntry))]
    [DynamicDependency(Shape, typeof(DeployAddedFileEntry))]
    [DynamicDependency(Shape, typeof(DeployRebuiltPakEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployedMod>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployFileEntry>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployAddedFileEntry>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<DeployRebuiltPakEntry>))]
    public DeployService()
    {
    }

    public DeployResult Deploy(
        StoreLayout layout,
        string gameRoot,
        string? profileName,
        bool acceptWarnings,
        bool dryRun,
        bool acceptDrift = false,
        IProgress<DeployProgress>? progress = null,
        IReadOnlyDictionary<string, OwnershipState>? assumeOwnership = null)
        => DeployAsync(layout, gameRoot, profileName, acceptWarnings, dryRun, acceptDrift, progress, CancellationToken.None, assumeOwnership)
            .GetAwaiter().GetResult();

    /// <summary>
    /// Async overload of <see cref="Deploy"/> for callers (e.g. a GUI) that must
    /// not block their UI thread on the disk + (cache-warming) network IO a deploy
    /// drives. The synchronous <c>Deploy</c> is a thin wrapper over this. The token
    /// is honoured at each stage boundary up to the commit-point write sequence;
    /// once the backup-and-write phase begins the deploy runs to completion so it
    /// can never leave the game install half-written (deeper mid-apply cancellation
    /// is future work).
    /// </summary>
    public Task<DeployResult> DeployAsync(
        StoreLayout layout,
        string gameRoot,
        string? profileName,
        bool acceptWarnings,
        bool dryRun,
        bool acceptDrift = false,
        IProgress<DeployProgress>? progress = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, OwnershipState>? assumeOwnership = null)
        => Task.Run(
            () => DeployCore(layout, gameRoot, profileName, acceptWarnings, dryRun, acceptDrift, progress, assumeOwnership, cancellationToken),
            cancellationToken);

    private DeployResult DeployCore(
        StoreLayout layout,
        string gameRoot,
        string? profileName,
        bool acceptWarnings,
        bool dryRun,
        bool acceptDrift,
        IProgress<DeployProgress>? progress,
        IReadOnlyDictionary<string, OwnershipState>? assumeOwnership,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Preflight: empty/missing path is its own failure mode — surface
        // gameRootMissing (same diagnostic the planner would emit) rather
        // than the structural gameLayoutUnrecognised which is for valid-
        // but-wrong-contents paths.
        var rootPreflight = new List<ManagerDiagnostic>();
        if (!ServicePreconditions.RequireGameRoot(gameRoot, rootPreflight))
        {
            return new DeployResult { Outcome = DeployOutcome.Failed, Diagnostics = rootPreflight };
        }

        // Layout-aware dispatch: live installs get pak-aware deploy
        // (extract cache, rebuild touched paks, write into <gameRoot>/pak/).
        // Extracted layouts keep the original loose-XML write path. Unrecognised
        // surfaces a clear error rather than the late targetFileMissing chain.
        var detected = GameLayoutDetector.Detect(gameRoot);

        // Resolve the install's expansion ownership (present/owned/effective) from
        // the REAL game root here — before a live-install deploy switches to the
        // extract-cache root — so the ownership gate sees the true on-disk paks,
        // not whatever the cache happens to hold. Threaded into the plan on both
        // paths; the transient --assume-owned/--assume-not-owned overrides ride along.
        var expansions = ExpansionOwnershipService.ResolveForInstall(layout, gameRoot, assumeOwnership);

        switch (detected.Kind)
        {
            case GameLayoutKind.Unrecognised:
                return new DeployResult
                {
                    Outcome = DeployOutcome.Failed,
                    Diagnostics = new List<ManagerDiagnostic>
                    {
                        new(ManagerDiagnosticSeverity.Error,
                            ManagerDiagnosticCodes.GameLayoutUnrecognised,
                            $"'{gameRoot}' is not a recognised Pioneers of Pagonia folder " +
                            $"(expected pak/*.pak or core/gdb/*.gd.xml).")
                    },
                };
            case GameLayoutKind.LiveInstall:
                return DeployToLiveInstall(layout, detected, profileName, acceptWarnings, dryRun, acceptDrift, progress, expansions, cancellationToken);
            case GameLayoutKind.ExtractedLayout:
            default:
                return DeployToExtractedLayout(layout, gameRoot, profileName, acceptWarnings, dryRun, acceptDrift, expansions, cancellationToken);
        }
    }

    private DeployResult DeployToExtractedLayout(
        StoreLayout layout,
        string gameRoot,
        string? profileName,
        bool acceptWarnings,
        bool dryRun,
        bool acceptDrift,
        IReadOnlyList<ExpansionState> expansions,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        // Extracted layouts have no exe → version null → game-vs-mod check degrades
        // to intra-profile only. (A live install routes to DeployToLiveInstall.)
        var extractedGameVersion = GameVersionReader.TryRead(gameRoot, out var extractedVer, out _) ? extractedVer : null;
        var planResult = _planService.Plan(layout, gameRoot, profileName, extractedGameVersion, expansions);
        diagnostics.AddRange(planResult.ManagerDiagnostics);

        if (planResult.PatcherPlan is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByErrors,
                "Deploy aborted: manager-level errors prevented planning. See diagnostics above."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        var patcherPlan = planResult.PatcherPlan;
        var patcherErrors = patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(p => p.Diagnostics))
            .Where(d => d.Severity == PatchDiagnosticSeverity.Error)
            .ToList();

        if (patcherErrors.Count > 0 || patcherPlan.Conflicts.Count > 0 || patcherPlan.EntryConflicts.Count > 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByErrors,
                "Deploy aborted: patcher plan has errors or conflicts. Run 'plan' for details."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        // Ownership advisories (present-but-not-owned / unknown) warn but never
        // block — ownership never gates deployment, only presence does (Phase 9
        // load-bearing rule; lets a non-owner deploy a host's modded co-op set).
        var managerWarnings = planResult.ManagerDiagnostics
            .Any(d => d.Severity == ManagerDiagnosticSeverity.Warning && !ExpansionGate.IsNonBlockingAdvisory(d.Code));
        var patcherWarnings = patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(p => p.Diagnostics))
            .Any(d => d.Severity == PatchDiagnosticSeverity.Warning);

        if ((managerWarnings || patcherWarnings) && !acceptWarnings)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByWarnings,
                "Deploy aborted: plan contains warnings. Pass --accept-warnings to override."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        // An empty profile is only "nothing to apply" when no mod ships a pak: block either.
        // Pattern B paks come from the manifest itself, not from Writes/EntryWrites, so a
        // pak-only mod must still go through the apply path.
        var hasPakMods = patcherPlan.ModPlans.Any(p => p.Mod.Manifest.Pak is not null
            && !string.IsNullOrWhiteSpace(p.Mod.Manifest.Pak.Name));

        if (patcherPlan.Writes.Count == 0 && patcherPlan.EntryWrites.Count == 0 && !hasPakMods)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.DeployEmpty,
                $"Profile '{planResult.ProfileName}' has nothing to apply. Deploy is a noop."));
            return new DeployResult
            {
                Outcome = DeployOutcome.Completed,
                ProfileName = planResult.ProfileName,
                Diagnostics = diagnostics,
            };
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        var timestamp = GenerateTimestamp();

        // Live-state drift preflight: if the previous deploy under this fingerprint
        // wrote files that something else has changed since, don't silently
        // overwrite the foreign change. Blocks unless --force / acceptDrift; dry-run
        // surfaces the drift without blocking.
        if (PreflightLiveStateDrift(layout, gameRoot, fingerprint, acceptDrift, dryRun, diagnostics))
        {
            return new DeployResult
            {
                ProfileName = planResult.ProfileName,
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        // Stage by running PatchApplier into a temp dir; diff against the original to learn
        // exactly which files changed.
        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            $"pagonia-manager-deploy-stage-{Guid.NewGuid():N}");

        try
        {
            var applyDiagnostics = _applier.Apply(gameRoot, stagingRoot, patcherPlan, cancellationToken);
            diagnostics.AddRange(applyDiagnostics.Select(ManagerDiagnostic.From));
            if (applyDiagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.DeployBlockedByErrors,
                    "Deploy aborted: patcher apply produced errors while staging."));
                return new DeployResult
                {
                    ProfileName = planResult.ProfileName,
                    GameFingerprint = fingerprint,
                    Diagnostics = diagnostics,
                };
            }

            var modifiedFiles = ComputeModifiedFiles(gameRoot, stagingRoot);

            // Build Pattern B overlay paks for every mod that declares pak: in mod.yaml.
            // The patcher's PakScaffoldWriter already left the loose-file scaffold under
            // <staging>/<pak.name>/; we just turn that into a real .pak via paker's PakPacker
            // and stage it for deploy to <game>/mods/<pak.name>.pak.
            var pakBuilder = new PakBuilder();
            var addedFiles = new List<DeployAddedFileEntry>();
            var pakDeployments = new List<(string SourceMod, string StagedPakPath, string TargetRelativePath)>();

            foreach (var modPlan in patcherPlan.ModPlans)
            {
                var pak = modPlan.Mod.Manifest.Pak;
                if (pak is null || string.IsNullOrWhiteSpace(pak.Name))
                {
                    continue;
                }

                var pakName = pak.Name.Trim();
                var stagedPakPath = Path.Combine(stagingRoot, GameLayoutConstants.PakStagingFileName(pakName));
                var buildResult = pakBuilder.Build(stagingRoot, pakName, stagedPakPath, cancellationToken);
                diagnostics.AddRange(buildResult.Diagnostics);

                if (!buildResult.Success)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        $"Pattern B pak build for mod '{modPlan.Mod.Manifest.Id}' failed; deploy aborted."));
                    return new DeployResult
                    {
                        ProfileName = planResult.ProfileName,
                        GameFingerprint = fingerprint,
                        Diagnostics = diagnostics,
                    };
                }

                var targetRelative = GameLayoutConstants.PakTargetRelativePath(pakName);
                var targetPath = Path.Combine(gameRoot, GameLayoutConstants.ModsFolderName, $"{pakName}{GameLayoutConstants.PakExtension}");

                // Refuse to overwrite a same-named pak that already exists in <game>/mods/.
                // It could be from a previous unreverted deploy (run rollback first) or from
                // an unrelated mod the user installed by hand — either way, silent overwrite
                // would risk data loss without an entry in our backup.
                if (File.Exists(targetPath))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        $"Pattern B target '{targetPath}' already exists. Run 'rollback' first, or move the existing file aside; deploy refuses to overwrite without a backup entry."));
                    return new DeployResult
                    {
                        ProfileName = planResult.ProfileName,
                        GameFingerprint = fingerprint,
                        Diagnostics = diagnostics,
                    };
                }

                var pakBytes = File.ReadAllBytes(stagedPakPath);
                addedFiles.Add(new DeployAddedFileEntry
                {
                    RelativePath = targetRelative,
                    DeployedSha256 = ComputeSha256(pakBytes),
                    SourceMod = modPlan.Mod.Manifest.Id,
                    ByteSize = buildResult.ByteSize,
                });
                pakDeployments.Add((modPlan.Mod.Manifest.Id, stagedPakPath, targetRelative));
            }

            if (dryRun)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.DeployDryRun,
                    $"Dry-run: would modify {modifiedFiles.Count} file(s) and add {addedFiles.Count} file(s) to '{gameRoot}'. Nothing was written."));
                return new DeployResult
                {
                    Outcome = DeployOutcome.DryRun,
                    GameFingerprint = fingerprint,
                    Timestamp = timestamp,
                    ProfileName = planResult.ProfileName,
                    ModifiedFileCount = modifiedFiles.Count,
                    AddedFileCount = addedFiles.Count,
                    Diagnostics = diagnostics,
                };
            }

            // Last yield point before the commit-point write sequence. Past here the
            // deploy runs to completion regardless of the token, so it can never leave
            // the game install half-written.
            cancellationToken.ThrowIfCancellationRequested();

            var backupDir = layout.DeployBackupDirectory(fingerprint, timestamp);
            Directory.CreateDirectory(backupDir);
            var gameRootFull = Path.GetFullPath(gameRoot);

            // Commit-point ordering — the operations are arranged so that any crash leaves
            // a detectable state, never a silent lie. The hierarchy is:
            //   1. Backup originals     (orphan-safe if next steps crash)
            //   2. Write modified files (per-file atomic via AtomicFile)
            //   3. Write Pattern B paks (per-file atomic)
            //   4. Write the deploy manifest (rollback needs this to find backups + added paks)
            //   5. Append history       (THE commit point; visible in 'status' / 'deploy-status')
            //   6. Update state.lastDeploy
            // A caught write error during steps 2-3 self-heals: the backups from step 1 are
            // restored and any overlay paks removed, so a failed deploy leaves the install in its
            // pre-deploy state. A hard *crash* (process kill / power loss) mid-write can
            // still leave partial state, but with NO history entry — 'status' then reflects the
            // previous deploy, so the user sees the discrepancy rather than a phantom success.

            // 1. Backup originals first. AtomicFile guarantees per-file atomicity.
            foreach (var modified in modifiedFiles)
            {
                var sourcePath = Path.Combine(gameRoot, modified.RelativePath);
                var backupPath = Path.Combine(backupDir, modified.RelativePath);
                AtomicFile.WriteAllBytes(backupPath, File.ReadAllBytes(sourcePath));
            }

            // 2-3. Mutate the live install (modified files, then Pattern B overlay paks). Each
            //    write is per-file atomic, but a failure partway (e.g. a disk-full / permission
            //    error on file K) would otherwise leave the game half-modified with no manifest
            //    or history to roll back. Self-heal: on any write failure, restore every original
            //    from the backups just written in step 1 and remove any overlay paks already
            //    placed, leaving the install in its exact pre-deploy state, then fail cleanly.
            var writtenOverlays = new List<string>();
            try
            {
                foreach (var modified in modifiedFiles)
                {
                    var stagedPath = Path.Combine(stagingRoot, modified.RelativePath);
                    var targetPath = Path.Combine(gameRoot, modified.RelativePath);
                    AtomicFile.WriteAllBytes(targetPath, File.ReadAllBytes(stagedPath));
                }

                foreach (var (_, stagedPakPath, targetRelative) in pakDeployments)
                {
                    var targetPath = Path.Combine(gameRoot, targetRelative.Replace('/', Path.DirectorySeparatorChar));
                    AtomicFile.WriteAllBytes(targetPath, File.ReadAllBytes(stagedPakPath));
                    writtenOverlays.Add(targetPath);
                }
            }
            catch (Exception writeEx) when (writeEx is IOException or UnauthorizedAccessException)
            {
                foreach (var modified in modifiedFiles)
                {
                    var backupPath = Path.Combine(backupDir, modified.RelativePath);
                    var targetPath = Path.Combine(gameRoot, modified.RelativePath);
                    if (File.Exists(backupPath))
                    {
                        try { AtomicFile.WriteAllBytes(targetPath, File.ReadAllBytes(backupPath)); }
                        catch (IOException) { /* best-effort restore */ }
                    }
                }
                foreach (var overlay in writtenOverlays)
                {
                    try { if (File.Exists(overlay)) { File.Delete(overlay); } }
                    catch (IOException) { /* best-effort cleanup */ }
                }
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.DeployMidWriteRolledBack,
                    $"Deploy failed while writing to the game ({writeEx.Message}); the install was restored to its pre-deploy state. Nothing was committed."));
                return new DeployResult
                {
                    ProfileName = planResult.ProfileName,
                    GameFingerprint = fingerprint,
                    BackupDirectory = backupDir,
                    Diagnostics = diagnostics,
                };
            }

            // 4. Write the manifest now that files are in place. Rollback needs this; if it
            //    fails to write the deploy is unrollback-able but the user sees no 'success'
            //    confirmation (we are still ahead of the history-write commit point).
            var manifest = new DeployManifest
            {
                DeployVersion = StoreLayoutConstants.CurrentDeployVersion,
                Timestamp = timestamp,
                GameRoot = gameRootFull,
                GameFingerprint = fingerprint,
                // Extracted layouts have no exe, so this is null in the common
                // case — but read it anyway: a live install with an extra extracted
                // core/gdb/ alongside still routes here and does carry a version.
                GameProductVersion = GameVersionReader.TryRead(gameRootFull, out var extractedVersion, out _)
                    ? extractedVersion
                    : null,
                Profile = planResult.ProfileName ?? string.Empty,
                Mods = patcherPlan.ModPlans
                    .Select(p => new DeployedMod { Id = p.Mod.Manifest.Id, Version = p.Mod.Manifest.Version })
                    .ToList(),
                ModifiedFiles = modifiedFiles,
                AddedFiles = addedFiles,
            };
            var manifestPath = layout.DeployManifestFile(fingerprint, timestamp);
            Directory.CreateDirectory(layout.DeployTimestampDirectory(fingerprint, timestamp));
            AtomicFile.WriteAllText(manifestPath, ManagerYaml.CreateSerializer().Serialize(manifest));

            // 5. Append to history — THE commit point. After this line, 'status' and
            //    'deploy-status' acknowledge the deploy.
            //
            //    Use TryRead instead of Read here: at this point files are already on disk,
            //    manifest is written. A corrupt history.yaml would let the old Read() throw
            //    an unhandled InvalidOperationException out of Deploy(). With TryRead we
            //    surface the corruption as a DeployHistoryUnreadable error and return
            //    Failed — the files + manifest are still on disk and the manifest path is
            //    in the result so the user can recover manually (delete the broken
            //    history.yaml and re-run, or hand-edit to add the new entry).
            if (!_historyStore.TryRead(layout, fingerprint, out var history, out var historyError))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.DeployHistoryUnreadable,
                    historyError + " Files have been written to the game and the manifest was saved, but the history could not be updated. Restore or remove the corrupt history.yaml and rerun deploy/rollback."));
                return new DeployResult
                {
                    ProfileName = planResult.ProfileName,
                    GameFingerprint = fingerprint,
                    Timestamp = timestamp,
                    ModifiedFileCount = modifiedFiles.Count,
                    AddedFileCount = addedFiles.Count,
                    ManifestPath = manifestPath,
                    BackupDirectory = backupDir,
                    Diagnostics = diagnostics,
                };
            }
            var updatedHistory = new DeployHistory
            {
                DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
                GameFingerprint = fingerprint,
                GameRoot = gameRootFull,
                Deploys = new List<DeployHistoryEntry>
                {
                    new()
                    {
                        Timestamp = timestamp,
                        Profile = planResult.ProfileName ?? string.Empty,
                        ModCount = manifest.Mods.Count,
                        FileCount = manifest.ModifiedFiles.Count + manifest.AddedFiles.Count,
                    },
                }.Concat(history.Deploys).ToList(),
            };
            _historyStore.Write(layout, fingerprint, updatedHistory);

            // 6. Stamp the active store state with this deploy as the last successful one.
            //    StoreState.LastDeploy was schema'd and tested but never populated in
            //    practice. Read-modify-write so we don't clobber ActiveProfile or
            //    StoreVersion.
            if (_stateReader.Exists(layout))
            {
                var currentState = _stateReader.Read(layout);
                _stateWriter.Write(layout, new StoreState
                {
                    StoreVersion = currentState.StoreVersion,
                    ActiveProfile = currentState.ActiveProfile,
                    LastDeploy = new StoreLastDeploy
                    {
                        Timestamp = timestamp,
                        GameRoot = gameRootFull,
                        Profile = planResult.ProfileName ?? string.Empty,
                    },
                    DefaultGameRoot = currentState.DefaultGameRoot,
                    SubscribedCatalogs = currentState.SubscribedCatalogs,
                    CatalogMaxDepth = currentState.CatalogMaxDepth,
                    AllowInsecureSources = currentState.AllowInsecureSources,
                    CatalogCacheStalenessHours = currentState.CatalogCacheStalenessHours,
                    AllowInsecureCatalogSources = currentState.AllowInsecureCatalogSources,
                    Installs = currentState.Installs,
                });
            }

            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.DeployCompleted,
                $"Deployed {modifiedFiles.Count} modified + {addedFiles.Count} added file(s) from profile '{planResult.ProfileName}'."));

            return new DeployResult
            {
                Outcome = DeployOutcome.Completed,
                GameFingerprint = fingerprint,
                Timestamp = timestamp,
                ProfileName = planResult.ProfileName,
                ModifiedFileCount = modifiedFiles.Count,
                AddedFileCount = addedFiles.Count,
                ManifestPath = manifestPath,
                BackupDirectory = backupDir,
                Diagnostics = diagnostics,
            };
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                try { Directory.Delete(stagingRoot, recursive: true); }
                catch (IOException) { }
            }
        }
    }

    private DeployResult DeployToLiveInstall(
        StoreLayout layout,
        GameLayout detected,
        string? profileName,
        bool acceptWarnings,
        bool dryRun,
        bool acceptDrift,
        IProgress<DeployProgress>? progress,
        IReadOnlyList<ExpansionState> expansions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<ManagerDiagnostic>();
        var gameRoot = detected.Root;

        // Stage 1 — ensure the pak cache is warm so the patcher resolver has
        // XML files to look at. Subsequent calls within the same install
        // fingerprint short-circuit, so the repeat cost is negligible.
        progress?.Report(new DeployProgress("extract", null, "Ensuring pak extract cache"));
        // pass the active profile's required pak basenames
        // so the cache only extracts what the mods actually touch. Saves ~3× on
        // first-plan latency when the user has only core.pak-touching mods.
        var requiredPaks = PakRequirementAnalyzer.ComputeRequiredPaks(layout, profileName);
        var cacheResult = new PakCacheService().Ensure(layout, detected, requiredPaks, progress);
        diagnostics.AddRange(cacheResult.Diagnostics);
        if (!cacheResult.Success)
        {
            return new DeployResult { Diagnostics = diagnostics };
        }
        var cacheRoot = cacheResult.CacheRoot;

        // Stage 2 — plan against the cache. Pass the install's real exe version
        // (the cache root has no exe) so the plan can run the game-vs-mod check.
        progress?.Report(new DeployProgress("plan", null, "Planning patches"));
        var planResult = _planService.Plan(layout, cacheRoot, profileName, detected.GameProductVersion, expansions);
        diagnostics.AddRange(planResult.ManagerDiagnostics);
        if (planResult.PatcherPlan is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByErrors,
                "Deploy aborted: manager-level errors prevented planning. See diagnostics above."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        var patcherPlan = planResult.PatcherPlan;
        var patcherErrors = patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(p => p.Diagnostics))
            .Where(d => d.Severity == PatchDiagnosticSeverity.Error)
            .ToList();

        if (patcherErrors.Count > 0 || patcherPlan.Conflicts.Count > 0 || patcherPlan.EntryConflicts.Count > 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByErrors,
                "Deploy aborted: patcher plan has errors or conflicts. Run 'plan' for details."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        // Ownership advisories warn but never block deployment (Phase 9 rule) — see
        // the extracted-layout path for the rationale.
        var managerWarnings = planResult.ManagerDiagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Warning && !ExpansionGate.IsNonBlockingAdvisory(d.Code));
        var patcherWarnings = patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(p => p.Diagnostics))
            .Any(d => d.Severity == PatchDiagnosticSeverity.Warning);
        if ((managerWarnings || patcherWarnings) && !acceptWarnings)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployBlockedByWarnings,
                "Deploy aborted: warnings present. Re-run with --accept-warnings to proceed anyway."));
            return new DeployResult { ProfileName = planResult.ProfileName, Diagnostics = diagnostics };
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        var timestamp = GenerateTimestamp();

        // game-update awareness. If the user previously
        // deployed to this same gameRoot path but with a different fingerprint,
        // Steam (or some other game-update mechanism) has touched the install
        // since then. The backups under <store>/deploys/<old-fp>/ are pre-update
        // bytes — rollback would put them over a post-update install. Warn the
        // user; don't block the deploy itself, since the NEW deploy under the
        // current fingerprint is perfectly safe.
        if (new OrphanedDeployFinder().AnyPriorDeployForGameRootHasDifferentFingerprint(
                layout, gameRoot, fingerprint, out var priorFingerprint, out var priorVersion))
        {
            // Name the versions when both the prior manifest and the current
            // install expose a ProductVersion; otherwise fall back to the
            // opaque-fingerprint wording (older deploys never recorded a version,
            // and the exe may be stripped/absent on this install).
            var currentVersion = detected.GameProductVersion;
            var staleBackupNote =
                $"Older backups under <store>/deploys/{priorFingerprint}/ apply to the pre-update version and will not restore cleanly over the current install. Run 'pagonia-manager deploys list-orphans' to inspect.";
            var message = (!string.IsNullOrWhiteSpace(priorVersion) && !string.IsNullOrWhiteSpace(currentVersion))
                ? $"Pioneers of Pagonia updated from v{priorVersion} to v{currentVersion} since the last deploy. {staleBackupNote}"
                : $"This install previously had a deploy under fingerprint '{priorFingerprint}' but its fingerprint is now '{fingerprint}' — most likely a Pioneers of Pagonia update. {staleBackupNote}";
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.GameUpdatedSinceLastDeploy,
                message));
        }

        // Live-state drift preflight (see DeployToExtractedLayout for the rationale).
        // Runs before the expensive rebuild so a block costs nothing.
        if (PreflightLiveStateDrift(layout, gameRoot, fingerprint, acceptDrift, dryRun, diagnostics))
        {
            return new DeployResult
            {
                ProfileName = planResult.ProfileName,
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        var stagingRoot = Path.Combine(
            Path.GetTempPath(),
            $"pagonia-manager-deploy-stage-{Guid.NewGuid():N}");

        try
        {
            // Stage 3 — apply patches. The sparse fast-path: when no mod uses
            // entries: ops or pak: blocks, we skip PatchApplier.CopyGameRoot
            // entirely and run ApplySparse instead, which returns just the
            // patched files as in-memory bytes.
            // Eliminates the ~hundreds-of-MBs disk-IO that the slow-path's
            // mirror-then-diff approach imposes on every live-install deploy.
            var canUseSparse = patcherPlan.EntryWrites.Count == 0
                && patcherPlan.ModPlans.All(m => m.Mod.Manifest.Pak is null);

            // Per-file modifications keyed by relative path. Loaded into bytes
            // for both paths so the per-pak rebuild downstream uses a single
            // unified code path (PakRebuilder's byte-array overload).
            Dictionary<string, byte[]> changedBytes;
            List<DeployFileEntry> modifiedFiles;

            if (canUseSparse)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.DeployUsedSparsePath,
                    "Sparse-apply fast path — no staging tree, no full-cache copy."));
                progress?.Report(new DeployProgress("apply", null, "Applying patches in memory"));
                var sparseResult = _applier.ApplySparse(cacheRoot, patcherPlan, cancellationToken);
                diagnostics.AddRange(sparseResult.Diagnostics.Select(ManagerDiagnostic.From));
                if (!sparseResult.Success)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        "Deploy aborted: patcher apply produced errors while applying in memory."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }
                changedBytes = new Dictionary<string, byte[]>(sparseResult.ChangedFiles, StringComparer.OrdinalIgnoreCase);

                // Build modifiedFiles from in-memory bytes — no staging tree
                // walk, no second-pass file enumeration. Skip files where the
                // patcher produced bytes identical to the source (same skip
                // semantics ComputeModifiedFiles applies in the slow path).
                modifiedFiles = new List<DeployFileEntry>();
                foreach (var (relativePath, newBytes) in changedBytes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    // Re-reading the cached original here (rather than threading the exact
                    // pre-patch bytes out of ApplySparse) is safe only because the pak cache is
                    // immutable for the duration of a single deploy: deploy runs single-threaded
                    // and PakCacheService self-heals/extracts before the patch phase, never during
                    // it. If a concurrent cache writer is ever introduced, carry the originals
                    // through SparseApplyResult instead so this can't read post-mutation bytes.
                    var originalPath = Path.Combine(cacheRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var originalBytes = File.ReadAllBytes(originalPath);
                    if (originalBytes.AsSpan().SequenceEqual(newBytes)) continue;
                    modifiedFiles.Add(new DeployFileEntry
                    {
                        RelativePath = relativePath,
                        OriginalSha256 = ComputeSha256(originalBytes),
                        DeployedSha256 = ComputeSha256(newBytes),
                    });
                }
            }
            else
            {
                var reason = patcherPlan.EntryWrites.Count > 0
                    ? "entry-level operations present"
                    : "Pattern B pak block detected";
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.DeployFellBackToFullApply,
                    $"Full-apply slow path with disk staging (reason: {reason})."));
                progress?.Report(new DeployProgress("apply", null, "Applying patches to staging"));
                var applyDiagnostics = _applier.Apply(cacheRoot, stagingRoot, patcherPlan, cancellationToken);
                diagnostics.AddRange(applyDiagnostics.Select(ManagerDiagnostic.From));
                if (applyDiagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        "Deploy aborted: patcher apply produced errors while staging."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }

                modifiedFiles = ComputeModifiedFiles(cacheRoot, stagingRoot);

                // Load the staged bytes into memory so the per-pak rebuild
                // downstream uses the same byte-overload as the sparse path.
                // Typical pak rebuild touches a handful of small XMLs; the
                // transient memory cost is in the low-MB range.
                changedBytes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var modified in modifiedFiles)
                {
                    var stagedPath = Path.Combine(stagingRoot, modified.RelativePath);
                    changedBytes[modified.RelativePath] = File.ReadAllBytes(stagedPath);
                }
            }

            // Stage 4 — Pattern B overlay paks (unchanged from the extracted-
            // layout path; they still land in <gameRoot>/mods/<pakname>.pak).
            var pakBuilder = new PakBuilder();
            var addedFiles = new List<DeployAddedFileEntry>();
            var pakDeployments = new List<(string SourceMod, string StagedPakPath, string TargetRelativePath)>();

            foreach (var modPlan in patcherPlan.ModPlans)
            {
                var pak = modPlan.Mod.Manifest.Pak;
                if (pak is null || string.IsNullOrWhiteSpace(pak.Name)) continue;

                var pakName = pak.Name.Trim();
                var stagedPakPath = Path.Combine(stagingRoot, GameLayoutConstants.PakStagingFileName(pakName));
                var buildResult = pakBuilder.Build(stagingRoot, pakName, stagedPakPath, cancellationToken);
                diagnostics.AddRange(buildResult.Diagnostics);

                if (!buildResult.Success)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        $"Pattern B pak build for mod '{modPlan.Mod.Manifest.Id}' failed; deploy aborted."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }

                var targetRelative = GameLayoutConstants.PakTargetRelativePath(pakName);
                var targetPath = Path.Combine(gameRoot, GameLayoutConstants.ModsFolderName, $"{pakName}{GameLayoutConstants.PakExtension}");
                if (File.Exists(targetPath))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DeployBlockedByErrors,
                        $"Pattern B target '{targetPath}' already exists. Run 'rollback' first, or move the existing file aside; deploy refuses to overwrite without a backup entry."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }

                addedFiles.Add(new DeployAddedFileEntry
                {
                    RelativePath = targetRelative,
                    DeployedSha256 = ComputeFileSha256(stagedPakPath),
                    SourceMod = modPlan.Mod.Manifest.Id,
                    ByteSize = buildResult.ByteSize,
                });
                pakDeployments.Add((modPlan.Mod.Manifest.Id, stagedPakPath, targetRelative));
            }

            // Stage 5 — group modified files by their owning canonical pak.
            // We read each discovered pak's index once, build a reverse map
            // entry-name -> pak path. A modified file with no owning pak is
            // suspicious (cache out of sync?) and aborts the deploy.
            var ownerByEntry = new Dictionary<string, string>(StringComparer.Ordinal);
            var reader = new PakReader();
            foreach (var pakPath in detected.DiscoveredPaks)
            {
                FileStream pakStream;
                try
                {
                    // DiscoveredPaks was snapshotted at Detect time; a pak removed or
                    // locked since then must surface as a clean diagnostic, not a crash.
                    pakStream = File.OpenRead(pakPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.PakRebuildFailed,
                        $"Could not open '{Path.GetFileName(pakPath)}' while building owner map: {exception.Message}."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }

                using (pakStream)
                {
                var openResult = reader.OpenIndex(pakStream);
                if (!openResult.Success || openResult.Index is null)
                {
                    diagnostics.AddRange(openResult.Diagnostics.Select(ManagerDiagnostic.From));
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.PakRebuildFailed,
                        $"Could not read index of '{Path.GetFileName(pakPath)}' while building owner map."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }
                foreach (var entry in openResult.Index.Entries)
                {
                    // First pak wins — paks aren't supposed to overlap, but a duplicate
                    // would mean two source paks contain the same path; we'd patch the
                    // first one we discover. Surface it (when the owners actually differ).
                    if (!ownerByEntry.TryAdd(entry.Filename, pakPath)
                        && !string.Equals(ownerByEntry[entry.Filename], pakPath, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new ManagerDiagnostic(
                            ManagerDiagnosticSeverity.Warning,
                            ManagerDiagnosticCodes.DuplicatePakEntryOwner,
                            $"Entry '{entry.Filename}' appears in more than one source pak ('{Path.GetFileName(ownerByEntry[entry.Filename])}' and '{Path.GetFileName(pakPath)}'); patching the first."));
                    }
                }
                }
            }

            // Bucket modified files by owning pak. Track contributing mods per pak
            // by joining with the patcher plan's mod->writes mapping. Per-pak
            // values are the in-memory bytes (sparse path) or staging-loaded
            // bytes (slow path) — uniform now so PakRebuilder's byte-overload
            // does the work for both.
            var modifiedByPak = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.Ordinal);
            foreach (var modified in modifiedFiles)
            {
                if (!ownerByEntry.TryGetValue(modified.RelativePath, out var owningPak))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.ModifiedFileMissingOwningPak,
                        $"Modified file '{modified.RelativePath}' isn't in any discovered pak — cache may be stale. Re-run plan after refreshing the cache."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }
                if (!modifiedByPak.TryGetValue(owningPak, out var perPak))
                {
                    perPak = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                    modifiedByPak[owningPak] = perPak;
                }
                perPak[modified.RelativePath] = changedBytes[modified.RelativePath];
            }

            if (dryRun)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.DeployDryRun,
                    $"Dry-run: would rebuild {modifiedByPak.Count} pak(s) and add {addedFiles.Count} overlay pak(s) to '{gameRoot}'. Nothing was written."));
                return new DeployResult
                {
                    Outcome = DeployOutcome.DryRun,
                    GameFingerprint = fingerprint,
                    Timestamp = timestamp,
                    ProfileName = planResult.ProfileName,
                    ModifiedFileCount = modifiedFiles.Count,
                    AddedFileCount = addedFiles.Count,
                    RebuiltPakCount = modifiedByPak.Count,
                    Diagnostics = diagnostics,
                };
            }

            // Last yield point before the commit-point write sequence (backup +
            // rebuild). Past here the deploy runs to completion regardless of the
            // token, so it can never leave the game install half-written.
            cancellationToken.ThrowIfCancellationRequested();

            var backupDir = layout.DeployBackupDirectory(fingerprint, timestamp);
            Directory.CreateDirectory(backupDir);
            var gameRootFull = Path.GetFullPath(gameRoot);

            // Same commit-point ordering as the extracted-layout deploy:
            //   1. Backup originals (pak files this time, not loose XMLs)
            //   2. Write rebuilt paks into <game>/pak/
            //   3. Write Pattern B overlay paks
            //   4. Manifest
            //   5. History (commit point)
            //   6. state.lastDeploy

            // 1 + 2. Rebuild each affected pak. PakRebuilder streams both the
            //        original and the output so multi-GB paks don't trip the
            //        .NET 2 GB single-array limit. Backups use AtomicFile.CopyAtomic
            //        (also streaming) for the same reason.
            var rebuiltEntries = new List<DeployRebuiltPakEntry>(modifiedByPak.Count);
            var rebuilder = new PakRebuilder();
            var pakIndex = 0;
            var pakCount = modifiedByPak.Count;
            foreach (var (pakPath, replacements) in modifiedByPak.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                pakIndex++;
                var pakName = Path.GetFileName(pakPath);
                var backupRel = $"{GameLayoutConstants.PakFolderName}/{pakName}";
                var backupPath = Path.Combine(backupDir, GameLayoutConstants.PakFolderName, pakName);
                // Backup + rebuild share the "repack" stage — keeping them under one
                // stage label means the percent stays monotonic across paks (a
                // separate "backup" stage would force the stage to bounce
                // backup→repack→backup… on a multi-pak deploy).
                progress?.Report(new DeployProgress("repack", pakIndex * 100 / pakCount, $"Backing up {pakName} ({pakIndex}/{pakCount})"));
                AtomicFile.CopyAtomic(pakPath, backupPath);

                progress?.Report(new DeployProgress("repack", pakIndex * 100 / pakCount, $"Rebuilding {pakName} ({pakIndex}/{pakCount})"));
                var rebuild = rebuilder.Rebuild(pakPath, pakPath, replacements);
                diagnostics.AddRange(rebuild.Diagnostics.Select(ManagerDiagnostic.From));
                if (!rebuild.Success)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.PakRebuildFailed,
                        $"Rebuilding '{pakName}' failed; deploy aborted. Backup at '{backupPath}' — restore it manually if the live pak is corrupted."));
                    return new DeployResult { ProfileName = planResult.ProfileName, GameFingerprint = fingerprint, Diagnostics = diagnostics };
                }

                var contributingMods = patcherPlan.ModPlans
                    .Where(p => p.Writes.Any(w => replacements.ContainsKey(NormalizeRelative(w.File))))
                    .Select(p => p.Mod.Manifest.Id)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();

                rebuiltEntries.Add(new DeployRebuiltPakEntry
                {
                    PakName = pakName,
                    TargetRelativePath = $"{GameLayoutConstants.PakFolderName}/{pakName}",
                    BackupRelativePath = backupRel,
                    OriginalSha256 = rebuild.OriginalSha256,
                    NewSha256 = rebuild.NewSha256,
                    ByteSizeBefore = rebuild.OriginalByteSize,
                    ByteSizeAfter = rebuild.NewByteSize,
                    ContributingMods = contributingMods,
                });

                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.PakRebuilt,
                    $"Rebuilt '{pakName}' ({rebuild.EntriesReplaced}/{rebuild.EntriesTotal} entries replaced, {rebuild.OriginalByteSize} -> {rebuild.NewByteSize} bytes)."));
            }

            // 3. Pattern B overlay paks. CopyAtomic streams to avoid loading the
            //    whole staged pak into memory, mirroring the canonical-pak write path.
            foreach (var (_, stagedPakPath, targetRelative) in pakDeployments)
            {
                var targetPath = Path.Combine(gameRoot, targetRelative.Replace('/', Path.DirectorySeparatorChar));
                AtomicFile.CopyAtomic(stagedPakPath, targetPath);
            }

            // 4. Manifest. RebuiltPaks is populated; ModifiedFiles stays empty
            //    (live-install deploys don't write loose XMLs into the game root).
            var manifest = new DeployManifest
            {
                DeployVersion = StoreLayoutConstants.CurrentDeployVersion,
                Timestamp = timestamp,
                GameRoot = gameRootFull,
                GameFingerprint = fingerprint,
                // The version detection already read off this live install.
                GameProductVersion = detected.GameProductVersion,
                Profile = planResult.ProfileName ?? string.Empty,
                Mods = patcherPlan.ModPlans
                    .Select(p => new DeployedMod { Id = p.Mod.Manifest.Id, Version = p.Mod.Manifest.Version })
                    .ToList(),
                ModifiedFiles = new List<DeployFileEntry>(),
                AddedFiles = addedFiles,
                RebuiltPaks = rebuiltEntries,
            };
            var manifestPath = layout.DeployManifestFile(fingerprint, timestamp);
            Directory.CreateDirectory(layout.DeployTimestampDirectory(fingerprint, timestamp));
            AtomicFile.WriteAllText(manifestPath, ManagerYaml.CreateSerializer().Serialize(manifest));

            // 5. History — commit point.
            if (!_historyStore.TryRead(layout, fingerprint, out var history, out var historyError))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.DeployHistoryUnreadable,
                    historyError + " Paks have been rebuilt and the manifest was saved, but the history could not be updated. Restore or remove the corrupt history.yaml and rerun deploy/rollback."));
                return new DeployResult
                {
                    ProfileName = planResult.ProfileName,
                    GameFingerprint = fingerprint,
                    Timestamp = timestamp,
                    ModifiedFileCount = modifiedFiles.Count,
                    AddedFileCount = addedFiles.Count,
                    RebuiltPakCount = rebuiltEntries.Count,
                    ManifestPath = manifestPath,
                    BackupDirectory = backupDir,
                    Diagnostics = diagnostics,
                };
            }
            var updatedHistory = new DeployHistory
            {
                DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
                GameFingerprint = fingerprint,
                GameRoot = gameRootFull,
                Deploys = new List<DeployHistoryEntry>
                {
                    new()
                    {
                        Timestamp = timestamp,
                        Profile = planResult.ProfileName ?? string.Empty,
                        ModCount = manifest.Mods.Count,
                        // FileCount summarises what changed on disk so 'deploy-status' can
                        // show a single number — for live installs that's paks + overlays.
                        FileCount = rebuiltEntries.Count + addedFiles.Count,
                    },
                }.Concat(history.Deploys).ToList(),
            };
            _historyStore.Write(layout, fingerprint, updatedHistory);

            // 6. state.lastDeploy.
            if (_stateReader.Exists(layout))
            {
                var currentState = _stateReader.Read(layout);
                _stateWriter.Write(layout, new StoreState
                {
                    StoreVersion = currentState.StoreVersion,
                    ActiveProfile = currentState.ActiveProfile,
                    LastDeploy = new StoreLastDeploy
                    {
                        Timestamp = timestamp,
                        GameRoot = gameRootFull,
                        Profile = planResult.ProfileName ?? string.Empty,
                    },
                    DefaultGameRoot = currentState.DefaultGameRoot,
                    SubscribedCatalogs = currentState.SubscribedCatalogs,
                    CatalogMaxDepth = currentState.CatalogMaxDepth,
                    AllowInsecureSources = currentState.AllowInsecureSources,
                    CatalogCacheStalenessHours = currentState.CatalogCacheStalenessHours,
                    AllowInsecureCatalogSources = currentState.AllowInsecureCatalogSources,
                    Installs = currentState.Installs,
                });
            }

            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.DeployCompleted,
                $"Deployed {rebuiltEntries.Count} rebuilt pak(s) + {addedFiles.Count} overlay pak(s) from profile '{planResult.ProfileName}'."));

            return new DeployResult
            {
                Outcome = DeployOutcome.Completed,
                GameFingerprint = fingerprint,
                Timestamp = timestamp,
                ProfileName = planResult.ProfileName,
                ModifiedFileCount = modifiedFiles.Count,
                AddedFileCount = addedFiles.Count,
                RebuiltPakCount = rebuiltEntries.Count,
                ManifestPath = manifestPath,
                BackupDirectory = backupDir,
                Diagnostics = diagnostics,
            };
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                try { Directory.Delete(stagingRoot, recursive: true); }
                catch (IOException) { }
            }
        }
    }

    private static string NormalizeRelative(string path)
        => path.Replace('\\', '/');

    private static List<DeployFileEntry> ComputeModifiedFiles(string gameRoot, string stagingRoot)
    {
        var result = new List<DeployFileEntry>();

        foreach (var stagedFile in Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(stagingRoot, stagedFile);
            var originalPath = Path.Combine(gameRoot, relativePath);

            // Modified-files diff covers only XML files within the original game-gdb tree.
            // Pak scaffolds (which PatchApplier writes under stagingRoot/<modId>/) take the
            // separate addedFiles path below — they're packed by PakBuilder and copied to
            // <game>/mods/, with no backup to restore on rollback.
            if (!File.Exists(originalPath))
            {
                continue;
            }

            var originalBytes = File.ReadAllBytes(originalPath);
            var stagedBytes = File.ReadAllBytes(stagedFile);

            if (originalBytes.AsSpan().SequenceEqual(stagedBytes))
            {
                continue;
            }

            result.Add(new DeployFileEntry
            {
                RelativePath = relativePath.Replace('\\', '/'),
                OriginalSha256 = ComputeSha256(originalBytes),
                DeployedSha256 = ComputeSha256(stagedBytes),
            });
        }

        return result;
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Run the live-state drift check against the previous deploy under this
    /// fingerprint (if any) and fold the result into <paramref name="diagnostics"/>.
    /// Returns <c>true</c> when the caller must abort the deploy: drift was found
    /// and neither <paramref name="acceptDrift"/> nor <paramref name="dryRun"/> is
    /// set. Drift is always surfaced as <c>manager.liveStateDrift</c> warnings;
    /// the block adds a <c>manager.deployBlockedByDrift</c> error on top.
    /// </summary>
    private bool PreflightLiveStateDrift(
        StoreLayout layout,
        string gameRoot,
        string fingerprint,
        bool acceptDrift,
        bool dryRun,
        List<ManagerDiagnostic> diagnostics)
    {
        if (!TryReadLatestManifest(layout, fingerprint, out var priorManifest))
        {
            return false;
        }

        var drifts = new LiveStateInspector().Inspect(gameRoot, priorManifest);
        if (drifts.Count == 0)
        {
            return false;
        }

        foreach (var drift in drifts)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.LiveStateDrift,
                DescribeDrift(drift) + " — another tool or a hand-edit changed it since the last deploy.",
                drift.RelativePath));
        }

        // Surfaced but not blocking: the user asked to overwrite (--force), or this
        // is a dry-run that writes nothing anyway.
        if (acceptDrift || dryRun)
        {
            return false;
        }

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Error,
            ManagerDiagnosticCodes.DeployBlockedByDrift,
            $"Deploy aborted: {drifts.Count} live file(s) changed since the last deploy (listed above). " +
            "Re-run with --force to overwrite the foreign change(s), or 'rollback' first to inspect them."));
        return true;
    }

    /// <summary>Short "<path> (expected ab12…, now cd34… / missing)" descriptor for a drift.</summary>
    private static string DescribeDrift(LiveStateDrift drift)
    {
        var actual = drift.ActualSha256 is null ? "missing" : Short(drift.ActualSha256);
        return $"'{drift.RelativePath}' (expected {Short(drift.ExpectedSha256)}, now {actual})";

        static string Short(string sha) => sha.Length <= 12 ? sha : sha[..12] + "…";
    }

    /// <summary>Best-effort read of the most recent deploy manifest recorded for
    /// <paramref name="fingerprint"/>. Returns false when there is no prior deploy,
    /// the manifest file is gone, or it can't be parsed.</summary>
    private bool TryReadLatestManifest(StoreLayout layout, string fingerprint, out DeployManifest manifest)
    {
        manifest = null!;
        if (!_historyStore.TryRead(layout, fingerprint, out var history, out _)) return false;
        if (history.Deploys.Count == 0) return false;

        var manifestPath = layout.DeployManifestFile(fingerprint, history.Deploys[0].Timestamp);
        if (!File.Exists(manifestPath)) return false;

        try
        {
            var parsed = ManagerYaml.CreateDeserializer().Deserialize<DeployManifest>(File.ReadAllText(manifestPath));
            if (parsed is null) return false;
            manifest = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Streaming SHA-256 — used on pak-sized files (potentially > 2 GB)
    /// where loading into a byte array would throw IOException.</summary>
    private static string ComputeFileSha256(string path) => FileHashing.ComputeFileSha256(path);

    private static string GenerateTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        return now.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture);
    }
}
