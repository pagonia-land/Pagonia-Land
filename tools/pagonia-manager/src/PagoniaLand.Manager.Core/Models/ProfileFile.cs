using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

public sealed class ProfileFile
{
    [YamlMember(Alias = "profileVersion")]
    public string ProfileVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    // Set when the profile was created via `collection install` — links the profile back
    // to its source collection so a future re-resolve can refresh it. Left null for
    // hand-created profiles.
    [YamlMember(Alias = "collection")]
    public string? Collection { get; init; }

    [YamlMember(Alias = "enabledMods")]
    public List<ProfileEnabledMod> EnabledMods { get; init; } = [];

    [YamlMember(Alias = "loadOrder")]
    public List<string> LoadOrder { get; init; } = [];
}

public sealed class ProfileEnabledMod
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;

    // Per-profile user-supplied tweak overrides (tweakId → value), keyed by the
    // tweak ids the mod declares in its `mod.yaml`. Three meanings:
    //   null       — no overrides recorded; resolve every tweak to its default.
    //   empty dict — explicitly "no overrides" (round-trips as `tweaks: {}`).
    //   non-empty  — user-supplied values that override the mod-declared defaults.
    // Values are kept as raw scalar strings, mirroring the patcher's
    // CollectionMod.Tweaks / LockedMod.Tweaks convention — the mod's declaration
    // owns the type, and the override is parsed/validated against it on demand.
    // Optional: an absent `tweaks` key reads back as null.
    [YamlMember(Alias = "tweaks")]
    public Dictionary<string, string>? Tweaks { get; init; }
}
