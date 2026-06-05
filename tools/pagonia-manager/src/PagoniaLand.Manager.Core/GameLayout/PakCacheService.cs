using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using PagoniaLand.Paker;

namespace PagoniaLand.Manager;

/// <summary>
/// Outcome of <see cref="PakCacheService.Ensure"/>. Carries the cache root the
/// patcher should resolve against, the install fingerprint the cache belongs to,
/// whether the cache was already warm, the list of paks that were extracted on
/// a miss (empty on a hit), and any diagnostics emitted along the way.
/// </summary>
public sealed record CacheEnsureResult(
    string CacheRoot,
    string Fingerprint,
    bool FromCache,
    IReadOnlyList<string> ExtractedPaks,
    IReadOnlyList<ManagerDiagnostic> Diagnostics,
    bool Success);

/// <summary>
/// Extract-and-reuse cache for the canonical paks of a live Pioneers of Pagonia
/// install. Lives at <c>&lt;store&gt;/cache/extract-&lt;fingerprint&gt;/</c>, where
/// fingerprint is a stable hash of the install's pak file paths + sizes + mtimes.
/// One cache per fingerprint is kept warm; older caches are pruned on next ensure.
/// <para>
/// Cache layout mirrors the on-disk shape the patcher already resolves against
/// (same as the repo's <c>game-gdb/</c>): each pak gets its basename as a
/// folder prefix, then the pak's internal paths underneath.
/// </para>
/// </summary>
public sealed class PakCacheService
{
    // AOT: pin PakCacheStatus shape so YamlDotNet's read/write reflection
    // survives trimming. Same Shape constant the deploy services use.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(PakCacheStatus))]
    [DynamicDependency(Shape, typeof(PakCacheEntry))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<PakCacheEntry>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<string>))]
    public PakCacheService()
    {
    }

    /// <summary>
    /// Ensure the cache holds every pak in <paramref name="requiredPakBasenames"/>.
    /// On a full hit returns the existing cache root in microseconds. On miss
    /// or partial hit, only the missing paks are extracted; previously-warm
    /// paks are left untouched. On the first failed pak the partial cache is
    /// wiped and the call returns with Success=false plus diagnostics.
    /// </summary>
    /// <param name="requiredPakBasenames">Pak basenames (no extension, e.g.
    /// "core", "dlc1") the caller needs warm. Pass <c>null</c> to extract
    /// every pak <paramref name="detected"/> discovered — the historical
    /// "all paks" behaviour, kept for callers that don't know the working
    /// set yet. An empty list is a valid request (no extraction, returns
    /// the cache root with no work done).</param>
    /// <param name="progress">Optional structured progress sink for a UI layer.
    /// Each per-pak extract reports a <see cref="DeployProgress"/> under the
    /// <c>"extract"</c> stage with a percentage; the service stays
    /// presentation-agnostic.</param>
    public CacheEnsureResult Ensure(
        StoreLayout layout,
        GameLayout detected,
        IReadOnlyCollection<string>? requiredPakBasenames = null,
        IProgress<DeployProgress>? progress = null)
    {
        if (detected.Kind != GameLayoutKind.LiveInstall)
        {
            // Defensive — callers should branch on Kind themselves. Surface
            // the misuse rather than silently returning an empty result that
            // would hide the bug.
            throw new ArgumentException(
                $"PakCacheService.Ensure requires a LiveInstall layout (got {detected.Kind}).",
                nameof(detected));
        }

        var diagnostics = new List<ManagerDiagnostic>();
        var fingerprint = ComputePakFingerprint(detected);
        var cacheRoot = layout.PakCacheDirectory(fingerprint);

        // Pre-resolve the requested working set. Null = "extract every discovered
        // pak" (back-compat with the historical all-paks behaviour); otherwise
        // intersect with discovered paks so a caller asking for a pak that doesn't
        // exist in this install (e.g. mod targets dlc1 but DLC not installed) is
        // a no-op for that pak rather than an error.
        var discoveredByBasename = detected.DiscoveredPaks
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase);
        var requested = requiredPakBasenames is null
            ? discoveredByBasename.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
            : requiredPakBasenames
                .Where(name => discoveredByBasename.ContainsKey(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // Read whatever per-pak status already exists for this fingerprint.
        // Stale v2 caches with the old .extract-complete sentinel get cleared
        // because v3 changed the schema tag in ComputePakFingerprint — they
        // sit under a different cache dir name and PruneStaleCaches deletes
        // them at the end of this call.
        var statusFile = layout.PakCacheStatusFile(fingerprint);
        var extractedMap = ReadStatus(statusFile);

        // Notice canonical paks changed outside the manager since they were cached.
        // Foreign edits re-extract (dropped from the map below) + warn; manager
        // deploys are recognised and stay silent. Runs only over warm requested
        // paks, so the hash cost is bounded to the active profile's working set.
        DetectExternalPakChanges(layout, detected, discoveredByBasename, requested, extractedMap, statusFile, diagnostics);

        var stillMissing = requested
            .Where(name => !extractedMap.ContainsKey(name))
            .ToList();

        if (stillMissing.Count == 0)
        {
            PruneStaleCaches(layout, keepFingerprint: fingerprint);
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.PakCacheReused,
                $"Reused extract cache at '{cacheRoot}' ({requested.Count}/{detected.DiscoveredPaks.Count} requested pak(s) already on disk)."));
            return new CacheEnsureResult(
                CacheRoot: cacheRoot,
                Fingerprint: fingerprint,
                FromCache: true,
                ExtractedPaks: Array.Empty<string>(),
                Diagnostics: diagnostics,
                Success: true);
        }

        // Partial vs full miss — surface a partialHit info before extracting
        // so the user sees "extracting dlc1 (3 paks already warm)" instead of
        // a misleading "extracting from scratch" feel.
        if (extractedMap.Count > 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.PakCachePartialHit,
                $"Cache had {extractedMap.Count} pak(s) already extracted; adding {stillMissing.Count} more ({string.Join(", ", stillMissing)})."));
        }
        else
        {
            Directory.CreateDirectory(cacheRoot);
        }

        var reader = new PakReader();
        var extracted = new List<string>();
        var total = stillMissing.Count;
        for (var i = 0; i < total; i++)
        {
            var basename = stillMissing[i];
            var pakPath = discoveredByBasename[basename];
            var pakName = Path.GetFileName(pakPath);
            progress?.Report(new DeployProgress("extract", (i + 1) * 100 / total, $"Extracting {pakName} ({i + 1}/{total})"));

            try
            {
                ExtractPakInto(reader, pakPath, cacheRoot);
                extracted.Add(pakPath);
                // Record the source pak's hash so the next ensure can tell an
                // external edit from a manager-authored deploy.
                extractedMap[basename] = FileHashing.ComputeFileSha256(pakPath);
                // Persist the status after EVERY pak so a crash mid-batch
                // leaves the cache in a coherent state: extracted paks are
                // recorded, the next ensure call resumes from there instead
                // of redoing the work.
                WriteStatus(statusFile, extractedMap);
            }
            catch (Exception ex)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.PakCacheExtractFailed,
                    $"Extracting {pakName} failed: {ex.Message}"));
                // Different from v2: we DON'T wipe the whole cache on per-pak failure.
                // Paks that DID extract cleanly stay in the cache and are recorded in
                // .extract-status.yaml — the next ensure call resumes by extracting
                // only what's still missing. Wiping would punish the user for a single
                // bad pak (e.g. transient I/O hiccup) by forcing them to redo the
                // gigabytes of pak data that extracted fine.
                return new CacheEnsureResult(
                    CacheRoot: cacheRoot,
                    Fingerprint: fingerprint,
                    FromCache: false,
                    ExtractedPaks: extracted,
                    Diagnostics: diagnostics,
                    Success: false);
            }
        }

        PruneStaleCaches(layout, keepFingerprint: fingerprint);

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.PakCacheRefreshed,
            $"Extracted {extracted.Count} pak(s) into '{cacheRoot}'."));

        if (requested.Count < detected.DiscoveredPaks.Count)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.PakCacheSelective,
                $"Selective extract: {requested.Count} of {detected.DiscoveredPaks.Count} discovered pak(s) needed by the active profile."));
        }

        return new CacheEnsureResult(
            CacheRoot: cacheRoot,
            Fingerprint: fingerprint,
            FromCache: false,
            ExtractedPaks: extracted,
            Diagnostics: diagnostics,
            Success: true);
    }

    /// <summary>Read the sidecar as a basename → source-pak-SHA-256 map. Missing
    /// or corrupt file → empty map (the next extraction overwrites it). A v4
    /// sidecar never reaches here — the v5 tag puts it under a different cache dir.</summary>
    private static Dictionary<string, string> ReadStatus(string statusFile)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(statusFile)) return map;
        try
        {
            var yaml = File.ReadAllText(statusFile);
            if (string.IsNullOrWhiteSpace(yaml)) return map;
            var status = ManagerYaml.CreateDeserializer().Deserialize<PakCacheStatus>(yaml);
            if (status?.ExtractedPaks is null) return map;
            foreach (var entry in status.ExtractedPaks)
            {
                if (!string.IsNullOrWhiteSpace(entry.Name))
                {
                    map[entry.Name] = entry.PakSha256 ?? string.Empty;
                }
            }
            return map;
        }
        catch
        {
            // Corrupt status file → treat as empty, the next extraction will
            // overwrite it. Better than refusing to proceed when recovery is
            // straightforward.
            return map;
        }
    }

    private static void WriteStatus(string statusFile, IReadOnlyDictionary<string, string> extracted)
    {
        var entries = extracted
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new PakCacheEntry { Name = kv.Key, PakSha256 = kv.Value })
            .ToList();
        var yaml = ManagerYaml.CreateSerializer().Serialize(new PakCacheStatus { ExtractedPaks = entries });
        AtomicFile.WriteAllText(statusFile, yaml);
    }

    /// <summary>
    /// For each warm pak we'd otherwise reuse, recompute the live source
    /// pak's SHA-256 and compare to the hash recorded at extract time.
    /// <list type="bullet">
    /// <item><description>Equal → unchanged, fast path (no manifest reads).</description></item>
    /// <item><description>Differs but equals a manager-recorded <c>RebuiltPaks[].NewSha256</c>
    /// → a manager deploy wrote it; expected, silent, keep the original cache slice.</description></item>
    /// <item><description>Differs and matches nothing the manager wrote → a foreign
    /// edit; warn <c>manager.canonicalPakChangedExternally</c> and drop it from
    /// <paramref name="extractedMap"/> so the ensure loop re-extracts it (recording
    /// the new hash).</description></item>
    /// </list>
    /// Mutates <paramref name="extractedMap"/> + persists it when anything was dropped.
    /// </summary>
    private static void DetectExternalPakChanges(
        StoreLayout layout,
        GameLayout detected,
        IReadOnlyDictionary<string, string> discoveredByBasename,
        IReadOnlyList<string> requested,
        Dictionary<string, string> extractedMap,
        string statusFile,
        List<ManagerDiagnostic> diagnostics)
    {
        var warmRequested = requested.Where(extractedMap.ContainsKey).ToList();
        if (warmRequested.Count == 0)
        {
            return;
        }

        HashSet<string>? managerWrittenHashes = null; // loaded lazily — only on a difference
        var changed = false;

        foreach (var basename in warmRequested)
        {
            var pakPath = discoveredByBasename[basename];
            string liveHash;
            try
            {
                liveHash = FileHashing.ComputeFileSha256(pakPath);
            }
            catch
            {
                // Unreadable live pak (locked / removed mid-call) — leave the warm
                // slice as-is; downstream extraction would surface a real error.
                continue;
            }

            var recorded = extractedMap[basename];
            if (string.IsNullOrEmpty(recorded)
                || string.Equals(liveHash, recorded, StringComparison.OrdinalIgnoreCase))
            {
                continue; // unchanged (or no baseline to compare) — fast path
            }

            managerWrittenHashes ??= CollectManagerWrittenPakHashes(layout, detected.Root);
            if (managerWrittenHashes.Contains(liveHash))
            {
                // The manager itself rewrote this pak on a deploy. Expected: the
                // cache still represents the canonical original, so keep it and the
                // recorded (original) baseline untouched.
                continue;
            }

            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CanonicalPakChangedExternally,
                $"Canonical pak '{Path.GetFileName(pakPath)}' changed outside the manager since it was cached — re-extracting so the cache matches. " +
                "Another tool or a manual repack edited your game data; this is a heads-up, not an error."));
            extractedMap.Remove(basename); // → re-extracted by the ensure loop, which records the new hash
            changed = true;
        }

        if (changed)
        {
            WriteStatus(statusFile, extractedMap);
        }
    }

    /// <summary>
    /// Collect every <c>RebuiltPaks[].NewSha256</c> the manager recorded across all
    /// deploys for <paramref name="gameRoot"/>'s install. A live pak whose current
    /// hash is in this set was written by a manager deploy (not a foreign tool).
    /// Best-effort: unreadable history/manifests are skipped. Read lazily, so the
    /// common "nothing changed" path never touches these files.
    /// </summary>
    private static HashSet<string> CollectManagerWrittenPakHashes(StoreLayout layout, string gameRoot)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var gameFingerprint = GameFingerprint.Compute(gameRoot);
        if (!new DeployHistoryStore().TryRead(layout, gameFingerprint, out var history, out _))
        {
            return hashes;
        }

        foreach (var deploy in history.Deploys)
        {
            var manifestPath = layout.DeployManifestFile(gameFingerprint, deploy.Timestamp);
            if (!File.Exists(manifestPath))
            {
                continue;
            }
            try
            {
                var manifest = ManagerYaml.CreateDeserializer().Deserialize<DeployManifest>(File.ReadAllText(manifestPath));
                if (manifest is null)
                {
                    continue;
                }
                foreach (var pak in manifest.RebuiltPaks)
                {
                    if (!string.IsNullOrWhiteSpace(pak.NewSha256))
                    {
                        hashes.Add(pak.NewSha256);
                    }
                }
            }
            catch
            {
                // skip unreadable manifest
            }
        }
        return hashes;
    }

    // Cache schema version. Bump when the on-disk extract layout changes so
    // older caches don't get reused with stale contents.
    //   v1: prefixed every entry with the pak basename — broke on real PoP
    //       paks where entries already start with "core/", "dlc1/" etc.
    //   v2: extracts entries verbatim (their in-pak path IS the extracted path)
    //   v3: per-pak completion tracking via .extract-status.yaml (replaces v2's
    //       global .extract-complete sentinel). Enables selective + incremental
    //       extract — only requested paks land in the cache, missing ones get
    //       added on subsequent ensure calls without re-extracting warm paks.
    //   v4: fingerprint no longer includes per-pak size/mtime — those drift on
    //       every manager-deploy and were silently invalidating the cache after
    //       every deploy/rollback round-trip. v4 hashes the discovered pak
    //       filename list + system.json content instead (DLC install/uninstall
    //       + Steam update detected; manager-owned writes don't trigger
    //       invalidation).
    //   v5: status sidecar records per-pak source SHA-256 (PakCacheEntry) so an
    //       out-of-band change to a canonical pak is detected at extract time
    //       (manager.canonicalPakChangedExternally). The install fingerprint is
    //       unchanged from v4 — this is a per-pak content check layered on top,
    //       NOT folded into the fingerprint. Tag bumped so v4 sidecars (no hash)
    //       are re-extracted under a fresh dir rather than misread.
    private const string PakCacheSchemaTag = "v5";

    /// <summary>
    /// Stable short hash identifying "this Pioneers of Pagonia install version".
    /// Hashes:
    /// <list type="bullet">
    /// <item><description>The sorted list of pak filenames — detects DLC install /
    /// uninstall (new / missing entries in the list).</description></item>
    /// <item><description>The contents of <c>system.json</c> — Steam updates touch
    /// this on every game version bump.</description></item>
    /// </list>
    /// <para>Deliberately omits per-pak file sizes and modification times. The
    /// manager itself rewrites pak files on every deploy + rollback, so any
    /// fingerprint that depended on pak file state would silently invalidate
    /// the cache after every round-trip — exactly the bug earlier work fixes.
    /// Manual edits to paks outside the manager don't trigger a cache refresh
    /// either, but they're rare and surface clearly as
    /// <c>patcher.expectedValueMismatch</c> on the next deploy attempt.</para>
    /// </summary>
    internal static string ComputePakFingerprint(GameLayout detected)
    {
        var sb = new StringBuilder();

        // Pak filename list — DLC install/uninstall changes this; manager-deploy
        // doesn't. Sorted for stability across detection ordering.
        foreach (var pakName in detected.DiscoveredPaks
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append(pakName).Append('\n');
        }

        // system.json content — Steam updates change this on every game version
        // bump. The manager never writes to system.json.
        var systemJsonPath = Path.Combine(detected.Root, GameLayoutConstants.SystemFingerprintFile);
        if (File.Exists(systemJsonPath))
        {
            var bytes = File.ReadAllBytes(systemJsonPath);
            sb.Append("system.json|").Append(bytes.Length).Append('|');
            sb.Append(Convert.ToHexString(SHA256.HashData(bytes))).Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return $"{PakCacheSchemaTag}-{Convert.ToHexString(hash, 0, 8).ToLowerInvariant()}";
    }

    /// <summary>
    /// Read a pak's index and write every entry's bytes into
    /// <c>&lt;cacheRoot&gt;/&lt;entry.Filename&gt;</c>. Real Pioneers of Pagonia
    /// paks already embed the package prefix in each entry's filename (e.g.
    /// <c>core/gdb/buildings.gd.xml</c>), so extracting verbatim into
    /// <c>cacheRoot</c> reproduces the same shape the patcher resolves against
    /// in the repo's <c>game-gdb/</c>. Adding our own pak-basename prefix
    /// (the original cut) doubled the prefix and made the patcher fail
    /// with <c>targetFileMissing</c>.
    /// </summary>
    private static void ExtractPakInto(PakReader reader, string pakPath, string cacheRoot)
    {
        using var pakStream = File.OpenRead(pakPath);
        var openResult = reader.OpenIndex(pakStream);
        if (!openResult.Success || openResult.Index is null)
        {
            var detail = openResult.Diagnostics.FirstOrDefault()?.Message ?? "no diagnostic";
            throw new InvalidOperationException($"could not open pak index: {detail}");
        }

        foreach (var entry in openResult.Index.Entries)
        {
            var safeRelative = ToSafeRelativePath(entry.Filename);
            if (safeRelative is null)
            {
                throw new InvalidOperationException(
                    $"refusing to extract entry with unsafe path '{entry.Filename}'");
            }

            var outPath = Path.Combine(cacheRoot, safeRelative);
            var outDir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            using var outStream = File.Create(outPath);
            reader.ExtractEntry(pakStream, entry, outStream);
        }
    }

    /// <summary>
    /// Same path-traversal check the paker CLI uses on <c>unpack</c>: reject
    /// absolute paths, leading slashes, and any <c>..</c> segment. A pak that
    /// tries to escape its cache subtree is treated as adversarial.
    /// </summary>
    private static string? ToSafeRelativePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return null;
        var normalised = filename.Replace('\\', '/');
        if (normalised.StartsWith('/')) return null;
        if (Path.IsPathRooted(normalised)) return null;
        foreach (var segment in normalised.Split('/'))
        {
            if (segment == "." || segment == "..") return null;
        }
        return normalised.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Delete every other <c>extract-*</c> directory under the store's cache root.
    /// Best-effort — a locked file (e.g. someone has the previous cache open in
    /// Explorer) doesn't block the new cache from being usable, so swallow.
    /// </summary>
    private static void PruneStaleCaches(StoreLayout layout, string keepFingerprint)
    {
        var cacheDir = layout.CacheDirectory;
        if (!Directory.Exists(cacheDir)) return;

        var keepDir = layout.PakCacheDirectory(keepFingerprint);
        foreach (var dir in Directory.EnumerateDirectories(cacheDir))
        {
            var name = Path.GetFileName(dir);
            if (!name.StartsWith(StoreLayoutConstants.PakCacheFolderPrefix, StringComparison.Ordinal))
            {
                continue;
            }
            if (string.Equals(Path.GetFullPath(dir), Path.GetFullPath(keepDir), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
