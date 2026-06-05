using System.Text.Json;

namespace PagoniaLand.Manager;

/// <summary>
/// Translates a <see cref="ModIoSource"/> into a pre-signed download URL by
/// calling the mod.io REST API + checks the mod's tags to detect maps (which
/// the game handles natively via its in-game UGC list, not via the manager).
/// Intentionally thin: ZIP download + extraction is delegated to
/// <see cref="DirectUrlFetcher"/>, so this class only owns the API surface
/// and the slug-alias / map-skip / rate-limit semantics that are
/// mod.io-specific.
/// </summary>
public sealed class ModIoFetcher
{
    /// <summary>
    /// Read-only mod.io API key shipped embedded in the manager binary. mod.io
    /// keys for GET endpoints are intentionally public — the same pattern
    /// iModYourAnno + Anno 117 Mod Manager use. Personal OAuth tokens are
    /// only required for user-account actions (subscribe / rate / upload)
    /// which the manager doesn't perform.
    /// <para>
    /// This is a read-only key registered at https://mod.io/apikey/widget —
    /// it only authorizes public GET requests, so embedding it in the client
    /// is safe. To override it (custom key for testing / development), set the
    /// <c>PAGONIA_MODIO_API_KEY</c> env var. If this is ever blanked out and
    /// no env var is set, the fetcher refuses to run with a clear
    /// <see cref="ManagerDiagnosticCodes.ModIoApiError"/> message.
    /// </para>
    /// </summary>
    public const string DefaultApiKey = "8663a18b73cc46ce3c7f206ae7cfaa03";

    public const string ApiKeyEnvironmentVariable = "PAGONIA_MODIO_API_KEY";

    private const string ApiBase = "https://api.mod.io/v1";

    private readonly IRemoteContentFetcher _http;
    private readonly string? _apiKey;

    /// <param name="environment">Reader for the <c>PAGONIA_MODIO_API_KEY</c> env
    /// var. Defaults to <see cref="Environment.GetEnvironmentVariable(string)"/>;
    /// a GUI Settings dialog can inject a closure that reads its own settings store
    /// instead, so overriding the key doesn't require mutating process-level env
    /// (which would leak into spawned subprocesses). Mirrors the injectable reader
    /// <see cref="StoreRootResolver.Resolve"/> already accepts.</param>
    public ModIoFetcher(IRemoteContentFetcher http, string? apiKeyOverride = null, Func<string, string?>? environment = null)
    {
        _http = http;
        _apiKey = ResolveApiKey(apiKeyOverride, environment ?? Environment.GetEnvironmentVariable);
    }

    public ModIoFetchResult Fetch(ModIoSource source, CancellationToken cancellationToken = default)
        => FetchAsync(source, cancellationToken).GetAwaiter().GetResult();

