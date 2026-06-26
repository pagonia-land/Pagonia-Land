using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Fetches a <see cref="Catalog"/> from a <see cref="CatalogSource"/>. Two
/// transports today: <see cref="GitHubCatalogSource"/> goes through the same
/// <see cref="IRemoteContentFetcher"/> the mod / collection fetchers use,
/// so production hits raw.githubusercontent.com and tests substitute the
/// in-memory fake. <see cref="FileCatalogSource"/> reads from disk directly
/// — used by the bundled example catalog, offline workshops, LAN-hosted
/// classroom setups, and aggregator tests.
/// </summary>
public class CatalogFetcher
{
    // YamlDotNet reflects over these types via Deserialize<Catalog>(). Without
    // these pins the AOT trimmer strips their property setters, so a published
    // (Native AOT) binary throws "Exception during deserialization" on any
    // catalog parse even though it works under JIT. Mirrors the rooting pattern
    // every other YAML-backed model uses (ProfileStore, DeployService, ...).
    // CachingCatalogFetcher chains this ctor via base(), so the Catalog group is
    // rooted for the cache-read path too — only its CatalogCacheMeta is pinned
    // separately there.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    private readonly IRemoteContentFetcher _httpFetcher;
    private readonly bool _allowInsecureCatalogSources;

