using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>per-pak completion status for the extract
/// cache. Replaces the v2 schema's single <c>.extract-complete</c> sentinel
/// with a YAML file that lists which pak basenames are fully extracted, so
/// <see cref="PakCacheService"/> can incrementally add missing paks rather
/// than restarting extraction from scratch every time a previously-unknown
/// pak becomes required.
/// <para>v5 records, per extracted pak, the SHA-256 of the source pak captured
/// at extract time (<see cref="PakCacheEntry.PakSha256"/>). That lets the next
/// ensure notice an out-of-band change to a canonical pak — surfaced as
/// <c>manager.canonicalPakChangedExternally</c> — without folding pak content
/// back into the install fingerprint (which would resurrect the post-deploy
/// self-invalidation v4 removed).</para></summary>
public sealed class PakCacheStatus
{
    /// <summary>Paks fully extracted into this cache directory, each with the
    /// source pak's hash at extract time. Order is stable for diff-friendliness;
    /// callers sort by name before writing.</summary>
    [YamlMember(Alias = "extractedPaks")]
    public List<PakCacheEntry> ExtractedPaks { get; init; } = [];
}

/// <summary>One extracted pak's bookkeeping in the cache status sidecar.</summary>
public sealed class PakCacheEntry
{
    /// <summary>Pak basename, no extension (e.g. <c>core</c>, <c>dlc1</c>).</summary>
    [YamlMember(Alias = "name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>SHA-256 of the source pak file this cache slice was extracted
    /// from, captured at extract time. Compared against the live pak on the next
    /// ensure to detect external edits.</summary>
    [YamlMember(Alias = "pakSha256")]
    public string PakSha256 { get; init; } = string.Empty;
}