    public async Task<ModIoFetchResult> FetchAsync(ModIoSource source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (string.IsNullOrEmpty(_apiKey))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                $"No mod.io API key configured. Set the {ApiKeyEnvironmentVariable} env var to a read-only key from https://mod.io/apikey/widget."));
            return ModIoFetchResult.Failure(diagnostics);
        }

        // Resolve the game segment via ModIoGameAliases (accepts the
        // numeric id "8242" or the slug "pioneers-of-pagonia"); anything
        // else surfaces modIoUnknownGameAlias.
        if (!TryResolveGameId(source.Game, out var gameId, out var aliasError))
        {
            diagnostics.Add(aliasError!);
            return ModIoFetchResult.Failure(diagnostics);
        }

        // GET the mod metadata. Two ways the API key rides along:
        // (1) header Authorization: Bearer <key>, (2) ?api_key=<key> query
        // string. Read-only ops conventionally use the query-string form.
        var url = $"{ApiBase}/games/{gameId}/mods/{source.ModId}?api_key={Uri.EscapeDataString(_apiKey!)}";
        RemoteFetchedContent? response;
        try
        {
            response = await _http.TryFetchAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Rate-limit comes back as 429; TryFetchAsync's EnsureSuccessStatusCode
            // would have thrown an HttpRequestException with the status code in
            // the message. Detect and re-frame as a warning rather than an error
            // so callers can implement back-off without crashing.
            if (ex is HttpRequestException httpEx && httpEx.StatusCode is System.Net.HttpStatusCode.TooManyRequests)
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModIoRateLimited,
                    $"mod.io rate-limited the request to '{url}'. Try again later, or use your own key via {ApiKeyEnvironmentVariable} for an isolated rate-limit bucket."));
                return ModIoFetchResult.Failure(diagnostics);
            }
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                $"mod.io API call to '{url}' failed: {ex.Message}"));
            return ModIoFetchResult.Failure(diagnostics);
        }

        if (response is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                $"mod.io returned 404 for game={gameId} mod={source.ModId}. Check both numeric ids on https://mod.io/g/<game>/m/<mod>."));
            return ModIoFetchResult.Failure(diagnostics);
        }

        // Parse the JSON. We only need a few fields — tags[].name for the
        // type check, modfile.download.binary_url for the actual download,
        // optionally modfile.filehash.md5 for future drift detection, and
        // a couple of display fields for diagnostics.
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(response.Text);
        }
        catch (Exception ex)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                $"Could not parse mod.io response for game={gameId} mod={source.ModId}: {ex.Message}"));
            return ModIoFetchResult.Failure(diagnostics);
        }

        try
        {
            var root = doc.RootElement;
            var modName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? source.ModId : source.ModId;

            // Type / category detection. mod.io exposes the per-game tag
            // taxonomy through the `tags` array. For Pioneers of Pagonia the
            // only configured type today is "Map" — those are managed by the
            // game's in-game UGC subscription, NOT by this manager. Bail
            // cleanly with an info diagnostic so the user sees why nothing
            // was installed.
            var isMap = false;
            if (root.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsEl.EnumerateArray())
                {
                    if (tag.TryGetProperty("name", out var tagName)
                        && string.Equals(tagName.GetString(), "Map", StringComparison.OrdinalIgnoreCase))
                    {
                        isMap = true;
                        break;
                    }
                }
            }

            if (isMap)
            {
                diagnostics.Add(Info(ManagerDiagnosticCodes.ModIoMapTypeSkipped,
                    $"mod.io mod '{modName}' (game={gameId} mod={source.ModId}) is tagged as Map. " +
                    "Maps are managed in-game via the UGC subscription list (settings → mods → mod.io), " +
                    "not via 'pagonia-manager install'. No files were downloaded."));
                return ModIoFetchResult.MapSkip(gameId, source.ModId, modName, diagnostics);
            }

            // Pull the modfile metadata. mod.io ships exactly one "current"
            // modfile per mod in the /mods/{id} response; for version pinning
            // we'd hit /mods/{id}/files separately (deferred — see version
            // handling below).
            if (!root.TryGetProperty("modfile", out var modfileEl) || modfileEl.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                    $"mod.io mod '{modName}' (game={gameId} mod={source.ModId}) has no current modfile to download."));
                return ModIoFetchResult.Failure(diagnostics);
            }

            string? binaryUrl = null;
            string? md5 = null;
            string? filename = null;
            string? version = null;
            if (modfileEl.TryGetProperty("download", out var dlEl)
                && dlEl.ValueKind == JsonValueKind.Object
                && dlEl.TryGetProperty("binary_url", out var binUrlEl))
            {
                binaryUrl = binUrlEl.GetString();
            }
            if (modfileEl.TryGetProperty("filehash", out var hashEl)
                && hashEl.ValueKind == JsonValueKind.Object
                && hashEl.TryGetProperty("md5", out var md5El))
            {
                md5 = md5El.GetString();
            }
            if (modfileEl.TryGetProperty("filename", out var nameEl2))
            {
                filename = nameEl2.GetString();
            }
            if (modfileEl.TryGetProperty("version", out var versionEl))
            {
                version = versionEl.GetString();
            }

            if (string.IsNullOrEmpty(binaryUrl))
            {
                diagnostics.Add(Error(ManagerDiagnosticCodes.ModIoApiError,
                    $"mod.io mod '{modName}' (game={gameId} mod={source.ModId}) modfile has no binary_url. The download link may have expired — retry the install."));
                return ModIoFetchResult.Failure(diagnostics);
            }

            // If the user pinned a version (modio:.../#0.1.0) but the current
            // modfile is a different version, surface an info diagnostic
            // explaining that we're installing the latest (version-pinning
            // proper is deferred to a follow-up). Doesn't block the install —
            // the user explicitly asked for THIS mod, and the latest version
            // is what mod.io offered.
            if (!string.IsNullOrEmpty(source.Version)
                && !string.IsNullOrEmpty(version)
                && !string.Equals(source.Version, version, StringComparison.Ordinal))
            {
                diagnostics.Add(Info(ManagerDiagnosticCodes.ModIoVersionPinNotImplemented,
                    $"Requested version '{source.Version}' but mod.io's current modfile is '{version}'. Version-pinning isn't implemented yet; installing the current version."));
            }

            return ModIoFetchResult.Ok(
                gameId,
                source.ModId,
                version ?? source.Version ?? string.Empty,
                modName,
                binaryUrl,
                filename ?? "modio-mod.zip",
                md5,
                diagnostics);
        }
        finally
        {
            doc.Dispose();
        }
    }

    private static bool TryResolveGameId(string game, out string numericId, out ManagerDiagnostic? error)
    {
        error = null;
        if (ModIoGameAliases.TryResolve(game, out numericId))
        {
            return true;
        }
        error = Error(ManagerDiagnosticCodes.ModIoUnknownGameAlias,
            $"Unknown mod.io game '{game}'. Accepted: {ModIoGameAliases.Describe()}.");
        return false;
    }

    private static string? ResolveApiKey(string? overrideValue, Func<string, string?> environment)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue)) { return overrideValue; }
        var fromEnv = environment(ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv)) { return fromEnv; }
        if (!string.IsNullOrWhiteSpace(DefaultApiKey)) { return DefaultApiKey; }
        return null;
    }

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);
}

