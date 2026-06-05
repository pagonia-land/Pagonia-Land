using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class CollectionLock
{
    [YamlMember(Alias = "collectionLockVersion")]
    public string CollectionLockVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "collectionId")]
    public string CollectionId { get; init; } = string.Empty;

    [YamlMember(Alias = "collectionVersion")]
    public string CollectionVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "gameDatabaseVersion")]
    public string GameDatabaseVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "generatedAt")]
    public string GeneratedAt { get; init; } = string.Empty;

    [YamlMember(Alias = "mods")]
    public List<LockedMod> Mods { get; init; } = [];
}

public sealed class LockedMod
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    [YamlMember(Alias = "resolvedSource")]
    public string ResolvedSource { get; init; } = string.Empty;

    [YamlMember(Alias = "archiveSha256")]
    public string ArchiveSha256 { get; init; } = string.Empty;

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Optional. Transport-neutral identifier pinning where this mod came
    /// from at install time, e.g. <c>gh:owner/repo#&lt;commit-sha&gt;/&lt;mod-id&gt;</c>.
    /// Empty for purely-local installs. Used by re-install to fetch the exact
    /// same bytes from the same commit even after the source branch moves on
    /// the remote.
    /// </summary>
    [YamlMember(Alias = "source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Optional. ISO-8601 timestamp captured when this mod's source was
    /// resolved (commit SHA pinned). Together with <see cref="Source"/> records
    /// when the resolution happened.
    /// </summary>
    [YamlMember(Alias = "resolvedAt")]
    public string ResolvedAt { get; init; } = string.Empty;

    /// <summary>
    /// Optional. The tweak values (<c>tweakId → value</c>) resolved for this mod when the lockfile
    /// was generated — the curator's overrides folded over the mod's defaults. Re-applying from the
    /// lockfile pins these exact values so the substitution is reproducible even after the mod author
    /// changes a default in a later version. <c>null</c> for mods that declare no tweaks.
    /// </summary>
    [YamlMember(Alias = "tweaks")]
    public Dictionary<string, string>? Tweaks { get; init; }
}

/// <summary>
/// Supported <see cref="CollectionLock.CollectionLockVersion"/> values. The
/// initial 0.1 format carries the optional per-mod <c>source</c> / <c>resolvedAt</c>
/// (remote-fetch provenance) and <c>tweaks</c> (pinned tweak values) fields.
/// New writes emit <see cref="Current"/>; reads accept only it.
/// </summary>
public static class CollectionLockVersions
{
    public const string V0_1 = "0.1";
    public const string Current = V0_1;
}
