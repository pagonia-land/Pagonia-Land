using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>Outcome of an assisted dependency pull.</summary>
public sealed class AssistedDependencyResult
{
    /// <summary>Dependency ids freshly installed by this pull (transitive), in install order.</summary>
    public IReadOnlyList<string> InstalledDependencies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>
/// After a mod is installed from a <c>gh:</c> repo, pulls its missing <c>dependencies</c> — and
/// theirs, transitively — so the user doesn't have to hand-install each one. For each missing
/// dependency it resolves a source, preferring the <b>same repo</b> the mod came from (the common
/// "a mod and its sibling deps live together" case) and falling back to a search across the user's
/// <b>subscribed catalogs</b>; then installs it through the normal remote-install path and recurses on
/// that dependency's own declarations. A visited set guards against dependency cycles. A dependency
/// that can't be resolved anywhere is a per-dep warning (<c>manager.modDependencyUnresolved</c>) — the
/// pull continues with the rest rather than failing wholesale.
///
/// <para>Opt-in: the caller decides whether to run it (the CLI's <c>install --with-deps</c>, or the
/// interactive "install its dependencies too?" confirm). This service does no prompting.</para>
/// </summary>
public sealed class AssistedDependencyInstaller
{
    private readonly IRemoteContentFetcher _http;
    private readonly bool _allowInsecureSources;

    public AssistedDependencyInstaller(IRemoteContentFetcher http, bool allowInsecureSources)
    {
        _http = http;
        _allowInsecureSources = allowInsecureSources;
    }

    /// <param name="sameRepo">The repo the root mod came from — tried first for each dependency.
    /// Null for a non-<c>gh:</c> install (local / zip / mod.io), where only the catalog search applies.</param>
    public AssistedDependencyResult InstallMissing(
        StoreLayout layout,
        IReadOnlyList<string> directDependencies,
        GitHubSource? sameRepo,
        IReadOnlyList<CatalogSource> catalogSubscriptions,
        int? catalogMaxDepth,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var pulled = new List<string>();
        var installedIds = new HashSet<string>(
            new ModLister().List(layout).Select(m => m.Id), StringComparer.Ordinal);

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(directDependencies);

        // Catalog repos are aggregated lazily — only if a dependency isn't found in the same repo.
        IReadOnlyList<AggregatedRepo>? catalogRepos = null;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var depId = queue.Dequeue();
            if (!visited.Add(depId) || installedIds.Contains(depId))
            {
                continue;
            }

            var spec = ResolveDependencySpec(depId, sameRepo, catalogSubscriptions, catalogMaxDepth,
                ref catalogRepos, diagnostics, cancellationToken);
            if (spec is null)
            {
                var where = sameRepo is null
                    ? "your subscribed catalogs"
                    : $"{sameRepo.Owner}/{sameRepo.Repo} or your subscribed catalogs";
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModDependencyUnresolved,
                    $"Couldn't resolve dependency '{depId}' from {where} — install it manually."));
                continue;
            }

