using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

public sealed class DeployManifest
{
    [YamlMember(Alias = "deployVersion")]
    public string DeployVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [YamlMember(Alias = "gameRoot")]
    public string GameRoot { get; init; } = string.Empty;

    [YamlMember(Alias = "gameFingerprint")]
    public string GameFingerprint { get; init; } = string.Empty;

    // The game's ProductVersion (its real gameDatabaseVersion) live at deploy
    // time, read from the exe. Purely informational provenance — not used by
    // rollback — but it makes a manifest self-describing ("this deploy targeted
    // v1.4.2"). null when the version was unreadable (extracted layout / fixture);
    // OmitNull keeps it out of those manifests entirely.
    [YamlMember(Alias = "gameProductVersion")]
    public string? GameProductVersion { get; init; }

    [YamlMember(Alias = "profile")]
    public string Profile { get; init; } = string.Empty;

    [YamlMember(Alias = "mods")]
    public List<DeployedMod> Mods { get; init; } = [];

    [YamlMember(Alias = "modifiedFiles")]
    public List<DeployFileEntry> ModifiedFiles { get; init; } = [];

    // Files this deploy created that did NOT exist in <gameRoot> before (no backup to restore
    // on rollback — the file is just deleted). Currently used for Pattern B overlay paks
    // landing in <game>/mods/<modid>.pak.
    [YamlMember(Alias = "addedFiles")]
    public List<DeployAddedFileEntry> AddedFiles { get; init; } = [];

    // Optional — paks rebuilt from a live game install. Empty
    // for extracted-layout deploys (which use ModifiedFiles instead). Rollback
    // dispatches on whichever list is populated: paks restored from backup,
    // XML files restored from backup, then Pattern B paks deleted.
    [YamlMember(Alias = "rebuiltPaks")]
    public List<DeployRebuiltPakEntry> RebuiltPaks { get; init; } = [];
}

public sealed class DeployedMod
{
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    [YamlMember(Alias = "version")]
    public string Version { get; init; } = string.Empty;
}

public sealed class DeployFileEntry
{
    [YamlMember(Alias = "relativePath")]
    public string RelativePath { get; init; } = string.Empty;

    [YamlMember(Alias = "originalSha256")]
    public string OriginalSha256 { get; init; } = string.Empty;

    [YamlMember(Alias = "deployedSha256")]
    public string DeployedSha256 { get; init; } = string.Empty;
}

public sealed class DeployAddedFileEntry
{
    [YamlMember(Alias = "relativePath")]
    public string RelativePath { get; init; } = string.Empty;

    [YamlMember(Alias = "deployedSha256")]
    public string DeployedSha256 { get; init; } = string.Empty;

    [YamlMember(Alias = "sourceMod")]
    public string SourceMod { get; init; } = string.Empty;

    [YamlMember(Alias = "byteSize")]
    public long ByteSize { get; init; }
}

/// <summary>One pak rebuilt during a live-install deploy. The pak's original
/// bytes live at <see cref="BackupRelativePath"/> (relative to the deploy's
/// backup directory) so rollback can restore it byte-identically. Hashes let
/// rollback validate the backup before overwriting the live pak.</summary>
public sealed class DeployRebuiltPakEntry
{
    /// <summary>Pak filename including extension — e.g. "core.pak".</summary>
    [YamlMember(Alias = "pakName")]
    public string PakName { get; init; } = string.Empty;

    /// <summary>Path relative to <c>&lt;gameRoot&gt;</c> where the live pak sits —
    /// always <c>pak/&lt;pakName&gt;</c>. Stored for symmetry with other entry
    /// types; rollback uses it to locate the write target.</summary>
    [YamlMember(Alias = "targetRelativePath")]
    public string TargetRelativePath { get; init; } = string.Empty;

    /// <summary>Path relative to the deploy's backup directory where the original
    /// pak was copied before write — always <c>pak/&lt;pakName&gt;</c>.</summary>
    [YamlMember(Alias = "backupRelativePath")]
    public string BackupRelativePath { get; init; } = string.Empty;

    [YamlMember(Alias = "originalSha256")]
    public string OriginalSha256 { get; init; } = string.Empty;

    [YamlMember(Alias = "newSha256")]
    public string NewSha256 { get; init; } = string.Empty;

    [YamlMember(Alias = "byteSizeBefore")]
    public long ByteSizeBefore { get; init; }

    [YamlMember(Alias = "byteSizeAfter")]
    public long ByteSizeAfter { get; init; }

    /// <summary>Mod ids whose patches contributed to this rebuild — informational,
    /// not used by rollback. Helps a future "which mod touched which pak?" view.</summary>
    [YamlMember(Alias = "contributingMods")]
    public List<string> ContributingMods { get; init; } = [];
}

public sealed class DeployHistory
{
    [YamlMember(Alias = "deployHistoryVersion")]
    public string DeployHistoryVersion { get; init; } = string.Empty;

    [YamlMember(Alias = "gameFingerprint")]
    public string GameFingerprint { get; init; } = string.Empty;

    [YamlMember(Alias = "gameRoot")]
    public string GameRoot { get; init; } = string.Empty;

    // Newest-first.
    [YamlMember(Alias = "deploys")]
    public List<DeployHistoryEntry> Deploys { get; init; } = [];
}

public sealed class DeployHistoryEntry
{
    [YamlMember(Alias = "timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [YamlMember(Alias = "profile")]
    public string Profile { get; init; } = string.Empty;

    [YamlMember(Alias = "modCount")]
    public int ModCount { get; init; }

    [YamlMember(Alias = "fileCount")]
    public int FileCount { get; init; }
}
