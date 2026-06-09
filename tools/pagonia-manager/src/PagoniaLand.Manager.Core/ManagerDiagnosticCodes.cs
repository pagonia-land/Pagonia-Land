namespace PagoniaLand.Manager;

public static class ManagerDiagnosticCodes
{
    public const string StoreNotInitialised = "manager.storeNotInitialised";
    public const string StoreStateUnreadable = "manager.storeStateUnreadable";
    public const string StoreSchemaVersionUnsupported = "manager.storeSchemaVersionUnsupported";

    public const string ModSourceNotFound = "manager.modSourceNotFound";
    public const string ModSourceNotAFolderOrZip = "manager.modSourceNotAFolderOrZip";
    public const string ModManifestMissing = "manager.modManifestMissing";
    public const string ModAlreadyInstalled = "manager.modAlreadyInstalled";
    public const string ModNotInstalled = "manager.modNotInstalled";
    public const string ModVersionAmbiguous = "manager.modVersionAmbiguous";
    public const string ModVersionNotInstalled = "manager.modVersionNotInstalled";

    public const string ProfileMissing = "manager.profileMissing";
    public const string ModAlreadyEnabled = "manager.modAlreadyEnabled";
    public const string ModNotEnabled = "manager.modNotEnabled";
    public const string ProfileDriftCleaned = "manager.profileDriftCleaned";
    public const string MoveTargetNotInLoadOrder = "manager.moveTargetNotInLoadOrder";
    public const string MoveAnchorNotInLoadOrder = "manager.moveAnchorNotInLoadOrder";
    public const string MovePositionOutOfRange = "manager.movePositionOutOfRange";

    public const string ProfileAlreadyExists = "manager.profileAlreadyExists";
    public const string ProfileNameInvalid = "manager.profileNameInvalid";
    public const string ProfileActiveDeletion = "manager.profileActiveDeletion";
    public const string ProfileDefaultDeletion = "manager.profileDefaultDeletion";
    // A local duplicate of an existing profile (snapshot / experiment branch).
    public const string ProfileCopied = "manager.profileCopied";

    // profile export → shareable collection.
    public const string ProfileExported = "manager.profileExported";
    // A mod in the exported profile had no recoverable remote source; written as
    // source: "local" (won't fetch elsewhere without a matching --mods-root).
    public const string ProfileExportLocalSource = "manager.profileExportLocalSource";
    // The profile has no enabled mods. A collection requires at least one mod
    // (collection.schema.json mods minItems 1), so the export is refused.
    public const string ProfileExportEmpty = "manager.profileExportEmpty";

    public const string CollectionAlreadyInstalled = "manager.collectionAlreadyInstalled";
    public const string CollectionNotInstalled = "manager.collectionNotInstalled";
    public const string CollectionRemoteSourceUnsupported = "manager.collectionRemoteSourceUnsupported";
    public const string CollectionInstallAborted = "manager.collectionInstallAborted";

    public const string ProfileEmpty = "manager.profileEmpty";
    public const string ProfileGameVersionMismatch = "manager.profileGameVersionMismatch";
    public const string ModInstallMissing = "manager.modInstallMissing";
    // an enabled mod is installed on disk but its manifest could not be parsed —
    // must not be silently dropped from health/plan roll-ups.
    public const string ModManifestUnreadable = "manager.modManifestUnreadable";
    public const string GameRootMissing = "manager.gameRootMissing";

    // two enabled mods destructively (Replace/Unload) target the same inherited
    // GameDatabase entity — the engine resolves by load order (last-loaded wins),
    // so the earlier mod's change is silently overridden. Advisory (warning).
    public const string CrossModOverlayConflict = "manager.crossModOverlayConflict";

    public const string DeployBlockedByErrors = "manager.deployBlockedByErrors";
    public const string DeployBlockedByWarnings = "manager.deployBlockedByWarnings";

    // live-state drift — a target the last deploy wrote was changed out-of-band
    // (hand-edit, second mod tool, partial Steam patch) before this deploy
    // overwrites it or a rollback reverts over it.
    public const string LiveStateDrift = "manager.liveStateDrift";
    public const string DeployBlockedByDrift = "manager.deployBlockedByDrift";
    public const string RollbackBlockedByDrift = "manager.rollbackBlockedByDrift";

    // game-vs-mod gameDatabaseVersion compatibility (advisory). Compares each
    // enabled mod's declared version to the install's real version (exe ProductVersion).
    public const string ModGameVersionDrift = "manager.modGameVersionDrift";
    public const string ModGameVersionMismatch = "manager.modGameVersionMismatch";
    public const string DeployEmpty = "manager.deployEmpty";
    public const string DeployCompleted = "manager.deployCompleted";
    public const string DeployDryRun = "manager.deployDryRun";
    public const string RollbackNothingToRollback = "manager.rollbackNothingToRollback";
    public const string RollbackCompleted = "manager.rollbackCompleted";
    public const string RollbackBackupMissing = "manager.rollbackBackupMissing";
    public const string DeployHistoryUnreadable = "manager.deployHistoryUnreadable";
    // a write to the live install threw mid-deploy; the install was restored from the
    // just-written backups (and any overlay paks removed) to its pre-deploy state.
    public const string DeployMidWriteRolledBack = "manager.deployMidWriteRolledBack";