            var resolution = InstallSourceResolver.ResolveRemote(spec, layout, _http, _allowInsecureSources);
            if (resolution is null || resolution.Aborted || resolution.InstallSource is null)
            {
                diagnostics.AddRange((resolution?.Diagnostics ?? Array.Empty<ManagerDiagnostic>())
                    .Where(d => d.Severity == ManagerDiagnosticSeverity.Error));
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModDependencyUnresolved,
                    $"Couldn't fetch dependency '{depId}' from '{spec}'."));
                continue;
            }

            InstallResult install;
            try
            {
                install = new ModInstaller().Install(resolution.InstallSource, layout, resolution.RemoteProvenance);
            }
            finally
            {
                if (resolution.TempDir is not null && Directory.Exists(resolution.TempDir))
                {
                    try { Directory.Delete(resolution.TempDir, recursive: true); } catch { /* best-effort */ }
                }
            }

            if (install.Outcome == InstallOutcome.Failed || string.IsNullOrWhiteSpace(install.Version))
            {
                diagnostics.AddRange(install.Diagnostics.Where(d => d.Severity == ManagerDiagnosticSeverity.Error));
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModDependencyUnresolved,
                    $"Installing dependency '{depId}' failed; install it manually."));
                continue;
            }

            installedIds.Add(depId);
            if (install.Outcome == InstallOutcome.Installed)
            {
                pulled.Add(depId);
                diagnostics.Add(Info(ManagerDiagnosticCodes.ModDependencyInstalled,
                    $"Installed dependency '{depId}@{install.Version}'."));
            }

            // Recurse: enqueue the just-installed dependency's own missing dependencies.
            var read = new ManifestReader().ReadMod(layout.ModVersionDirectory(depId, install.Version!));
            if (read.Value is not null)
            {
                foreach (var sub in read.Value.Manifest.Dependencies)
                {
                    if (!installedIds.Contains(sub) && !visited.Contains(sub))
                    {
                        queue.Enqueue(sub);
                    }
                }
            }
        }

        return new AssistedDependencyResult { InstalledDependencies = pulled, Diagnostics = diagnostics };
    }

    /// <summary>A <c>gh:</c> install spec for <paramref name="depId"/>, preferring the same repo, then
    /// any subscribed-catalog repo whose index lists it. Null when nothing offers it.</summary>
    private string? ResolveDependencySpec(
        string depId,
        GitHubSource? sameRepo,
        IReadOnlyList<CatalogSource> catalogSubscriptions,
        int? catalogMaxDepth,
        ref IReadOnlyList<AggregatedRepo>? catalogRepos,
        List<ManagerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        // 1. Same repo as the mod that needs it (when it came from one). Use the *same ref* the root
        // mod was pinned to, not HEAD — installing a mod pinned to a tag/branch with --with-deps must
        // pull its siblings from that same ref, or the dependency set is a silent version skew.
        if (sameRepo is not null
            && RepoListsMod(new GitHubSource(sameRepo.Owner, sameRepo.Repo, sameRepo.Ref, null, sameRepo.BasePath), depId, cancellationToken))
        {
            var refSegment = string.IsNullOrEmpty(sameRepo.Ref) || string.Equals(sameRepo.Ref, "HEAD", StringComparison.Ordinal)
                ? string.Empty
                : $"#{sameRepo.Ref}";
            return $"gh:{sameRepo.Owner}/{sameRepo.Repo}{BaseSegment(sameRepo.BasePath)}{refSegment}/{depId}";
        }

        // 2. Subscribed catalogs (aggregated once, lazily).
        if (catalogSubscriptions.Count > 0)
        {
            catalogRepos ??= new CatalogAggregator(new CatalogFetcher(_http))
                .Aggregate(catalogSubscriptions, catalogMaxDepth, cancellationToken: cancellationToken)
                .Repos;

            foreach (var repo in catalogRepos)
            {
                if (sameRepo is not null
                    && string.Equals(repo.Owner, sameRepo.Owner, StringComparison.Ordinal)
                    && string.Equals(repo.Repo, sameRepo.Repo, StringComparison.Ordinal))
                {
                    continue; // already tried as the same repo
                }
                if (RepoListsMod(new GitHubSource(repo.Owner, repo.Repo, "HEAD", null, repo.IndexPath), depId, cancellationToken))
                {
                    return $"gh:{repo.Owner}/{repo.Repo}{BaseSegment(repo.IndexPath)}/{depId}";
                }
            }
        }

        return null;
    }

    private bool RepoListsMod(GitHubSource repo, string modId, CancellationToken cancellationToken)
    {
        try
        {
            var fetch = new RepoIndexFetcher(_http).Fetch(repo, cancellationToken);
            return fetch is { Success: true, HasIndex: true, Index: not null }
                && fetch.Index.Mods.Any(m => string.Equals(m.Id, modId, StringComparison.Ordinal));
        }
        catch
        {
            return false; // an unreachable repo just means "not found here"
        }
    }

    private static string BaseSegment(string basePath)
        => string.IsNullOrEmpty(basePath) ? string.Empty : $":{basePath}";

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);
}
