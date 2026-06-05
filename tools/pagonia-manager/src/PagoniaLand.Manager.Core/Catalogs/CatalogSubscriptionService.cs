namespace PagoniaLand.Manager;

/// <summary>
/// CRUD over <c>state.yaml.subscribedCatalogs</c>. The user-facing CLI
/// (<c>catalog add / remove / list</c>) goes through here so the
/// validation + dedup rules live in one place.
/// </summary>
public sealed class CatalogSubscriptionService
{
    private readonly StoreStateReader _reader = new();
    private readonly StoreStateWriter _writer = new();

    public CatalogSubscriptionResult Add(StoreLayout layout, string spec)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!CatalogSourceParser.TryParse(spec, out var source))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"'{spec}' is not a recognised catalog source. Expected 'gh:owner/repo[#ref][/path]', 'https://host/path/catalog.yaml', or 'file:absolute-or-relative-path'."));
            return new CatalogSubscriptionResult { Success = false, Diagnostics = diagnostics };
        }

        var canonical = source.Canonical;
        var state = _reader.Read(layout);

        // Dedup on canonical form — adding the same catalog twice is a no-op
        // info diagnostic, not an error. Lets idempotent scripts re-subscribe
        // freely.
        if (state.SubscribedCatalogs.Any(s =>
            CatalogSourceParser.TryParse(s, out var existing) &&
            string.Equals(existing.Canonical, canonical, StringComparison.Ordinal)))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.CatalogSubscribed,
                $"Already subscribed to '{canonical}' (no-op)."));
            return new CatalogSubscriptionResult { Success = true, Source = source, Diagnostics = diagnostics };
        }

        var updated = new List<string>(state.SubscribedCatalogs) { spec };
        _writer.Write(layout, new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            LastDeploy = state.LastDeploy,
            DefaultGameRoot = state.DefaultGameRoot,
            SubscribedCatalogs = updated,
            CatalogMaxDepth = state.CatalogMaxDepth,
            AllowInsecureSources = state.AllowInsecureSources,
            CatalogCacheStalenessHours = state.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = state.AllowInsecureCatalogSources,
            Installs = state.Installs,
        });

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.CatalogSubscribed,
            $"Subscribed to catalog '{canonical}'."));

        return new CatalogSubscriptionResult { Success = true, Source = source, Diagnostics = diagnostics };
    }

    public CatalogSubscriptionResult Remove(StoreLayout layout, string spec)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!CatalogSourceParser.TryParse(spec, out var source))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CatalogFetchFailed,
                $"'{spec}' is not a recognised catalog source."));
            return new CatalogSubscriptionResult { Success = false, Diagnostics = diagnostics };
        }

        var canonical = source.Canonical;
        var state = _reader.Read(layout);

        // Remove every subscription that canonicalises to the same source.
        // Lets the user pass either the original spec they typed or any
        // equivalent form.
        var kept = state.SubscribedCatalogs
            .Where(s =>
            {
                if (!CatalogSourceParser.TryParse(s, out var existing)) { return true; }
                return !string.Equals(existing.Canonical, canonical, StringComparison.Ordinal);
            })
            .ToList();

        if (kept.Count == state.SubscribedCatalogs.Count)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.CatalogUnsubscribed,
                $"Not subscribed to '{canonical}' (no-op)."));
            return new CatalogSubscriptionResult { Success = true, Source = source, Diagnostics = diagnostics };
        }

        _writer.Write(layout, new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            LastDeploy = state.LastDeploy,
            DefaultGameRoot = state.DefaultGameRoot,
            SubscribedCatalogs = kept,
            CatalogMaxDepth = state.CatalogMaxDepth,
            AllowInsecureSources = state.AllowInsecureSources,
            CatalogCacheStalenessHours = state.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = state.AllowInsecureCatalogSources,
            Installs = state.Installs,
        });

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.CatalogUnsubscribed,
            $"Unsubscribed from catalog '{canonical}'."));

        return new CatalogSubscriptionResult { Success = true, Source = source, Diagnostics = diagnostics };
    }

    public IReadOnlyList<CatalogSource> List(StoreLayout layout)
    {
        var state = _reader.Read(layout);
        var sources = new List<CatalogSource>();
        foreach (var spec in state.SubscribedCatalogs)
        {
            if (CatalogSourceParser.TryParse(spec, out var source))
            {
                sources.Add(source);
            }
        }
        return sources;
    }

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);
}

public sealed class CatalogSubscriptionResult
{
    public bool Success { get; init; }
    public CatalogSource? Source { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}
