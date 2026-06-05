using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class ModManifest
{
    [YamlMember(Alias = "patchFormatVersion")]
    public string PatchFormatVersion { get; init; } = string.Empty;

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

    [YamlMember(Alias = "requiredPackages")]
    public List<string> RequiredPackages { get; init; } = [];

    [YamlMember(Alias = "optionalPackages")]
    public List<string> OptionalPackages { get; init; } = [];

    [YamlMember(Alias = "requiresNewGame")]
    public SafetyState? RequiresNewGame { get; init; }

    [YamlMember(Alias = "safeToRemove")]
    public SafetyState? SafeToRemove { get; init; }

    [YamlMember(Alias = "multiplayerSafe")]
    public SafetyState? MultiplayerSafe { get; init; }

    [YamlMember(Alias = "campaignSafe")]
    public SafetyState? CampaignSafe { get; init; }

    [YamlMember(Alias = "dependencies")]
    public List<string> Dependencies { get; init; } = [];

    [YamlMember(Alias = "loadAfter")]
    public List<string> LoadAfter { get; init; } = [];

    [YamlMember(Alias = "loadBefore")]
    public List<string> LoadBefore { get; init; } = [];

    [YamlMember(Alias = "incompatibleWith")]
    public List<string> IncompatibleWith { get; init; } = [];

    [YamlMember(Alias = "patches")]
    public List<string> Patches { get; init; } = [];

    [YamlMember(Alias = "patchSets")]
    public List<PatchSet> PatchSets { get; init; } = [];

    [YamlMember(Alias = "entries")]
    public EntryOperations? Entries { get; init; }

    [YamlMember(Alias = "tweaks")]
    public List<TweakDeclaration> Tweaks { get; init; } = [];

    [YamlMember(Alias = "pak")]
    public PakMetadata? Pak { get; init; }

    [YamlMember(Alias = "homepage")]
    public string? Homepage { get; init; }

    [YamlMember(Alias = "repository")]
    public string? Repository { get; init; }

    [YamlMember(Alias = "downloadUrl")]
    public string? DownloadUrl { get; init; }

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

public sealed class PatchSet
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "optional")]
    public bool Optional { get; init; }

    [YamlMember(Alias = "requiresPackages")]
    public List<string> RequiresPackages { get; init; } = [];

    [YamlMember(Alias = "patches")]
    public List<string> Patches { get; init; } = [];
}

/// <summary>
/// Binary pak-entry operations that sit next to the declarative XML
/// patches. Each list targets a pak entry by its in-pak path
/// (<c>&lt;pak&gt;/&lt;subdir&gt;/&lt;file&gt;</c>); the per-pak XML files are
/// handled by <see cref="ModManifest.Patches"/> / <see cref="ModManifest.PatchSets"/>
/// instead.
/// </summary>
public sealed class EntryOperations
{
    [YamlMember(Alias = "replace")]
    public List<EntryFileMapping> Replace { get; init; } = [];

    [YamlMember(Alias = "add")]
    public List<EntryFileMapping> Add { get; init; } = [];

    [YamlMember(Alias = "delete")]
    public List<string> Delete { get; init; } = [];
}

/// <summary>
/// One <c>replace</c> or <c>add</c> entry: the target path inside the pak and
/// the mod-folder-relative source file that should be written into that pak
/// entry's slot.
/// </summary>
public sealed class EntryFileMapping
{
    [YamlMember(Alias = "path")]
    public string Path { get; init; } = string.Empty;

    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;
}

/// <summary>
/// Optional Pattern B overlay-pak metadata. When present, the patcher's apply
/// step writes the standard module skeleton — <c>&lt;Name&gt;/manifest.json</c>,
/// <c>&lt;Name&gt;/files.json</c>, <c>&lt;Name&gt;/&lt;Name&gt;.gd.bin</c>, and
/// <c>&lt;Name&gt;/memory.bin</c> — into the output tree so a subsequent
/// <c>sandbox-pack.ps1</c> (no <c>-BasePak</c>) produces an engine-loadable
/// standalone overlay pak.
/// </summary>
public sealed class PakMetadata
{
    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    [YamlMember(Alias = "summary")]
    public string Summary { get; init; } = string.Empty;

    [YamlMember(Alias = "author")]
    public string Author { get; init; } = string.Empty;

    [YamlMember(Alias = "image")]
    public string Image { get; init; } = string.Empty;

    /// <summary>
    /// Other modules this pak needs in order to make sense. Defaults to
    /// <c>["core"]</c> at scaffold time when left empty.
    /// </summary>
    [YamlMember(Alias = "dependencies")]
    public List<string> Dependencies { get; init; } = [];
}
