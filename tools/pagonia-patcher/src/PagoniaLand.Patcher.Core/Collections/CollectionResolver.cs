using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class CollectionResolver
{
    private readonly ManifestReader _reader = new();
    private readonly ISerializer _serializer = PatcherYaml.CreateSerializer();
    private readonly FormatVersionPolicy _formatPolicy = new();

    public ReadResult<CollectionResolution> Resolve(string collectionPath, string modsRoot)
    {
        var collectionResult = _reader.ReadCollectionManifest(collectionPath);

        if (!collectionResult.Success || collectionResult.Value is null)
        {
            return ReadResult<CollectionResolution>.Failed(collectionResult.Diagnostics.ToArray());
        }

        var diagnostics = new List<PatchDiagnostic>(collectionResult.Diagnostics);
        var localMods = ReadLocalMods(modsRoot, diagnostics);
        var resolvedMods = new List<ResolvedCollectionMod>();

        foreach (var collectionMod in OrderCollectionMods(collectionResult.Value))
        {
            if (!collectionMod.Enabled)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Info,
                    DiagnosticCodes.CollectionModSkipped,
                    $"Skipped disabled collection mod '{collectionMod.Id}'."));
                continue;
            }

            var match = localMods.FirstOrDefault(mod =>
                string.Equals(mod.Manifest.Id, collectionMod.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(mod.Manifest.Version, collectionMod.Version, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                var severity = collectionMod.Required ? PatchDiagnosticSeverity.Error : PatchDiagnosticSeverity.Warning;
                diagnostics.Add(new PatchDiagnostic(
                    severity,
                    DiagnosticCodes.CollectionModMissing,
                    $"Collection mod '{collectionMod.Id}' version '{collectionMod.Version}' was not found under '{modsRoot}'."));
                continue;
            }

            var sha256 = ComputeDirectorySha256(match.Directory);
            resolvedMods.Add(new ResolvedCollectionMod(collectionMod, match, collectionResult.Value.Id, match.Directory, sha256));
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.CollectionModResolved,
                $"Resolved collection mod '{collectionMod.Id}' to '{match.Directory}'."));
        }

        var collectionLock = new CollectionLock
        {
            CollectionLockVersion = FormatVersionPolicy.CurrentVersion(ManagedFormat.CollectionLock),
            CollectionId = collectionResult.Value.Id,
            CollectionVersion = collectionResult.Value.Version,
            GameDatabaseVersion = collectionResult.Value.GameDatabaseVersion,
            GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
            Mods = resolvedMods
                .Select(mod => new LockedMod
                {
                    Id = mod.LoadedMod.Manifest.Id,
                    Version = mod.LoadedMod.Manifest.Version,
                    ResolvedSource = mod.LocalPath,
                    ArchiveSha256 = mod.Sha256,
                    Enabled = true,
                    // Pin the effective tweak values (collection overrides folded over the mod's
                    // declared defaults) so a re-apply reproduces the exact same substitution.
                    Tweaks = BuildLockedTweaks(mod.LoadedMod, mod.CollectionMod),
                    // Local resolves leave Source + ResolvedAt empty. Remote
                    // resolves (via the manager's RemoteFetcher) post-process
                    // the lockfile to fill them in.
                })
                .ToList(),
        };

        var resolution = new CollectionResolution(collectionResult.Value, resolvedMods, collectionLock, diagnostics);
        return resolution.Success
            ? ReadResult<CollectionResolution>.Ok(resolution, diagnostics.ToArray())
            : ReadResult<CollectionResolution>.Failed(diagnostics.ToArray());
    }

    public ReadResult<CollectionSetResolution> ResolveMany(IReadOnlyList<string> collectionPaths, string modsRoot)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var collections = new List<CollectionManifest>();
        var resolvedMods = new List<ResolvedCollectionMod>();
        var resolvedById = new Dictionary<string, ResolvedCollectionMod>(StringComparer.OrdinalIgnoreCase);

        if (collectionPaths.Count == 0)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionSetEmpty,
                "At least one collection is required."));
        }

        var localMods = ReadLocalMods(modsRoot, diagnostics);

        foreach (var collectionPath in collectionPaths)
        {
            var collectionResult = _reader.ReadCollectionManifest(collectionPath);
            diagnostics.AddRange(collectionResult.Diagnostics);

            if (!collectionResult.Success || collectionResult.Value is null)
            {
                continue;
            }

            var collection = collectionResult.Value;
            collections.Add(collection);

            foreach (var collectionMod in OrderCollectionMods(collection))
            {
                if (!collectionMod.Enabled)
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Info,
                        DiagnosticCodes.CollectionModSkipped,
                        $"Skipped disabled collection mod '{collectionMod.Id}' from collection '{collection.Id}'."));
                    continue;
                }

                if (resolvedById.TryGetValue(collectionMod.Id, out var existing))
                {
                    if (string.Equals(existing.LoadedMod.Manifest.Version, collectionMod.Version, StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(new PatchDiagnostic(
                            PatchDiagnosticSeverity.Info,
                            DiagnosticCodes.CollectionModDuplicateSkipped,
                            $"Skipped duplicate mod '{collectionMod.Id}' version '{collectionMod.Version}' from collection '{collection.Id}'."));
                    }
                    else
                    {
                        diagnostics.Add(new PatchDiagnostic(
                            PatchDiagnosticSeverity.Error,
                            DiagnosticCodes.CollectionModVersionConflict,
                            $"Mod '{collectionMod.Id}' is requested as version '{existing.LoadedMod.Manifest.Version}' by collection '{existing.SourceCollectionId}' and version '{collectionMod.Version}' by collection '{collection.Id}'."));
                    }

                    continue;
                }

                var match = localMods.FirstOrDefault(mod =>
                    string.Equals(mod.Manifest.Id, collectionMod.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(mod.Manifest.Version, collectionMod.Version, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                {
                    var severity = collectionMod.Required ? PatchDiagnosticSeverity.Error : PatchDiagnosticSeverity.Warning;
                    diagnostics.Add(new PatchDiagnostic(
                        severity,
                        DiagnosticCodes.CollectionModMissing,
                        $"Collection '{collection.Id}' mod '{collectionMod.Id}' version '{collectionMod.Version}' was not found under '{modsRoot}'."));
                    continue;
                }

                var sha256 = ComputeDirectorySha256(match.Directory);
                var resolvedMod = new ResolvedCollectionMod(collectionMod, match, collection.Id, match.Directory, sha256);
                resolvedMods.Add(resolvedMod);
                resolvedById.Add(collectionMod.Id, resolvedMod);
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Info,
                    DiagnosticCodes.CollectionModResolved,
                    $"Resolved collection '{collection.Id}' mod '{collectionMod.Id}' to '{match.Directory}'."));
            }
        }

        var gameDatabaseVersions = collections
            .Select(collection => collection.GameDatabaseVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (gameDatabaseVersions.Count > 1)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionGameDatabaseVersionConflict,
                $"Collections target different GameDatabase versions: {string.Join(", ", gameDatabaseVersions)}."));
        }

        var resolution = new CollectionSetResolution(collections, resolvedMods, diagnostics);
        return resolution.Success
            ? ReadResult<CollectionSetResolution>.Ok(resolution, diagnostics.ToArray())
            : ReadResult<CollectionSetResolution>.Failed(diagnostics.ToArray());
    }

    public ReadResult<LockResolution> ResolveFromLock(string lockPath, string modsRoot)
    {
        var lockResult = _reader.ReadCollectionLock(lockPath);

        if (!lockResult.Success || lockResult.Value is null)
        {
            return ReadResult<LockResolution>.Failed(lockResult.Diagnostics.ToArray());
        }

        var diagnostics = new List<PatchDiagnostic>(lockResult.Diagnostics);

        // Lockfile format-version gate via the shared tiered reader: a newer minor
        // reads (with a recommend-update note), a newer/retired major or a malformed
        // value aborts with a structured, actionable diagnostic — so a future bump
        // never silently produces an under-validated install.
        var verdict = _formatPolicy.Evaluate(ManagedFormat.CollectionLock, lockResult.Value.CollectionLockVersion);
        if (verdict.Diagnostic is not null)
        {
            diagnostics.Add(verdict.Diagnostic);
        }
        if (!verdict.Accepted)
        {
            return ReadResult<LockResolution>.Failed(diagnostics.ToArray());
        }

        var localMods = ReadLocalMods(modsRoot, diagnostics);
        var resolvedMods = new List<LoadedMod>();

        foreach (var lockedMod in lockResult.Value.Mods)
        {
            if (!lockedMod.Enabled)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Info,
                    DiagnosticCodes.LockfileModSkipped,
                    $"Skipped disabled locked mod '{lockedMod.Id}'."));
                continue;
            }

            var match = localMods.FirstOrDefault(mod =>
                string.Equals(mod.Manifest.Id, lockedMod.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(mod.Manifest.Version, lockedMod.Version, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.LockfileModMissing,
                    $"Locked mod '{lockedMod.Id}' version '{lockedMod.Version}' was not found under '{modsRoot}'."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lockedMod.ArchiveSha256))
            {
                var currentHash = ComputeDirectorySha256(match.Directory);

                if (!string.Equals(currentHash, lockedMod.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Error,
                        DiagnosticCodes.LockfileArchiveHashMismatch,
                        $"Locked mod '{lockedMod.Id}' content hash does not match. Expected '{lockedMod.ArchiveSha256}', found '{currentHash}'.",
                        match.Directory));
                    continue;
                }
            }

            resolvedMods.Add(match);
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.LockfileModResolved,
                $"Resolved locked mod '{lockedMod.Id}' to '{match.Directory}'."));
        }

        var resolution = new LockResolution(lockResult.Value, resolvedMods, diagnostics);
        return resolution.Success
            ? ReadResult<LockResolution>.Ok(resolution, diagnostics.ToArray())
            : ReadResult<LockResolution>.Failed(diagnostics.ToArray());
    }

    // The tweak values to pin into the lockfile for one resolved mod: each declared tweak resolved
    // to the collection-supplied override when the curator set one, otherwise the mod's default.
    // Returns null when the mod declares no tweaks so the lockfile stays clean.
    private static Dictionary<string, string>? BuildLockedTweaks(LoadedMod mod, CollectionMod collectionMod)
    {
        if (mod.Manifest.Tweaks.Count == 0)
        {
            return null;
        }

        var tweaks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tweak in mod.Manifest.Tweaks)
        {
            tweaks[tweak.Id] = collectionMod.Tweaks is not null && collectionMod.Tweaks.TryGetValue(tweak.Id, out var supplied)
                ? supplied
                : tweak.Default;
        }

        return tweaks;
    }

    private static IReadOnlyList<CollectionMod> OrderCollectionMods(CollectionManifest collection)
    {
        if (collection.LoadOrder.Count == 0)
        {
            return collection.Mods;
        }

        var modsById = collection.Mods.ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var orderedMods = new List<CollectionMod>();

        foreach (var modId in collection.LoadOrder)
        {
            if (modsById.Remove(modId, out var mod))
            {
                orderedMods.Add(mod);
            }
        }

        orderedMods.AddRange(modsById.Values);
        return orderedMods;
    }

    public void WriteLockFile(CollectionLock collectionLock, string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, _serializer.Serialize(collectionLock));
    }

    private List<LoadedMod> ReadLocalMods(string modsRoot, List<PatchDiagnostic> diagnostics)
    {
        var mods = new List<LoadedMod>();

        if (!Directory.Exists(modsRoot))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ModsRootMissing,
                $"Mods root does not exist: {modsRoot}",
                modsRoot));
            return mods;
        }

        foreach (var directory in Directory.EnumerateDirectories(modsRoot))
        {
            var result = _reader.ReadMod(directory);
            diagnostics.AddRange(result.Diagnostics.Where(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error));

            if (result.Value is not null)
            {
                mods.Add(result.Value);
            }
        }

        return mods;
    }

    // The collection lockfile's archiveSha256 hashes the whole resolved mod folder; the shared
    // helper owns the canonicalisation (also used by the index mirror's contentHash, over the
    // narrower mod-payload set).
    private static string ComputeDirectorySha256(string directory) => ContentHash.OfDirectory(directory);
}
