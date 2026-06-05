namespace PagoniaLand.Manager;

/// <summary>
/// Walks a set of subscribed catalogs + every catalog they federate to,
/// recursively, and flattens the whole graph into one deduplicated list
/// of mod-distribution repos. Cycle detection (visited-set on the
/// canonical source string), depth cap (default 5), and dedup on
/// <c>(owner, repo)</c> with a trust-signal list of every vouching
/// catalog. The aggregator is read-only — it doesn't mutate state or
/// write to disk.
/// </summary>
public sealed class CatalogAggregator
{
    public const int DefaultMaxDepth = 5;

    private readonly CatalogFetcher _fetcher;

    public CatalogAggregator(CatalogFetcher fetcher)
    {
        _fetcher = fetcher;
    }

    public CatalogAggregateResult Aggregate(
        IEnumerable<CatalogSource> subscriptions,
        int? maxDepthOverride = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
        => AggregateAsync(subscriptions, maxDepthOverride, forceRefresh, cancellationToken).GetAwaiter().GetResult();

    public async Task<CatalogAggregateResult> AggregateAsync(
        IEnumerable<CatalogSource> subscriptions,
        int? maxDepthOverride = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var maxDepth = (maxDepthOverride is > 0) ? maxDepthOverride.Value : DefaultMaxDepth;
        var diagnostics = new List<ManagerDiagnostic>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // Repo dedup uses a stable (owner/repo) key, case-sensitively to match
        // GitHub's API behaviour. The first catalog to vouch for a given repo
        // contributes the summary + tags; later catalogs only extend the
        // VouchedBy list. Trust signal: more vouches = more catalogs pointing
        // at the same repo.
        var reposByKey = new Dictionary<string, AggregatedRepo>(StringComparer.Ordinal);
        var visitedSources = new List<CatalogSource>();

        // BFS queue carries (source, depthFromRoot, parentDir-for-relative-file).
        // parentDir matters because a catalog can include a federated
        // file:./sub/child.yaml that should resolve against the catalog file's
        // own directory, not the running process's cwd.
        var queue = new Queue<PendingFetch>();
        foreach (var sub in subscriptions)
        {
            queue.Enqueue(new PendingFetch(sub, Depth: 0, ParentDirectory: null));
        }

        while (queue.Count > 0)
        {
            var (source, depth, parentDir) = queue.Dequeue();
            cancellationToken.ThrowIfCancellationRequested();

            // Cycle check on canonical source. A → B → A bails on the second
            // visit; surface as info (the user still sees what came from the
            // first hop, but they're warned about the loop in their graph).
            if (!visited.Add(source.Canonical))
            {
                diagnostics.Add(Info(ManagerDiagnosticCodes.CatalogCycleDetected,
                    $"Catalog cycle detected — '{source.Canonical}' was already visited; skipping the second occurrence."));
                continue;
            }

            if (depth >= maxDepth)
            {
                diagnostics.Add(Warning(ManagerDiagnosticCodes.CatalogDepthCapped,
                    $"Catalog depth cap ({maxDepth}) reached at '{source.Canonical}' — descendant catalogs not followed."));
                // Still fetch THIS catalog (it's at the cap, not past it),
                // just don't enqueue its sub-catalogs.
            }

            var fetch = await _fetcher.FetchAsync(source, forceRefresh, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(fetch.Diagnostics);
            if (!fetch.Success || fetch.Catalog is null)
            {
                // Failure is non-fatal for the overall aggregation: the user
                // still gets the repos from OTHER subscribed catalogs that
                // resolved fine. The per-source error is in diagnostics.
                continue;
            }
            visitedSources.Add(fetch.Source);

            foreach (var repo in fetch.Catalog.Repos)
            {
                if (string.IsNullOrWhiteSpace(repo.Owner) || string.IsNullOrWhiteSpace(repo.Repo))
                {
                    continue;
                }
                var key = $"{repo.Owner}/{repo.Repo}";
                if (!reposByKey.TryGetValue(key, out var existing))
                {
                    existing = new AggregatedRepo
                    {
                        Owner = repo.Owner,
                        Repo = repo.Repo,
                        Summary = repo.Summary,
                        Tags = new List<string>(repo.Tags),
                        IndexPath = repo.IndexPath,
                        VouchedBy = new List<CatalogSource>(),
                    };
                    reposByKey[key] = existing;
                }
                else if (!string.Equals(existing.IndexPath, repo.IndexPath, StringComparison.Ordinal))
                {
                    // Two catalogs vouch for the same (owner, repo) but disagree on
                    // where its index lives. First vouch wins (the install path uses
                    // existing.IndexPath); warn so the divergence is visible rather
                    // than silently picking one.
                    diagnostics.Add(Warning(ManagerDiagnosticCodes.CatalogRepoIndexPathConflict,
                        $"Catalog '{fetch.Source.Canonical}' lists {key} with indexPath " +
                        $"'{DescribeIndexPath(repo.IndexPath)}', but an earlier catalog used " +
                        $"'{DescribeIndexPath(existing.IndexPath)}'. Keeping the first."));
                }
                existing.VouchedBy.Add(fetch.Source);
            }

            if (depth >= maxDepth)
            {
                continue; // depth-capped; don't enqueue children
            }

            // Enqueue federated sub-catalogs. file:./relative resolves against
            // the parent catalog's directory; gh: and file:// (absolute)
            // ignore the parent dir.
            foreach (var subRef in fetch.Catalog.Catalogs)
            {
                if (string.IsNullOrWhiteSpace(subRef.Source)) { continue; }

                var subParentDir = source is FileCatalogSource file
                    ? Path.GetDirectoryName(file.AbsolutePath) ?? string.Empty
                    : parentDir ?? string.Empty;

                if (!CatalogSourceParser.TryParseRelativeTo(subRef.Source, subParentDir, out var childSource))
                {
                    diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                        $"Catalog '{fetch.Source.Canonical}' references sub-catalog '{subRef.Source}' which is not a recognised source spec."));
                    continue;
                }

                queue.Enqueue(new PendingFetch(childSource, depth + 1, subParentDir));
            }
        }

        return new CatalogAggregateResult
        {
            Repos = reposByKey.Values
                .OrderBy(r => r.Owner, StringComparer.Ordinal)
                .ThenBy(r => r.Repo, StringComparer.Ordinal)
                .ToList(),
            VisitedSources = visitedSources,
            Diagnostics = diagnostics,
        };
    }