/// <summary>
/// Result of <see cref="ModIoFetcher.FetchAsync"/>. Success carries the
/// pre-signed download URL the CLI hands to <see cref="DirectUrlFetcher"/>;
/// the <see cref="IsMapType"/> branch is also "success" semantically — the
/// fetcher did its job, the mod just isn't installable by this manager.
/// </summary>
public sealed class ModIoFetchResult
{
    public bool Success { get; init; }
    public bool IsMapType { get; init; }
    public string? GameId { get; init; }
    public string? ModId { get; init; }
    public string? Version { get; init; }
    public string? ModName { get; init; }
    public string? BinaryUrl { get; init; }
    public string? Filename { get; init; }
    public string? Md5 { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();

    public static ModIoFetchResult Ok(
        string gameId,
        string modId,
        string version,
        string modName,
        string binaryUrl,
        string filename,
        string? md5,
        IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = true,
            IsMapType = false,
            GameId = gameId,
            ModId = modId,
            Version = version,
            ModName = modName,
            BinaryUrl = binaryUrl,
            Filename = filename,
            Md5 = md5,
            Diagnostics = diagnostics,
        };

    public static ModIoFetchResult MapSkip(string gameId, string modId, string modName, IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new()
        {
            Success = true,
            IsMapType = true,
            GameId = gameId,
            ModId = modId,
            ModName = modName,
            Diagnostics = diagnostics,
        };

    public static ModIoFetchResult Failure(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new() { Success = false, Diagnostics = diagnostics };
}
