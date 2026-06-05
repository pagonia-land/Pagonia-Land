namespace PagoniaLand.Manager;

public static class StoreLayoutConstants
{
    // Initial format. state.yaml may carry an optional `installs:` map of
    // per-game-install expansion-ownership records (keyed by gameRoot
    // fingerprint); an absent `installs:` reads as every declarable expansion
    // unknown.
    public const string CurrentStoreVersion = "0.1";
    // Initial format. Each enabledMods[] entry may carry an optional `tweaks`
    // map of per-profile user-supplied tweak overrides; an absent `tweaks` key
    // reads as null = "use mod defaults".
    public const string CurrentProfileVersion = "0.1";
    // Initial format. Manifests optionally carry a `rebuiltPaks` list when the
    // deploy was against a live game install; rollback dispatches on whichever
    // list is populated.
    public const string CurrentDeployVersion = "0.1";
    public const string DefaultProfileName = "default";
    public const string ProfileFileSuffix = ".profile.yaml";
    public const string CollectionLockFileSuffix = ".lock.yaml";
    public const string CollectionManifestFileSuffix = ".collection.yaml";
    public const string StateFileName = "state.yaml";
    public const string ModsFolderName = "mods";
    public const string ProfilesFolderName = "profiles";
    public const string CollectionsFolderName = "collections";
    public const string CollectionLocksFolderName = "locks";
    public const string DeploysFolderName = "deploys";
    public const string DeployBackupFolderName = "backup";
    public const string DeployManifestFileName = "manifest.yaml";
    public const string DeployHistoryFileName = "history.yaml";

    // pak extract cache shared across plans/deploys against the same
    // live game install. Stale fingerprints get GC'd on next ensure (only one
    // cache directory per layout root is kept warm).
    public const string CacheFolderName = "cache";
    public const string PakCacheFolderPrefix = "extract-";

    /// <summary>Legacy global "all paks done" sentinel from cache schema v2.
    /// Kept as a constant only so PakCacheService can recognise v2 caches and
    /// migrate them by re-extraction (the v3 schema uses a richer per-pak
    /// status file instead — see <see cref="PakCacheStatusFileName"/>).</summary>
    public const string PakCacheCompleteSentinelFileName = ".extract-complete";

    /// <summary>per-pak completion status for the extract
    /// cache. Lists which pak basenames have been fully extracted so the next
    /// ensure can incrementally add the missing ones rather than restarting
    /// from scratch. YAML written atomically every time the cache grows.</summary>
    public const string PakCacheStatusFileName = ".extract-status.yaml";
}

public sealed class StoreLayout
{
    public string Root { get; }

    public StoreLayout(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Store root must not be empty.", nameof(root));
        }

        Root = Path.GetFullPath(root);
    }

    public string ModsDirectory => Path.Combine(Root, StoreLayoutConstants.ModsFolderName);
    public string ProfilesDirectory => Path.Combine(Root, StoreLayoutConstants.ProfilesFolderName);
    public string CollectionsDirectory => Path.Combine(Root, StoreLayoutConstants.CollectionsFolderName);
    public string CollectionLocksDirectory => Path.Combine(CollectionsDirectory, StoreLayoutConstants.CollectionLocksFolderName);
    public string StateFile => Path.Combine(Root, StoreLayoutConstants.StateFileName);

    public string ProfileFile(string profileName)
        => Path.Combine(ProfilesDirectory, profileName + StoreLayoutConstants.ProfileFileSuffix);

    public string CollectionLockFile(string collectionId)
        => Path.Combine(CollectionLocksDirectory, collectionId + StoreLayoutConstants.CollectionLockFileSuffix);

    public string ModVersionDirectory(string modId, string version)
        => Path.Combine(ModsDirectory, modId, version);

    public string CollectionDirectory(string collectionId)
        => Path.Combine(CollectionsDirectory, collectionId);

    public string CollectionVersionDirectory(string collectionId, string collectionVersion)
        => Path.Combine(CollectionDirectory(collectionId), collectionVersion);

    public string CollectionManifestFile(string collectionId, string collectionVersion)
        => Path.Combine(
            CollectionVersionDirectory(collectionId, collectionVersion),
            collectionId + StoreLayoutConstants.CollectionManifestFileSuffix);

    public string DeploysDirectory => Path.Combine(Root, StoreLayoutConstants.DeploysFolderName);

    public string DeployFingerprintDirectory(string fingerprint)
        => Path.Combine(DeploysDirectory, fingerprint);

    public string DeployHistoryFile(string fingerprint)
        => Path.Combine(DeployFingerprintDirectory(fingerprint), StoreLayoutConstants.DeployHistoryFileName);

    public string DeployTimestampDirectory(string fingerprint, string timestamp)
        => Path.Combine(DeployFingerprintDirectory(fingerprint), timestamp);

    public string DeployManifestFile(string fingerprint, string timestamp)
        => Path.Combine(DeployTimestampDirectory(fingerprint, timestamp), StoreLayoutConstants.DeployManifestFileName);

    public string DeployBackupDirectory(string fingerprint, string timestamp)
        => Path.Combine(DeployTimestampDirectory(fingerprint, timestamp), StoreLayoutConstants.DeployBackupFolderName);

    // pak extract cache.
    public string CacheDirectory => Path.Combine(Root, StoreLayoutConstants.CacheFolderName);

    public string PakCacheDirectory(string fingerprint)
        => Path.Combine(CacheDirectory, StoreLayoutConstants.PakCacheFolderPrefix + fingerprint);

    public string PakCacheCompleteSentinel(string fingerprint)
        => Path.Combine(PakCacheDirectory(fingerprint), StoreLayoutConstants.PakCacheCompleteSentinelFileName);

    public string PakCacheStatusFile(string fingerprint)
        => Path.Combine(PakCacheDirectory(fingerprint), StoreLayoutConstants.PakCacheStatusFileName);
}
