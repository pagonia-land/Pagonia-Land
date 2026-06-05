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
