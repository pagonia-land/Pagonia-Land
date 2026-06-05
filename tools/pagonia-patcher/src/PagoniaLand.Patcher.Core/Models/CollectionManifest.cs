using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class CollectionManifest
{
    [YamlMember(Alias = "collectionFormatVersion")]
    public string CollectionFormatVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    [YamlMember(Alias = "author")]
    public string Author { get; init; } = string.Empty;

    [YamlMember(Alias = "gameDatabaseVersion")]
    public string GameDatabaseVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "conflictPolicy")]
    public string ConflictPolicy { get; init; } = string.Empty;

    [YamlMember(Alias = "mods")]
    public List<CollectionMod> Mods { get; init; } = [];

    [YamlMember(Alias = "loadOrder")]
    public List<string> LoadOrder { get; init; } = [];

    [YamlMember(Alias = "requiresNewGame")]
    public SafetyState? RequiresNewGame { get; init; }

    [YamlMember(Alias = "safeToRemove")]
    public SafetyState? SafeToRemove { get; init; }

    [YamlMember(Alias = "multiplayerSafe")]
    public SafetyState? MultiplayerSafe { get; init; }

    [YamlMember(Alias = "campaignSafe")]
    public SafetyState? CampaignSafe { get; init; }

    [YamlMember(Alias = "homepage")]
    public string? Homepage { get; init; }

    [YamlMember(Alias = "repository")]
    public string? Repository { get; init; }

    [YamlMember(Alias = "updateUrl")]
    public string? UpdateUrl { get; init; }

    [YamlMember(Alias = "license")]
    public string? License { get; init; }

    [YamlMember(Alias = "category")]
    public string? Category { get; init; }

    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; init; } = [];

    [YamlMember(Alias = "previewImages")]
    public List<string> PreviewImages { get; init; } = [];
}

public sealed class CollectionMod
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;

    [YamlMember(Alias = "required")]
    public bool Required { get; init; } = true;

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; } = true;

    [YamlMember(Alias = "requiresPackages")]
    public List<string> RequiresPackages { get; init; } = [];

    [YamlMember(Alias = "notes")]
    public string? Notes { get; init; }

    /// <summary>
    /// Optional curator-supplied tweak values (<c>tweakId → value</c>) that override the mod's
    /// declared defaults. This is the headline collection use case: ship a "Hardcore" and an "Easy"
    /// preset from the same mods with different tweak values. <c>null</c> when the curator left the
    /// mod's defaults alone.
    /// </summary>
    [YamlMember(Alias = "tweaks")]
    public Dictionary<string, string>? Tweaks { get; init; }
}
