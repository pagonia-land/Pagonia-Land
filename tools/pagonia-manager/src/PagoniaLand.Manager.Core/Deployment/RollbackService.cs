using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public enum RollbackOutcome
{
    Failed,
    Reverted,
    NothingToRollback,
}

public sealed class RollbackResult
{
    public RollbackOutcome Outcome { get; init; } = RollbackOutcome.Failed;
    public string? GameFingerprint { get; init; }
    public string? RevertedTimestamp { get; init; }
    public string? RevertedProfile { get; init; }
    public int RestoredFileCount { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class RollbackService
{
    private readonly DeployHistoryStore _historyStore = new();

    // AOT: deserializing the deploy manifest needs YamlDotNet reflection over these types.
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
    public RollbackService()
    {
    }

    public RollbackResult Rollback(StoreLayout layout, string gameRoot, bool acceptDrift = false, IProgress<DeployProgress>? progress = null)
        => RollbackAsync(layout, gameRoot, acceptDrift, progress, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Async overload of <see cref="Rollback"/> for callers (e.g. a GUI) that must
    /// not block their thread on disk IO. The synchronous <c>Rollback</c> is a thin
    /// wrapper over this. The token is honoured at the orchestration boundary
    /// (before any file is restored); the inner restore loop stays uninterruptible
    /// for now.
    /// </summary>
    public Task<RollbackResult> RollbackAsync(StoreLayout layout, string gameRoot, bool acceptDrift = false, IProgress<DeployProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => RollbackCore(layout, gameRoot, acceptDrift, progress, cancellationToken), cancellationToken);

    private RollbackResult RollbackCore(StoreLayout layout, string gameRoot, bool acceptDrift, IProgress<DeployProgress>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireGameRoot(gameRoot, diagnostics))
        {
            return new RollbackResult { Diagnostics = diagnostics };
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);

        if (!_historyStore.Exists(layout, fingerprint))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.RollbackNothingToRollback,
                $"No prior deploys recorded for game root '{gameRoot}' (fingerprint '{fingerprint}')."));
            return new RollbackResult
            {
                Outcome = RollbackOutcome.NothingToRollback,
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        if (!_historyStore.TryRead(layout, fingerprint, out var history, out var historyError))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployHistoryUnreadable,
                historyError));
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        if (history.Deploys.Count == 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.RollbackNothingToRollback,
                $"Deploy history exists but is empty for fingerprint '{fingerprint}'."));
            return new RollbackResult
            {
                Outcome = RollbackOutcome.NothingToRollback,
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        var latest = history.Deploys[0];
        var manifestPath = layout.DeployManifestFile(fingerprint, latest.Timestamp);
        if (!File.Exists(manifestPath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployHistoryUnreadable,
                $"Deploy history references '{latest.Timestamp}' but its manifest file is missing."));
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        DeployManifest? manifest;
        try
        {
            var manifestYaml = File.ReadAllText(manifestPath);
            manifest = ManagerYaml.CreateDeserializer().Deserialize<DeployManifest>(manifestYaml);
        }
        catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or IOException)
        {
            // A corrupt or partially-written manifest must not crash the rollback task;
            // surface it as a clean Failed outcome (mirrors DeployHistoryStore.TryRead).
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployHistoryUnreadable,
                $"Deploy manifest at '{manifestPath}' could not be read: {exception.Message}"));
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        if (manifest is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployHistoryUnreadable,
                $"Deploy manifest at '{manifestPath}' is empty or invalid."));
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        // Live-state drift preflight. The existing rollbackHashMismatch check below
        // verifies the *backup* is intact; this verifies the *live target* is still
        // what we deployed. If a later hand-edit / another tool changed it, restoring
        // the backup would silently discard that change — refuse unless --force. Done
        // up front (before any write) so a refusal leaves the install fully untouched.
        var liveDrifts = new LiveStateInspector().Inspect(gameRoot, manifest);
        if (liveDrifts.Count > 0)
        {
            foreach (var drift in liveDrifts)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.LiveStateDrift,
                    $"'{drift.RelativePath}' changed since deploy '{latest.Timestamp}' — restoring the backup would discard that change.",
                    drift.RelativePath));
            }

            if (!acceptDrift)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.RollbackBlockedByDrift,
                    $"Rollback refused: {liveDrifts.Count} live file(s) changed since deploy '{latest.Timestamp}'. " +
                    "Re-run with --force to overwrite them, or inspect them first; nothing was changed."));
                return new RollbackResult
                {
                    Outcome = RollbackOutcome.Failed,
                    GameFingerprint = fingerprint,
                    Diagnostics = diagnostics,
                };
            }
        }

        // Last yield point before the first restore write. A cancel here leaves the
        // install fully untouched (the drift preflight above wrote nothing).
        cancellationToken.ThrowIfCancellationRequested();

        var backupDir = layout.DeployBackupDirectory(fingerprint, latest.Timestamp);
        var restored = 0;

        // Dispatch on which list the manifest populated:
        //   - RebuiltPaks  -> live-install deploy, restore whole pak files
        //   - ModifiedFiles -> extracted-layout deploy, restore loose XMLs
        // Pattern B AddedFiles below run in both modes (they're always
        // <gameRoot>/mods/*.pak overlay paks that need deletion).
        if (manifest.RebuiltPaks.Count > 0)
        {
            var pakIndex = 0;
            var pakTotal = manifest.RebuiltPaks.Count;
            foreach (var entry in manifest.RebuiltPaks)
            {
                pakIndex++;
                var backupPath = Path.Combine(backupDir,
                    entry.BackupRelativePath.Replace('/', Path.DirectorySeparatorChar));
                var targetPath = Path.Combine(gameRoot,
                    entry.TargetRelativePath.Replace('/', Path.DirectorySeparatorChar));

                progress?.Report(new DeployProgress("restore", pakIndex * 100 / pakTotal, $"Restoring {entry.PakName} ({pakIndex}/{pakTotal})"));

                if (!File.Exists(backupPath))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.RollbackBackupMissing,
                        $"Backup pak '{backupPath}' for '{entry.PakName}' is missing; cannot restore."));
                    continue;
                }

                // Verify the backup wasn't tampered with or truncated. If the
                // SHA recorded at deploy time doesn't match what's on disk now,
                // refuse the restore — overwriting the live pak with wrong bytes
                // would brick the install in a way 'rollback' is supposed to fix.
                var backupSha = ComputeFileSha256(backupPath);
                if (!string.Equals(backupSha, entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.RollbackHashMismatch,
                        $"Backup pak '{backupPath}' SHA-256 {backupSha} does not match the {entry.OriginalSha256} recorded at deploy time. Refusing to overwrite the live pak with possibly-corrupt bytes."));
                    continue;
                }

                // Streaming copy via AtomicFile — same .tmp + File.Move pattern
                // PakRebuilder uses, and equally important for multi-GB paks
                // (File.ReadAllBytes here would hit the 2 GB single-array limit).
                AtomicFile.CopyAtomic(backupPath, targetPath);
                restored++;
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.PakRollbackRestored,
                    $"Restored '{entry.PakName}' from backup ({entry.ByteSizeBefore} bytes)."));
            }
        }
        else
        {
            foreach (var entry in manifest.ModifiedFiles)
            {
                var backupPath = Path.Combine(backupDir, entry.RelativePath);
                var targetPath = Path.Combine(gameRoot, entry.RelativePath);

                if (!File.Exists(backupPath))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.RollbackBackupMissing,
                        $"Backup file '{backupPath}' for '{entry.RelativePath}' is missing; cannot restore."));
                    continue;
                }

                // Same integrity discipline as the RebuiltPaks branch above: if
                // the backup's SHA-256 no longer matches what was recorded at
                // deploy time, refuse rather than overwrite the live file with
                // possibly-corrupt bytes.
                var backupSha = ComputeFileSha256(backupPath);
                if (!string.Equals(backupSha, entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.RollbackHashMismatch,
                        $"Backup file '{backupPath}' SHA-256 {backupSha} does not match the {entry.OriginalSha256} recorded at deploy time. Refusing to overwrite the live file with possibly-corrupt bytes."));
                    continue;
                }

                AtomicFile.WriteAllBytes(targetPath, File.ReadAllBytes(backupPath));
                restored++;
            }
        }

        // If any canonical-pak / loose-XML restore failed (missing or corrupt backup),
        // abort NOW — before touching the Pattern B overlay paks. Deleting overlays while
        // the canonical paks are only partly restored would leave a mixed, unbootable
        // install with no clean undo path.
        if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
        {
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        // Pattern B addedFiles have no backup — they were created by deploy. Just delete them.
        // Missing is treated as info (user may have already cleaned up out-of-band).
        var deletedAdded = 0;
        if (manifest.AddedFiles.Count > 0)
        {
            progress?.Report(new DeployProgress("remove", null, $"Removing {manifest.AddedFiles.Count} overlay pak(s)"));
        }
        foreach (var added in manifest.AddedFiles)
        {
            var targetPath = Path.Combine(
                gameRoot,
                added.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(targetPath))
            {
                // Never destroy a foreign overlay. AddedFiles have no backup, so if the
                // live bytes no longer match what we deployed, someone replaced this pak
                // after deploy — leave it in place (even under --force) and warn, rather
                // than silently discarding their file.
                var liveSha = ComputeFileSha256(targetPath);
                if (!string.IsNullOrEmpty(added.DeployedSha256)
                    && !string.Equals(liveSha, added.DeployedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.RollbackAddedFileChanged,
                        $"Overlay '{targetPath}' changed since deploy '{latest.Timestamp}' (live SHA-256 {liveSha} != deployed {added.DeployedSha256}); left in place instead of deleted. Remove it manually if you no longer want it."));
                    continue;
                }
                try
                {
                    File.Delete(targetPath);
                    deletedAdded++;
                }
                catch (IOException ex)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.RollbackBackupMissing,
                        $"Could not delete added file '{targetPath}': {ex.Message}"));
                    continue;
                }
            }
            else
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.RollbackAddedFileMissing,
                    $"Added file '{targetPath}' was already gone; nothing to delete."));
            }
        }

        if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
        {
            return new RollbackResult
            {
                GameFingerprint = fingerprint,
                Diagnostics = diagnostics,
            };
        }

        // Pop the entry from history (everything else stays — rollback is one-step).
        var remaining = history.Deploys.Skip(1).ToList();
        var updatedHistory = new DeployHistory
        {
            DeployHistoryVersion = history.DeployHistoryVersion,
            GameFingerprint = history.GameFingerprint,
            GameRoot = history.GameRoot,
            Deploys = remaining,
        };
        _historyStore.Write(layout, fingerprint, updatedHistory);

        // Remove the rolled-back deploy's timestamp directory (manifest + backup).
        try
        {
            Directory.Delete(layout.DeployTimestampDirectory(fingerprint, latest.Timestamp), recursive: true);
        }
        catch (IOException ex)
        {
            // History is already updated, so the leftover dir is harmless to rollback — but
            // surface it so the user can reclaim the space and it isn't mistaken for a live backup.
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.RollbackLeftoverDirectory,
                $"Rolled back, but could not remove the deploy directory for '{latest.Timestamp}': {ex.Message}. Remove it manually to reclaim space."));
        }

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.RollbackCompleted,
            $"Restored {restored} modified + deleted {deletedAdded} added file(s) from deploy '{latest.Timestamp}' (profile '{latest.Profile}')."));

        return new RollbackResult
        {
            Outcome = RollbackOutcome.Reverted,
            GameFingerprint = fingerprint,
            RevertedTimestamp = latest.Timestamp,
            RevertedProfile = latest.Profile,
            RestoredFileCount = restored + deletedAdded,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Streaming SHA-256 — used to validate pak backups before
    /// restore. Pak files can exceed 2 GB, so a byte-array hash would
    /// throw IOException on real installs.</summary>
    private static string ComputeFileSha256(string path) => FileHashing.ComputeFileSha256(path);
}
