using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// On-disk cache layer over <see cref="CatalogFetcher"/>. The aggregator
/// would otherwise hit raw.githubusercontent.com once per subscribed
/// catalog on every <c>catalog browse</c>; this wrapper keeps a per-catalog cache
/// directory under <c>&lt;store&gt;/cache/catalogs/&lt;sanitised-canonical&gt;_&lt;8hex&gt;/</c>
/// (catalog.yaml plus a meta sidecar with the fetch timestamp). The 8-hex
/// SHA1 suffix disambiguates two sources whose 80-char sanitised prefix
/// collides. Entries fresher than <c>state.yaml.catalogCacheStalenessHours</c>
/// (default 24h) serve from disk.
/// <para>
/// <c>file:</c> sources bypass the cache entirely — the source file IS the
/// canonical bytes, and the round-trip cost is a single ReadAllText against
/// the user's own disk.
/// </para>
/// </summary>
public sealed class CachingCatalogFetcher : CatalogFetcher
{
    public const int DefaultStalenessHours = 24;

    private readonly StoreLayout _layout;
    private readonly TimeSpan _staleness;

    /// <summary>Wraps the network fetcher with on-disk caching rooted at the store's cache directory.</summary>
    /// <param name="httpFetcher">Underlying transport used on cache miss.</param>
    /// <param name="layout">Store layout; the cache lives at <c>&lt;Root&gt;/cache/catalogs/</c>.</param>
    /// <param name="stalenessHours">Cache entries older than this trigger a fresh fetch on the next access. Null / non-positive use <see cref="DefaultStalenessHours"/>.</param>
    // The Catalog group is rooted by the base CatalogFetcher ctor (chained
    // below); only this cache sidecar type is unique to the caching layer, so
    // pin it here against the AOT trimmer. See CatalogFetcher for the why.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields,
        typeof(CatalogCacheMeta))]
    public CachingCatalogFetcher(IRemoteContentFetcher httpFetcher, StoreLayout layout, int? stalenessHours = null, bool allowInsecureCatalogSources = false)
        : base(httpFetcher, allowInsecureCatalogSources)
    {
        _layout = layout;
        var hours = (stalenessHours is > 0) ? stalenessHours.Value : DefaultStalenessHours;
        _staleness = TimeSpan.FromHours(hours);
    }

    public override async Task<CatalogFetchResult> FetchAsync(CatalogSource source, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        // file: sources bypass the cache — the source IS the canonical bytes
        // and the read cost is identical to the cache hit cost. Delegate
        // straight to the base fetcher.
        if (source is FileCatalogSource)
        {
            return await base.FetchAsync(source, forceRefresh, cancellationToken).ConfigureAwait(false);
        }

        var cacheDir = ResolveCacheDir(source);
        var catalogPath = Path.Combine(cacheDir, "catalog.yaml");
        var metaPath = Path.Combine(cacheDir, "cache-meta.yaml");

        // Cache-hit attempt. Skipped on forceRefresh. Skipped if the meta is
        // missing / corrupt / stale. If anything's off we fall through to a
        // fresh fetch + cache-write, which heals the corrupt-meta case
        // naturally on the next read.
        if (!forceRefresh && File.Exists(catalogPath) && File.Exists(metaPath))
        {
            CatalogCacheMeta? meta;
            try
            {
                meta = ReadMeta(metaPath);
            }
            catch (Exception ex)
            {
                meta = null;
                // Note the corruption; fresh fetch will overwrite both files.
                var corruptDiagnostics = new List<ManagerDiagnostic>
                {
                    new(ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.CatalogCacheCorrupt,
                        $"Catalog cache meta at '{metaPath}' is unreadable ({ex.Message}); refetching."),
                };
                return await FetchFreshAndCache(source, forceRefresh, cacheDir, catalogPath, metaPath, corruptDiagnostics, cancellationToken).ConfigureAwait(false);
            }

            if (meta is not null && TryParseFetchedAt(meta.FetchedAt, out var fetchedAtUtc) && IsFresh(fetchedAtUtc))
            {
                // Cache hit. Read the YAML, parse it through the base
                // fetcher's parser, return as a CatalogFetchResult with the
                // catalogStale info diagnostic so the user knows where the
                // data came from.
                try
                {
                    var text = await File.ReadAllTextAsync(catalogPath, cancellationToken).ConfigureAwait(false);
                    var catalog = ParseFromCache(text);
                    var age = DateTime.UtcNow - fetchedAtUtc;
                    var staleDiagnostic = new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Info,
                        ManagerDiagnosticCodes.CatalogStale,
                        $"Serving '{source.Canonical}' from cache (fetched {FormatAge(age)} ago, threshold {_staleness.TotalHours:F0}h). Run 'catalog refresh' to force re-fetch.");
                    // Re-pin the canonical source for github sources so the
                    // aggregator's visited-set sees a stable identity (mirrors
                    // the base fetcher's behaviour on a network hit). URL
                    // sources have no SHA equivalent — the URL itself is the
                    // pinned identity, so source-as-is is correct.
                    var pinnedSource = meta.CommitSha is { Length: > 0 } sha && source is GitHubCatalogSource gh
                        ? gh with { Ref = sha }
                        : source;
                    // Gate the cached catalog's format version exactly like a fresh fetch — the
                    // cache-hit path returns here without going through the base fetcher.
                    return GateFormatVersion(CatalogFetchResult.Ok(pinnedSource, catalog, text, meta.CommitSha, new[] { staleDiagnostic }));
                }
                catch (Exception ex)
                {
                    // Cache content unreadable — refetch.
                    var corruptDiagnostics = new List<ManagerDiagnostic>
                    {
                        new(ManagerDiagnosticSeverity.Warning,
                            ManagerDiagnosticCodes.CatalogCacheCorrupt,
                            $"Catalog cache content at '{catalogPath}' is unreadable ({ex.Message}); refetching."),
                    };
                    return await FetchFreshAndCache(source, forceRefresh, cacheDir, catalogPath, metaPath, corruptDiagnostics, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return await FetchFreshAndCache(source, forceRefresh, cacheDir, catalogPath, metaPath, new List<ManagerDiagnostic>(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<CatalogFetchResult> FetchFreshAndCache(
        CatalogSource source,
        bool forceRefresh,
        string cacheDir,
        string catalogPath,
        string metaPath,
        List<ManagerDiagnostic> seedDiagnostics,
        CancellationToken cancellationToken)
    {
        var fresh = await base.FetchAsync(source, forceRefresh, cancellationToken).ConfigureAwait(false);
        if (!fresh.Success || fresh.RawText is null)
        {
            // Pass the seed diagnostics + the underlying fetch's diagnostics
            // through; do NOT touch the cache on failure (leaves a previously-
            // good entry in place so the next attempt can still serve it).
            var merged = new List<ManagerDiagnostic>(seedDiagnostics);
            merged.AddRange(fresh.Diagnostics);
            return CatalogFetchResult.Failure(source, merged);
        }

        try
        {
            Directory.CreateDirectory(cacheDir);
            // Write atomically: stage to .tmp + move. Avoids torn writes that
            // would brick the cache on a crash mid-write.
            var catalogTmp = catalogPath + ".tmp";
            var metaTmp = metaPath + ".tmp";
            await File.WriteAllTextAsync(catalogTmp, fresh.RawText, cancellationToken).ConfigureAwait(false);

            var meta = new CatalogCacheMeta
            {
                Canonical = fresh.Source.Canonical,
                FetchedAt = DateTime.UtcNow.ToString("O"),
                CommitSha = fresh.CommitSha ?? string.Empty,
                SourceType = SourceTypeLabel(fresh.Source),
            };
            var serializer = new SerializerBuilder().Build();
            await File.WriteAllTextAsync(metaTmp, serializer.Serialize(meta), cancellationToken).ConfigureAwait(false);

            // Atomic-replace. File.Move with overwrite=true is the cross-
            // platform "rename atomically" primitive.
            File.Move(catalogTmp, catalogPath, overwrite: true);
            File.Move(metaTmp, metaPath, overwrite: true);

            var cacheWrittenDiagnostic = new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.CatalogCacheWritten,
                $"Wrote cache entry for '{fresh.Source.Canonical}' to '{cacheDir}'.");
            var combined = new List<ManagerDiagnostic>(seedDiagnostics);
            combined.AddRange(fresh.Diagnostics);
            combined.Add(cacheWrittenDiagnostic);
            return CatalogFetchResult.Ok(fresh.Source, fresh.Catalog!, fresh.RawText, fresh.CommitSha, combined);
        }
        catch (Exception ex)
        {
            // Cache-write failure isn't fatal — we have valid in-memory data.
            // Surface the failure as info (not error: the install / browse
            // still succeeds) and return the fresh data.
            var combined = new List<ManagerDiagnostic>(seedDiagnostics);
            combined.AddRange(fresh.Diagnostics);
            combined.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CatalogCacheCorrupt,
                $"Could not write catalog cache to '{cacheDir}': {ex.Message}. Continuing with in-memory data."));
            return CatalogFetchResult.Ok(fresh.Source, fresh.Catalog!, fresh.RawText, fresh.CommitSha, combined);
        }
    }

    private string ResolveCacheDir(CatalogSource source)
    {
        // Sanitise the canonical string to a filesystem-safe directory name +
        // append a short hash discriminator so two sources sharing the same
        // 80-char prefix don't collide into the same dir (would silently
        // overwrite each other's cache on the next fetch). The canonical
        // lives ALSO in cache-meta.yaml as the authoritative identifier —
        // this is just a debuggable dir name. Truncated aggressively to keep
        // paths reasonable on Windows (260-char path limit).
        var canonical = source.Canonical;
        var prefixLen = Math.Min(canonical.Length, 80);
        var sanitised = new char[prefixLen];
        for (var i = 0; i < prefixLen; i++)
        {
            var c = canonical[i];
            sanitised[i] = char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_';
        }
        var hashBytes = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(canonical));
        var hashSuffix = Convert.ToHexString(hashBytes, 0, 4).ToLowerInvariant();
        var dirName = new string(sanitised) + "_" + hashSuffix;
        return Path.Combine(_layout.Root, "cache", "catalogs", dirName);
    }

    private static CatalogCacheMeta ReadMeta(string path)
    {
        var text = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<CatalogCacheMeta>(text)
            ?? throw new InvalidOperationException("Parsed cache-meta.yaml as null.");
    }

    private static bool TryParseFetchedAt(string raw, out DateTime utc)
    {
        // `DateTime.ToString("O")` always emits the round-trip-format
        // including an offset, so RoundtripKind alone is the right style.
        // RoundtripKind and AssumeUniversal are mutually exclusive — combining
        // them throws ArgumentException at runtime.
        if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
        {
            utc = dt.ToUniversalTime();
            return true;
        }
        utc = default;
        return false;
    }

    private bool IsFresh(DateTime fetchedAtUtc) => DateTime.UtcNow - fetchedAtUtc < _staleness;

    private static Catalog ParseFromCache(string text)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<Catalog>(text)
            ?? throw new InvalidOperationException("Parsed cached catalog.yaml as null.");
    }

    private static string SourceTypeLabel(CatalogSource s) => s switch
    {
        GitHubCatalogSource => "github",
        FileCatalogSource => "file",
        UrlCatalogSource => "url",
        _ => s.GetType().Name,
    };

    private static string FormatAge(TimeSpan age) => age.TotalHours switch
    {
        < 1 => $"{age.TotalMinutes:F0}m",
        < 24 => $"{age.TotalHours:F1}h",
        _ => $"{age.TotalDays:F1}d",
    };
}

/// <summary>
/// On-disk sidecar next to each cached catalog.yaml. Carries enough metadata
/// for the staleness check + the canonical-source pinning the aggregator
/// needs without re-fetching.
/// </summary>
public sealed class CatalogCacheMeta
{
    [YamlMember(Alias = "canonical")]
    public string Canonical { get; init; } = string.Empty;

    [YamlMember(Alias = "fetchedAt")]
    public string FetchedAt { get; init; } = string.Empty;

    [YamlMember(Alias = "commitSha")]
    public string CommitSha { get; init; } = string.Empty;

    [YamlMember(Alias = "sourceType")]
    public string SourceType { get; init; } = string.Empty;
}