    [DynamicDependency(Shape, typeof(Catalog))]
    [DynamicDependency(Shape, typeof(CatalogMetadata))]
    [DynamicDependency(Shape, typeof(CatalogRepoEntry))]
    [DynamicDependency(Shape, typeof(CatalogReference))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<CatalogRepoEntry>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<CatalogReference>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<string>))]
    public CatalogFetcher(IRemoteContentFetcher httpFetcher, bool allowInsecureCatalogSources = false)
    {
        _httpFetcher = httpFetcher;
        _allowInsecureCatalogSources = allowInsecureCatalogSources;
    }

    public CatalogFetchResult Fetch(CatalogSource source, bool forceRefresh = false, CancellationToken cancellationToken = default)
        => FetchAsync(source, forceRefresh, cancellationToken).GetAwaiter().GetResult();

    /// <summary>
    /// Fetch + parse a catalog. The <paramref name="forceRefresh"/> flag is
    /// ignored by the base fetcher (it always hits the network for gh: /
    /// http(s): sources and the disk for file:) — subclasses like
    /// <c>CachingCatalogFetcher</c> use it to skip cache reads while still
    /// updating the cache on success.
    /// </summary>
    public virtual async Task<CatalogFetchResult> FetchAsync(CatalogSource source, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        _ = forceRefresh; // base fetcher has no cache; subclasses observe.
        var result = source switch
        {
            GitHubCatalogSource gh => await FetchGitHubAsync(gh, cancellationToken).ConfigureAwait(false),
            FileCatalogSource file => FetchFile(file),
            UrlCatalogSource url => await FetchUrlAsync(url, cancellationToken).ConfigureAwait(false),
            _ => CatalogFetchResult.Failure(
                source,
                new[] { Error(ManagerDiagnosticCodes.CatalogFetchFailed, $"Unsupported catalog source type: {source.GetType().Name}.") }),
        };
        return GateFormatVersion(result);
    }

    /// <summary>
    /// Apply the shared format-version policy to a parsed catalog result. A newer-minor
    /// catalog reads with an info note; a newer/retired major or malformed version turns a
    /// successful fetch into a failure carrying the actionable diagnostic. The cache-read
    /// path in <c>CachingCatalogFetcher</c> reuses this so a cached newer-major catalog is
    /// gated identically to a freshly-fetched one.
    /// </summary>
    protected static CatalogFetchResult GateFormatVersion(CatalogFetchResult result)
    {
        if (!result.Success || result.Catalog is null)
        {
            return result;
        }

        var diagnostics = new List<ManagerDiagnostic>(result.Diagnostics);
        return FormatVersionGate.TryAcceptCatalog(result.Catalog.CatalogFormatVersion, diagnostics)
            ? CatalogFetchResult.Ok(result.Source, result.Catalog, result.RawText!, result.CommitSha, diagnostics)
            : CatalogFetchResult.Failure(result.Source, diagnostics);
    }

    private async Task<CatalogFetchResult> FetchGitHubAsync(GitHubCatalogSource source, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        // Resolve ref -> commit SHA so the cache + canonical-source string
        // pins exact code. Mirrors the mod + collection fetchers.
        string commitSha;
        try
        {
            var resolved = await _httpFetcher.ResolveCommitShaAsync(source.Owner, source.Repo, source.Ref, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                    $"Catalog ref '{source.Ref}' not found in {source.Owner}/{source.Repo}."));
                return CatalogFetchResult.Failure(source, diagnostics);
            }
            commitSha = resolved;
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Resolving catalog ref '{source.Ref}' in {source.Owner}/{source.Repo} failed: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        // source.Path is already restricted to a URL-safe charset at parse time
        // (CatalogSourceParser.IsValidRepoPath); escape each segment anyway so a
        // future parser change can't silently produce a malformed URL. Splitting
        // on '/' keeps the path separators intact while escaping the segments.
        var escapedPath = string.Join('/', Array.ConvertAll(source.Path.Split('/'), Uri.EscapeDataString));
        var url = $"https://raw.githubusercontent.com/{source.Owner}/{source.Repo}/{commitSha}/{escapedPath}";
        RemoteFetchedContent? content;
        try
        {
            content = await _httpFetcher.TryFetchAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Fetching catalog from {url} failed: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }
        if (content is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Catalog not found at {url}."));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        Catalog? parsed;
        try
        {
            parsed = ParseCatalogYaml(content.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogMalformed,
                $"Could not parse catalog at {url}: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        // For GitHub catalogs we always re-canonicalise the source with the
        // resolved SHA so the aggregator's cycle-detection visited-set keeps
        // a stable identity even when the user originally asked for "main".
        var pinned = source with { Ref = commitSha };
        return CatalogFetchResult.Ok(pinned, parsed, content.Text, commitSha, diagnostics);
    }

    private async Task<CatalogFetchResult> FetchUrlAsync(UrlCatalogSource source, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        // http:// (no TLS) — surface a warning unless explicitly opted in.
        // We don't block the fetch: catalog reads are read-only, the
        // worst-case is a tampered repo listing that the user reviews before
        // installing anything from it. The warning is informational so
        // pinned-trust deployments (LAN, intranet) can flip the flag once.
        if (source.IsInsecure && !_allowInsecureCatalogSources)
        {
            diagnostics.Add(Warning(ManagerDiagnosticCodes.CatalogInsecureHttp,
                $"Catalog source '{source.SourceUri}' uses plain http. Set state.yaml.allowInsecureCatalogSources: true to silence this warning."));
        }

        var url = source.SourceUri.AbsoluteUri;
        RemoteFetchedContent? content;
        try
        {
            content = await _httpFetcher.TryFetchAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Fetching catalog from {url} failed: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }
        if (content is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Catalog not found at {url}."));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        Catalog parsed;
        try
        {
            parsed = ParseCatalogYaml(content.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogMalformed,
                $"Could not parse catalog at {url}: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        // No commit-SHA pinning available — the URL itself is the canonical
        // identity. The aggregator's visited-set keys on Canonical, which
        // url-normalises scheme + host (see UrlCatalogSource.Canonical).
        return CatalogFetchResult.Ok(source, parsed, content.Text, commitSha: null, diagnostics);
    }

    private static CatalogFetchResult FetchFile(FileCatalogSource source)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (!File.Exists(source.AbsolutePath))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Local catalog not found at {source.AbsolutePath}."));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        string text;
        try
        {
            text = File.ReadAllText(source.AbsolutePath);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"Reading local catalog '{source.AbsolutePath}' failed: {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        Catalog parsed;
        try
        {
            parsed = ParseCatalogYaml(text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogMalformed,
                $"Could not parse catalog at '{source.AbsolutePath}': {ex.Message}"));
            return CatalogFetchResult.Failure(source, diagnostics);
        }

        return CatalogFetchResult.Ok(source, parsed, text, commitSha: null, diagnostics);
    }

    private static Catalog ParseCatalogYaml(string text)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        var catalog = deserializer.Deserialize<Catalog>(text)
            ?? throw new InvalidOperationException("Parsed catalog.yaml as null.");
        return catalog;
    }

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);
}

/// <summary>
/// Result of a single catalog fetch. On success carries the parsed
/// <see cref="Catalog"/> plus the raw YAML text (so callers can cache it
/// verbatim) and, for GitHub sources, the resolved commit SHA. The
/// <see cref="Source"/> field always reflects the SHA-pinned canonical
/// source so the aggregator's visited-set sees a stable identity.
/// </summary>
public sealed class CatalogFetchResult
{
    public bool Success { get; init; }
    public required CatalogSource Source { get; init; }
    public Catalog? Catalog { get; init; }
    public string? RawText { get; init; }
    public string? CommitSha { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static CatalogFetchResult Ok(
        CatalogSource source,
        Catalog catalog,
        string rawText,
        string? commitSha,
        IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = true,
            Source = source,
            Catalog = catalog,
            RawText = rawText,
            CommitSha = commitSha,
            Diagnostics = diagnostics,
        };

    public static CatalogFetchResult Failure(CatalogSource source, IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = false,
            Source = source,
            Diagnostics = diagnostics,
        };
}