    public const string SchemaValidationOk = "manager.schemaValidationOk";
    public const string SchemaValidationFailed = "manager.schemaValidationFailed";

    public const string PakBuildSucceeded = "manager.pakBuildSucceeded";
    public const string PakBuildFailed = "manager.pakBuildFailed";
    public const string PakScaffoldMissing = "manager.pakScaffoldMissing";
    public const string RollbackAddedFileMissing = "manager.rollbackAddedFileMissing";

    // live game-install deploy (pak-aware).
    public const string GameLayoutUnrecognised = "manager.gameLayoutUnrecognised";
    public const string PakCacheRefreshed = "manager.pakCacheRefreshed";
    public const string PakCacheReused = "manager.pakCacheReused";
    public const string PakCacheExtractFailed = "manager.pakCacheExtractFailed";
    // a canonical source pak was changed by another tool / by hand since it was
    // cached (and not by a manager deploy) — detected at extract time, re-extracted.
    public const string CanonicalPakChangedExternally = "manager.canonicalPakChangedExternally";

    // pak repack + live-install write-back.
    public const string PakRebuilt = "manager.pakRebuilt";
    public const string PakRebuildFailed = "manager.pakRebuildFailed";
    public const string ModifiedFileMissingOwningPak = "manager.modifiedFileMissingOwningPak";

    // live-install rollback from pak backups.
    public const string PakRollbackRestored = "manager.pakRollbackRestored";
    public const string RollbackHashMismatch = "manager.rollbackHashMismatch";
    // rollback succeeded but its timestamp directory couldn't be deleted (e.g. a locked
    // backup) — warn so the leftover isn't mistaken for a live backup.
    public const string RollbackLeftoverDirectory = "manager.rollbackLeftoverDirectory";
    // two source paks contain the same entry path while building the deploy owner map;
    // the first discovered pak is patched. Surfaced so the overlap is visible.
    public const string DuplicatePakEntryOwner = "manager.duplicatePakEntryOwner";
    // a Pattern B overlay pak was replaced after deploy (live SHA-256 no longer
    // matches the recorded deployedSha256) — rollback leaves it in place instead
    // of deleting a file it no longer owns. Warning, non-blocking.
    public const string RollbackAddedFileChanged = "manager.rollbackAddedFileChanged";

    // persistent default game folder.
    public const string DefaultGameRootStored = "manager.defaultGameRootStored";
    public const string DefaultGameRootCleared = "manager.defaultGameRootCleared";

    // selective pak extract.
    public const string PakCachePartialHit = "manager.pakCachePartialHit";
    public const string PakCacheSelective = "manager.pakCacheSelective";

    // sparse patch apply.
    public const string DeployUsedSparsePath = "manager.deployUsedSparsePath";
    public const string DeployFellBackToFullApply = "manager.deployFellBackToFullApply";

    // game-update awareness.
    public const string GameUpdatedSinceLastDeploy = "manager.gameUpdatedSinceLastDeploy";
    public const string OrphanedDeploysPresent = "manager.orphanedDeploysPresent";
    public const string OrphanedDeployCleaned = "manager.orphanedDeployCleaned";

    // backup retention.
    public const string DeployCleanRemoved = "manager.deployCleanRemoved";
    public const string DeployCleanKept = "manager.deployCleanKept";
    public const string DeployCleanRefusedLatest = "manager.deployCleanRefusedLatest";
    public const string DeploysStorageHigh = "manager.deploysStorageHigh";

    // remote sources — install --from gh:<owner>/<repo>[#<ref>]/<mod-id-or-path>.
    public const string RemoteFetchFailed = "manager.remoteFetchFailed";
    public const string RemoteIndexMalformed = "manager.remoteIndexMalformed";
    public const string ModNotInRepoIndex = "manager.modNotInRepoIndex";
    public const string RemoteResolvedToCommit = "manager.remoteResolvedToCommit";

    // remote collections — collection install --from gh:<owner>/<repo>[#<ref>]/<id>.
    public const string CrossRepoSourceResolved = "manager.crossRepoSourceResolved";
    public const string CollectionLockfileVersionMismatch = "manager.collectionLockfileVersionMismatch";
    public const string ProfileCreatedFromCollection = "manager.profileCreatedFromCollection";
    public const string ProfileActivatedFromCollection = "manager.profileActivatedFromCollection";
    // (manager.profileAlreadyExists already defined above with the other profile codes.)

