using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

public sealed class StoreState
{
    [YamlMember(Alias = "storeVersion")]
    public string StoreVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "activeProfile")]
    public string? ActiveProfile { get; init; }

    [YamlMember(Alias = "lastDeploy")]
    public StoreLastDeploy? LastDeploy { get; init; }

    /// <summary>
    /// User's preferred game-install path. Persisted from the first time the
    /// user enters / confirms a path in the Plan + Deploy / Rollback wizards,
    /// so subsequent wizard runs become a one-key confirm instead of a full
    /// re-entry. Distinct from <see cref="LastDeploy"/>.GameRoot — that's
    /// per-fingerprint and tied to deploy success, this is per-store and
    /// tracks user intent (set on entry, not on successful deploy).
    /// </summary>
    [YamlMember(Alias = "defaultGameRoot")]
    public string? DefaultGameRoot { get; init; }

    /// <summary>
    /// Catalog source specs the user has subscribed to via
    /// <c>pagonia-manager catalog add</c>. The aggregator walks every entry
    /// (plus their federated <c>catalogs:</c> references, with cycle + depth
    /// + dedup protection) when the user runs <c>catalog browse</c>. Empty
    /// by default; new stores can be pre-seeded with a default subscription
    /// at first-run via the CLI / wizard.
    /// </summary>
    [YamlMember(Alias = "subscribedCatalogs")]
    public List<string> SubscribedCatalogs { get; init; } = new();

    /// <summary>
    /// Optional override for the catalog-federation depth cap. Default 5
    /// (applied by <c>CatalogAggregator</c> when this is null or &lt;= 0).
    /// </summary>
    [YamlMember(Alias = "catalogMaxDepth")]
    public int? CatalogMaxDepth { get; init; }

    /// <summary>
    /// Opt-in flag for plain <c>http://</c> direct-URL install sources.
    /// When false (default), an http:// install surfaces
    /// <c>manager.directUrlInsecureHttp</c> as a warning AND aborts; when
    /// true the warning still fires but install proceeds. https:// sources
    /// are unaffected. Lets users on locked-down corporate / LAN
    /// deployments use known-trusted internal hosts without forcing TLS.
    /// </summary>
    [YamlMember(Alias = "allowInsecureSources")]
    public bool AllowInsecureSources { get; init; }

    /// <summary>
    /// Optional override for the on-disk catalog cache staleness threshold.
    /// Default 24 hours (applied by <c>CachingCatalogFetcher</c> when this
    /// is null or &lt;= 0). Lowering the value forces fresher data at the
    /// cost of more raw.githubusercontent.com round-trips; raising it cuts
    /// traffic for users on a stable subscription set.
    /// </summary>
    [YamlMember(Alias = "catalogCacheStalenessHours")]
    public int? CatalogCacheStalenessHours { get; init; }

    /// <summary>
    /// Opt-in for plain <c>http://</c> catalog sources. When false (default),
    /// an http:// catalog still fetches but the manager emits
    /// <c>manager.catalogInsecureHttp</c> as a warning. When true, the
    /// warning is suppressed. Distinct from <see cref="AllowInsecureSources"/>
    /// which gates plain-http install — catalog reads are read-only and
    /// lower-risk than installs, so the catalog flag only silences a
    /// warning rather than gating execution.
    /// </summary>
    [YamlMember(Alias = "allowInsecureCatalogSources")]
    public bool AllowInsecureCatalogSources { get; init; }

    /// <summary>
    /// Per-game-install expansion-ownership records, keyed by gameRoot
    /// fingerprint (the same <see cref="GameFingerprint"/> the deploy history
    /// uses). Ownership is a fact about the installation/account, stable across
    /// every profile — so it lives here at store scope, never in a portable
    /// profile. Absent / empty (an older store with no installs map) ⇒ every declarable
    /// expansion resolves as <c>unknown</c>, with no migration step. Read-modify-
    /// write paths must carry this map forward so an unrelated state write (a
    /// deploy stamp, a catalog edit) never silently drops ownership.
    /// </summary>
    [YamlMember(Alias = "installs")]
    public Dictionary<string, InstallRecord> Installs { get; init; } = new();
}

public sealed class StoreLastDeploy
{
    [YamlMember(Alias = "timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [YamlMember(Alias = "gameRoot")]
    public string GameRoot { get; init; } = string.Empty;

    [YamlMember(Alias = "profile")]
    public string Profile { get; init; } = string.Empty;
}
