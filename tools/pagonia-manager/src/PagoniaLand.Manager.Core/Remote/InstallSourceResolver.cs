namespace PagoniaLand.Manager;

/// <summary>
/// Outcome of resolving a remote install-source spec into something
/// <see cref="ModInstaller"/> can consume. Frontend-agnostic: it carries
/// <see cref="ManagerDiagnostic"/>s for the caller to render (the scripted
/// command prints them, the interactive wizard renders them through Spectre)
/// rather than writing to any console itself.
/// </summary>
public sealed class RemoteSourceResolution
{
    /// <summary>The fetch failed or was refused — do not install; the caller
    /// surfaces <see cref="Diagnostics"/> and bails.</summary>
    public bool Aborted { get; init; }

    /// <summary>mod.io returned a Map-type mod: a clean success with nothing to
    /// install (maps are handled in-game). <see cref="MapModName"/> names it.</summary>
    public bool MapTypeSkipped { get; init; }
    public string? MapModName { get; init; }

    /// <summary>Directory (or .zip) to hand to <see cref="ModInstaller"/>.</summary>
    public string? InstallSource { get; init; }

    /// <summary>Provenance string recorded in the install sidecar's
    /// <c>source</c> field (the pinned <c>gh:</c> / <c>modio:</c> / <c>url:</c>
    /// identifier).</summary>
    public string? RemoteProvenance { get; init; }

    /// <summary>Temp directory the fetch staged into — the caller deletes it
    /// after <see cref="ModInstaller"/> has copied what it needs.</summary>
    public string? TempDir { get; init; }

    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>
/// Resolves a remote install-source spec (<c>gh:</c>, <c>modio:</c>, or a direct
/// <c>https?://…zip</c> URL) into a local directory ModInstaller can install
/// from. Shared by the scripted <c>install</c> command and the interactive
/// Install-a-Mod wizard so both accept the same source forms with identical
/// fetch, provenance, insecure-HTTP gating, and archive-drift behaviour. Pure
/// orchestration over the Core fetchers with no console I/O, so any frontend
/// (CLI today, GUI later) reuses it; the <see cref="IRemoteContentFetcher"/> is
/// injected so the dispatch is testable against an in-memory fake.
/// </summary>
public static class InstallSourceResolver
{
    /// <summary>
    /// Returns <c>null</c> when <paramref name="spec"/> is not a remote source
    /// (the caller installs it as a local folder / zip path). Otherwise fetches
    /// the source and returns the resolution.
    /// </summary>
    public static RemoteSourceResolution? ResolveRemote(
        string spec,
        StoreLayout layout,
        IRemoteContentFetcher http,
        bool allowInsecureSources)
    {
        if (!RemoteSourceParser.TryParse(spec, out var parsed))
        {
            return null;
        }

        var diagnostics = new List<ManagerDiagnostic>();

        if (parsed is GitHubSource gh)
        {
            var fetch = new RemoteFetcher(http).FetchMod(gh);
            diagnostics.AddRange(fetch.Diagnostics);
            if (!fetch.Success || fetch.TempDirectory is null)
            {
                return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
            }
            return new RemoteSourceResolution
            {
                InstallSource = fetch.TempDirectory,
                TempDir = fetch.TempDirectory,
                RemoteProvenance = fetch.ResolvedSource,
                Diagnostics = diagnostics,
            };
        }

        if (parsed is DirectUrlSource directUrl)
        {
            // http:// requires an explicit opt-in. The warning fires either way
            // (so log scans pick up "installed over plain HTTP"); the install
            // only proceeds when state.yaml has allowInsecureSources: true.
            if (directUrl.IsHttp)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.DirectUrlInsecureHttp,
                    $"Install source '{directUrl.Url}' is plain HTTP — content can be tampered with in transit. Use https:// when possible."));
                if (!allowInsecureSources)
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Error,
                        ManagerDiagnosticCodes.DirectUrlInsecureHttp,
                        "Refusing to install over plain HTTP. Set 'allowInsecureSources: true' in state.yaml to opt in (or use the https:// equivalent)."));
                    return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
                }
            }

            var fetch = new DirectUrlFetcher(http).Fetch(directUrl);
            diagnostics.AddRange(fetch.Diagnostics);
            if (!fetch.Success || fetch.TempRoot is null || fetch.ModRootDirectory is null)
            {
                return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
            }

            diagnostics.AddRange(DriftDiagnostics(layout, directUrl.Url, fetch.ArchiveSha256));
            return new RemoteSourceResolution
            {
                InstallSource = fetch.ModRootDirectory,
                TempDir = fetch.TempRoot,
                RemoteProvenance = fetch.ResolvedSource,
                Diagnostics = diagnostics,
            };
        }

        if (parsed is ModIoSource modio)
        {
            var modIoResult = new ModIoFetcher(http).Fetch(modio);
            diagnostics.AddRange(modIoResult.Diagnostics);
            if (!modIoResult.Success)
            {
                return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
            }
            if (modIoResult.IsMapType)
            {
                return new RemoteSourceResolution { MapTypeSkipped = true, MapModName = modIoResult.ModName, Diagnostics = diagnostics };
            }
            if (string.IsNullOrEmpty(modIoResult.BinaryUrl))
            {
                return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
            }

            // mod.io pre-signed download URLs are always https; chain into the
            // DirectUrlFetcher for the actual download + unpack.
            var pseudoDirect = new DirectUrlSource(modIoResult.BinaryUrl, IsHttp: false);
            var downloadResult = new DirectUrlFetcher(http).Fetch(pseudoDirect);
            diagnostics.AddRange(downloadResult.Diagnostics);
            if (!downloadResult.Success || downloadResult.TempRoot is null || downloadResult.ModRootDirectory is null)
            {
                return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
            }

            // Record modio: provenance, not the expiring signed-URL identifier
            // DirectUrlFetcher would have produced.
            var versionFragment = string.IsNullOrEmpty(modIoResult.Version) ? "" : $"#{modIoResult.Version}";
            return new RemoteSourceResolution
            {
                InstallSource = downloadResult.ModRootDirectory,
                TempDir = downloadResult.TempRoot,
                RemoteProvenance = $"modio:{modIoResult.GameId}/{modIoResult.ModId}{versionFragment}",
                Diagnostics = diagnostics,
            };
        }

        return new RemoteSourceResolution { Aborted = true, Diagnostics = diagnostics };
    }

    // Drift detection: scan existing installs for any whose sidecar source names
    // the same URL but a different SHA. Non-blocking info diagnostics — the new
    // bytes still install.
    private static IEnumerable<ManagerDiagnostic> DriftDiagnostics(StoreLayout layout, string url, string? newSha)
    {
        if (string.IsNullOrWhiteSpace(newSha)) { yield break; }

        var prefix = $"url:{url}#";
        foreach (var mod in new ModLister().List(layout))
        {
            if (mod.Source is null || !mod.Source.StartsWith(prefix, StringComparison.Ordinal)) { continue; }
            var existingSha = mod.Source[prefix.Length..];
            if (string.Equals(existingSha, newSha, StringComparison.OrdinalIgnoreCase)) { continue; }
            yield return new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.DirectUrlArchiveDrift,
                $"Archive at '{url}' has drifted since the previous install of '{mod.Id}@{mod.Version}': previous sha-prefix {existingSha[..16]}..., current sha-prefix {newSha[..16]}.... Not blocking the install.");
        }
    }
}
