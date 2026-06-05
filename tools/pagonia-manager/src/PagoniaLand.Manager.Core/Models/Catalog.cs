using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// Deserialised view of a <c>catalog.yaml</c>. Matches the public schema
/// at <c>schemas/mod-patches/catalog.schema.json</c>. Only fields the
/// manager actually consumes are modelled; unknown fields are ignored so
/// the schema can grow without breaking older managers.
/// </summary>
public sealed class Catalog
{
    [YamlMember(Alias = "catalogFormatVersion")]
    public string CatalogFormatVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "catalog")]
    public CatalogMetadata? CatalogMeta { get; init; }

    [YamlMember(Alias = "repos")]
    public List<CatalogRepoEntry> Repos { get; init; } = new();

    [YamlMember(Alias = "catalogs")]
    public List<CatalogReference> Catalogs { get; init; } = new();
}

public sealed class CatalogMetadata
{
    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "maintainer")]
    public string Maintainer { get; init; } = string.Empty;

    [YamlMember(Alias = "homepage")]
    public string Homepage { get; init; } = string.Empty;

    [YamlMember(Alias = "license")]
    public string License { get; init; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; init; } = new();
}

public sealed class CatalogRepoEntry
{
    [YamlMember(Alias = "owner")]
    public string Owner { get; init; } = string.Empty;

    [YamlMember(Alias = "repo")]
    public string Repo { get; init; } = string.Empty;

    /// <summary>
    /// Optional repo-relative directory holding this repo's <c>index.yaml</c>
    /// (and under which its mod folders resolve). Empty = the index is at the
    /// repo root, today's behaviour. Lets one repo host a mod-distribution tree
    /// in a subfolder. Parsed and carried here; consumed by the fetcher/install
    /// path once subdirectory support ships.
    /// </summary>
    [YamlMember(Alias = "indexPath")]
    public string IndexPath { get; init; } = string.Empty;

    [YamlMember(Alias = "summary")]
    public string Summary { get; init; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; init; } = new();
}

public sealed class CatalogReference
{
    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;

    [YamlMember(Alias = "summary")]
    public string Summary { get; init; } = string.Empty;
}
