using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Deserialised view of a mod-distribution repo's top-level <c>index.yaml</c>
/// catalog. Matches <c>schemas/mod-patches/repo-index.schema.json</c>.
/// Only the fields the manager actually uses for install + display are modelled
/// here; unknown fields in the YAML are ignored so the schema can grow without
/// breaking older managers.
/// </summary>
public sealed class RepoIndex
{
    [YamlMember(Alias = "indexFormatVersion")]
    public string IndexFormatVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "repo")]
    public RepoIndexRepo? Repo { get; init; }

    [YamlMember(Alias = "mods")]
    public List<RepoIndexMod> Mods { get; init; } = new();

    [YamlMember(Alias = "collections")]
    public List<RepoIndexCollection> Collections { get; init; } = new();
}

public sealed class RepoIndexRepo
{
    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "author")]
    public string Author { get; init; } = string.Empty;

    [YamlMember(Alias = "homepage")]
    public string Homepage { get; init; } = string.Empty;

    [YamlMember(Alias = "license")]
    public string License { get; init; } = string.Empty;
}

public sealed class RepoIndexMod
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "path")]
    public string Path { get; init; } = string.Empty;

    [YamlMember(Alias = "displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    [YamlMember(Alias = "gameDatabaseVersion")]
    public string GameDatabaseVersion { get; init; } = string.Empty;

    /// <summary>
    /// Optional SHA-256 over the mod's logical payload (mod.yaml + referenced patches), maintained by
    /// the patcher's <c>index build</c>. When present, the consumer re-computes it on the fetched
    /// download and warns on a mismatch (<c>manager.modContentHashMismatch</c>) — download integrity
    /// plus same-version content-drift detection. Empty when the index doesn't advertise it.
    /// </summary>
    [YamlMember(Alias = "contentHash")]
    public string ContentHash { get; init; } = string.Empty;

    [YamlMember(Alias = "safetyFlags")]
    public RepoIndexSafetyFlags? SafetyFlags { get; init; }
}

/// <summary>
/// The four catalog-level safety markers an index entry may carry, so the browse
/// view can warn users <em>before</em> install. Modelled as strings (<c>true</c> /
/// <c>false</c> / <c>unknown</c>) to mirror the YAML verbatim without coupling the
/// manager's general-purpose index deserializers to the patcher's <c>SafetyState</c>
/// converter. The authoritative copy lives in each mod's <c>mod.yaml</c>; this is the
/// mirrored cache the manager cross-checks at install time.
/// </summary>
public sealed class RepoIndexSafetyFlags
{
    [YamlMember(Alias = "requiresNewGame")]
    public string? RequiresNewGame { get; init; }

    [YamlMember(Alias = "safeToRemove")]
    public string? SafeToRemove { get; init; }

    [YamlMember(Alias = "multiplayerSafe")]
    public string? MultiplayerSafe { get; init; }

    [YamlMember(Alias = "campaignSafe")]
    public string? CampaignSafe { get; init; }
}

public sealed class RepoIndexCollection
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "path")]
    public string Path { get; init; } = string.Empty;

    [YamlMember(Alias = "displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    [YamlMember(Alias = "gameDatabaseVersion")]
    public string GameDatabaseVersion { get; init; } = string.Empty;
}