    private static string DescribeIndexPath(string indexPath)
        => indexPath.Length == 0 ? "(root)" : indexPath;

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);

    private readonly record struct PendingFetch(CatalogSource Source, int Depth, string? ParentDirectory);
}

/// <summary>
/// Flattened view of the aggregated catalog graph. <see cref="Repos"/> is
/// deduplicated on (owner, repo); each entry's <see cref="AggregatedRepo.VouchedBy"/>
/// names every subscribed-or-federated catalog that listed it (trust signal
/// for the user — "this repo appears in 3 catalogs you follow").
/// </summary>
public sealed class CatalogAggregateResult
{
    public IReadOnlyList<AggregatedRepo> Repos { get; init; } = Array.Empty<AggregatedRepo>();
    public IReadOnlyList<CatalogSource> VisitedSources { get; init; } = Array.Empty<CatalogSource>();
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

public sealed class AggregatedRepo
{
    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Optional repo-relative directory holding this repo's <c>index.yaml</c>,
    /// carried from the catalog repo entry (empty = repo root). The install
    /// path turns it into the <c>gh:owner/repo:indexPath/mod-id</c> base segment.
    /// On a (owner, repo) dedup the first vouch's value wins.
    /// </summary>
    public string IndexPath { get; init; } = string.Empty;

    /// <summary>
    /// Every catalog source in the aggregated graph that listed this repo.
    /// More entries = more catalogs vouching for it (a soft trust signal).
    /// </summary>
    public List<CatalogSource> VouchedBy { get; init; } = new();
}