    // mod.io adapter — install --from modio:<game>/<mod-id>[#<version>].
    public const string ModIoApiError = "manager.modIoApiError";
    public const string ModIoRateLimited = "manager.modIoRateLimited";
    public const string ModIoMapTypeSkipped = "manager.modIoMapTypeSkipped";
    public const string ModIoCollectionsUnsupported = "manager.modIoCollectionsUnsupported";
    public const string ModIoUnknownGameAlias = "manager.modIoUnknownGameAlias";
    public const string ModIoVersionPinNotImplemented = "manager.modIoVersionPinNotImplemented";
    // mod.io returned a download URL whose scheme is not https — refuse rather than
    // fetch UGC bytes over an unencrypted/unknown transport.
    public const string ModIoInsecureDownloadUrl = "manager.modIoInsecureDownloadUrl";

    // direct-URL ZIP source — install --from https://example.com/<mod>.zip.
    public const string DirectUrlFetched = "manager.directUrlFetched";
    public const string DirectUrlFetchFailed = "manager.directUrlFetchFailed";
    public const string DirectUrlInsecureHttp = "manager.directUrlInsecureHttp";
    public const string DirectUrlTraversalRefused = "manager.directUrlTraversalRefused";
    // a downloaded archive declares too many entries / too much uncompressed data to
    // extract safely — refused as a possible zip bomb.
    public const string DirectUrlArchiveTooLarge = "manager.directUrlArchiveTooLarge";
    public const string DirectUrlArchiveDrift = "manager.directUrlArchiveDrift";

    // catalogs — federated multi-subscription discovery.
    public const string CatalogFetchFailed = "manager.catalogFetchFailed";
    public const string CatalogMalformed = "manager.catalogMalformed";
    public const string CatalogSubscribed = "manager.catalogSubscribed";
    public const string CatalogUnsubscribed = "manager.catalogUnsubscribed";
    public const string CatalogCycleDetected = "manager.catalogCycleDetected";
    public const string CatalogDepthCapped = "manager.catalogDepthCapped";
    public const string CatalogStale = "manager.catalogStale";
    public const string CatalogCacheWritten = "manager.catalogCacheWritten";
    public const string CatalogCacheCorrupt = "manager.catalogCacheCorrupt";
    public const string CatalogInsecureHttp = "manager.catalogInsecureHttp";
    public const string CatalogRepoIndexPathConflict = "manager.catalogRepoIndexPathConflict";
    public const string DefaultCatalogSeeded = "manager.defaultCatalogSeeded";

    // tweak configuration — per-profile user overrides on top of a mod's declared tweaks.
    // tweakValueOutOfRange mirrors the patcher's plan-time warning code but is raised at
    // error severity here: at `tweak set` time the user supplied the value and can fix it.
    public const string TweakUnknownMod = "manager.tweakUnknownMod";
    public const string TweakUnknownId = "manager.tweakUnknownId";
    public const string TweakValueOutOfRange = "manager.tweakValueOutOfRange";
    public const string TweakValueInvalid = "manager.tweakValueInvalid";
    // A `collection install --overwrite` reseeded the profile's tweak map from the
    // collection, discarding the user's overrides stored since the last install.
    public const string TweakOverridesResetByReinstall = "manager.tweakOverridesResetByReinstall";

    // Tweak id migration when a mod author renames a tweak and lists the old id
    // under `aliases:`. No silent data loss on rename.
    public const string TweakMigratedFromAlias = "manager.tweakMigratedFromAlias";
    public const string TweakAliasConflict = "manager.tweakAliasConflict";
    public const string TweakOrphanedOverride = "manager.tweakOrphanedOverride";

    // expansion ownership — the plan/deploy gate. Presence is the hard constraint
    // (no pak to patch → error); ownership is advisory (present-but-not-owned still
    // deploys, for co-op parity with an owning host → warning, never blocks deploy).
    public const string ModExpansionNotPresent = "manager.modExpansionNotPresent";       // error  — required expansion absent on disk
    public const string ModExpansionNotOwned = "manager.modExpansionNotOwned";           // warning (non-blocking) — present, not owned
    public const string ExpansionOwnershipUnknown = "manager.expansionOwnershipUnknown"; // warning (non-blocking) — present, ownership not declared
    public const string ModOptionalExpansionSkipped = "manager.modOptionalExpansionSkipped";   // info — optional content's expansion is absent (skipped, real reason)
    public const string ModOptionalExpansionInactive = "manager.modOptionalExpansionInactive"; // info — optional content's expansion present but not effective (still deploys, solo-inert)
    // `expansions set` wrote a per-install ownership record.
    public const string ExpansionOwnershipSet = "manager.expansionOwnershipSet";         // info
    public const string ExpansionPackageNotDeclarable = "manager.expansionPackageNotDeclarable"; // error — tried to set core/tools (always owned) or an unknown package
}
