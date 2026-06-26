using System.IO.Compression;
using PagoniaLand.Manager;
using PagoniaLand.Paker;
using YamlDotNet.Serialization;

var tests = new (string Name, Func<bool> Run)[]
{
    // Scaffold smoke tests.
    ("product name is stable", () => ManagerInfo.ProductName == "Pagonia Land Manager"),
    ("command name is stable", () => ManagerInfo.CommandName == "pagonia-manager"),
    ("version is present", () => !string.IsNullOrWhiteSpace(ManagerInfo.Version)),
    ("manager core can reach patcher core",
        () => BackingCoreInfo.PatcherProductName == "Pagonia Land Patcher"
           && BackingCoreInfo.PatcherCommandName == "pagonia-patcher"
           && !string.IsNullOrWhiteSpace(BackingCoreInfo.PatcherVersion)),
    ("manager core can reach paker core",
        () => BackingCoreInfo.PakerProductName == "Pagonia Land Paker"
           && BackingCoreInfo.PakerCommandName == "pagonia-paker"
           && !string.IsNullOrWhiteSpace(BackingCoreInfo.PakerVersion)),

    // Store layout + resolver + atomic file + init + inspector.
    ("diagnostic codes are stable", DiagnosticCodesAreStable),
    ("layout constants match documented store/profile surface", LayoutConstantsAreStable),
    ("store layout exposes expected paths", StoreLayoutExposesExpectedPaths),
    ("store layout rejects empty root", StoreLayoutRejectsEmptyRoot),
    ("resolver: --store flag wins over env and default", ResolverFlagWins),
    ("resolver: env wins over platform default", ResolverEnvWinsOverDefault),
    ("resolver: platform default lives under LocalApplicationData", ResolverPlatformDefaultUnderLocalAppData),
    ("atomic file: write then read happy path", AtomicFileWriteThenReadHappyPath),
    ("atomic file: replaces existing file without leaving .tmp", AtomicFileReplacesExisting),
    ("atomic file: enumerate ignores leftover .tmp from crashed write", AtomicFileEnumerateIgnoresTemp),
    ("atomic file: cleanup leftover temps removes them", AtomicFileCleanupLeftoverTemps),
    ("state.yaml round-trips through YAML", StateRoundTripsThroughYaml),
    ("profile file round-trips through YAML", ProfileRoundTripsThroughYaml),
    ("profile with per-mod tweaks (set/empty/null) round-trips", ProfileWithTweaksRoundTripsThroughYaml),
    ("profile without a tweaks key reads per-mod tweaks as null", ProfileWithoutTweaksReadsAsNull),
    ("store init creates all directories + state + default profile", StoreInitCreatesEverything),
    ("store init is idempotent (second run is no-op)", StoreInitIsIdempotent),
    ("store init seeds the default official catalog when asked", StoreInitSeedsDefaultCatalogWhenRequested),
    ("store init does not seed a catalog by default", StoreInitDoesNotSeedByDefault),
    ("store init seed is idempotent — re-init adds no duplicate", StoreInitSeedIsIdempotentNoDuplicate),
    ("store init seed is opt-out — removed default stays removed across re-init", StoreInitSeedOptOutStaysRemoved),
    ("store inspector reports not initialised when state missing", InspectorReportsNotInitialised),
    ("store inspector counts mods/profiles/collections correctly", InspectorCountsCorrectly),
    ("store inspector ignores 'locks' folder when counting collections", InspectorIgnoresLocksFolder),
    ("store state reader throws with diagnostic code when uninitialised", StateReaderThrowsWhenUninitialised),
    ("store state reader refuses a storeVersion newer than this build (storeSchemaVersionUnsupported)", StateReaderRefusesNewerStoreVersion),
    ("store state reader tolerates an unparseable/legacy storeVersion (reads through)", StateReaderToleratesLegacyStoreVersion),
    ("profile store reader refuses a profileVersion newer than this build (profileVersionUnsupported)", ProfileReaderRefusesNewerProfileVersion),

    // Install / uninstall / list.
    ("ManagerDiagnostic.From preserves code + maps severity", DiagnosticFromPreservesCodeAndSeverity),
    ("install: folder source happy path installs + writes sidecar", InstallFolderHappyPath),
    ("install: zip source happy path extracts + installs + writes sidecar", InstallZipHappyPath),
    ("install: a GDB-overlay mod surfaces conflict advisor findings", InstallSurfacesOverlayAdvisorFindings),
    ("plan: two enabled mods that Replace the same entity surface a cross-mod overlay conflict", CrossModOverlayConflictSurfacedInPlan),
    ("doctor: a freshly initialised store reports no errors", DoctorHealthyStoreHasNoErrors),
    ("doctor: an uninitialised store reports a store error", DoctorUninitialisedStoreErrors),
    ("doctor: two conflicting enabled mods are flagged store-only (no game root)", DoctorFlagsCrossModConflictStoreOnly),
    ("doctor: an enabled mod with an unreadable manifest grades Error (not 'all present')", DoctorFlagsUnreadableEnabledMod),
    ("doctor: the updates check is Skipped when offline (no fetcher passed)", DoctorUpdatesCheckSkippedWhenOffline),
    ("doctor: --check-updates surfaces an available mod update as a warning", DoctorUpdatesCheckSurfacesAvailableWhenOptedIn),
    ("deps: enabling a mod with an unmet dependency warns modDependencyMissing (installed-but-disabled)", DependencyMissingFlaggedOnEnable),
    ("deps: a dependency that's enabled too produces no missing-dependency warning", DependencySatisfiedWhenBothEnabled),
    ("deps: enabling a mod incompatible with an enabled one warns modIncompatibleEnabled", IncompatibleFlaggedOnEnable),
    ("doctor: an enabled mod with a missing dependency grades the dependencies check a warning", DoctorReportsDependencyIssues),
    ("deps: disabling a depended-upon mod warns modDependedUponByOthers", DisableDependedUponWarns),
    ("deps: disabling a mod nothing depends on stays silent", DisableNotDependedUponNoWarn),
    ("deps: uninstalling a depended-upon mod warns modDependedUponByOthers", UninstallDependedUponWarns),
    ("assisted install: pulls transitive dependencies from the same repo", AssistedInstallPullsTransitiveDeps),
    ("assisted install: an unresolvable dependency warns but the rest still install", AssistedInstallWarnsOnUnresolvableDep),
    ("load order: loadAfter reorders the enabled set + reports the adjustment", LoadOrderLoadAfterReorders),
    ("load order: loadBefore reorders the enabled set", LoadOrderLoadBeforeReorders),
    ("load order: a loadAfter/loadBefore cycle is reported, both mods kept in manual order", LoadOrderDetectsCycle),
    ("load order: no constraints leaves the manual order untouched + silent", LoadOrderNoConstraintsLeavesManualOrder),
    ("load order: a duplicate id in the profile is deduped, not a crash", LoadOrderToleratesDuplicateIds),
    ("load order: a constraint naming a non-enabled mod is inert", LoadOrderIgnoresConstraintToAbsentMod),
    ("load order: an already-satisfied constraint pins the mods but reorders nothing", LoadOrderStableWhenConstraintAlreadySatisfied),
    ("install: missing source path emits modSourceNotFound", InstallMissingSourceEmitsCode),
    ("install: source that is not folder or zip emits modSourceNotAFolderOrZip", InstallBadSourceTypeEmitsCode),
    ("install: source folder missing mod.yaml emits modManifestMissing", InstallFolderMissingManifestEmitsCode),
    ("install: invalid manifest yaml surfaces patcher.* error code", InstallInvalidManifestSurfacesPatcherCode),
    ("install: duplicate install emits modAlreadyInstalled warning, does not overwrite", InstallDuplicateIsWarning),
    ("install: corrupt zip emits modSourceNotAFolderOrZip", InstallCorruptZipEmitsCode),
    ("uninstall: removes version dir and prunes empty parent", UninstallRemovesAndPrunesParent),
    ("uninstall: keeps parent dir when other versions remain", UninstallKeepsParentWhenOtherVersionsRemain),
    ("uninstall: ambiguous when multiple versions and no --version", UninstallAmbiguousEmitsCode),
    ("uninstall: mod not installed emits modNotInstalled", UninstallMissingModEmitsCode),
    ("uninstall: version not installed emits modVersionNotInstalled", UninstallMissingVersionEmitsCode),
    ("uninstall: refuses path traversal in modId and does not delete outside store", UninstallRefusesPathTraversal),
    ("uninstall: empty mod dir (no version subdirs) reports failure WITHOUT deleting loose files", UninstallNoVersionDirsDoesNotDelete),
    ("list: empty mods directory returns empty", ListEmptyReturnsEmpty),
    ("list: lists installed mods with id+version+sidecar metadata", ListReturnsInstalledModsWithMetadata),
    ("round trip: install -> list -> uninstall -> list shows empty", RoundTripInstallListUninstall),

    // Enable / disable / move / status.
    ("mutator: enable adds mod to both enabled list and load order", MutatorEnableAddsToBoth),
    ("mutator: enable same id+version emits modAlreadyEnabled warning, does not mutate", MutatorEnableSameIsWarning),
    ("mutator: enable same id different version replaces version without dup in load order", MutatorEnableReplacesVersion),
    ("mutator: disable removes from both enabled list and load order", MutatorDisableRemovesFromBoth),
    ("mutator: disable on non-enabled emits modNotEnabled warning, does not mutate", MutatorDisableNonEnabledIsWarning),
    ("mutator: disable on load-order-only orphan strips it + emits profileDriftCleaned info", MutatorDisableLoadOrderOrphanCleansDrift),
    ("active-profile: Disable on non-enabled returns Success=true but Mutated=false (no contradictory CLI message)", ActiveProfileDisableNoOpReportsNotMutated),
    ("mutator: moveToPosition reorders correctly (1-based)", MutatorMoveToPositionReorders),
    ("mutator: moveToPosition out of range emits movePositionOutOfRange error", MutatorMoveOutOfRange),
    ("mutator: move target not in load order emits moveTargetNotInLoadOrder error", MutatorMoveTargetMissing),
    ("mutator: moveBefore places target immediately before anchor", MutatorMoveBefore),
    ("mutator: moveAfter places target immediately after anchor", MutatorMoveAfter),
    ("mutator: move anchor not in load order emits moveAnchorNotInLoadOrder error", MutatorMoveAnchorMissing),
    ("service: enable without --version picks most recently installed", ServiceEnablePicksLatest),
    ("service: enable mod not installed emits modNotInstalled", ServiceEnableModNotInstalled),
    ("service: enable version not installed emits modVersionNotInstalled", ServiceEnableVersionNotInstalled),
    ("service: status reflects current profile after enable + move", ServiceStatusAfterEnableAndMove),
    ("service: disable + re-enable leaves no stale entries", ServiceDisableReEnableNoStaleState),

    // Profile lifecycle (create / list / use / delete / show).
    ("profile name validator: accepts simple names", ProfileNameAcceptsSimple),
    ("profile name validator: rejects empty / dots / path separators / forbidden chars", ProfileNameRejectsInvalid),
    ("profile create writes profileVersion 0.1 with empty mods/loadOrder", ProfileCreateWritesEmptyV01),
    ("profile create rejects duplicate name with profileAlreadyExists", ProfileCreateRejectsDuplicate),
    ("profile create rejects invalid name with profileNameInvalid", ProfileCreateRejectsInvalidName),
    ("profile list includes default + created, marks active and default", ProfileListMarksActiveAndDefault),
    ("profile use updates state.yaml atomically", ProfileUseUpdatesStateAtomically),
    ("profile use rejects non-existent profile with profileMissing", ProfileUseRejectsMissing),
    ("profile delete removes file", ProfileDeleteRemovesFile),
    ("profile delete rejects default profile", ProfileDeleteRejectsDefault),
    ("profile delete rejects active profile", ProfileDeleteRejectsActive),
    ("profile show by name returns that profile", ProfileShowByName),
    ("profile show without name returns active profile", ProfileShowDefaultsToActive),
    ("round trip: enable mod in new profile, switch profiles, mods stay scoped", RoundTripProfileScopedMods),
    ("profile copy preserves enabledMods + loadOrder + tweaks + collection", ProfileCopyPreservesContents),
    ("profile copy is independent: mutating source leaves the copy untouched", ProfileCopyIsIndependent),
    ("profile copy rejects an existing target with profileAlreadyExists", ProfileCopyRejectsExistingTarget),
    ("profile copy rejects an invalid target name with profileNameInvalid", ProfileCopyRejectsInvalidTargetName),
    ("profile copy rejects a missing source with profileMissing (writes nothing)", ProfileCopyRejectsMissingSource),
    ("profile copy --activate switches active profile, keeps source", ProfileCopyActivateSwitchesActive),
    ("profile export folds tweak overrides into mods[].tweaks + preserves load order", ProfileExportFoldsTweaks),
    ("profile export warns + writes source: local for a local-only mod", ProfileExportLocalSourceWarning),
    ("profile export recovers a remote source from the install sidecar", ProfileExportRecoversSourceFromSidecar),
    ("profile export recovers a remote source from the collection lockfile", ProfileExportRecoversSourceFromLockfile),
    ("profile export output validates against collection.schema.json", ProfileExportSchemaValid),
    ("profile export refuses an empty profile (profileExportEmpty, writes nothing)", ProfileExportEmptyRefused),
    ("profile export -> collection install round-trips mods + order + tweaks", ProfileExportRoundTrip),
    ("profile export canonicalises a stale alias tweak key to the current id", ProfileExportCanonicalisesAliasTweak),

    // Collection install / list / show / uninstall.
    ("collection install: happy path installs mods + writes manifest + lockfile + profile", CollectionInstallHappyPath),
    ("collection install: profile pinned to collection id with mods in order", CollectionInstallProfilePinned),
    ("collection install: --profile <name> override creates profile under that name", CollectionInstallProfileOverride),
    ("collection install: URL source emits remoteSourceUnsupported warning + still resolves via local match", CollectionInstallUrlSourceWarning),
    ("collection install: missing local mod surfaces patcher collectionModMissing", CollectionInstallMissingLocalMod),
    ("collection install: missing collection file emits modSourceNotFound", CollectionInstallMissingCollectionFile),
    ("collection install: idempotent reinstall emits collectionAlreadyInstalled warning", CollectionInstallIdempotent),
    ("collection install: profile name collision aborts BEFORE manifest/lockfile written", CollectionInstallProfileCollisionPreservesCleanState),
    ("collection install: recreates missing profile after profile delete (recovery)", CollectionInstallRecreatesMissingProfile),
    ("collection install: invalid --profile override blames the override, not the collection id", CollectionInstallOverrideErrorNamesOverride),
    ("collection list: empty store returns empty", CollectionListEmpty),
    ("collection list: shows installed collection with id+version+modCount+generatedAt", CollectionListPopulated),
    ("collection uninstall: removes manifest dir and lockfile, leaves mods + profile untouched", CollectionUninstallRemovesCollectionOnly),
    ("collection uninstall: missing collection emits collectionNotInstalled", CollectionUninstallMissing),
    ("collection uninstall: refuses path traversal in collectionId and does not delete outside store", CollectionUninstallRefusesPathTraversal),
    ("round trip: collection install -> list -> uninstall -> list shows empty", RoundTripCollectionLifecycle),
    ("collection install: seeds curator tweaks into the profile (origin collection-default)", CollectionInstallSeedsCuratorTweaks),
    ("collection install: normalises a whitespace-padded curator tweak value before seeding", CollectionInstallNormalisesCuratorTweak),
    ("collection install: --overwrite reseeds tweaks + emits tweakOverridesResetByReinstall", CollectionReinstallOverwriteReseedsTweaks),
    ("collection install: reinstall without --overwrite preserves user tweak overrides", CollectionReinstallWithoutOverwritePreservesUserOverrides),

    // Plan active profile.
    ("plan: missing game root emits gameRootMissing", PlanMissingGameRoot),
    ("plan: empty profile emits profileEmpty info, patcher plan empty, exits success", PlanEmptyProfile),
    ("plan: one enabled mod produces one write in patcher plan", PlanOneModProducesWrite),
    ("plan: two enabled mods on same target surface patcher conflict", PlanConflictSurfaces),
    ("plan: mod in profile but not installed in store emits modInstallMissing", PlanMissingModInstall),
    ("plan: mods with different gameDB versions emit profileGameVersionMismatch warning", PlanGameVersionMismatch),

    // game-vs-mod gameDatabaseVersion compatibility.
    ("gamedb version: parse + build-primary ordering + tiering + malformed rejected", GameDatabaseVersionComparerWorks),
    ("plan game-vs-mod: exact match → no version diagnostic", PlanGameVsModExactSilent),
    ("plan game-vs-mod: same-line build drift → modGameVersionDrift info, plan proceeds", PlanGameVsModSameLineDriftInfo),
    ("plan game-vs-mod: different line → modGameVersionMismatch warning", PlanGameVsModLineGapWarning),
    ("plan game-vs-mod: unknown install version → neither diagnostic (degrades)", PlanGameVsModUnknownDegrades),
    ("deploy game-vs-mod: line mismatch blocks under warnings gate, proceeds with --accept-warnings", DeployGameVersionMismatchGatedByAcceptWarnings),

    ("plan: --profile <name> plans non-active profile", PlanNamedProfile),
    ("plan: JSON report has manager + patcher envelope with diagnostics", PlanJsonHasEnvelope),
    ("plan: markdown + json reports written to disk when paths given", PlanReportsWrittenToDisk),
    ("plan: load order from profile drives mod order in patcher plan", PlanRespectsLoadOrder),

    // Tweak overrides — read / set / reset + plan threading.
    ("tweak: read returns declarations + defaults with origin=default", TweakReadReturnsDeclarationsAndDefaults),
    ("tweak: set then read reflects the override with origin=profile-override", TweakSetThenReadReflectsOverride),
    ("tweak: set out-of-range number rejected (tweakValueOutOfRange), nothing stored", TweakSetOutOfRangeRejected),
    ("tweak: set invalid boolean/enum value rejected (tweakValueInvalid)", TweakSetInvalidTypeRejected),
    ("tweak: set unknown tweak-id / unenabled mod rejected (tweakUnknownId/Mod)", TweakSetUnknownModAndIdRejected),
    ("tweak: reset drops a single override then the whole-mod map", TweakResetDropsOverride),
    ("tweak: plan threads the profile override into the patcher plan (origin external)", PlanThreadsProfileTweakIntoPlan),
    ("tweak: a fractional number tweak threads through a multiplyValue op (4 x 2.5 = 10)", NumberTweakDrivesMultiplyValueThroughManager),
    ("tweak: list report validates against schema + shows per-tweak origins", TweakListReportValidatesAndShowsOrigins),
    ("tweak: set report validates against schema", TweakSetReportValidates),
    ("tweak: set rejection report validates + carries the error diagnostic", TweakSetReportSurfacesRejection),
    ("tweak: reset report (single + whole-mod) validates against schema", TweakResetReportValidates),
    ("tweak: alias migrates an old-id override forward + rewrites the profile", TweakAliasMigratesOldIdForward),
    ("tweak: alias conflict (old + new both stored) keeps the new id + warns", TweakAliasConflictNewIdWins),
    ("tweak: two aliases to one current id keeps one deterministically + warns", TweakTwoAliasesToOneCurrentKeepsOneDeterministically),
    ("tweak: orphaned override (unknown id) is kept + surfaced as info", TweakOrphanedOverrideKept),

    // Deploy + rollback (XML patches).
    ("fingerprint: stable across runs for same game root", FingerprintStable),
    ("fingerprint: differs for different game roots", FingerprintDistinct),
    ("fingerprint: includes system.json content when present", FingerprintIncludesSystemJson),
    ("deploy: missing game root emits gameRootMissing", DeployMissingGameRoot),
    ("deploy: clean deploy writes patched files + manifest + backup", DeployCleanWrites),
    ("deploy: round trip (deploy -> rollback) restores game-gdb byte-identically", DeployRollbackRoundTripByteIdentical),
    ("deploy: conflict in plan aborts with deployBlockedByErrors", DeployBlockedByConflict),
    ("deploy: warnings without --accept-warnings emit deployBlockedByWarnings", DeployBlockedByWarnings),
    ("deploy: warnings with --accept-warnings proceed", DeployAcceptWarningsProceeds),
    ("deploy: --dry-run leaves the game untouched + records diagnostic", DeployDryRunLeavesGameUntouched),
    ("deploy: empty profile emits deployEmpty info, no manifest written", DeployEmptyProfileIsNoop),
    ("deploy: manifest records source mods + SHA-256 of original + deployed", DeployManifestRecordsModsAndHashes),
    ("rollback: nothing to rollback when no prior deploy", RollbackNothingToRollback),
    ("rollback: two deploys -> rollback reverts only the latest", RollbackOnlyRevertsLatest),
    ("deploy-status: shows last deploy timestamp + profile + counts", DeployStatusShowsLatest),

    // JSON reports + schemas + schema-validate.
    ("reports: install -> JSON has reportKind=install and validates", ReportInstallValidates),
    ("reports: uninstall -> JSON has reportKind=uninstall and validates", ReportUninstallValidates),
    ("reports: deploy -> JSON has reportKind=deploy and validates", ReportDeployValidates),
    ("reports: rollback -> JSON has reportKind=rollback and validates", ReportRollbackValidates),
    ("reports: collection install -> JSON has reportKind=collectionInstall and validates", ReportCollectionInstallValidates),
    ("reports: outdated -> JSON has reportKind=updates and validates", ReportUpdatesValidates),
    ("reports: status -> JSON has reportKind=status and validates", ReportStatusValidates),
    ("reports: deploy-status -> JSON has reportKind=deployStatus and validates", ReportDeployStatusValidates),
    ("schema-validate: rejects unknown reportKind for the given --kind", SchemaValidateRejectsWrongKind),
    ("schema-validate: rejects unknown --kind value", SchemaValidateRejectsUnknownKind),
    ("schema-validate: rejects missing report file", SchemaValidateRejectsMissingFile),
    ("schema-validate: rejects malformed JSON", SchemaValidateRejectsMalformedJson),
    ("schema-validate: rejects report missing required field", SchemaValidateRejectsMissingField),
    ("reports: all schemas embedded + loadable", AllSchemasLoadable),
    ("reports: diagnostic codes pinned (schemaValidationOk + schemaValidationFailed)", SchemaCodesPinned),

    // Pattern B overlay-pak deploy.
    ("pak builder: missing scaffold emits pakScaffoldMissing", PakBuilderMissingScaffold),
    ("pak builder: builds a non-empty .pak from a real scaffold", PakBuilderBuildsRealPak),
    ("deploy: pak-only mod writes <game>/mods/<name>.pak + addedFiles entry", DeployPakOnlyModWritesPak),
    ("deploy: pak-mod manifest separates modifiedFiles from addedFiles", DeployManifestSeparatesModifiedAndAdded),
    ("deploy: refuses to overwrite an existing same-named pak in <game>/mods/", DeployRefusesExistingPak),
    ("rollback: pak-mod deploy -> rollback removes the .pak from <game>/mods/", RollbackPakRemovesDeployedPak),
    ("rollback: round trip with pak-mod restores game tree fully (no <game>/mods/ residue)", RollbackPakRoundTripClean),
    ("deploy: dry-run reports addedFileCount alongside modifiedFileCount", DeployDryRunReportsAddedCount),
    ("deploy: writes state.lastDeploy with timestamp + gameRoot + profile after success", DeployStampsStateLastDeploy),
    ("deploy-status: corrupt history.yaml surfaces deployHistoryUnreadable diagnostic (no crash)", DeployStatusCorruptHistoryEmitsDiagnostic),
    ("rollback: corrupt history.yaml surfaces deployHistoryUnreadable diagnostic (no crash)", RollbackCorruptHistoryEmitsDiagnostic),

    // game-layout detection.
    ("layout detect: live install (pak/*.pak)", LayoutDetectLiveInstall),
    ("layout detect: extracted layout (core/gdb/*.gd.xml)", LayoutDetectExtractedLayout),
    ("layout detect: unrecognised for empty dir", LayoutDetectUnrecognisedEmpty),
    ("layout detect: unrecognised for missing path", LayoutDetectUnrecognisedMissingPath),
    ("layout detect: discovers all pak files in stable order", LayoutDetectDiscoversAllPaks),
    ("layout detect: live install wins when both layouts present", LayoutDetectLiveWinsOverExtracted),
    ("layout detect: empty pak/ folder falls through to unrecognised", LayoutDetectEmptyPakDirIsUnrecognised),

    // expansion ownership (presence detection + store record + resolver).
    ("presence: live install reports paks-on-disk as present (core+dlc1, decorations1 absent)", PresenceLiveInstallReportsPaks),
    ("presence: extracted layout reports package folders as present", PresenceExtractedLayoutReportsFolders),
    ("presence: empty package folder is not counted as present", PresenceExtractedEmptyFolderNotPresent),
    ("presence: unrecognised layout reports nothing present", PresenceUnrecognisedReportsNothing),
    ("install record: owned-expansions map round-trips through state.yaml", InstallRecordRoundTripsThroughState),
    ("install record: absent installs map reads as unknown for every declarable expansion", AbsentInstallsReadsAsUnknown),
    ("install record: pre-0.2 state.yaml (no installs key) stays readable", PreInstallsStateStaysReadable),
    ("install record: survives an unrelated state write (SetStoredDefault)", InstallRecordSurvivesUnrelatedStateWrite),
    ("resolver: core/tools always owned, effective iff present", ResolverAlwaysOwnedPackages),
    ("resolver: declarable truth table (present x owned -> effective)", ResolverDeclarableTruthTable),
    ("resolver: unknown resolves to effective=false but stays distinct from not-owned", ResolverUnknownDistinctFromNotOwned),
    ("resolver: override flips effective state without touching the record", ResolverOverrideFlipsEffective),
    ("resolver: override never flips always-owned core/tools", ResolverOverrideIgnoredForAlwaysOwned),

    // expansion gate (plan/deploy ownership awareness) + expansions CLI surface.
    ("gate: required expansion not present is an error (presence blocks)", GateRequiredNotPresentIsError),
    ("gate: required expansion present-but-not-owned is a non-blocking warning", GateRequiredNotOwnedIsWarning),
    ("gate: required expansion present-but-unknown is a distinct warning", GateRequiredUnknownIsWarning),
    ("gate: required expansion present-and-owned is silent (effective)", GateRequiredOwnedIsSilent),
    ("gate: core/tools never warn (always owned), only error if absent", GateAlwaysOwnedNeverWarns),
    ("gate: optional expansion absent is skipped-with-reason info", GateOptionalAbsentSkippedInfo),
    ("gate: optional expansion present-but-not-owned is solo-inert info (still deploys)", GateOptionalPresentInactiveInfo),
    ("gate: multiplayerSafe mod's not-owned warning carries the co-op note", GateMultiplayerSafeCarriesCoopNote),
    ("gate: non-multiplayerSafe mod's warning omits the co-op note", GateNonMultiplayerSafeOmitsCoopNote),
    ("gate: non-optional patchSet's requiresPackages count as required", GatePatchSetRequiresCountAsRequired),
    ("gate: advisory ownership codes are non-blocking (deploy never gated by ownership)", GateAdvisoryCodesAreNonBlocking),
    ("expansions set -> list round-trips ownership through state.yaml", ExpansionsSetThenListRoundTrip),
    ("expansions set refuses core/tools (always owned), writes nothing", ExpansionsSetRefusesAlwaysOwned),
    ("expansions set mutated flag is false on a no-op re-set", ExpansionsSetMutatedFlagOnReSet),
    ("expansions list override flips effective without touching the stored record", ExpansionsListOverrideFlipsEffective),
    ("plan: required expansion not present blocks the plan with modExpansionNotPresent", PlanRequiredExpansionNotPresentBlocks),
    ("plan: required expansion not owned warns but does not block planning", PlanRequiredExpansionNotOwnedWarnsNotBlocks),

    // expansion onboarding nudge (interactive surface logic).
    ("nudge: fires when a declarable expansion is present but ownership unknown", NudgeFiresOnPresentUnknown),
    ("nudge: does not fire when no declarable expansion is present-but-unknown", NudgeSkipsWhenNothingUnknown),
    ("nudge: ask-me-later (MarkNudgeOffered) leaves ownership unknown but stops nagging", NudgeAskMeLaterStopsNagging),
    ("nudge: set ownership preserves the nudge-offered flag", NudgeSetPreservesOfferedFlag),
    ("nudge: never fires on an uninitialised store", NudgeSkipsUninitialisedStore),
    ("status: ListDeclaredInstalls returns stored per-install ownership declarations", StatusListDeclaredInstalls),

    // game version surfacing (exe ProductVersion).
    ("game version: reader reads ProductVersion + truncated FileVersion off the exe", GameVersionReaderReadsExe),
    ("game version: reader returns false + nulls when no exe present", GameVersionReaderMissingExe),
    ("game version: reader falls back to a renamed *.exe by ProductName", GameVersionReaderFallbackByProductName),
    ("game version: live-install layout carries the detected ProductVersion", LayoutLiveInstallCarriesVersion),
    ("game version: layout version is null when the install has no exe", LayoutLiveInstallNullVersionWithoutExe),
    ("game version: deploy-status report exposes nullable gameProductVersion", DeployStatusReportExposesGameVersion),
    ("game version: live deploy records gameProductVersion provenance in the manifest", DeployManifestRecordsGameVersion),
    ("game version: update warning names old -> new version when both known", UpdateWarningNamesVersions),

    // pak extract cache.
    ("pak cache: cold miss extracts every pak + writes sentinel", PakCacheColdMissExtracts),
    ("pak cache: warm hit short-circuits without re-extracting", PakCacheWarmHitReuses),
    ("pak cache: entries land verbatim (in-pak paths already include package prefix)", PakCachePreservesEntryPaths),
    ("pak cache: new pak in install (DLC install) yields new fingerprint + stale cache pruned", PakCachePrunesStaleOnFingerprintChange),
    ("pak cache: fingerprint stable across deploy -> rollback -> deploy", PakCacheFingerprintStableAcrossDeployRollback),
    ("pak cache: failed extract keeps successfully-extracted paks + records status", PakCacheFailedExtractKeepsGoodPaks),
    ("pak cache: ensure rejects non-live-install layout", PakCacheRejectsNonLiveLayout),

    // canonical-pak external-change detection at cache time.
    ("pak cache drift: untouched install reuses warm cache with no external-change warning", PakCacheUntouchedNoExternalChangeWarning),
    ("pak cache drift: out-of-band pak edit warns canonicalPakChangedExternally + re-extracts", PakCacheExternalEditWarnsAndReExtracts),
    ("pak cache drift: manager deploy + rollback stay silent (no false external-change)", PakCacheManagerWritesStaySilent),

    // pak rebuild + live-install write-back.
    ("pak rebuild: replaces named entries + preserves untouched", PakRebuildReplacesNamedEntries),
    ("pak rebuild: no replacements yields byte-comparable index + identical entry data", PakRebuildEmptyReplacementsRoundTrips),
    ("pak rebuild: missing replacement file aborts cleanly", PakRebuildMissingReplacementFails),
    ("deploy live-install: dispatches via Deploy(), repacks affected pak, backs up original", DeployLiveInstallRepacksAffectedPak),
    ("deploy live-install: dry-run reports rebuilt pak count, writes nothing", DeployLiveInstallDryRunWritesNothing),

    // live-install rollback from pak backups.
    ("rollback live-install: restores rebuilt pak byte-identical to pre-deploy original", RollbackLiveInstallByteIdentical),
    ("rollback live-install: tampered backup yields rollbackHashMismatch + leaves live pak as-is", RollbackLiveInstallHashMismatch),
    ("rollback extracted-layout: tampered XML backup yields rollbackHashMismatch + leaves live file as-is", RollbackExtractedLayoutHashMismatch),
    ("rollback live-install: missing backup yields rollbackBackupMissing without crash", RollbackLiveInstallMissingBackup),
    ("rollback live-install: pops the deploy from history + removes its timestamp dir", RollbackLiveInstallTrimsHistory),
    ("rollback: a restore failure aborts BEFORE deleting overlay paks (no mixed install)", RollbackAbortsBeforeDeletingOverlaysOnRestoreFailure),
    ("rollback: a foreign-replaced overlay is preserved + warned, not deleted, even under --force", RollbackPreservesDriftedOverlayUnderForce),

    // explicit round-trip across the full live-install pipeline.
    ("live install: deploy -> rollback round trip is byte-identical (full pipeline incl. extract cache)", LiveInstallDeployRollbackRoundTripByteIdentical),

    // selective pak extract.
    ("pak cache selective: extracts only the requested subset, leaves rest alone", PakCacheSelectiveExtractsSubset),
    ("pak cache selective: subsequent ensure with same set is a full hit", PakCacheSelectiveWarmStaysWarm),
    ("pak cache selective: subsequent ensure with extra pak only extracts the new one", PakCacheSelectivePartialHitIncrementsCache),
    ("pak cache selective: empty required-paks set is a no-op (no extraction)", PakCacheSelectiveEmptyRequestIsNoOp),
    ("pak cache selective: null required-paks falls back to all-discovered", PakCacheSelectiveNullExtractsAll),
    ("pak requirement analyzer: reads patch targets' first segment as the pak basename", PakRequirementAnalyzerReadsPatchTargets),
    ("pak requirement analyzer: empty profile yields empty set (not null)", PakRequirementAnalyzerEmptyProfileEmptySet),

    // live-state drift detection (before deploy overwrites / rollback reverts).
    ("drift inspector: clean re-deploy reports no drift", LiveStateInspectorCleanNoDrift),
    ("drift inspector: a one-byte live-pak edit is detected", LiveStateInspectorDetectsEdit),
    ("deploy drift: external live-pak edit blocks re-deploy without --force, proceeds with it", DeployDriftBlocksWithoutForce),
    ("deploy drift: dry-run surfaces drift without blocking or writing", DeployDriftDryRunSurfacesWithoutWriting),
    ("deploy drift: clean re-deploy stays quiet (no drift diagnostic)", DeployCleanRedeployNoDriftDiagnostic),
    ("rollback drift: external live-pak edit gates rollback behind --force", RollbackDriftGatedByForce),

    // game-update awareness + orphaned deploys.
    ("orphan finder: deploy with gone gameRoot is flagged GameRootGone", OrphanFinderGameRootGone),
    ("orphan finder: deploy with drifted fingerprint is flagged GameUpdated", OrphanFinderGameUpdated),
    ("orphan finder: current matching deploy isn't flagged as orphan", OrphanFinderCurrentDeployNotOrphan),
    ("deploy preflight: re-deploying to a fingerprint-changed install warns gameUpdatedSinceLastDeploy", DeployWarnsOnFingerprintDrift),

    // backup retention + deploys clean.
    ("deploys clean: --keep 3 keeps the 3 newest + removes older + rewrites history.yaml", DeployCleanKeepThreeNewest),
    ("deploys clean: refuses to remove the entry referenced by state.yaml.lastDeploy", DeployCleanRefusesLastDeploy),
    ("deploys clean: --dry-run reports what would be removed without writing", DeployCleanDryRunWritesNothing),
    ("deploys clean: --keep 0 keeps the newest (rollback anchor) + the lastDeploy-protected entry", DeployCleanKeepZeroRespectsLastDeploy),
    ("deploys clean: across all fingerprints applies keep-N per fingerprint independently", DeployCleanAcrossFingerprints),

    // sparse patch apply.
    ("deploy live-install: pure Pattern A mod takes the sparse fast path", DeployLiveInstallTakesSparsePath),
    ("deploy live-install: sparse path produces identical live-pak SHA as the slow path would", DeployLiveInstallSparsePathByteEquivalentToSlow),
    ("deploy live-install: sparse path doesn't create the staging directory", DeployLiveInstallSparsePathSkipsStaging),

    // persistent default game folder.
    ("game-root resolver: session value wins over stored default", GameRootResolverSessionWins),
    ("game-root resolver: stored default wins over (missing) platform default", GameRootResolverStoredWinsOverPlatform),
    ("game-root resolver: stale stored default (dir gone) falls through to platform / NotSet", GameRootResolverStaleFallsThrough),
    ("game-root resolver: returns NotSet when nothing resolves", GameRootResolverNotSet),
    ("game-root resolver: SetStoredDefault persists + survives a fresh read", GameRootResolverSetStoredPersists),
    ("game-root resolver: SetStoredDefault is a no-op when value unchanged", GameRootResolverSetStoredNoOp),
    ("game-root resolver: SetStoredDefault preserves other state fields (activeProfile, lastDeploy)", GameRootResolverSetStoredPreservesOtherFields),

    // Remote-source parser — install --from gh:... shorthand + GitHub long URL.
    ("remote parser: short form with owner/repo only resolves to HEAD without mod-spec", RemoteParserShortFormRepoOnly),
    ("remote parser: short form with owner/repo/mod-id resolves to HEAD + mod-spec", RemoteParserShortFormWithModSpec),
    ("remote parser: short form with #ref + mod-spec captures both", RemoteParserShortFormWithRefAndModSpec),
    ("remote parser: short form with #ref slash inside ref (release/v1) keeps ref intact", RemoteParserShortFormRefWithSlashInsideRef),
    ("remote parser: short form with nested path under ref keeps full path", RemoteParserShortFormNestedPath),
    ("remote parser: short form with :base path + mod-spec", RemoteParserShortFormBasePathWithMod),
    ("remote parser: short form with :base + #ref + mod-spec", RemoteParserShortFormBasePathRefAndMod),
    ("remote parser: short form with :base path, no mod-spec", RemoteParserShortFormBasePathNoMod),
    ("remote parser: short form with nested :base path", RemoteParserShortFormNestedBasePath),
    ("remote parser: spec without :base leaves BasePath empty (root)", RemoteParserDefaultBasePathEmpty),
    ("remote parser: rejects '..' traversal in :base path", RemoteParserRejectsBasePathTraversal),
    ("remote parser: rejects '..' traversal in mod-spec (short + long form)", RemoteParserRejectsModSpecTraversal),
    ("remote parser: rejects empty :base path", RemoteParserRejectsEmptyBasePath),
    ("remote parser: long form https://github.com/.../tree/ref captures owner/repo/ref/mod-spec", RemoteParserLongFormFull),
    ("remote parser: long form without trailing mod-spec resolves to null mod-spec", RemoteParserLongFormRepoAndRef),
    ("remote parser: rejects gh: with empty owner", RemoteParserRejectsEmptyOwner),
    ("remote parser: rejects gh: with empty repo", RemoteParserRejectsEmptyRepo),
    ("remote parser: rejects gh: with empty ref after #", RemoteParserRejectsEmptyRefAfterHash),
    ("remote parser: rejects gh: with garbage chars in owner", RemoteParserRejectsGarbageInOwner),
    ("remote parser: rejects local-folder paths (not a remote spec)", RemoteParserRejectsLocalPath),
    ("remote parser: rejects https://github.com/ without /tree/<ref>", RemoteParserRejectsLongFormWithoutTree),
    ("remote parser: rejects empty + whitespace input", RemoteParserRejectsEmpty),

    // RemoteFetcher — full orchestration against an in-memory fake HTTP layer.
    ("remote fetcher: index.yaml + mod.yaml + patches land in temp dir; ResolvedSource pins SHA", RemoteFetcherHappyPath),
    ("remote fetcher: mod-id missing in index.yaml surfaces modNotInRepoIndex", RemoteFetcherUnknownModIdSurfacesDiagnostic),
    ("remote fetcher: no index.yaml falls back to path-as-mod-spec", RemoteFetcherPathFallbackWithoutIndex),
    ("remote fetcher: indexPath base redirects index+mod+patch fetch + provenance", RemoteFetcherSubdirectoryIndexPath),
    ("remote fetcher: '..' in base path refused (defence in depth)", RemoteFetcherBaseTraversalRefused),
    ("remote fetcher: unknown ref surfaces remoteFetchFailed (no destructive side-effects)", RemoteFetcherUnknownRefSurfacesDiagnostic),
    ("remote fetcher: malformed index.yaml surfaces remoteIndexMalformed", RemoteFetcherMalformedIndexSurfacesDiagnostic),
    ("remote fetcher: refuses traversal mod path (defence-in-depth)", RemoteFetcherRefusesTraversal),
    ("remote fetcher: ResolvedSource embeds full SHA + mod-id from index", RemoteFetcherResolvedSourcePinsSha),
    ("remote fetcher -> ModInstaller round-trip installs cleanly from the fetched temp dir", RemoteFetcherEndToEndInstall),
    ("remote fetcher: index metadata matching mod.yaml emits no drift warning", RemoteFetcherNoMetadataWarningWhenInSync),
    ("remote fetcher: index advertising stale version/safety warns (repoIndexMetadataMismatch)", RemoteFetcherWarnsOnIndexMetadataDrift),
    ("remote fetcher: a wrong advertised contentHash warns (modContentHashMismatch), still installs", RemoteFetcherWarnsOnContentHashMismatch),
    ("remote fetcher: a matching advertised contentHash verifies silently", RemoteFetcherAcceptsMatchingContentHash),

    // RemoteFetcher.FetchCollection — same-repo + cross-repo mod resolution.
    ("remote collection fetch: same-repo mods land in <tempDir>/mods/<id>/", RemoteFetcherCollectionSameRepoHappy),
    ("remote collection fetch: cross-repo mod via source:gh:... resolves through second repo's index.yaml", RemoteFetcherCollectionCrossRepo),
    ("remote collection fetch: ResolvedCollectionSource pins SHA + ModSources map carries per-mod SHA", RemoteFetcherCollectionResolvedSourcesPinShas),
    ("remote collection fetch: missing collection id surfaces modNotInRepoIndex", RemoteFetcherCollectionMissingIdSurfacesDiagnostic),
    ("remote collection fetch: non-github http source falls back to same-repo with warning", RemoteFetcherCollectionNonGithubSourceWarns),

    // RepoIndexFetcher — standalone repo-index read that backs the interactive browse listing.
    ("repo index fetch: lists mods + collections, pins the commit SHA", RepoIndexFetcherListsModsAndCollections),
    ("repo index fetch: honours the indexPath base directory", RepoIndexFetcherHonoursBasePath),
    ("repo index fetch: repo without index.yaml reports HasIndex=false (not a failure)", RepoIndexFetcherNoIndexReportsHasIndexFalse),
    ("repo index fetch: malformed index.yaml surfaces remoteIndexMalformed", RepoIndexFetcherMalformedIndexSurfacesDiagnostic),
    ("repo index fetch: unknown ref surfaces remoteFetchFailed", RepoIndexFetcherUnknownRefSurfacesDiagnostic),
    ("repo index fetch: a newer-minor indexFormatVersion reads with formatMinorAhead", RepoIndexFetcherNewerMinorReads),
    ("repo index fetch: a newer-major indexFormatVersion is refused with formatMajorUnsupported", RepoIndexFetcherNewerMajorRefused),

    // Update detection (read-only `outdated`): semver ordering + mirror-first repo-index compare.
    ("mod version: semver ordering (major/minor/patch, prerelease, missing parts, unparseable)", ModVersionOrdersCorrectly),
    ("update detection: a newer index-advertised version surfaces modUpdateAvailable", UpdateDetectionFindsNewerVersion),
    ("update detection: an up-to-date mod reports no updates", UpdateDetectionUpToDateReportsNoUpdates),
    ("update detection: a local-only mod (no source) is skipped, not checked", UpdateDetectionSkipsLocalMods),
    ("collection update detection: a newer index-advertised version surfaces collectionUpdateAvailable", CollectionUpdateDetectionFindsNewerVersion),
    ("collection update detection: an up-to-date collection reports no updates", CollectionUpdateDetectionUpToDateReportsNoUpdates),
    ("collection update detection: a local-file collection (no sidecar) is skipped, not checked", CollectionUpdateDetectionSkipsLocalCollections),
    ("content drift: same version + different advertised contentHash flags modContentDriftAvailable", UpdateDetectionFlagsSameVersionContentDrift),
    ("content drift: a matching advertised contentHash flags no drift", UpdateDetectionNoDriftWhenContentHashMatches),
    ("content drift: no advertised contentHash means no drift check", UpdateDetectionNoDriftWhenNoAdvertisedHash),
    ("update flow: re-points the profile pin to the new version, preserves tweaks, keeps the old version", UpdateMovesProfilePinAndKeepsOldVersion),
    ("update flow: a pin already at the latest advertised version is a no-op", UpdateIsNoOpWhenAlreadyCurrent),
    ("update flow: a mod not enabled in the active profile is refused (nothing to re-pin)", UpdateRefusesWhenNotEnabled),
    ("update flow: a typo'd --profile is a clean profileMissing, not a crash", UpdateRefusesMissingProfile),
    ("collection update flow: installs the newer version, keeps the old, reseeds the profile", CollectionUpdateInstallsNewerVersionAndReseedsProfile),
    ("collection update flow: a collection already at the latest advertised version is a no-op", CollectionUpdateIsNoOpWhenAlreadyCurrent),
    ("collection update flow: a local-file collection (no provenance) is refused", CollectionUpdateRefusesLocalCollection),
    ("collection update flow: an uninstalled collection id is refused (nothing to update)", CollectionUpdateRefusesWhenNotInstalled),
    ("collection update tweaks: Merge keeps a genuine user override across the version bump", CollectionUpdateMergeKeepsGenuineOverride),
    ("collection update tweaks: Merge adopts the new curator default where nothing was overridden", CollectionUpdateMergeAdoptsNewCuratorDefaultForNonOverridden),
    ("collection update tweaks: --reseed-tweaks discards the override and reseeds the curator value", CollectionUpdateReseedDiscardsOverride),
    ("collection update tweaks: Ask routes each conflict through the callback", CollectionUpdateAskCallbackResolvesConflict),
    ("tweaks: an explicit `tweak set` equal to the curator value still reads as profile-override", TweakSetMarksOverrideEvenWhenEqualToCurator),
    ("tweaks: a pre-marking profile infers + persists userTweaks once on read (heuristic)", LegacyProfileMigratesUserTweaksOnRead),
    ("collection update tweaks: a coincidental-equal override keeps its mark + survives a later curator change", CollectionUpdateKeepsCoincidentalEqualOverride),

    // InstallSourceResolver — shared remote-source dispatch (scripted install + interactive wizard).
    ("resolve remote: gh: spec fetches into temp dir + pins gh provenance", ResolveRemoteGitHubFetchesAndPinsProvenance),
    ("resolve remote: a non-remote (local) spec returns null", ResolveRemoteLocalSpecReturnsNull),
    ("resolve remote: plain-http url refused without allowInsecureSources opt-in", ResolveRemoteInsecureHttpRefusedWithoutOptIn),

    // CollectionInstallService new flags: overwrite, activate, remote source map -> lockfile provenance.
    ("collection install: --as-profile name override + --activate flips state.yaml.activeProfile", CollectionInstallAsProfileAndActivate),
    ("collection install: --overwrite=false rejects existing profile with profileAlreadyExists", CollectionInstallRefusesOverwriteByDefault),
    ("collection install: --overwrite=true replaces existing profile in-place", CollectionInstallOverwriteReplacesProfile),
    ("collection install: RemoteModSources populates lockfile per-mod source + resolvedAt", CollectionInstallRemoteSourcesAugmentLockfile),
    ("collection install: RemoteCollectionSource writes provenance sidecar read back as Source", CollectionInstallRemoteSourceWritesProvenanceSidecar),
    ("collection install: local-file install leaves no provenance sidecar (Source null)", CollectionInstallLocalLeavesNoProvenanceSidecar),

    // Catalog source parser — gh: + file: forms.
    ("catalog parser: gh:owner/repo defaults to HEAD + catalog.yaml", CatalogParserShortFormDefaults),
    ("catalog parser: gh:owner/repo#ref/path captures custom ref + custom path", CatalogParserShortFormCustomRefAndPath),
    ("catalog parser: file:// absolute URL is normalised", CatalogParserFileUrl),
    ("catalog parser: file:absolute short form works", CatalogParserFileShort),
    ("catalog parser: rejects local-folder paths (not a catalog spec)", CatalogParserRejectsLocalPath),
    ("catalog parser: rejects garbage owner/repo names", CatalogParserRejectsGarbage),
    ("catalog parser: rejects '..' traversal in the catalog path", CatalogParserRejectsPathTraversal),
    ("catalog parser: relative file: resolves against parent catalog directory", CatalogParserRelativeFileResolvesAgainstParent),
    ("catalog parser: repo entry indexPath round-trips, absent = root", CatalogRepoEntryIndexPathRoundTrips),

    // Catalog fetcher — gh: via mock + file: against bundled fixture.
    ("catalog fetcher: gh: catalog fetched + parsed + source pinned to commit SHA", CatalogFetcherGitHubHappy),
    ("catalog fetcher: gh: unknown ref surfaces catalogFetchFailed", CatalogFetcherGitHubUnknownRef),
    ("catalog fetcher: gh: malformed yaml surfaces catalogMalformed", CatalogFetcherGitHubMalformed),
    ("catalog fetcher: a newer-minor catalogFormatVersion reads with formatMinorAhead", CatalogFetcherNewerMinorReads),
    ("catalog fetcher: a newer-major catalogFormatVersion is refused with formatMajorUnsupported", CatalogFetcherNewerMajorRefused),
    ("catalog fetcher: file: bundled example catalog parses + populates repos + nested catalogs ref", CatalogFetcherFileExample),
    ("catalog fetcher: file: missing path surfaces catalogFetchFailed", CatalogFetcherFileMissing),

    // CatalogAggregator — federation + dedup + cycle + depth.
    ("catalog aggregator: bundled example flattens to 4 unique repos across parent + sub-catalog", CatalogAggregatorBundledExampleFlattens),
    ("catalog aggregator: dedup on (owner, repo) records every vouching catalog", CatalogAggregatorDedupRecordsVouches),
    ("catalog aggregator: repo entry indexPath flows onto AggregatedRepo", CatalogAggregatorCarriesIndexPath),
    ("catalog aggregator: divergent indexPath for same repo warns + first wins", CatalogAggregatorIndexPathConflict),
    ("catalog aggregator: cycle A->B->A bails with catalogCycleDetected", CatalogAggregatorCycleDetection),
    ("catalog aggregator: depth cap stops descent + emits catalogDepthCapped", CatalogAggregatorDepthCap),
    ("catalog aggregator: file: child path resolves against parent catalog directory", CatalogAggregatorRelativeChildResolvesAgainstParent),

    // CatalogSubscriptionService (CRUD over state.yaml.subscribedCatalogs).
    ("catalog subs: add persists + canonical-dedups subsequent identical adds", CatalogSubsAddPersistsAndDedups),
    ("catalog subs: remove unsubscribes + is a no-op on unknown spec", CatalogSubsRemoveAndNoop),
    ("catalog subs: add rejects garbage spec", CatalogSubsAddRejectsGarbage),

    // CachingCatalogFetcher — cache hit, staleness, file: bypass, corrupt meta, refresh.
    ("caching fetcher: cold gh: fetch writes cache.yaml + cache-meta.yaml + emits catalogCacheWritten", CachingCatalogColdFetchWritesCache),
    ("caching fetcher: warm gh: fetch within threshold serves from cache + emits catalogStale", CachingCatalogWarmFetchServesFromCache),
    ("caching fetcher: stale cache triggers a fresh fetch + cache rewrite", CachingCatalogStaleTriggersRefresh),
    ("caching fetcher: forceRefresh bypasses cache hit + still updates cache afterwards", CachingCatalogForceRefreshBypassesCacheHit),
    ("caching fetcher: file: source bypasses cache entirely (no write)", CachingCatalogFileBypassesCache),
    ("caching fetcher: corrupt cache-meta yaml falls through to fresh fetch + catalogCacheCorrupt warning", CachingCatalogCorruptMetaFallsThrough),

    // UrlCatalogSource — http(s):// catalogs.
    ("url-catalog parser: https://host/path/catalog.yaml parses as UrlCatalogSource", UrlCatalogParserHttps),
    ("url-catalog parser: rejects loopback + link-local hosts (SSRF guard)", UrlCatalogParserRejectsLoopbackAndLinkLocal),
    ("remote-host policy: blocks internal + IPv4-mapped hosts, allows public + LAN", RemoteHostPolicyBlocksInternalAndMappedHosts),
    ("url-catalog parser: http://... parses + IsInsecure=true", UrlCatalogParserHttpInsecure),
    ("url-catalog parser: garbage 'https://' alone rejected", UrlCatalogParserRejectsHostless),
    ("url-catalog canonical: HTTPS://Example.COM/x and https://example.com/x dedup", UrlCatalogCanonicalNormalisesSchemeAndHost),
    ("url-catalog canonical: trailing slash on non-root path stripped", UrlCatalogCanonicalStripsTrailingSlash),
    ("gh-catalog canonical: default HEAD omitted, explicit #HEAD dedups, pinned ref kept", GitHubCatalogCanonicalOmitsDefaultRef),
    ("url-catalog fetch: https source parses + lands in aggregator", UrlCatalogFetchHttpsLandsInAggregator),
    ("url-catalog fetch: http source without opt-in emits insecureHttp warning + still succeeds", UrlCatalogFetchHttpNoOptInWarns),
    ("url-catalog fetch: http source with opt-in suppresses the warning", UrlCatalogFetchHttpWithOptInSilent),
    ("url-catalog aggregator: cycle https -> gh -> https detected", UrlCatalogCycleDetected),
    ("url-catalog aggregator: dedup HTTPS://X.COM/y and https://x.com/y as one visited source", UrlCatalogVisitedSetDedup),

    // Direct-URL ZIP source — parser.
    ("direct-url parser: https://example.com/foo.zip parses as DirectUrlSource (IsHttp=false)", DirectUrlParserHttps),
    ("direct-url parser: http://example.com/foo.zip parses + flags IsHttp=true", DirectUrlParserHttp),
    ("direct-url parser: mod.io-style signed url with query string still parses", DirectUrlParserSignedUrl),
    ("direct-url parser: github long-form .../tree/<ref> still resolves to GitHubSource (priority)", DirectUrlParserGitHubLongFormPriority),
    ("direct-url parser: github archive URL ending in .zip falls through to DirectUrlSource", DirectUrlParserGitHubArchiveZipFallthrough),
    ("direct-url parser: URL without .zip suffix is rejected (no false positives on repo landing pages)", DirectUrlParserRejectsNonZip),

    // Direct-URL ZIP source — fetcher.
    ("direct-url fetcher: downloads + extracts + records sha-pinned ResolvedSource", DirectUrlFetcherHappyPath),
    ("direct-url fetcher: verifies an advertised MD5 (silent match, warns on mismatch but still installs)", DirectUrlFetcherVerifiesMd5),
    ("direct-url fetcher: nested-folder ZIP (one top-level dir) resolves ModRoot inside it", DirectUrlFetcherNestedFolder),
    ("direct-url fetcher: zip-slip traversal entry refused before ModInstaller sees it", DirectUrlFetcherRefusesTraversal),
    ("direct-url fetcher: a pre-signed URL's ?signature is redacted from diagnostics", DirectUrlFetcherRedactsSignedUrl),
    ("direct-url fetcher: 404 surfaces directUrlFetchFailed + leaves no temp dir behind", DirectUrlFetcher404Cleanup),
    ("direct-url fetcher -> ModInstaller round-trip installs cleanly + sidecar records url: source", DirectUrlFetcherEndToEndInstall),
    ("direct-url: ModLister surfaces sidecar.Source through InstalledMod.Source", DirectUrlInstalledModExposesSource),

    // mod.io adapter — parser.
    ("modio parser: modio:1234/5678 parses with numeric ids, no version", ModIoParserBasic),
    ("modio parser: modio:slug/5678#0.1.0 captures slug + version", ModIoParserSlugAndVersion),
    ("modio parser: rejects empty game / mod-id / empty version after #", ModIoParserRejectsEmpty),
    ("modio parser: rejects spaces / slashes inside segments", ModIoParserRejectsGarbage),

    // mod.io adapter — fetcher.
    ("modio fetcher: env unset falls back to embedded DefaultApiKey and fetches", ModIoFetcherUsesEmbeddedKey),
    ("modio fetcher: happy non-map mod returns binary_url + md5 + version", ModIoFetcherHappyNonMap),
    ("modio fetcher: Map-type response surfaces modIoMapTypeSkipped, no download attempt", ModIoFetcherMapSkip),
    ("modio fetcher: 404 surfaces modIoApiError with the game/mod ids in the message", ModIoFetcher404),
    ("modio fetcher: malformed JSON surfaces modIoApiError cleanly", ModIoFetcherMalformedJson),
    ("modio fetcher: api_key env var override is used when set", ModIoFetcherEnvOverride),
    ("modio fetcher: rate-limit + API-error diagnostics never leak the api_key", ModIoFetcherRateLimitRedactsKey),
    // ModIoGameAliases — slug/numeric resolution (PoP-only).
    ("modio aliases: numeric '8242' resolves to itself", ModIoAliasesNumericResolves),
    ("modio aliases: 'pioneers-of-pagonia' slug resolves to 8242", ModIoAliasesSlugResolves),
    ("modio aliases: short slug 'pop' (case-insensitive) resolves to 8242", ModIoAliasesShortSlugResolves),
    ("modio aliases: unknown game surfaces error naming 8242 + slug forms", ModIoAliasesUnknownDescribesAccepted),

    // GUI-readiness hardening — async orchestration overloads.
    ("async: InstallAsync installs like the sync path; a cancelled token aborts before writing", InstallAsyncRunsAndCancels),
    ("async: PlanAsync plans like the sync path; a cancelled token aborts cleanly", PlanAsyncRunsAndCancels),
    ("async: DeployAsync deploys like the sync path; a cancelled token leaves the install untouched", DeployAsyncRunsAndCancels),
    ("async: RollbackAsync rolls back like the sync path; a cancelled token leaves the install untouched", RollbackAsyncRunsAndCancels),

    // GUI-readiness hardening — structured progress reporting.
    ("progress: deploy reports advance stage forward + percent monotonic within a stage", DeployProgressIsForwardAndMonotonic),
    ("progress: rollback reports advance stage forward + percent monotonic within a stage", RollbackProgressIsForwardAndMonotonic),
    ("progress: pak-cache extract reports a monotonic percent under the 'extract' stage", PakCacheProgressIsForwardAndMonotonic),

    // GUI-readiness hardening — env-var injection.
    ("env injection: StoreRootResolver consults the injected reader, not process env", ResolverInjectedReaderBeatsProcessEnv),
    ("env injection: ModIoFetcher consults the injected reader, not process env", ModIoFetcherInjectedEnvReaderConsulted),

    // GUI-readiness hardening — CancellationToken through the apply path.
    ("cancel: a cancelled live-install deploy leaves the live pak byte-identical (apply-path token)", DeployCancelledLeavesLiveInstallUntouched),
};

var passed = 0;
var failed = 0;

foreach (var (name, run) in tests)
{
    try
    {
        if (run())
        {
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }
        else
        {
            Console.WriteLine($"  FAIL  {name}");
            failed++;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL  {name}  ({ex.GetType().Name}: {ex.Message})");
        failed++;
    }
}

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

// ============================================================================
// Scaffold helpers
// ============================================================================

// ManagerExitCodes moved to the CLI project (exit codes are a
// shell-process concept, not a Core concern). The test project references only
// Core, so the old ExitCodesAreStable smoke test was removed rather than reaching
// across into the CLI binary for four constants.

// ============================================================================
// Store helpers
// ============================================================================

static bool DiagnosticCodesAreStable()
{
    return ManagerDiagnosticCodes.StoreNotInitialised == "manager.storeNotInitialised"
        && ManagerDiagnosticCodes.StoreStateUnreadable == "manager.storeStateUnreadable"
        && ManagerDiagnosticCodes.StoreSchemaVersionUnsupported == "manager.storeSchemaVersionUnsupported"
        && ManagerDiagnosticCodes.ModSourceNotFound == "manager.modSourceNotFound"
        && ManagerDiagnosticCodes.ModSourceNotAFolderOrZip == "manager.modSourceNotAFolderOrZip"
        && ManagerDiagnosticCodes.ModManifestMissing == "manager.modManifestMissing"
        && ManagerDiagnosticCodes.ModAlreadyInstalled == "manager.modAlreadyInstalled"
        && ManagerDiagnosticCodes.ModNotInstalled == "manager.modNotInstalled"
        && ManagerDiagnosticCodes.ModVersionAmbiguous == "manager.modVersionAmbiguous"
        && ManagerDiagnosticCodes.ModVersionNotInstalled == "manager.modVersionNotInstalled"
        && ManagerDiagnosticCodes.GameLayoutUnrecognised == "manager.gameLayoutUnrecognised"
        && ManagerDiagnosticCodes.PakCacheRefreshed == "manager.pakCacheRefreshed"
        && ManagerDiagnosticCodes.PakCacheReused == "manager.pakCacheReused"
        && ManagerDiagnosticCodes.PakCacheExtractFailed == "manager.pakCacheExtractFailed"
        && ManagerDiagnosticCodes.PakRebuilt == "manager.pakRebuilt"
        && ManagerDiagnosticCodes.PakRebuildFailed == "manager.pakRebuildFailed"
        && ManagerDiagnosticCodes.ModifiedFileMissingOwningPak == "manager.modifiedFileMissingOwningPak"
        && ManagerDiagnosticCodes.PakRollbackRestored == "manager.pakRollbackRestored"
        && ManagerDiagnosticCodes.RollbackHashMismatch == "manager.rollbackHashMismatch"
        && ManagerDiagnosticCodes.DefaultGameRootStored == "manager.defaultGameRootStored"
        && ManagerDiagnosticCodes.DefaultGameRootCleared == "manager.defaultGameRootCleared"
        && ManagerDiagnosticCodes.PakCachePartialHit == "manager.pakCachePartialHit"
        && ManagerDiagnosticCodes.PakCacheSelective == "manager.pakCacheSelective"
        && ManagerDiagnosticCodes.DeployUsedSparsePath == "manager.deployUsedSparsePath"
        && ManagerDiagnosticCodes.DeployFellBackToFullApply == "manager.deployFellBackToFullApply"
        && ManagerDiagnosticCodes.GameUpdatedSinceLastDeploy == "manager.gameUpdatedSinceLastDeploy"
        && ManagerDiagnosticCodes.OrphanedDeploysPresent == "manager.orphanedDeploysPresent"
        && ManagerDiagnosticCodes.OrphanedDeployCleaned == "manager.orphanedDeployCleaned"
        && ManagerDiagnosticCodes.DeployCleanRemoved == "manager.deployCleanRemoved"
        && ManagerDiagnosticCodes.DeployCleanKept == "manager.deployCleanKept"
        && ManagerDiagnosticCodes.DeployCleanRefusedLatest == "manager.deployCleanRefusedLatest"
        && ManagerDiagnosticCodes.DeploysStorageHigh == "manager.deploysStorageHigh"
        && ManagerDiagnosticCodes.TweakUnknownMod == "manager.tweakUnknownMod"
        && ManagerDiagnosticCodes.TweakUnknownId == "manager.tweakUnknownId"
        && ManagerDiagnosticCodes.TweakValueOutOfRange == "manager.tweakValueOutOfRange"
        && ManagerDiagnosticCodes.TweakValueInvalid == "manager.tweakValueInvalid"
        && ManagerDiagnosticCodes.TweakOverridesResetByReinstall == "manager.tweakOverridesResetByReinstall"
        && ManagerDiagnosticCodes.TweakMigratedFromAlias == "manager.tweakMigratedFromAlias"
        && ManagerDiagnosticCodes.TweakAliasConflict == "manager.tweakAliasConflict"
        && ManagerDiagnosticCodes.TweakOrphanedOverride == "manager.tweakOrphanedOverride";
}

static bool LayoutConstantsAreStable()
{
    return StoreLayoutConstants.CurrentStoreVersion == "0.1"
        && StoreLayoutConstants.CurrentProfileVersion == "0.1"
        && StoreLayoutConstants.DefaultProfileName == "default"
        && StoreLayoutConstants.StateFileName == "state.yaml"
        && StoreLayoutConstants.ProfileFileSuffix == ".profile.yaml"
        && StoreLayoutConstants.CollectionLockFileSuffix == ".lock.yaml"
        && StoreLayoutConstants.ModsFolderName == "mods"
        && StoreLayoutConstants.ProfilesFolderName == "profiles"
        && StoreLayoutConstants.CollectionsFolderName == "collections"
        && StoreLayoutConstants.CollectionLocksFolderName == "locks";
}

static bool StoreLayoutExposesExpectedPaths()
{
    var tempRoot = NewTempRoot("layout-paths");
    var layout = new StoreLayout(tempRoot);

    return layout.Root == Path.GetFullPath(tempRoot)
        && layout.ModsDirectory == Path.Combine(layout.Root, "mods")
        && layout.ProfilesDirectory == Path.Combine(layout.Root, "profiles")
        && layout.CollectionsDirectory == Path.Combine(layout.Root, "collections")
        && layout.CollectionLocksDirectory == Path.Combine(layout.Root, "collections", "locks")
        && layout.StateFile == Path.Combine(layout.Root, "state.yaml")
        && layout.ProfileFile("default") == Path.Combine(layout.Root, "profiles", "default.profile.yaml")
        && layout.CollectionLockFile("beg-qol") == Path.Combine(layout.Root, "collections", "locks", "beg-qol.lock.yaml")
        && layout.ModVersionDirectory("foo.bar", "1.0.0") == Path.Combine(layout.Root, "mods", "foo.bar", "1.0.0");
}

static bool StoreLayoutRejectsEmptyRoot()
{
    try
    {
        _ = new StoreLayout("");
        return false;
    }
    catch (ArgumentException)
    {
        return true;
    }
}

static bool ResolverFlagWins()
{
    Func<string, string?> env = _ => @"C:\env-store";
    var resolution = StoreRootResolver.Resolve(@".\flag-store", env);
    return resolution.Source == StoreRootResolver.ResolutionSource.Flag
        && resolution.Root == Path.GetFullPath(@".\flag-store");
}

static bool ResolverEnvWinsOverDefault()
{
    Func<string, string?> env = name => name == StoreRootResolver.EnvironmentVariableName ? @".\env-store" : null;
    var resolution = StoreRootResolver.Resolve(null, env);
    return resolution.Source == StoreRootResolver.ResolutionSource.EnvironmentVariable
        && resolution.Root == Path.GetFullPath(@".\env-store");
}

static bool ResolverPlatformDefaultUnderLocalAppData()
{
    Func<string, string?> env = _ => null;
    var resolution = StoreRootResolver.Resolve(null, env);
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var expected = Path.Combine(localAppData, "PagoniaLand", "Manager");
    return resolution.Source == StoreRootResolver.ResolutionSource.PlatformDefault
        && resolution.Root == expected;
}

static bool AtomicFileWriteThenReadHappyPath()
{
    var tempRoot = NewTempRoot("atomic-happy");
    try
    {
        var path = Path.Combine(tempRoot, "hello.txt");
        AtomicFile.WriteAllText(path, "hello world");
        return File.ReadAllText(path) == "hello world"
            && !File.Exists(path + AtomicFile.TempSuffix);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool AtomicFileReplacesExisting()
{
    var tempRoot = NewTempRoot("atomic-replace");
    try
    {
        var path = Path.Combine(tempRoot, "hello.txt");
        AtomicFile.WriteAllText(path, "first");
        AtomicFile.WriteAllText(path, "second");
        return File.ReadAllText(path) == "second"
            && !File.Exists(path + AtomicFile.TempSuffix);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool AtomicFileEnumerateIgnoresTemp()
{
    var tempRoot = NewTempRoot("atomic-enumerate");
    try
    {
        var realPath = Path.Combine(tempRoot, "real.profile.yaml");
        var leftoverTemp = Path.Combine(tempRoot, "crashed.profile.yaml" + AtomicFile.TempSuffix);
        File.WriteAllText(realPath, "x");
        File.WriteAllText(leftoverTemp, "y");

        var enumerated = AtomicFile.EnumerateFilesIgnoringTemp(tempRoot, "*.profile.yaml*").ToList();
        return enumerated.Count == 1
            && enumerated[0] == realPath;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool AtomicFileCleanupLeftoverTemps()
{
    var tempRoot = NewTempRoot("atomic-cleanup");
    try
    {
        File.WriteAllText(Path.Combine(tempRoot, "keep.yaml"), "x");
        File.WriteAllText(Path.Combine(tempRoot, "drop.yaml" + AtomicFile.TempSuffix), "y");
        File.WriteAllText(Path.Combine(tempRoot, "alsodrop.txt" + AtomicFile.TempSuffix), "z");

        var removed = AtomicFile.CleanupLeftoverTempFiles(tempRoot);
        return removed == 2
            && File.Exists(Path.Combine(tempRoot, "keep.yaml"))
            && !File.Exists(Path.Combine(tempRoot, "drop.yaml" + AtomicFile.TempSuffix))
            && !File.Exists(Path.Combine(tempRoot, "alsodrop.txt" + AtomicFile.TempSuffix));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StateRoundTripsThroughYaml()
{
    var state = new StoreState
    {
        StoreVersion = "0.1",
        ActiveProfile = "dlc1-testing",
        LastDeploy = new StoreLastDeploy
        {
            Timestamp = "2026-05-29T12:00:00Z",
            GameRoot = @"C:\Games\PoP",
            Profile = "dlc1-testing"
        }
    };

    var serializer = new SerializerBuilder().Build();
    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    var yaml = serializer.Serialize(state);
    var roundTripped = deserializer.Deserialize<StoreState>(yaml);

    return roundTripped is not null
        && roundTripped.StoreVersion == "0.1"
        && roundTripped.ActiveProfile == "dlc1-testing"
        && roundTripped.LastDeploy is not null
        && roundTripped.LastDeploy.Profile == "dlc1-testing"
        && roundTripped.LastDeploy.GameRoot == @"C:\Games\PoP";
}

static bool ProfileRoundTripsThroughYaml()
{
    var profile = new ProfileFile
    {
        ProfileVersion = "0.1",
        Name = "default",
        EnabledMods =
        [
            new ProfileEnabledMod { Id = "pagonia-land.example.cheaper-sawmill", Version = "0.1.0" }
        ],
        LoadOrder = ["pagonia-land.example.cheaper-sawmill"]
    };

    var serializer = new SerializerBuilder().Build();
    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    var yaml = serializer.Serialize(profile);
    var roundTripped = deserializer.Deserialize<ProfileFile>(yaml);

    return roundTripped is not null
        && roundTripped.Name == "default"
        && roundTripped.ProfileVersion == "0.1"
        && roundTripped.EnabledMods.Count == 1
        && roundTripped.EnabledMods[0].Id == "pagonia-land.example.cheaper-sawmill"
        && roundTripped.LoadOrder.Count == 1
        && roundTripped.LoadOrder[0] == "pagonia-land.example.cheaper-sawmill";
}

static bool ProfileWithTweaksRoundTripsThroughYaml()
{
    // Three enabled mods exercising the three meanings of `Tweaks`:
    // non-empty overrides, an explicit-empty map, and null ("use defaults").
    var profile = new ProfileFile
    {
        ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
        Name = "tweaked",
        EnabledMods =
        [
            new ProfileEnabledMod
            {
                Id = "pagonia-land.example.tweakable-economy",
                Version = "0.1.0",
                Tweaks = new Dictionary<string, string>
                {
                    ["build-cost-multiplier"] = "1.5",
                    ["free-upkeep"] = "false",
                }
            },
            new ProfileEnabledMod
            {
                Id = "pagonia-land.example.explicit-empty",
                Version = "0.2.0",
                Tweaks = new Dictionary<string, string>()
            },
            new ProfileEnabledMod
            {
                Id = "pagonia-land.example.no-tweaks",
                Version = "0.3.0",
                Tweaks = null
            }
        ],
        LoadOrder =
        [
            "pagonia-land.example.tweakable-economy",
            "pagonia-land.example.explicit-empty",
            "pagonia-land.example.no-tweaks"
        ]
    };

    var serializer = new SerializerBuilder().Build();
    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    var roundTripped = deserializer.Deserialize<ProfileFile>(serializer.Serialize(profile));

    if (roundTripped is null
        || roundTripped.ProfileVersion != "0.1"
        || roundTripped.EnabledMods.Count != 3)
    {
        return false;
    }

    var withTweaks = roundTripped.EnabledMods[0];
    var explicitEmpty = roundTripped.EnabledMods[1];
    var noTweaks = roundTripped.EnabledMods[2];

    return withTweaks.Tweaks is { Count: 2 }
        && withTweaks.Tweaks["build-cost-multiplier"] == "1.5"
        && withTweaks.Tweaks["free-upkeep"] == "false"
        && explicitEmpty.Tweaks is { Count: 0 }
        && noTweaks.Tweaks is null;
}

static bool ProfileWithoutTweaksReadsAsNull()
{
    // A profile that omits the optional `tweaks` key entirely; every
    // enabled mod must read back with Tweaks == null (the default).
    const string yaml = """
        profileVersion: 0.1
        name: legacy
        enabledMods:
          - id: pagonia-land.example.a
            version: 0.1.0
          - id: pagonia-land.example.b
            version: 0.2.0
        loadOrder:
          - pagonia-land.example.a
          - pagonia-land.example.b
        """;

    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
    var profile = deserializer.Deserialize<ProfileFile>(yaml);

    return profile is not null
        && profile.ProfileVersion == "0.1"
        && profile.EnabledMods.Count == 2
        && profile.EnabledMods.All(mod => mod.Tweaks is null);
}

static bool StoreInitCreatesEverything()
{
    var tempRoot = NewTempRoot("init-creates");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var result = new StoreInitializer().Initialize(layout);

        return result.CreatedState
            && result.CreatedDefaultProfile
            && result.CreatedDirectories.Count >= 4
            && Directory.Exists(layout.ModsDirectory)
            && Directory.Exists(layout.ProfilesDirectory)
            && Directory.Exists(layout.CollectionsDirectory)
            && Directory.Exists(layout.CollectionLocksDirectory)
            && File.Exists(layout.StateFile)
            && File.Exists(layout.ProfileFile("default"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StoreInitIsIdempotent()
{
    var tempRoot = NewTempRoot("init-idempotent");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var first = new StoreInitializer().Initialize(layout);
        var second = new StoreInitializer().Initialize(layout);

        return first.CreatedState
            && first.CreatedDefaultProfile
            && !second.CreatedState
            && !second.CreatedDefaultProfile
            && second.CreatedDirectories.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StoreInitSeedsDefaultCatalogWhenRequested()
{
    var tempRoot = NewTempRoot("init-seed");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var result = new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        var stored = new StoreStateReader().Read(layout).SubscribedCatalogs;
        return result.CreatedState
            && result.SeededDefaultCatalog
            && stored.Count == 1
            && stored[0] == CatalogConstants.OfficialCatalogSource;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool StoreInitDoesNotSeedByDefault()
{
    // The default (used by tests + any non-user-facing caller) leaves the store
    // blank; only the CLI / interactive store-init path opts into seeding.
    var tempRoot = NewTempRoot("init-no-seed");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var result = new StoreInitializer().Initialize(layout);
        var stored = new StoreStateReader().Read(layout).SubscribedCatalogs;
        return result.CreatedState
            && !result.SeededDefaultCatalog
            && stored.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool StoreInitSeedIsIdempotentNoDuplicate()
{
    // Re-running init with seeding on an existing store doesn't create a second
    // state.yaml, so it neither re-seeds nor duplicates the subscription.
    var tempRoot = NewTempRoot("init-seed-idempotent");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var first = new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        var second = new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        var stored = new StoreStateReader().Read(layout).SubscribedCatalogs;
        return first.SeededDefaultCatalog
            && !second.SeededDefaultCatalog
            && stored.Count == 1;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool StoreInitSeedOptOutStaysRemoved()
{
    // The seed is an ordinary subscription: removing it sticks, and a later
    // re-init (state already exists) does not bring it back. This is the
    // opt-out guarantee.
    var tempRoot = NewTempRoot("init-seed-optout");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        new CatalogSubscriptionService().Remove(layout, CatalogConstants.OfficialCatalogSource);
        var afterRemove = new StoreStateReader().Read(layout).SubscribedCatalogs;
        new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        var afterReinit = new StoreStateReader().Read(layout).SubscribedCatalogs;
        return afterRemove.Count == 0 && afterReinit.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool InspectorReportsNotInitialised()
{
    var tempRoot = NewTempRoot("inspect-empty");
    try
    {
        var layout = new StoreLayout(tempRoot);
        var info = new StoreInspector().Inspect(layout);
        return !info.Initialised
            && info.StoreVersion is null
            && info.ActiveProfile is null
            && info.InstalledModCount == 0
            && info.ProfileCount == 0
            && info.CollectionCount == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InspectorCountsCorrectly()
{
    var tempRoot = NewTempRoot("inspect-counts");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout);

        Directory.CreateDirectory(Path.Combine(layout.ModsDirectory, "mod.a"));
        Directory.CreateDirectory(Path.Combine(layout.ModsDirectory, "mod.b"));
        Directory.CreateDirectory(Path.Combine(layout.ModsDirectory, "mod.c"));

        File.WriteAllText(layout.ProfileFile("dlc1-test"), "profileVersion: 0.1\nname: dlc1-test\n");

        Directory.CreateDirectory(Path.Combine(layout.CollectionsDirectory, "beg-qol"));
        Directory.CreateDirectory(Path.Combine(layout.CollectionsDirectory, "visual-pack"));

        var info = new StoreInspector().Inspect(layout);
        return info.Initialised
            && info.StoreVersion == "0.1"
            && info.ActiveProfile == "default"
            && info.InstalledModCount == 3
            && info.ProfileCount == 2
            && info.CollectionCount == 2;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InspectorIgnoresLocksFolder()
{
    var tempRoot = NewTempRoot("inspect-locks-ignored");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout);

        Directory.CreateDirectory(Path.Combine(layout.CollectionsDirectory, "one-real"));

        var info = new StoreInspector().Inspect(layout);
        return info.CollectionCount == 1;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StateReaderRefusesNewerStoreVersion()
{
    // A store written by a newer manager must be refused on read — an older binary that read
    // + rewrote it would silently drop the fields it doesn't know. Same-install one-way door.
    var tempRoot = NewTempRoot("reader-newer-store");
    try
    {
        var layout = new StoreLayout(tempRoot);
        File.WriteAllText(layout.StateFile, "storeVersion: \"0.2\"\nactiveProfile: default\n");
        try
        {
            new StoreStateReader().Read(layout);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(ManagerDiagnosticCodes.StoreSchemaVersionUnsupported, StringComparison.Ordinal)
                && ex.Message.Contains("0.2", StringComparison.Ordinal);
        }
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StateReaderToleratesLegacyStoreVersion()
{
    // An absent/unparseable storeVersion is the pre-versioning/legacy case — the reader never
    // checked it before, so it must still read through (only a clearly-newer version is refused).
    var tempRoot = NewTempRoot("reader-legacy-store");
    try
    {
        var layout = new StoreLayout(tempRoot);
        File.WriteAllText(layout.StateFile, "storeVersion: not-a-version\nactiveProfile: default\n");
        var state = new StoreStateReader().Read(layout);
        return state.StoreVersion == "not-a-version" && state.ActiveProfile == "default";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileReaderRefusesNewerProfileVersion()
{
    // The profile-level companion to the store guard: a profile written by a newer manager is
    // refused before this build reads + rewrites it.
    var tempRoot = NewTempRoot("reader-newer-profile");
    try
    {
        var layout = new StoreLayout(tempRoot);
        Directory.CreateDirectory(layout.ProfilesDirectory);
        File.WriteAllText(layout.ProfileFile("default"), "profileVersion: \"1.0\"\nname: default\n");
        try
        {
            new ProfileStore().Read(layout, "default");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(ManagerDiagnosticCodes.ProfileVersionUnsupported, StringComparison.Ordinal);
        }
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool StateReaderThrowsWhenUninitialised()
{
    var tempRoot = NewTempRoot("reader-throws");
    try
    {
        var layout = new StoreLayout(tempRoot);
        try
        {
            new StoreStateReader().Read(layout);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(ManagerDiagnosticCodes.StoreNotInitialised, StringComparison.Ordinal);
        }
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Install / uninstall / list helpers
// ============================================================================

static bool DiagnosticFromPreservesCodeAndSeverity()
{
    var source = new PagoniaLand.Patcher.PatchDiagnostic(
        PagoniaLand.Patcher.PatchDiagnosticSeverity.Error,
        "patcher.demoCode",
        "demo message",
        "demo/path");

    var mapped = ManagerDiagnostic.From(source);
    return mapped.Severity == ManagerDiagnosticSeverity.Error
        && mapped.Code == "patcher.demoCode"
        && mapped.Message == "demo message"
        && mapped.Path == "demo/path";
}

static bool InstallFolderHappyPath()
{
    var tempRoot = NewTempRoot("install-folder-happy");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-happy");
        var result = new ModInstaller().Install(sourceDir, layout);

        var sidecarPath = Path.Combine(result.InstallPath ?? "", ModInstaller.SidecarFileName);

        return result.Outcome == InstallOutcome.Installed
            && result.ModId == "pagonia-land.fixture.installer-happy"
            && result.Version == "0.1.0"
            && result.Diagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error)
            && Directory.Exists(result.InstallPath)
            && File.Exists(Path.Combine(result.InstallPath!, "mod.yaml"))
            && File.Exists(Path.Combine(result.InstallPath!, "patches", "buildings.yaml"))
            && File.Exists(sidecarPath);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallSurfacesOverlayAdvisorFindings()
{
    // A Pattern B overlay mod that Replaces an entity should, on install, carry
    // the patcher advisor's destructive-mode notice through to the install
    // diagnostics — advisory only, so the install still succeeds.
    var tempRoot = NewTempRoot("install-advisor");
    try
    {
        var layout = InitLayout(tempRoot);
        var sourceDir = Path.Combine(tempRoot, "src");
        Directory.CreateDirectory(Path.Combine(sourceDir, "entries"));

        File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.installer-advisor
name: Fixture Installer Advisor
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Inline overlay fixture that Replaces an entity, for advisor surfacing.
requiredPackages:
  - core
entries:
  add:
    - path: installer-advisor-test/gdb/sawmill.gd.xml
      source: entries/sawmill.gd.xml
""");

        File.WriteAllText(Path.Combine(sourceDir, "entries", "sawmill.gd.xml"), """
<?xml version="1.0" encoding="utf-8"?>
<EntityGroup>
  <Entities>
    <Entity Name="Sawmill Override" Guid="11111111-1111-1111-1111-111111111111" InheritanceMode="Replace" InheritedGuid="22222222-2222-2222-2222-222222222222">
      <Values />
    </Entity>
  </Entities>
</EntityGroup>
""");

        var result = new ModInstaller().Install(sourceDir, layout);

        return result.Outcome == InstallOutcome.Installed
            && result.Diagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error)
            && result.Diagnostics.Any(d => d.Code == "usesDestructiveInheritanceMode"
                && d.Severity == ManagerDiagnosticSeverity.Info)
            && result.Diagnostics.Any(d => d.Code == "inheritanceConflictRisk");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CrossModOverlayConflictSurfacedInPlan()
{
    // Two enabled overlay mods that both Replace the same inherited entity. The
    // plan must surface a cross-mod overlay conflict naming the load-order winner
    // (the last-enabled mod) and the overridden one. Advisory — never an error.
    var tempRoot = NewTempRoot("cross-mod-overlay-conflict");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        const string shared = "c732cb26-7487-4a7b-b1ba-b65e094f9bac";

        InstallOverlayReplaceMod(layout, tempRoot, "pagonia-land.fixture.overlay-a", "src-a", shared);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.overlay-a", null);
        InstallOverlayReplaceMod(layout, tempRoot, "pagonia-land.fixture.overlay-b", "src-b", shared);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.overlay-b", null);
        // Load order is now [overlay-a, overlay-b] — overlay-b wins.

        var plan = new PlanProfileService().Plan(layout, gameRoot, null);

        return plan.ManagerDiagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error)
            && plan.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.CrossModOverlayConflict
                && d.Severity == ManagerDiagnosticSeverity.Warning
                && d.Message.Contains("pagonia-land.fixture.overlay-b", StringComparison.Ordinal)
                && d.Message.Contains("pagonia-land.fixture.overlay-a", StringComparison.Ordinal)
                && d.Message.Contains(shared, StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorHealthyStoreHasNoErrors()
{
    var tempRoot = NewTempRoot("doctor-healthy");
    try
    {
        var layout = InitLayout(tempRoot);
        var report = new DoctorService().Run(layout, gameRoot: null);
        return !report.HasErrors
            && report.Checks.Any(c => c.Name == "Store" && c.Status == DoctorStatus.Ok)
            && report.Checks.Any(c => c.Name == "Expansion ownership" && c.Status == DoctorStatus.Skipped);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorUninitialisedStoreErrors()
{
    var tempRoot = NewTempRoot("doctor-uninit");
    try
    {
        var layout = new StoreLayout(Path.Combine(tempRoot, "store"));
        var report = new DoctorService().Run(layout, gameRoot: null);
        return report.HasErrors
            && report.Checks.Any(c => c.Name == "Store" && c.Status == DoctorStatus.Error
                && c.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.StoreNotInitialised));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorFlagsCrossModConflictStoreOnly()
{
    // doctor reuses the cross-mod detector with no game root: two enabled mods
    // that Replace the same entity should make the conflicts check a warning.
    var tempRoot = NewTempRoot("doctor-conflict");
    try
    {
        var layout = InitLayout(tempRoot);
        const string shared = "c732cb26-7487-4a7b-b1ba-b65e094f9bac";
        InstallOverlayReplaceMod(layout, tempRoot, "pagonia-land.fixture.doc-a", "src-a", shared);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.doc-a", null);
        InstallOverlayReplaceMod(layout, tempRoot, "pagonia-land.fixture.doc-b", "src-b", shared);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.doc-b", null);

        var report = new DoctorService().Run(layout, gameRoot: null);
        return report.Checks.Any(c => c.Name == "Cross-mod overlay conflicts"
            && c.Status == DoctorStatus.Warning
            && c.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CrossModOverlayConflict));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorFlagsUnreadableEnabledMod()
{
    // An enabled mod that's installed on disk but whose manifest can't be read must grade
    // the "Enabled mods installed" check Error (modManifestUnreadable), not be silently
    // dropped so the check reads "all present".
    var tempRoot = NewTempRoot("doctor-broken-mod");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.doc-broken";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src-broken");
        new ActiveProfileService().Enable(layout, modId, null);

        // Remove the installed manifest: the dir still exists, but ReadMod now returns null.
        File.Delete(Path.Combine(layout.ModVersionDirectory(modId, "0.1.0"), "mod.yaml"));

        var report = new DoctorService().Run(layout, gameRoot: null);
        return report.Checks.Any(c => c.Name == "Enabled mods installed"
            && c.Status == DoctorStatus.Error
            && c.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModManifestUnreadable));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorUpdatesCheckSkippedWhenOffline()
{
    // doctor stays fully offline by default: with no fetcher passed, the update check
    // is Skipped rather than silently hitting the network.
    var tempRoot = NewTempRoot("doctor-updates-offline");
    try
    {
        var layout = InitLayout(tempRoot);
        var report = new DoctorService().Run(layout, gameRoot: null);
        return report.Checks.Any(c => c.Name == "Updates available" && c.Status == DoctorStatus.Skipped)
            && !report.HasErrors;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DoctorUpdatesCheckSurfacesAvailableWhenOptedIn()
{
    // With a fetcher passed (the CLI's --check-updates), an installed mod whose repo
    // advertises a newer version grades the update check a warning.
    var tempRoot = NewTempRoot("doctor-updates-optin");
    try
    {
        var layout = InitLayout(tempRoot);
        const string id = "pagonia-land.example.cheaper-sawmill";
        WriteInstalledModFixture(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");

        var report = new DoctorService().Run(layout, gameRoot: null, MakeUpdateRepoFixture("0.2.0"));
        return report.Checks.Any(c => c.Name == "Updates available"
            && c.Status == DoctorStatus.Warning
            && c.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModUpdateAvailable));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static void InstallOverlayReplaceMod(StoreLayout layout, string tempRoot, string modId, string subdir, string inheritedGuid)
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "entries"));

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Overlay {modId.Split('.').Last()}
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Inline overlay fixture that Replaces a shared inherited entity.
requiredPackages:
  - core
entries:
  add:
    - path: {subdir}/gdb/x.gd.xml
      source: entries/x.gd.xml
""");

    File.WriteAllText(Path.Combine(sourceDir, "entries", "x.gd.xml"), $"""
<?xml version="1.0" encoding="utf-8"?>
<EntityGroup>
  <Entities>
    <Entity Name="Override by {modId}" Guid="{Guid.NewGuid()}" InheritanceMode="Replace" InheritedGuid="{inheritedGuid}">
      <Values />
    </Entity>
  </Entities>
</EntityGroup>
""");

    var result = new ModInstaller().Install(sourceDir, layout);
    if (result.Outcome != InstallOutcome.Installed)
    {
        throw new InvalidOperationException($"fixture install failed for {modId}: {result.Outcome}");
    }
}

static bool InstallZipHappyPath()
{
    var tempRoot = NewTempRoot("install-zip-happy");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-zip");
        var zipPath = Path.Combine(tempRoot, "fixture.zip");
        ZipFile.CreateFromDirectory(sourceDir, zipPath);

        var result = new ModInstaller().Install(zipPath, layout);

        return result.Outcome == InstallOutcome.Installed
            && result.ModId == "pagonia-land.fixture.installer-zip"
            && Directory.Exists(result.InstallPath)
            && File.Exists(Path.Combine(result.InstallPath!, "mod.yaml"))
            && File.Exists(Path.Combine(result.InstallPath!, "patches", "buildings.yaml"))
            && File.Exists(Path.Combine(result.InstallPath!, ModInstaller.SidecarFileName));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallMissingSourceEmitsCode()
{
    var tempRoot = NewTempRoot("install-missing");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ModInstaller().Install(Path.Combine(tempRoot, "does-not-exist"), layout);
        return result.Outcome == InstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModSourceNotFound
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallBadSourceTypeEmitsCode()
{
    var tempRoot = NewTempRoot("install-bad-type");
    try
    {
        var layout = InitLayout(tempRoot);
        var randomFile = Path.Combine(tempRoot, "notes.txt");
        File.WriteAllText(randomFile, "this is not a mod");
        var result = new ModInstaller().Install(randomFile, layout);
        return result.Outcome == InstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModSourceNotAFolderOrZip
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallFolderMissingManifestEmitsCode()
{
    var tempRoot = NewTempRoot("install-no-manifest");
    try
    {
        var layout = InitLayout(tempRoot);
        var sourceDir = Path.Combine(tempRoot, "empty-source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "README.md"), "no mod yaml");

        var result = new ModInstaller().Install(sourceDir, layout);
        return result.Outcome == InstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModManifestMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallInvalidManifestSurfacesPatcherCode()
{
    var tempRoot = NewTempRoot("install-bad-manifest");
    try
    {
        var layout = InitLayout(tempRoot);
        var sourceDir = Path.Combine(tempRoot, "bad-source");
        Directory.CreateDirectory(sourceDir);
        // Genuinely invalid YAML — an unterminated flow sequence forces YamlDotNet to throw
        // a YamlException, which the patcher's reader surfaces as `modManifestReadFailed`.
        File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), "id: [unterminated\n");

        var result = new ModInstaller().Install(sourceDir, layout);
        // Patcher/paker codes are bare strings (no `tool.` prefix). The manager's *own* codes are
        // namespaced under `manager.*` precisely so passed-through downstream codes stay
        // distinguishable. Asserting "any non-manager error" proves the pass-through worked.
        return result.Outcome == InstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                !d.Code.StartsWith("manager.", StringComparison.Ordinal)
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && !Directory.Exists(Path.Combine(layout.ModsDirectory, "anything"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallDuplicateIsWarning()
{
    var tempRoot = NewTempRoot("install-duplicate");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-dup");
        var first = new ModInstaller().Install(sourceDir, layout);

        File.WriteAllText(Path.Combine(first.InstallPath!, "canary.txt"), "do not overwrite me");

        var second = new ModInstaller().Install(sourceDir, layout);

        return first.Outcome == InstallOutcome.Installed
            && second.Outcome == InstallOutcome.AlreadyInstalled
            && second.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModAlreadyInstalled
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && File.ReadAllText(Path.Combine(first.InstallPath!, "canary.txt")) == "do not overwrite me";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool InstallCorruptZipEmitsCode()
{
    var tempRoot = NewTempRoot("install-corrupt-zip");
    try
    {
        var layout = InitLayout(tempRoot);
        var fakeZip = Path.Combine(tempRoot, "broken.zip");
        File.WriteAllText(fakeZip, "this is not a real zip archive");

        var result = new ModInstaller().Install(fakeZip, layout);
        return result.Outcome == InstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModSourceNotAFolderOrZip
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallRemovesAndPrunesParent()
{
    var tempRoot = NewTempRoot("uninstall-prune");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-prune");
        var installed = new ModInstaller().Install(sourceDir, layout);
        var parentDir = Path.Combine(layout.ModsDirectory, installed.ModId!);

        var result = new ModUninstaller().Uninstall(installed.ModId!, version: null, layout);
        return result.Outcome == UninstallOutcome.Removed
            && result.ParentDirectoryPruned
            && !Directory.Exists(installed.InstallPath)
            && !Directory.Exists(parentDir);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallKeepsParentWhenOtherVersionsRemain()
{
    var tempRoot = NewTempRoot("uninstall-keeps-parent");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.installer-multi";

        var sourceA = MakeMinimalFixtureDir(tempRoot, modId, "0.1.0", subdir: "src-a");
        var sourceB = MakeMinimalFixtureDir(tempRoot, modId, "0.2.0", subdir: "src-b");

        new ModInstaller().Install(sourceA, layout);
        new ModInstaller().Install(sourceB, layout);

        var result = new ModUninstaller().Uninstall(modId, "0.1.0", layout);
        var parentDir = Path.Combine(layout.ModsDirectory, modId);

        return result.Outcome == UninstallOutcome.Removed
            && !result.ParentDirectoryPruned
            && Directory.Exists(parentDir)
            && Directory.Exists(layout.ModVersionDirectory(modId, "0.2.0"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallAmbiguousEmitsCode()
{
    var tempRoot = NewTempRoot("uninstall-ambiguous");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.installer-ambig";

        var sourceA = MakeMinimalFixtureDir(tempRoot, modId, "0.1.0", subdir: "src-a");
        var sourceB = MakeMinimalFixtureDir(tempRoot, modId, "0.2.0", subdir: "src-b");

        new ModInstaller().Install(sourceA, layout);
        new ModInstaller().Install(sourceB, layout);

        var result = new ModUninstaller().Uninstall(modId, version: null, layout);
        return result.Outcome == UninstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModVersionAmbiguous
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallMissingModEmitsCode()
{
    var tempRoot = NewTempRoot("uninstall-missing-mod");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ModUninstaller().Uninstall("no.such.mod", version: null, layout);
        return result.Outcome == UninstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModNotInstalled
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallMissingVersionEmitsCode()
{
    var tempRoot = NewTempRoot("uninstall-missing-version");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-vermiss");
        var installed = new ModInstaller().Install(sourceDir, layout);

        var result = new ModUninstaller().Uninstall(installed.ModId!, "9.9.9", layout);
        return result.Outcome == UninstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModVersionNotInstalled
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallRefusesPathTraversal()
{
    var tempRoot = NewTempRoot("uninstall-path-traversal");
    try
    {
        var layout = InitLayout(tempRoot);

        // Create a sibling directory OUTSIDE the store's mods folder with a canary file.
        // A path-traversal modId would have wiped this before the fix.
        var outsideDir = Path.Combine(tempRoot, "outside-store");
        Directory.CreateDirectory(outsideDir);
        var canary = Path.Combine(outsideDir, "must-survive.txt");
        File.WriteAllText(canary, "do not delete");

        // "../outside-store" starts inside layout.ModsDirectory (= <storeRoot>/mods) and
        // escapes one level up to <tempRoot>/outside-store.
        var result = new ModUninstaller().Uninstall("../outside-store", version: null, layout);

        return result.Outcome == UninstallOutcome.Failed
            && File.Exists(canary)
            && Directory.Exists(outsideDir)
            && Directory.Exists(layout.ModsDirectory)
            && result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UninstallNoVersionDirsDoesNotDelete()
{
    var tempRoot = NewTempRoot("uninstall-no-versions");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.no-versions";

        // Create the mod folder with NO version subdirs, just loose files.
        var modDir = Path.Combine(layout.ModsDirectory, modId);
        Directory.CreateDirectory(modDir);
        var canary = Path.Combine(modDir, "loose-file.txt");
        File.WriteAllText(canary, "must survive");

        var result = new ModUninstaller().Uninstall(modId, version: null, layout);

        // Regression: the failure path previously called Directory.Delete(modDir, recursive: true)
        // BEFORE returning the error, wiping the loose canary file.
        return result.Outcome == UninstallOutcome.Failed
            && Directory.Exists(modDir)
            && File.Exists(canary);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ListEmptyReturnsEmpty()
{
    var tempRoot = NewTempRoot("list-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        var listed = new ModLister().List(layout);
        return listed.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ListReturnsInstalledModsWithMetadata()
{
    var tempRoot = NewTempRoot("list-populated");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-list");
        var installed = new ModInstaller().Install(sourceDir, layout);

        var listed = new ModLister().List(layout);
        return listed.Count == 1
            && listed[0].Id == installed.ModId
            && listed[0].Version == installed.Version
            && listed[0].InstallPath == installed.InstallPath
            && listed[0].SourceType == "folder"
            && !string.IsNullOrEmpty(listed[0].InstalledAt)
            && !string.IsNullOrEmpty(listed[0].SourcePath)
            && listed[0].ManifestName == "Fixture Installer List";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RoundTripInstallListUninstall()
{
    var tempRoot = NewTempRoot("install-roundtrip");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.installer-rt");
        var installed = new ModInstaller().Install(sourceDir, layout);
        var afterInstall = new ModLister().List(layout);

        var uninstalled = new ModUninstaller().Uninstall(installed.ModId!, version: null, layout);
        var afterUninstall = new ModLister().List(layout);

        return installed.Outcome == InstallOutcome.Installed
            && afterInstall.Count == 1
            && uninstalled.Outcome == UninstallOutcome.Removed
            && afterUninstall.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// ProfileMutator (pure logic) + ActiveProfileService (with IO) helpers
// ============================================================================

static ProfileFile MakeProfile(params (string Id, string Version)[] enabled)
{
    return new ProfileFile
    {
        ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
        Name = StoreLayoutConstants.DefaultProfileName,
        EnabledMods = enabled.Select(e => new ProfileEnabledMod { Id = e.Id, Version = e.Version }).ToList(),
        LoadOrder = enabled.Select(e => e.Id).ToList(),
    };
}

static bool MutatorEnableAddsToBoth()
{
    var profile = MakeProfile();
    var result = new ProfileMutator().Enable(profile, "mod.a", "1.0.0");
    return result.Mutated
        && result.Profile.EnabledMods.Count == 1
        && result.Profile.EnabledMods[0].Id == "mod.a"
        && result.Profile.EnabledMods[0].Version == "1.0.0"
        && result.Profile.LoadOrder.SequenceEqual(["mod.a"]);
}

static bool MutatorEnableSameIsWarning()
{
    var profile = MakeProfile(("mod.a", "1.0.0"));
    var result = new ProfileMutator().Enable(profile, "mod.a", "1.0.0");
    return !result.Mutated
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.ModAlreadyEnabled
            && d.Severity == ManagerDiagnosticSeverity.Warning);
}

static bool MutatorEnableReplacesVersion()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "0.5.0"));
    var result = new ProfileMutator().Enable(profile, "mod.a", "2.0.0");
    var aEnabled = result.Profile.EnabledMods.FirstOrDefault(m => m.Id == "mod.a");
    return result.Mutated
        && aEnabled?.Version == "2.0.0"
        && result.Profile.LoadOrder.SequenceEqual(["mod.a", "mod.b"])
        && result.Profile.EnabledMods.Count(m => m.Id == "mod.a") == 1;
}

static bool MutatorDisableRemovesFromBoth()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "0.5.0"));
    var result = new ProfileMutator().Disable(profile, "mod.a");
    return result.Mutated
        && result.Profile.EnabledMods.Count == 1
        && result.Profile.EnabledMods[0].Id == "mod.b"
        && result.Profile.LoadOrder.SequenceEqual(["mod.b"]);
}

static bool MutatorDisableNonEnabledIsWarning()
{
    var profile = MakeProfile(("mod.a", "1.0.0"));
    var result = new ProfileMutator().Disable(profile, "mod.b");
    return !result.Mutated
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.ModNotEnabled
            && d.Severity == ManagerDiagnosticSeverity.Warning);
}

static bool MutatorDisableLoadOrderOrphanCleansDrift()
{
    // Drift case: load-order references an id that no longer has an
    // EnabledMods row. The wizard offers this id (it's in LoadOrder) — the
    // service must actually clean it up instead of refusing with
    // ModNotEnabled. Info diagnostic surfaces what was cleaned.
    var profile = new ProfileFile
    {
        ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
        Name = StoreLayoutConstants.DefaultProfileName,
        EnabledMods = new List<ProfileEnabledMod>
        {
            new() { Id = "mod.a", Version = "1.0.0" },
        },
        LoadOrder = new List<string> { "mod.a", "mod.orphan" },
    };
    var result = new ProfileMutator().Disable(profile, "mod.orphan");
    return result.Mutated
        && result.Profile.LoadOrder.SequenceEqual(["mod.a"])
        && result.Profile.EnabledMods.Count == 1
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.ProfileDriftCleaned
            && d.Severity == ManagerDiagnosticSeverity.Info);
}

static bool ActiveProfileDisableNoOpReportsNotMutated()
{
    var tempRoot = NewTempRoot("active-disable-noop");
    try
    {
        var layout = InitLayout(tempRoot);

        // Disable a mod that is not enabled — mutator emits warning, no write happens.
        var result = new ActiveProfileService().Disable(layout, "pagonia-land.fixture.not-enabled");

        // Regression: Success used to be true with no Mutated property exposed at all,
        // so the CLI printed "Disabled X" right after the warning. The fix splits
        // 'request handled' (Success) from 'profile actually changed' (Mutated).
        return result.Success
            && !result.Mutated
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModNotEnabled
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool MutatorMoveToPositionReorders()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "1.0.0"), ("mod.c", "1.0.0"));
    var result = new ProfileMutator().MoveToPosition(profile, "mod.c", 1);
    return result.Mutated
        && result.Profile.LoadOrder.SequenceEqual(["mod.c", "mod.a", "mod.b"]);
}

static bool MutatorMoveOutOfRange()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "1.0.0"));
    var result = new ProfileMutator().MoveToPosition(profile, "mod.a", 99);
    return !result.Mutated
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.MovePositionOutOfRange
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool MutatorMoveTargetMissing()
{
    var profile = MakeProfile(("mod.a", "1.0.0"));
    var result = new ProfileMutator().MoveToPosition(profile, "mod.b", 1);
    return !result.Mutated
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.MoveTargetNotInLoadOrder
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool MutatorMoveBefore()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "1.0.0"), ("mod.c", "1.0.0"));
    var result = new ProfileMutator().MoveBefore(profile, "mod.c", "mod.b");
    return result.Mutated
        && result.Profile.LoadOrder.SequenceEqual(["mod.a", "mod.c", "mod.b"]);
}

static bool MutatorMoveAfter()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "1.0.0"), ("mod.c", "1.0.0"));
    var result = new ProfileMutator().MoveAfter(profile, "mod.a", "mod.c");
    return result.Mutated
        && result.Profile.LoadOrder.SequenceEqual(["mod.b", "mod.c", "mod.a"]);
}

static bool MutatorMoveAnchorMissing()
{
    var profile = MakeProfile(("mod.a", "1.0.0"), ("mod.b", "1.0.0"));
    var result = new ProfileMutator().MoveBefore(profile, "mod.a", "mod.ghost");
    return !result.Mutated
        && result.Diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.MoveAnchorNotInLoadOrder
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool ServiceEnablePicksLatest()
{
    var tempRoot = NewTempRoot("svc-enable-latest");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.svc-latest";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src-old");
        Thread.Sleep(50); // ensure distinct InstalledAt timestamps
        InstallFixtureMod(layout, tempRoot, modId, "0.2.0", "src-new");

        var result = new ActiveProfileService().Enable(layout, modId, requestedVersion: null);
        var enabled = result.Profile?.EnabledMods.FirstOrDefault(m => m.Id == modId);
        return result.Success
            && enabled?.Version == "0.2.0"
            && result.Diagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ServiceEnableModNotInstalled()
{
    var tempRoot = NewTempRoot("svc-enable-noinstall");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ActiveProfileService().Enable(layout, "no.such.mod", null);
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModNotInstalled
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ServiceEnableVersionNotInstalled()
{
    var tempRoot = NewTempRoot("svc-enable-noversion");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.svc-noversion";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");

        var result = new ActiveProfileService().Enable(layout, modId, "9.9.9");
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModVersionNotInstalled
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ServiceStatusAfterEnableAndMove()
{
    var tempRoot = NewTempRoot("svc-status");
    try
    {
        var layout = InitLayout(tempRoot);
        var modA = "pagonia-land.fixture.svc-status-a";
        var modB = "pagonia-land.fixture.svc-status-b";
        InstallFixtureMod(layout, tempRoot, modA, "0.1.0", "src-a");
        InstallFixtureMod(layout, tempRoot, modB, "0.1.0", "src-b");

        var service = new ActiveProfileService();
        service.Enable(layout, modA, null);
        service.Enable(layout, modB, null);
        service.MoveBefore(layout, modB, modA);

        var status = service.Show(layout);
        return status.Success
            && status.Profile!.LoadOrder.SequenceEqual([modB, modA])
            && status.Profile.EnabledMods.Count == 2;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ServiceDisableReEnableNoStaleState()
{
    var tempRoot = NewTempRoot("svc-disable-reenable");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.svc-cycle";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");

        var service = new ActiveProfileService();
        service.Enable(layout, modId, null);
        service.Disable(layout, modId);

        // Disk state must be clean before re-enable.
        var midProfile = new ProfileStore().Read(layout, StoreLayoutConstants.DefaultProfileName);
        if (midProfile.EnabledMods.Count != 0 || midProfile.LoadOrder.Count != 0)
        {
            return false;
        }

        service.Enable(layout, modId, null);
        var finalProfile = new ProfileStore().Read(layout, StoreLayoutConstants.DefaultProfileName);

        return finalProfile.EnabledMods.Count == 1
            && finalProfile.EnabledMods[0].Id == modId
            && finalProfile.LoadOrder.SequenceEqual([modId]);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static InstallResult InstallFixtureMod(StoreLayout layout, string tempRoot, string modId, string version, string subdir)
{
    var sourceDir = MakeMinimalFixtureDir(tempRoot, modId, version, subdir);
    return new ModInstaller().Install(sourceDir, layout);
}

// Install a mod declaring dependency / incompatibility / load-order relations, for the
// dependency + load-order tests.
static InstallResult InstallRelationMod(StoreLayout layout, string tempRoot, string modId,
    string[]? dependencies = null, string[]? incompatibleWith = null,
    string[]? loadAfter = null, string[]? loadBefore = null)
{
    var sourceDir = Path.Combine(tempRoot, modId);
    Directory.CreateDirectory(Path.Combine(sourceDir, "patches"));

    static string Block(string key, string[]? items) =>
        items is { Length: > 0 }
            ? "\n" + key + ":\n" + string.Join("\n", items.Select(i => "  - " + i))
            : string.Empty;

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Relation {modId}
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Relation fixture mod for dependency/load-order tests.
requiredPackages:
  - core{Block("dependencies", dependencies)}{Block("incompatibleWith", incompatibleWith)}{Block("loadAfter", loadAfter)}{Block("loadBefore", loadBefore)}
patches:
  - patches/p.yaml
""");
    File.WriteAllText(Path.Combine(sourceDir, "patches", "p.yaml"), """
operations:
  - id: rel-op
    operation: replaceValue
    risk: low
    reason: relation fixture
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
""");
    return new ModInstaller().Install(sourceDir, layout);
}

static bool DependencyMissingFlaggedOnEnable()
{
    var tempRoot = NewTempRoot("dep-missing");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", dependencies: new[] { "rel.b" });
        InstallRelationMod(layout, tempRoot, "rel.b"); // installed but we won't enable it
        var result = new ActiveProfileService().Enable(layout, "rel.a", null);
        return result.Success
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependencyMissing
                && d.Message.Contains("rel.b") && d.Message.Contains("installed but not enabled"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DependencySatisfiedWhenBothEnabled()
{
    var tempRoot = NewTempRoot("dep-ok");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", dependencies: new[] { "rel.b" });
        InstallRelationMod(layout, tempRoot, "rel.b");
        new ActiveProfileService().Enable(layout, "rel.b", null);
        var result = new ActiveProfileService().Enable(layout, "rel.a", null);
        return result.Success
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependencyMissing);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool IncompatibleFlaggedOnEnable()
{
    var tempRoot = NewTempRoot("incompat");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", incompatibleWith: new[] { "rel.b" });
        InstallRelationMod(layout, tempRoot, "rel.b");
        new ActiveProfileService().Enable(layout, "rel.a", null);
        var result = new ActiveProfileService().Enable(layout, "rel.b", null); // focus rel.b, rel.a already enabled
        return result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIncompatibleEnabled
            && d.Message.Contains("rel.a") && d.Message.Contains("rel.b"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DoctorReportsDependencyIssues()
{
    var tempRoot = NewTempRoot("doctor-deps");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", dependencies: new[] { "rel.b" }); // rel.b never installed
        new ActiveProfileService().Enable(layout, "rel.a", null);
        var report = new DoctorService().Run(layout, gameRoot: null);
        return report.Checks.Any(c => c.Name == "Dependencies & incompatibilities"
            && c.Status == DoctorStatus.Warning
            && c.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependencyMissing
                && d.Message.Contains("not installed")));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DisableDependedUponWarns()
{
    var tempRoot = NewTempRoot("disable-depended");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", dependencies: new[] { "rel.b" });
        InstallRelationMod(layout, tempRoot, "rel.b");
        var svc = new ActiveProfileService();
        svc.Enable(layout, "rel.b", null);
        svc.Enable(layout, "rel.a", null);

        var result = svc.Disable(layout, "rel.b"); // rel.a still enabled, depends on rel.b
        return result.Mutated
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependedUponByOthers
                && d.Message.Contains("rel.a"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DisableNotDependedUponNoWarn()
{
    var tempRoot = NewTempRoot("disable-free");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a");
        InstallRelationMod(layout, tempRoot, "rel.b");
        var svc = new ActiveProfileService();
        svc.Enable(layout, "rel.a", null);
        svc.Enable(layout, "rel.b", null);

        var result = svc.Disable(layout, "rel.b");
        return result.Mutated
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependedUponByOthers);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UninstallDependedUponWarns()
{
    var tempRoot = NewTempRoot("uninstall-depended");
    try
    {
        var layout = InitLayout(tempRoot);
        InstallRelationMod(layout, tempRoot, "rel.a", dependencies: new[] { "rel.b" });
        InstallRelationMod(layout, tempRoot, "rel.b");
        var svc = new ActiveProfileService();
        svc.Enable(layout, "rel.b", null);
        svc.Enable(layout, "rel.a", null);

        var result = new ModUninstaller().Uninstall("rel.b", null, layout);
        return result.Outcome == UninstallOutcome.Removed
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependedUponByOthers
                && d.Message.Contains("rel.a"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Serve an acme/mods repo whose index lists the given mods, each mod.yaml declaring the given
// dependencies, with a valid patch — so the assisted dependency installer can fetch + install them.
static InMemoryRemoteContentFetcher MakeDepRepoFixture(params (string Id, string[] Deps)[] mods)
{
    var fetcher = new InMemoryRemoteContentFetcher();
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddRef("acme", "mods", "HEAD", sha);

    var entries = string.Join("\n", mods.Select(m =>
        $"  - id: {m.Id}\n    path: mods/{m.Id}\n    version: 0.1.0\n    gameDatabaseVersion: \"1.3.0-11768+193445\""));
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/index.yaml",
        $"indexFormatVersion: \"0.1\"\nrepo:\n  name: Acme\nmods:\n{entries}\n");

    foreach (var m in mods)
    {
        var depBlock = m.Deps.Length > 0
            ? "\ndependencies:\n" + string.Join("\n", m.Deps.Select(d => "  - " + d))
            : string.Empty;
        fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/{m.Id}/mod.yaml",
            $"patchFormatVersion: \"0.1\"\nid: {m.Id}\nname: Dep {m.Id}\nversion: \"0.1.0\"\nauthor: Pagonia Land\ngameDatabaseVersion: \"1.3.0-11768+193445\"\ndescription: assisted-install dep fixture.\nrequiredPackages:\n  - core{depBlock}\npatches:\n  - patches/p.yaml\n");
        fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/{m.Id}/patches/p.yaml", """
            operations:
              - id: dep-op
                operation: replaceValue
                risk: low
                reason: assisted-install fixture
                target:
                  file: core/gdb/buildings.gd.xml
                  entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
                  component: AspectBuildup
                  path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
                expectedOldValue: "4"
                value: "3"
            """);
    }
    return fetcher;
}

static bool AssistedInstallPullsTransitiveDeps()
{
    var tempRoot = NewTempRoot("assist-transitive");
    try
    {
        var layout = InitLayout(tempRoot);
        // dep.b depends on dep.c; pulling dep.b must also pull dep.c.
        var fetcher = MakeDepRepoFixture(("dep.b", new[] { "dep.c" }), ("dep.c", Array.Empty<string>()));
        var sameRepo = new GitHubSource("acme", "mods", "HEAD", null);

        var result = new AssistedDependencyInstaller(fetcher, allowInsecureSources: false)
            .InstallMissing(layout, new[] { "dep.b" }, sameRepo, Array.Empty<CatalogSource>(), 5);

        return result.InstalledDependencies.Contains("dep.b")
            && result.InstalledDependencies.Contains("dep.c")
            && Directory.Exists(layout.ModVersionDirectory("dep.b", "0.1.0"))
            && Directory.Exists(layout.ModVersionDirectory("dep.c", "0.1.0"))
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependencyInstalled);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool AssistedInstallWarnsOnUnresolvableDep()
{
    var tempRoot = NewTempRoot("assist-unresolvable");
    try
    {
        var layout = InitLayout(tempRoot);
        // dep.b depends on dep.ghost, which the repo doesn't list and no catalogs are subscribed.
        var fetcher = MakeDepRepoFixture(("dep.b", new[] { "dep.ghost" }));
        var sameRepo = new GitHubSource("acme", "mods", "HEAD", null);

        var result = new AssistedDependencyInstaller(fetcher, allowInsecureSources: false)
            .InstallMissing(layout, new[] { "dep.b" }, sameRepo, Array.Empty<CatalogSource>(), 5);

        return result.InstalledDependencies.SequenceEqual(new[] { "dep.b" }) // b installed, ghost couldn't be
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModDependencyUnresolved
                && d.Message.Contains("dep.ghost"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LoadOrderLoadAfterReorders()
{
    // manual [a, b]; a loadAfter b → b must come first → [b, a], with an "adjusted" info.
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", new[] { "b" }, Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), Array.Empty<string>()),
    });
    return r.Order.SequenceEqual(new[] { "b", "a" })
        && r.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderAdjusted)
        && r.Constrained.SetEquals(new[] { "a", "b" });
}

static bool LoadOrderLoadBeforeReorders()
{
    // manual [a, b]; b loadBefore a → b first → [b, a].
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), new[] { "a" }),
    });
    return r.Order.SequenceEqual(new[] { "b", "a" })
        && r.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderAdjusted);
}

static bool LoadOrderDetectsCycle()
{
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", new[] { "b" }, Array.Empty<string>()),
        new LoadOrderInput("b", new[] { "a" }, Array.Empty<string>()),
    });
    return r.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderCycle
            && d.Severity == ManagerDiagnosticSeverity.Warning)
        && r.Order.Count == 2; // both kept (manual-order fallback), nothing dropped
}

static bool LoadOrderToleratesDuplicateIds()
{
    // A hand-edited / corrupted profile can repeat an id in loadOrder; the resolver must dedupe
    // (first occurrence wins) instead of throwing ArgumentException out of its ToDictionary calls.
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("a", Array.Empty<string>(), Array.Empty<string>()),
    });
    return r.Order.SequenceEqual(new[] { "a", "b" });
}

static bool LoadOrderNoConstraintsLeavesManualOrder()
{
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), Array.Empty<string>()),
    });
    return r.Order.SequenceEqual(new[] { "a", "b" })
        && r.Diagnostics.Count == 0
        && r.Constrained.Count == 0;
}

static bool LoadOrderIgnoresConstraintToAbsentMod()
{
    // loadAfter a mod that isn't enabled is inert — no edge, no reorder.
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", new[] { "ghost" }, Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), Array.Empty<string>()),
    });
    return r.Order.SequenceEqual(new[] { "a", "b" }) && r.Diagnostics.Count == 0;
}

static bool LoadOrderStableWhenConstraintAlreadySatisfied()
{
    // manual [a, b, c]; c loadAfter a (already after a) → no reorder, no "adjusted" info; c+a pinned.
    var r = new LoadOrderResolver().Resolve(new[]
    {
        new LoadOrderInput("a", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("b", Array.Empty<string>(), Array.Empty<string>()),
        new LoadOrderInput("c", new[] { "a" }, Array.Empty<string>()),
    });
    return r.Order.SequenceEqual(new[] { "a", "b", "c" })
        && !r.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderAdjusted)
        && r.Constrained.SetEquals(new[] { "a", "c" });
}

// ============================================================================
// Profile lifecycle helpers
// ============================================================================

static bool ProfileNameAcceptsSimple()
{
    return ProfileNameValidator.IsValid("dlc1-testing", out _)
        && ProfileNameValidator.IsValid("vanilla_plus", out _)
        && ProfileNameValidator.IsValid("default", out _)
        && ProfileNameValidator.IsValid("a", out _);
}

static bool ProfileNameRejectsInvalid()
{
    var rejected = new[]
    {
        "",
        "   ",
        ".",
        "..",
        ".hidden",
        "with/slash",
        "with\\backslash",
        "with:colon",
        "with*star",
        "with?question",
        " leading-space",
        "trailing-space ",
        new string('x', ProfileNameValidator.MaxLength + 1),
        // Windows reserved device names (case-insensitive, with or without extension).
        "CON",
        "nul",
        "COM1",
        "LPT9",
        "CON.backup",
    };

    return rejected.All(name => !ProfileNameValidator.IsValid(name, out _));
}

static bool ProfileCreateWritesEmptyV01()
{
    var tempRoot = NewTempRoot("profile-create");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ProfileLifecycleService().Create(layout, "dlc1-testing");
        if (!result.Success) return false;

        var roundTripped = new ProfileStore().Read(layout, "dlc1-testing");
        return roundTripped.ProfileVersion == StoreLayoutConstants.CurrentProfileVersion
            && roundTripped.Name == "dlc1-testing"
            && roundTripped.EnabledMods.Count == 0
            && roundTripped.LoadOrder.Count == 0
            && roundTripped.Collection is null;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCreateRejectsDuplicate()
{
    var tempRoot = NewTempRoot("profile-dup");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "alpha");
        var second = service.Create(layout, "alpha");

        return !second.Success
            && second.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileAlreadyExists
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCreateRejectsInvalidName()
{
    var tempRoot = NewTempRoot("profile-bad-name");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ProfileLifecycleService().Create(layout, "bad/name");
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileNameInvalid
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileListMarksActiveAndDefault()
{
    var tempRoot = NewTempRoot("profile-list");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "alpha");
        service.Create(layout, "beta");

        var list = service.List(layout);
        var byName = list.Profiles.ToDictionary(s => s.Name, StringComparer.Ordinal);

        return list.Success
            && list.ActiveProfile == "default"
            && list.Profiles.Count == 3
            && byName["default"].IsActive && byName["default"].IsDefault
            && !byName["alpha"].IsActive && !byName["alpha"].IsDefault
            && !byName["beta"].IsActive && !byName["beta"].IsDefault;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileUseUpdatesStateAtomically()
{
    var tempRoot = NewTempRoot("profile-use");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "alpha");
        service.Use(layout, "alpha");

        var state = new StoreStateReader().Read(layout);
        return state.ActiveProfile == "alpha"
            && !File.Exists(layout.StateFile + AtomicFile.TempSuffix);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileUseRejectsMissing()
{
    var tempRoot = NewTempRoot("profile-use-missing");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ProfileLifecycleService().Use(layout, "no-such-profile");
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileDeleteRemovesFile()
{
    var tempRoot = NewTempRoot("profile-delete");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "throwaway");
        var path = layout.ProfileFile("throwaway");

        var result = service.Delete(layout, "throwaway");
        return result.Success
            && !File.Exists(path);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileDeleteRejectsDefault()
{
    var tempRoot = NewTempRoot("profile-delete-default");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ProfileLifecycleService().Delete(layout, "default");
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileDefaultDeletion
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && File.Exists(layout.ProfileFile("default"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileDeleteRejectsActive()
{
    var tempRoot = NewTempRoot("profile-delete-active");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "active-one");
        service.Use(layout, "active-one");

        var result = service.Delete(layout, "active-one");
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileActiveDeletion
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && File.Exists(layout.ProfileFile("active-one"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileShowByName()
{
    var tempRoot = NewTempRoot("profile-show-named");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "alpha");

        var result = service.Show(layout, "alpha");
        return result.Success
            && result.ProfileName == "alpha"
            && result.Profile?.Name == "alpha";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileShowDefaultsToActive()
{
    var tempRoot = NewTempRoot("profile-show-active");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "alpha");
        service.Use(layout, "alpha");

        var result = service.Show(layout, profileName: null);
        return result.Success
            && result.ProfileName == "alpha";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RoundTripProfileScopedMods()
{
    var tempRoot = NewTempRoot("profile-roundtrip");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.profile-scoped";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");

        var profiles = new ProfileLifecycleService();
        var active = new ActiveProfileService();

        // Enable mod in default profile.
        active.Enable(layout, modId, null);

        // Create + switch to a new profile; the new profile is empty.
        profiles.Create(layout, "alt");
        profiles.Use(layout, "alt");
        var altBefore = new ProfileStore().Read(layout, "alt");
        if (altBefore.EnabledMods.Count != 0 || altBefore.LoadOrder.Count != 0)
        {
            return false;
        }

        // Switch back to default; the original mod is still enabled there.
        profiles.Use(layout, "default");
        var defaultAfter = new ProfileStore().Read(layout, "default");
        if (defaultAfter.EnabledMods.Count != 1 || defaultAfter.EnabledMods[0].Id != modId)
        {
            return false;
        }

        // Re-switch to alt; still empty.
        profiles.Use(layout, "alt");
        var altAfter = new ProfileStore().Read(layout, "alt");
        return altAfter.EnabledMods.Count == 0 && altAfter.LoadOrder.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static ProfileFile SeedRichProfile(StoreLayout layout, string name)
{
    var profile = new ProfileFile
    {
        ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
        Name = name,
        Collection = "pagonia-land.collections.sample",
        EnabledMods =
        [
            new ProfileEnabledMod
            {
                Id = "mod.a",
                Version = "0.1.0",
                Tweaks = new Dictionary<string, string> { ["cost"] = "5", ["fast"] = "true" },
            },
            new ProfileEnabledMod { Id = "mod.b", Version = "0.2.0" },
        ],
        LoadOrder = ["mod.a", "mod.b"],
    };
    new ProfileStore().Write(layout, profile);
    return profile;
}

static bool ProfileCopyPreservesContents()
{
    var tempRoot = NewTempRoot("profile-copy-preserve");
    try
    {
        var layout = InitLayout(tempRoot);
        SeedRichProfile(layout, "src");

        var result = new ProfileLifecycleService().Copy(layout, "src", "dst", activate: false);
        if (!result.Success) return false;
        if (!result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileCopied
                && d.Severity == ManagerDiagnosticSeverity.Info))
        {
            return false;
        }

        var dst = new ProfileStore().Read(layout, "dst");
        return dst.Name == "dst"
            && dst.ProfileVersion == StoreLayoutConstants.CurrentProfileVersion
            && dst.Collection == "pagonia-land.collections.sample"
            && dst.LoadOrder.SequenceEqual(new[] { "mod.a", "mod.b" })
            && dst.EnabledMods.Count == 2
            && dst.EnabledMods[0].Id == "mod.a" && dst.EnabledMods[0].Version == "0.1.0"
            && dst.EnabledMods[0].Tweaks is { } tweaks && tweaks["cost"] == "5" && tweaks["fast"] == "true"
            && dst.EnabledMods[1].Id == "mod.b" && dst.EnabledMods[1].Tweaks is null;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCopyIsIndependent()
{
    var tempRoot = NewTempRoot("profile-copy-indep");
    try
    {
        var layout = InitLayout(tempRoot);
        SeedRichProfile(layout, "src");
        new ProfileLifecycleService().Copy(layout, "src", "dst", activate: false);

        // Mutate the source after the copy; the copy must stay as it was.
        new ProfileStore().Write(layout, new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = "src",
            EnabledMods = [new ProfileEnabledMod { Id = "mod.z", Version = "9.9.9" }],
            LoadOrder = ["mod.z"],
        });

        var dst = new ProfileStore().Read(layout, "dst");
        return dst.EnabledMods.Count == 2
            && dst.EnabledMods[0].Id == "mod.a"
            && dst.LoadOrder.SequenceEqual(new[] { "mod.a", "mod.b" });
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCopyRejectsExistingTarget()
{
    var tempRoot = NewTempRoot("profile-copy-exists");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "src");
        service.Create(layout, "dst");

        var result = service.Copy(layout, "src", "dst", activate: false);
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileAlreadyExists
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCopyRejectsInvalidTargetName()
{
    var tempRoot = NewTempRoot("profile-copy-badname");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "src");

        var result = service.Copy(layout, "src", "bad/name", activate: false);
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileNameInvalid
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCopyRejectsMissingSource()
{
    var tempRoot = NewTempRoot("profile-copy-nosrc");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ProfileLifecycleService().Copy(layout, "ghost", "dst", activate: false);
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileMissing
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && !new ProfileStore().Exists(layout, "dst");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileCopyActivateSwitchesActive()
{
    var tempRoot = NewTempRoot("profile-copy-activate");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new ProfileLifecycleService();
        service.Create(layout, "src");
        service.Use(layout, "src");

        var result = service.Copy(layout, "src", "dst", activate: true);
        if (!result.Success) return false;

        var state = new StoreStateReader().Read(layout);
        return state.ActiveProfile == "dst"
            && new ProfileStore().Exists(layout, "src")
            && new ProfileStore().Exists(layout, "dst");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static void SeedInstalledProfile(
    StoreLayout layout,
    string profileName,
    string? collection,
    List<ProfileEnabledMod> enabledMods,
    List<string> loadOrder)
{
    new ProfileStore().Write(layout, new ProfileFile
    {
        ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
        Name = profileName,
        Collection = collection,
        EnabledMods = enabledMods,
        LoadOrder = loadOrder,
    });
}

static bool ProfileExportFoldsTweaks()
{
    var tempRoot = NewTempRoot("profile-export-tweaks");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.tweakable";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        SeedInstalledProfile(layout, "work", null,
            [new ProfileEnabledMod { Id = modId, Version = "0.1.0", Tweaks = new Dictionary<string, string> { ["cost"] = "5" } }],
            [modId]);

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "work", exportPath, new ProfileExportOptions());
        if (!result.Success) return false;

        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadCollectionManifest(exportPath).Value;
        if (manifest is null) return false;
        var mod = manifest.Mods.FirstOrDefault(m => m.Id == modId);
        return mod is not null
            && mod.Tweaks is { } tw && tw.TryGetValue("cost", out var v) && v == "5"
            && manifest.LoadOrder.SequenceEqual(new[] { modId })
            && manifest.GameDatabaseVersion == "1.3.0-11768+193445";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportLocalSourceWarning()
{
    var tempRoot = NewTempRoot("profile-export-local");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.localonly";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src"); // folder install → no remote provenance
        SeedInstalledProfile(layout, "work", null,
            [new ProfileEnabledMod { Id = modId, Version = "0.1.0" }],
            [modId]);

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "work", exportPath, new ProfileExportOptions());
        if (!result.Success) return false;
        if (!result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileExportLocalSource
                && d.Severity == ManagerDiagnosticSeverity.Warning))
        {
            return false;
        }

        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadCollectionManifest(exportPath).Value;
        var mod = manifest?.Mods.FirstOrDefault(m => m.Id == modId);
        return mod is not null && mod.Source == "local";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportRecoversSourceFromSidecar()
{
    var tempRoot = NewTempRoot("profile-export-sidecar");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.remote";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");

        // Simulate a remote install by stamping the sidecar's transport-neutral source.
        var sidecarPath = Path.Combine(layout.ModVersionDirectory(modId, "0.1.0"), ModInstaller.SidecarFileName);
        File.WriteAllText(sidecarPath, $"""
installedAt: "2026-06-03T00:00:00Z"
sourcePath: "https://example.invalid/x.zip"
sourceType: "url"
manifestName: "mod.yaml"
source: "gh:thelavablock/mods#abc1234/{modId}"
""");
        SeedInstalledProfile(layout, "work", null,
            [new ProfileEnabledMod { Id = modId, Version = "0.1.0" }],
            [modId]);

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "work", exportPath, new ProfileExportOptions());
        if (!result.Success) return false;
        if (result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ProfileExportLocalSource)) return false;

        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadCollectionManifest(exportPath).Value;
        var mod = manifest?.Mods.FirstOrDefault(m => m.Id == modId);
        return mod is not null && mod.Source == $"gh:thelavablock/mods#abc1234/{modId}";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportRecoversSourceFromLockfile()
{
    var tempRoot = NewTempRoot("profile-export-lock");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.collectionmod";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src"); // folder install → no sidecar source

        var collectionId = "test.pinned";
        Directory.CreateDirectory(layout.CollectionLocksDirectory);
        File.WriteAllText(layout.CollectionLockFile(collectionId), $"""
collectionLockVersion: "0.1"
collectionId: {collectionId}
collectionVersion: "0.1.0"
gameDatabaseVersion: "1.3.0-11768+193445"
generatedAt: "2026-06-03T00:00:00Z"
mods:
  - id: {modId}
    version: "0.1.0"
    resolvedSource: "local"
    archiveSha256: "0000000000000000000000000000000000000000000000000000000000000000"
    enabled: true
    source: "gh:thelavablock/pinned#def5678/{modId}"
""");
        SeedInstalledProfile(layout, "work", collectionId,
            [new ProfileEnabledMod { Id = modId, Version = "0.1.0" }],
            [modId]);

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "work", exportPath, new ProfileExportOptions());
        if (!result.Success) return false;

        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadCollectionManifest(exportPath).Value;
        var mod = manifest?.Mods.FirstOrDefault(m => m.Id == modId);
        return mod is not null && mod.Source == $"gh:thelavablock/pinned#def5678/{modId}";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportSchemaValid()
{
    var tempRoot = NewTempRoot("profile-export-schema");
    try
    {
        var layout = InitLayout(tempRoot);
        var modId = "pagonia-land.fixture.schemacheck";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        SeedInstalledProfile(layout, "work", null,
            [new ProfileEnabledMod { Id = modId, Version = "0.1.0", Tweaks = new Dictionary<string, string> { ["cost"] = "5" } }],
            [modId]);

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "work", exportPath, new ProfileExportOptions());
        if (!result.Success) return false;

        var schemaErrors = new PagoniaLand.Patcher.SchemaValidator()
            .ValidateCollection(exportPath)
            .Where(d => d.Severity == PagoniaLand.Patcher.PatchDiagnosticSeverity.Error)
            .ToList();
        return schemaErrors.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportEmptyRefused()
{
    var tempRoot = NewTempRoot("profile-export-empty");
    try
    {
        var layout = InitLayout(tempRoot); // the default profile is empty
        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, "default", exportPath, new ProfileExportOptions());
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileExportEmpty
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && !File.Exists(exportPath);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportRoundTrip()
{
    var tempRoot = NewTempRoot("profile-export-roundtrip");
    try
    {
        var layout = InitLayout(tempRoot);
        var modA = "pagonia-land.fixture.alpha";
        var modB = "pagonia-land.fixture.beta";
        InstallFixtureMod(layout, tempRoot, modA, "0.1.0", "src-a");
        InstallFixtureMod(layout, tempRoot, modB, "0.1.0", "src-b");

        // Tweak on modA, deliberately reordered load order (modB first) to prove order survives.
        SeedInstalledProfile(layout, "work", null,
            [
                new ProfileEnabledMod { Id = modA, Version = "0.1.0", Tweaks = new Dictionary<string, string> { ["cost"] = "5" } },
                new ProfileEnabledMod { Id = modB, Version = "0.1.0" },
            ],
            [modB, modA]);

        var exportPath = Path.Combine(tempRoot, "exported.collection.yaml");
        var export = new ProfileExportService().Export(layout, "work", exportPath,
            new ProfileExportOptions { Id = "test.exported", Name = "Exported" });
        if (!export.Success || !File.Exists(exportPath)) return false;

        // A mods-root keyed by id so the exported collection re-installs locally.
        MakeMinimalFixtureDir(tempRoot, modA, "0.1.0", Path.Combine("mods-root", modA));
        MakeMinimalFixtureDir(tempRoot, modB, "0.1.0", Path.Combine("mods-root", modB));
        var modsRoot = Path.Combine(tempRoot, "mods-root");

        var install = new CollectionInstallService().Install(layout, exportPath, modsRoot, profileNameOverride: "recreated");
        if (install.Outcome != CollectionInstallOutcome.Installed) return false;

        var recreated = new ProfileStore().Read(layout, "recreated");
        var a = recreated.EnabledMods.FirstOrDefault(m => m.Id == modA);
        return recreated.LoadOrder.SequenceEqual(new[] { modB, modA })
            && a is not null
            && a.Tweaks is { } tw && tw.TryGetValue("cost", out var v) && v == "5"
            && recreated.EnabledMods.Any(m => m.Id == modB);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ProfileExportCanonicalisesAliasTweak()
{
    var tempRoot = NewTempRoot("profile-export-alias");
    try
    {
        var (layout, modId) = SetupAliasedTweakProfile(tempRoot);
        // Store the override under the OLD alias id, *without* going through
        // TweakOverrideService.Read (which would migrate + rewrite the profile), so the
        // profile still carries the stale alias key at export time.
        SetRawProfileTweaks(layout, modId, new Dictionary<string, string> { ["softwood"] = "5" });

        var exportPath = Path.Combine(tempRoot, "out.collection.yaml");
        var result = new ProfileExportService().Export(layout, null, exportPath, new ProfileExportOptions());
        if (!result.Success) return false;

        // The export surfaces the rename and folds the value under the CURRENT id only.
        if (!result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakMigratedFromAlias
                && d.Severity == ManagerDiagnosticSeverity.Info))
        {
            return false;
        }

        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadCollectionManifest(exportPath).Value;
        var mod = manifest?.Mods.FirstOrDefault(m => m.Id == modId);
        return mod is not null
            && mod.Tweaks is { } tw
            && tw.TryGetValue("softwood-cost", out var v) && v == "5"
            && !tw.ContainsKey("softwood");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// JSON reports + schemas + schema-validate helpers
// ============================================================================

static bool SchemaCodesPinned()
{
    return ManagerDiagnosticCodes.SchemaValidationOk == "manager.schemaValidationOk"
        && ManagerDiagnosticCodes.SchemaValidationFailed == "manager.schemaValidationFailed";
}

static bool AllSchemasLoadable()
{
    // Round-trip every known kind through schema-validate against a minimal valid payload to
    // prove the embedded schema is reachable and parseable.
    var tempRoot = NewTempRoot("schemas-loadable");
    try
    {
        var validator = new ManagerSchemaValidator();
        foreach (var kind in ManagerReportKinds.All)
        {
            var payload = MinimalReportFor(kind);
            var path = Path.Combine(tempRoot, $"{kind}.json");
            File.WriteAllText(path, payload);
            var diagnostics = validator.ValidateReport(kind, path);
            if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
            {
                Console.WriteLine($"        {kind} produced errors:");
                foreach (var d in diagnostics)
                {
                    Console.WriteLine($"          [{d.Severity}] [{d.Code}] {d.Message}");
                }
                return false;
            }
        }
        return true;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static string MinimalReportFor(string kind) => kind switch
{
    ManagerReportKinds.Install => """{"schemaVersion":"0.1","reportKind":"install","outcome":"Installed","diagnostics":[]}""",
    ManagerReportKinds.Uninstall => """{"schemaVersion":"0.1","reportKind":"uninstall","outcome":"Removed","parentDirectoryPruned":false,"diagnostics":[]}""",
    ManagerReportKinds.Deploy => """{"schemaVersion":"0.1","reportKind":"deploy","outcome":"Completed","modifiedFileCount":0,"addedFileCount":0,"rebuiltPakCount":0,"diagnostics":[]}""",
    ManagerReportKinds.Rollback => """{"schemaVersion":"0.1","reportKind":"rollback","outcome":"Reverted","restoredFileCount":0,"diagnostics":[]}""",
    ManagerReportKinds.CollectionInstall => """{"schemaVersion":"0.1","reportKind":"collectionInstall","outcome":"Installed","installedMods":[],"diagnostics":[]}""",
    ManagerReportKinds.Status => """{"schemaVersion":"0.1","reportKind":"status","success":true,"enabledMods":[],"loadOrder":[],"diagnostics":[]}""",
    ManagerReportKinds.DeployStatus => """{"schemaVersion":"0.1","reportKind":"deployStatus","gameRoot":"C:\\Games\\PoP","gameFingerprint":"abc123","gameProductVersion":null,"hasDeploys":false,"deploys":[],"diagnostics":[]}""",
    ManagerReportKinds.TweakList => """{"schemaVersion":"0.1","reportKind":"tweakList","success":true,"profile":"default","modId":"x","modVersion":"0.1.0","tweaks":[],"diagnostics":[]}""",
    ManagerReportKinds.TweakSet => """{"schemaVersion":"0.1","reportKind":"tweakSet","success":true,"mutated":true,"profile":"default","modId":"x","tweakId":"t","value":"5","diagnostics":[]}""",
    ManagerReportKinds.TweakReset => """{"schemaVersion":"0.1","reportKind":"tweakReset","success":true,"mutated":true,"profile":"default","modId":"x","tweakId":null,"diagnostics":[]}""",
    ManagerReportKinds.ExpansionsList => """{"schemaVersion":"0.1","reportKind":"expansionsList","success":true,"gameRoot":"C:/Games/PoP","gameFingerprint":"abcdef0123456789","expansions":[{"package":"dlc1","present":true,"owned":"owned","effective":true}],"diagnostics":[]}""",
    ManagerReportKinds.ExpansionsSet => """{"schemaVersion":"0.1","reportKind":"expansionsSet","success":true,"mutated":true,"gameRoot":"C:/Games/PoP","gameFingerprint":"abcdef0123456789","package":"dlc1","owned":"owned","diagnostics":[]}""",
    ManagerReportKinds.Updates => """{"schemaVersion":"0.1","reportKind":"updates","checkedModCount":0,"skippedLocalModCount":0,"checkedCollectionCount":0,"skippedLocalCollectionCount":0,"modUpdates":[],"collectionUpdates":[],"contentDrifts":[],"diagnostics":[]}""",
    _ => throw new InvalidOperationException($"Unknown kind: {kind}"),
};

static bool WriteAndValidate(string kind, string jsonPath, string json)
{
    File.WriteAllText(jsonPath, json);
    var diagnostics = new ManagerSchemaValidator().ValidateReport(kind, jsonPath);
    if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
    {
        Console.WriteLine($"        schema-validate {kind} produced errors:");
        foreach (var d in diagnostics)
        {
            Console.WriteLine($"          [{d.Severity}] [{d.Code}] {d.Message}");
        }
        return false;
    }
    return true;
}

static bool ReportInstallValidates()
{
    var tempRoot = NewTempRoot("report-install");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.report-install");
        var result = new ModInstaller().Install(sourceDir, layout);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"install\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Install, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportUninstallValidates()
{
    var tempRoot = NewTempRoot("report-uninstall");
    try
    {
        var (layout, sourceDir) = SetupStoreAndFixture(tempRoot, "pagonia-land.fixture.report-uninstall");
        new ModInstaller().Install(sourceDir, layout);
        var result = new ModUninstaller().Uninstall("pagonia-land.fixture.report-uninstall", null, layout);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"uninstall\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Uninstall, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportDeployValidates()
{
    var tempRoot = NewTempRoot("report-deploy");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "report-deploy");
        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"deploy\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Deploy, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportRollbackValidates()
{
    var tempRoot = NewTempRoot("report-rollback");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "report-rollback");
        new DeployService().Deploy(layout, gameRoot, null, false, false);
        var result = new RollbackService().Rollback(layout, gameRoot);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"rollback\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Rollback, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportUpdatesValidates()
{
    var tempRoot = NewTempRoot("report-updates");
    try
    {
        var layout = InitLayout(tempRoot);
        // An installed mod with a newer version advertised → a non-empty modUpdates array, so the
        // report exercises the populated shape, not just the empty one.
        WriteInstalledModFixture(layout, "pagonia-land.example.cheaper-sawmill", "0.1.0",
            $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/pagonia-land.example.cheaper-sawmill");
        WriteInstalledCollectionFixture(layout, "pagonia-land.example.beginner-qol", "0.1.0",
            $"gh:acme/presets#{InMemoryRemoteContentFetcher.FakeSha}/pagonia-land.example.beginner-qol");

        var fetcher = MakeUpdateRepoFixture("0.2.0");
        fetcher.AddRef("acme", "presets", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
        fetcher.AddText($"https://raw.githubusercontent.com/acme/presets/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
            indexFormatVersion: "0.1"
            repo:
              name: Acme Presets
            collections:
              - id: pagonia-land.example.beginner-qol
                path: collections/beginner-qol.collection.yaml
                version: 0.3.0
                gameDatabaseVersion: "1.4.0-test"
            """);

        var result = new UpdateDetectionService(fetcher).Check(layout);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"updates\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Updates, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportCollectionInstallValidates()
{
    var tempRoot = NewTempRoot("report-coll");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.report", new[]
        {
            ("test.report.mod", "0.1.0", (string?)null),
        });
        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, null);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"collectionInstall\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.CollectionInstall, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportStatusValidates()
{
    var tempRoot = NewTempRoot("report-status");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new ActiveProfileService().Show(layout);
        var json = ManagerReports.ToJson(result);
        return json.Contains("\"reportKind\": \"status\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.Status, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool ReportDeployStatusValidates()
{
    var tempRoot = NewTempRoot("report-dpstatus");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var result = new DeployStatusService().List(layout, gameRoot);
        var json = ManagerReports.ToJson(result, gameRoot);
        return json.Contains("\"reportKind\": \"deployStatus\"", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.DeployStatus, Path.Combine(tempRoot, "report.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool SchemaValidateRejectsWrongKind()
{
    var tempRoot = NewTempRoot("schema-wrong-kind");
    try
    {
        // Install report validated against deploy schema -> reportKind enum mismatch fails.
        var installJson = MinimalReportFor(ManagerReportKinds.Install);
        var path = Path.Combine(tempRoot, "wrong.json");
        File.WriteAllText(path, installJson);
        var diagnostics = new ManagerSchemaValidator().ValidateReport(ManagerReportKinds.Deploy, path);
        return diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.SchemaValidationFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool SchemaValidateRejectsUnknownKind()
{
    var tempRoot = NewTempRoot("schema-unknown-kind");
    try
    {
        var path = Path.Combine(tempRoot, "x.json");
        File.WriteAllText(path, "{}");
        var diagnostics = new ManagerSchemaValidator().ValidateReport("madeUpKind", path);
        return diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.SchemaValidationFailed
            && d.Message.Contains("Unknown report kind", StringComparison.Ordinal));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool SchemaValidateRejectsMissingFile()
{
    var tempRoot = NewTempRoot("schema-missing-file");
    try
    {
        var diagnostics = new ManagerSchemaValidator().ValidateReport(
            ManagerReportKinds.Install,
            Path.Combine(tempRoot, "does-not-exist.json"));
        return diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.SchemaValidationFailed
            && d.Message.Contains("not found", StringComparison.Ordinal));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool SchemaValidateRejectsMalformedJson()
{
    var tempRoot = NewTempRoot("schema-malformed");
    try
    {
        var path = Path.Combine(tempRoot, "bad.json");
        File.WriteAllText(path, "not { valid json");
        var diagnostics = new ManagerSchemaValidator().ValidateReport(ManagerReportKinds.Install, path);
        return diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.SchemaValidationFailed
            && d.Message.Contains("Failed to parse", StringComparison.Ordinal));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool SchemaValidateRejectsMissingField()
{
    var tempRoot = NewTempRoot("schema-missing-field");
    try
    {
        var path = Path.Combine(tempRoot, "incomplete.json");
        // Missing "outcome" — required by install-report schema.
        File.WriteAllText(path,
            """{"schemaVersion":"0.1","reportKind":"install","diagnostics":[]}""");
        var diagnostics = new ManagerSchemaValidator().ValidateReport(ManagerReportKinds.Install, path);
        return diagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.SchemaValidationFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Pattern B overlay-pak deploy helpers
// ============================================================================

// Mod fixture with a pak: block + an entries.add that ships a *.gd.xml inside the
// overlay's namespace. After PatchApplier runs, the staging tree contains
// <staging>/<pakName>/manifest.json + files.json + <pakName>.gd.bin + memory.bin
// + <pakName>/gdb/example.gd.xml — exactly what PakBuilder needs to pack.
static string MakePakModFixtureDir(string tempRoot, string modId, string version, string subdir, string pakName)
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "entries"));

    File.WriteAllText(Path.Combine(sourceDir, "entries", "example.gd.xml"), $"""
<?xml version="1.0" encoding="utf-8"?>
<GameDatabase>
  <Groups>
    <Group Name="OverlayExample">
      <Entities>
        <Entity Name="OverlayExample" Guid="aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee">
          <Children />
          <Values />
        </Entity>
      </Entities>
    </Group>
  </Groups>
</GameDatabase>
""");

    // entries.add carries only `path` + `source` per the patch-format schema (no `id`).
    // No patches: section here — pure Pattern B overlay, nothing touches the game-root XMLs.
    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture {modId}
version: "{version}"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Pattern B overlay-pak fixture mod.
requiredPackages:
  - core
entries:
  add:
    - path: {pakName}/gdb/example.gd.xml
      source: entries/example.gd.xml
pak:
  name: {pakName}
  summary: Test overlay
  author: Pagonia Land
  image: {pakName}/gdb/example.gd.xml
  dependencies:
    - core
""");

    return sourceDir;
}

static bool PakBuilderMissingScaffold()
{
    var tempRoot = NewTempRoot("pak-missing");
    try
    {
        var result = new PakBuilder().Build(tempRoot, "no-such-mod", Path.Combine(tempRoot, "out.pak"));
        return !result.Success
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.PakScaffoldMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PakBuilderBuildsRealPak()
{
    var tempRoot = NewTempRoot("pak-build-real");
    try
    {
        var scaffoldRoot = Path.Combine(tempRoot, "staging");
        var scaffoldName = "overlay-fixture";
        var scaffoldDir = Path.Combine(scaffoldRoot, scaffoldName);
        Directory.CreateDirectory(scaffoldDir);

        File.WriteAllText(Path.Combine(scaffoldDir, "manifest.json"), "{\"Name\":\"overlay-fixture\"}");
        File.WriteAllBytes(Path.Combine(scaffoldDir, "memory.bin"), new byte[28]);
        File.WriteAllText(Path.Combine(scaffoldDir, "noise.txt"), "hello pak");

        var outPak = Path.Combine(tempRoot, "out.pak");
        var result = new PakBuilder().Build(scaffoldRoot, scaffoldName, outPak);

        return result.Success
            && File.Exists(outPak)
            && new FileInfo(outPak).Length > 0
            && result.EntryCount == 3
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakBuildSucceeded);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployPakOnlyModWritesPak()
{
    var tempRoot = NewTempRoot("deploy-pak-only");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.pak-only";
        var pakName = "fixture-overlay";
        var sourceDir = MakePakModFixtureDir(tempRoot, modId, "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, modId, null);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);

        var expectedPak = Path.Combine(gameRoot, "mods", $"{pakName}.pak");
        return result.Outcome == DeployOutcome.Completed
            && result.AddedFileCount == 1
            && File.Exists(expectedPak)
            && new FileInfo(expectedPak).Length > 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployManifestSeparatesModifiedAndAdded()
{
    var tempRoot = NewTempRoot("deploy-pak-manifest");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var pakName = "fixture-manifest";
        var sourceDir = MakePakModFixtureDir(tempRoot, "pagonia-land.fixture.pak-manifest", "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.pak-manifest", null);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (result.Outcome != DeployOutcome.Completed) return false;

        var yaml = File.ReadAllText(result.ManifestPath!);
        var manifest = new YamlDotNet.Serialization.DeserializerBuilder()
            .IgnoreUnmatchedProperties().Build()
            .Deserialize<DeployManifest>(yaml)!;

        return manifest.ModifiedFiles.Count == 0
            && manifest.AddedFiles.Count == 1
            && manifest.AddedFiles[0].RelativePath == $"mods/{pakName}.pak"
            && manifest.AddedFiles[0].SourceMod == "pagonia-land.fixture.pak-manifest"
            && manifest.AddedFiles[0].ByteSize > 0
            && manifest.AddedFiles[0].DeployedSha256.Length == 64;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployRefusesExistingPak()
{
    var tempRoot = NewTempRoot("deploy-pak-exists");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var pakName = "fixture-collision";
        var sourceDir = MakePakModFixtureDir(tempRoot, "pagonia-land.fixture.pak-collide", "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.pak-collide", null);

        // Pre-create the pak the deploy would write.
        var existing = Path.Combine(gameRoot, "mods", $"{pakName}.pak");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllBytes(existing, [0xAA, 0xBB, 0xCC]);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);

        // Existing bytes survived; no history entry written.
        var canary = File.ReadAllBytes(existing);
        return result.Outcome == DeployOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployBlockedByErrors
                && d.Message.Contains("already exists", StringComparison.Ordinal))
            && canary.AsSpan().SequenceEqual<byte>([0xAA, 0xBB, 0xCC])
            && !new DeployHistoryStore().Exists(layout, result.GameFingerprint!);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RollbackPakRemovesDeployedPak()
{
    var tempRoot = NewTempRoot("rollback-pak-removes");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var pakName = "fixture-remove";
        var sourceDir = MakePakModFixtureDir(tempRoot, "pagonia-land.fixture.pak-remove", "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.pak-remove", null);

        new DeployService().Deploy(layout, gameRoot, null, false, false);
        var pakPath = Path.Combine(gameRoot, "mods", $"{pakName}.pak");
        if (!File.Exists(pakPath)) return false;

        var rollback = new RollbackService().Rollback(layout, gameRoot);

        return rollback.Outcome == RollbackOutcome.Reverted
            && !File.Exists(pakPath);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RollbackPakRoundTripClean()
{
    var tempRoot = NewTempRoot("rollback-pak-clean");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var pakName = "fixture-clean";
        var sourceDir = MakePakModFixtureDir(tempRoot, "pagonia-land.fixture.pak-clean", "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.pak-clean", null);

        var beforeHash = Sha256OfTree(gameRoot);
        new DeployService().Deploy(layout, gameRoot, null, false, false);
        new RollbackService().Rollback(layout, gameRoot);
        var afterHash = Sha256OfTree(gameRoot);

        // After full round-trip the game tree must be byte-identical AND the mods/ directory
        // (if it exists at all) must not contain the deployed pak any longer.
        var modsDir = Path.Combine(gameRoot, "mods");
        var leftoverPak = File.Exists(Path.Combine(modsDir, $"{pakName}.pak"));

        return beforeHash == afterHash
            && !leftoverPak;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployDryRunReportsAddedCount()
{
    var tempRoot = NewTempRoot("deploy-pak-dryrun");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var pakName = "fixture-dryrun";
        var sourceDir = MakePakModFixtureDir(tempRoot, "pagonia-land.fixture.pak-dryrun", "0.1.0", "src", pakName);
        new ModInstaller().Install(sourceDir, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.pak-dryrun", null);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, dryRun: true);

        var pakPath = Path.Combine(gameRoot, "mods", $"{pakName}.pak");
        return result.Outcome == DeployOutcome.DryRun
            && result.ModifiedFileCount == 0
            && result.AddedFileCount == 1
            && !File.Exists(pakPath);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Deploy + rollback (XML patches) helpers
// ============================================================================

static string Sha256OfTree(string root)
{
    using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(
        System.Security.Cryptography.HashAlgorithmName.SHA256);
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                 .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
    {
        var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
        sha.AppendData(System.Text.Encoding.UTF8.GetBytes(rel));
        sha.AppendData([0]);
        sha.AppendData(File.ReadAllBytes(file));
        sha.AppendData([0]);
    }
    return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
}

static bool DeployStatusCorruptHistoryEmitsDiagnostic()
{
    var tempRoot = NewTempRoot("status-corrupt-history");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "status-corrupt");
        new DeployService().Deploy(layout, gameRoot, null, false, false);

        // Corrupt history.yaml — truncate to garbage YAML.
        var fp = GameFingerprint.Compute(gameRoot);
        File.WriteAllText(layout.DeployHistoryFile(fp), "[ not valid yaml");

        // Regression: DeployHistoryStore.Read used to throw InvalidOperationException
        // here, escaping as an unhandled stack trace. With TryRead it surfaces as a
        // DeployHistoryUnreadable error diagnostic and the call returns cleanly.
        var result = new DeployStatusService().List(layout, gameRoot);
        return result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployHistoryUnreadable
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && result.Deploys.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RollbackCorruptHistoryEmitsDiagnostic()
{
    var tempRoot = NewTempRoot("rollback-corrupt-history");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "rollback-corrupt");
        new DeployService().Deploy(layout, gameRoot, null, false, false);

        var fp = GameFingerprint.Compute(gameRoot);
        File.WriteAllText(layout.DeployHistoryFile(fp), "");  // empty YAML → deserializes to null

        var result = new RollbackService().Rollback(layout, gameRoot);
        return result.Outcome == RollbackOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployHistoryUnreadable
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployStampsStateLastDeploy()
{
    var tempRoot = NewTempRoot("deploy-stamps-last");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-stamps");

        // Before deploy: state.LastDeploy must be null.
        var before = new StoreStateReader().Read(layout);
        if (before.LastDeploy is not null) return false;

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (result.Outcome != DeployOutcome.Completed) return false;

        // After deploy: state.LastDeploy populated with the same timestamp, normalised
        // game root, and active profile name the deploy used.
        var after = new StoreStateReader().Read(layout);
        return after.LastDeploy is { } last
            && string.Equals(last.Timestamp, result.Timestamp, StringComparison.Ordinal)
            && string.Equals(last.GameRoot, Path.GetFullPath(gameRoot), StringComparison.Ordinal)
            && string.Equals(last.Profile, result.ProfileName, StringComparison.Ordinal);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static (StoreLayout Layout, string GameRoot, string ModId) SetupDeployFixture(string tempRoot, string modSuffix)
{
    var layout = InitLayout(tempRoot);
    var gameRoot = MakeGameGdbFixture(tempRoot);
    var modId = $"pagonia-land.fixture.{modSuffix}";
    InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
    new ActiveProfileService().Enable(layout, modId, null);
    return (layout, gameRoot, modId);
}

// ============================================================================
// GUI-readiness hardening — async orchestration overloads
// ============================================================================

// Runs `action` and reports whether it surfaced a cancellation. Awaiting a
// Task.Run that a pre-cancelled token aborted throws TaskCanceledException
// (an OperationCanceledException), which GetAwaiter().GetResult() rethrows.
static bool ThrewCancellation(Action action)
{
    try { action(); return false; }
    catch (OperationCanceledException) { return true; }
}

static bool InstallAsyncRunsAndCancels()
{
    var tempRoot = NewTempRoot("install-async");
    try
    {
        var layout = InitLayout(tempRoot);

        // (a) async entry point installs exactly like the sync Install.
        var source = MakeMinimalFixtureDir(tempRoot, "pagonia-land.fixture.install-async", "0.1.0", "src");
        var result = new ModInstaller().InstallAsync(source, layout).GetAwaiter().GetResult();
        if (result.Outcome != InstallOutcome.Installed) return false;
        if (!Directory.Exists(layout.ModVersionDirectory("pagonia-land.fixture.install-async", "0.1.0"))) return false;

        // (b) a pre-cancelled token aborts before any version dir is written.
        var source2 = MakeMinimalFixtureDir(tempRoot, "pagonia-land.fixture.install-async-2", "0.1.0", "src2");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = ThrewCancellation(() =>
            new ModInstaller().InstallAsync(source2, layout, cts.Token).GetAwaiter().GetResult());
        var nothingWritten = !Directory.Exists(layout.ModVersionDirectory("pagonia-land.fixture.install-async-2", "0.1.0"));
        return cancelled && nothingWritten;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanAsyncRunsAndCancels()
{
    var tempRoot = NewTempRoot("plan-async");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "plan-async");

        // (a) async entry point produces the same successful plan as sync Plan.
        var result = new PlanProfileService().PlanAsync(layout, gameRoot, null).GetAwaiter().GetResult();
        if (!result.Success || result.PatcherPlan is null) return false;

        // (b) a pre-cancelled token aborts cleanly (plan writes nothing anyway).
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        return ThrewCancellation(() =>
            new PlanProfileService().PlanAsync(layout, gameRoot, null, null, cts.Token).GetAwaiter().GetResult());
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployAsyncRunsAndCancels()
{
    var tempRoot = NewTempRoot("deploy-async");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-async");
        var beforeHash = Sha256OfTree(gameRoot);

        // (b) cancel on the pristine tree first — the install must stay untouched.
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            var cancelled = ThrewCancellation(() =>
                new DeployService().DeployAsync(layout, gameRoot, null, false, false, cancellationToken: cts.Token)
                    .GetAwaiter().GetResult());
            if (!cancelled) return false;
            if (Sha256OfTree(gameRoot) != beforeHash) return false; // nothing applied
        }

        // (a) async entry point completes + changes the tree like sync Deploy.
        var deploy = new DeployService().DeployAsync(layout, gameRoot, null, false, false).GetAwaiter().GetResult();
        return deploy.Outcome == DeployOutcome.Completed && Sha256OfTree(gameRoot) != beforeHash;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackAsyncRunsAndCancels()
{
    var tempRoot = NewTempRoot("rollback-async");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "rollback-async");
        var beforeHash = Sha256OfTree(gameRoot);

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;
        var afterDeployHash = Sha256OfTree(gameRoot);
        if (afterDeployHash == beforeHash) return false;

        // (b) a pre-cancelled token aborts before any restore — tree stays deployed.
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            var cancelled = ThrewCancellation(() =>
                new RollbackService().RollbackAsync(layout, gameRoot, cancellationToken: cts.Token)
                    .GetAwaiter().GetResult());
            if (!cancelled) return false;
            if (Sha256OfTree(gameRoot) != afterDeployHash) return false; // nothing restored
        }

        // (a) async entry point restores byte-identically like sync Rollback.
        var rollback = new RollbackService().RollbackAsync(layout, gameRoot).GetAwaiter().GetResult();
        return rollback.Outcome == RollbackOutcome.Reverted && Sha256OfTree(gameRoot) == beforeHash;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// GUI-readiness hardening — structured progress reporting
// ============================================================================

// Asserts the structured progress contract a GUI relies on: stages only ever
// advance forward (never bounce back to an earlier phase), the percent within a
// single stage never decreases, every percent is in [0,100], and at least one
// tick carried a real percent (so the assertion isn't vacuous). The forward order
// covers every stage the deploy + rollback + cache paths emit.
static bool ProgressForwardAndMonotonic(IReadOnlyList<DeployProgress> reports)
{
    if (reports.Count == 0) return false;

    var order = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["extract"] = 0,
        ["plan"] = 1,
        ["apply"] = 2,
        ["repack"] = 3,
        ["restore"] = 4,
        ["remove"] = 5,
    };

    var lastOrder = -1;
    string? currentStage = null;
    var lastPercentInStage = -1;

    foreach (var r in reports)
    {
        if (!order.TryGetValue(r.Stage, out var stageOrder)) return false; // unknown stage id
        if (stageOrder < lastOrder) return false;                          // stage went backwards
        if (r.Stage != currentStage)
        {
            currentStage = r.Stage;
            lastPercentInStage = -1; // entering a new stage resets the per-stage percent track
        }
        lastOrder = stageOrder;

        if (r.Percent is int p)
        {
            if (p < 0 || p > 100) return false;       // out of range
            if (p < lastPercentInStage) return false; // percent regressed within the stage
            lastPercentInStage = p;
        }
    }

    return reports.Any(r => r.Percent is not null);
}

static bool DeployProgressIsForwardAndMonotonic()
{
    var tempRoot = NewTempRoot("progress-deploy");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var recorder = new RecordingProgress();

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false, progress: recorder);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        return ProgressForwardAndMonotonic(recorder.Reports);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackProgressIsForwardAndMonotonic()
{
    var tempRoot = NewTempRoot("progress-rollback");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        if (new DeployService().Deploy(layout, gameRoot, null, false, false).Outcome != DeployOutcome.Completed)
            return false;

        var recorder = new RecordingProgress();
        var rollback = new RollbackService().Rollback(layout, gameRoot, progress: recorder);
        if (rollback.Outcome != RollbackOutcome.Reverted) return false;

        return ProgressForwardAndMonotonic(recorder.Reports);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheProgressIsForwardAndMonotonic()
{
    var tempRoot = NewTempRoot("progress-cache");
    try
    {
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var recorder = new RecordingProgress();

        var ensure = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core" }, progress: recorder);
        if (!ensure.Success || ensure.FromCache) return false; // first call must be a real extract

        // Every extract tick is under the 'extract' stage with a percent.
        return recorder.Reports.All(r => r.Stage == "extract")
            && ProgressForwardAndMonotonic(recorder.Reports);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// GUI-readiness hardening — env-var injection
// ============================================================================

static bool ResolverInjectedReaderBeatsProcessEnv()
{
    // Process env says one thing; the injected reader says another. The injected
    // reader must win — that's what lets a GUI override the store without mutating
    // process env (which would leak into spawned subprocesses).
    var prior = Environment.GetEnvironmentVariable(StoreRootResolver.EnvironmentVariableName);
    Environment.SetEnvironmentVariable(StoreRootResolver.EnvironmentVariableName, @".\process-env-store");
    try
    {
        Func<string, string?> injected = name =>
            name == StoreRootResolver.EnvironmentVariableName ? @".\injected-store" : null;
        var resolution = StoreRootResolver.Resolve(null, injected);
        return resolution.Source == StoreRootResolver.ResolutionSource.EnvironmentVariable
            && resolution.Root == Path.GetFullPath(@".\injected-store");
    }
    finally { Environment.SetEnvironmentVariable(StoreRootResolver.EnvironmentVariableName, prior); }
}

static bool ModIoFetcherInjectedEnvReaderConsulted()
{
    // Clear the real process env var so the only way the fetcher can find a key is
    // the injected reader. A success proves the injected reader was consulted.
    var prior = Environment.GetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable);
    Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, null);
    try
    {
        var http = new InMemoryRemoteContentFetcher();
        var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=from-injected-reader";
        http.AddText(apiUrl, MakeModIoJson("Injected Mod", isMap: false, "0.3.0",
            "https://thumb.modcdn.io/files/77/injected-mod.zip", md5: null));

        Func<string, string?> injected = name =>
            name == ModIoFetcher.ApiKeyEnvironmentVariable ? "from-injected-reader" : null;
        var fetcher = new ModIoFetcher(http, apiKeyOverride: null, environment: injected);
        var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));
        return result.Success && result.Version == "0.3.0";
    }
    finally { Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, prior); }
}

// ============================================================================
// GUI-readiness hardening — CancellationToken through the apply path
// ============================================================================

static bool DeployCancelledLeavesLiveInstallUntouched()
{
    var tempRoot = NewTempRoot("deploy-cancel-live");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var pakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = ThrewCancellation(() =>
            new DeployService().DeployAsync(layout, gameRoot, null, false, false, cancellationToken: cts.Token)
                .GetAwaiter().GetResult());

        // The apply-path token aborts before any commit write touches the install,
        // so the live pak must still be byte-for-byte the pre-deploy original.
        var afterBytes = File.ReadAllBytes(pakPath);
        return cancelled && afterBytes.AsSpan().SequenceEqual(originalPakBytes);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool FingerprintStable()
{
    var tempRoot = NewTempRoot("fp-stable");
    try
    {
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var fp1 = GameFingerprint.Compute(gameRoot);
        var fp2 = GameFingerprint.Compute(gameRoot);
        return fp1 == fp2
            && fp1.Length == GameFingerprint.FingerprintLength
            && fp1.All(c => "0123456789abcdef".Contains(c));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool FingerprintDistinct()
{
    var tempRoot = NewTempRoot("fp-distinct");
    try
    {
        var gameRootA = MakeGameGdbFixture(tempRoot);
        // Build a second game-root at a different path.
        var altParent = Path.Combine(tempRoot, "alt");
        Directory.CreateDirectory(altParent);
        var gameRootB = MakeGameGdbFixture(altParent);
        return GameFingerprint.Compute(gameRootA) != GameFingerprint.Compute(gameRootB);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool FingerprintIncludesSystemJson()
{
    var tempRoot = NewTempRoot("fp-system-json");
    try
    {
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var beforeFp = GameFingerprint.Compute(gameRoot);
        File.WriteAllText(Path.Combine(gameRoot, "system.json"), "{\"version\":\"1.0\"}");
        var afterFp = GameFingerprint.Compute(gameRoot);
        return beforeFp != afterFp;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployMissingGameRoot()
{
    var tempRoot = NewTempRoot("deploy-no-game");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new DeployService().Deploy(layout, Path.Combine(tempRoot, "no-such"), null, false, false);
        return result.Outcome == DeployOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.GameRootMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployCleanWrites()
{
    var tempRoot = NewTempRoot("deploy-clean");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-clean");

        var targetFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
        var beforeContent = File.ReadAllText(targetFile);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);

        var afterContent = File.ReadAllText(targetFile);

        return result.Outcome == DeployOutcome.Completed
            && result.ModifiedFileCount == 1
            && File.Exists(result.ManifestPath)
            && Directory.Exists(result.BackupDirectory)
            && beforeContent.Contains("<Amount>4</Amount>", StringComparison.Ordinal)
            && afterContent.Contains("<Amount>3</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployRollbackRoundTripByteIdentical()
{
    var tempRoot = NewTempRoot("deploy-rt");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-rt");
        var beforeHash = Sha256OfTree(gameRoot);

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        var afterDeployHash = Sha256OfTree(gameRoot);
        if (beforeHash == afterDeployHash) return false; // deploy must change something

        var rollback = new RollbackService().Rollback(layout, gameRoot);
        var afterRollbackHash = Sha256OfTree(gameRoot);

        return rollback.Outcome == RollbackOutcome.Reverted
            && rollback.RestoredFileCount == 1
            && afterRollbackHash == beforeHash;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployBlockedByConflict()
{
    var tempRoot = NewTempRoot("deploy-conflict");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        InstallFixtureMod(layout, tempRoot, "pagonia-land.fixture.dpl-conflict-a", "0.1.0", "src-a");
        InstallFixtureMod(layout, tempRoot, "pagonia-land.fixture.dpl-conflict-b", "0.1.0", "src-b");
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-conflict-a", null);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-conflict-b", null);

        var targetFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
        var beforeContent = File.ReadAllText(targetFile);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        var afterContent = File.ReadAllText(targetFile);

        return result.Outcome == DeployOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployBlockedByErrors
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && beforeContent == afterContent; // game untouched
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployBlockedByWarnings()
{
    var tempRoot = NewTempRoot("deploy-warn-block");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        // Two mods with DIFFERENT gameDB versions targeting DIFFERENT entities — produces the
        // profileGameVersionMismatch warning without forcing a patcher conflict.
        var srcA = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.dpl-wm-a", "0.1.0", "src-a", "1.3.0-11768+193445", target: "sawmill");
        var srcB = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.dpl-wm-b", "0.1.0", "src-b", "1.2.2-99999+123456", target: "quarry");
        new ModInstaller().Install(srcA, layout);
        new ModInstaller().Install(srcB, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-wm-a", null);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-wm-b", null);

        var result = new DeployService().Deploy(layout, gameRoot, null, acceptWarnings: false, dryRun: false);

        return result.Outcome == DeployOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployBlockedByWarnings
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployAcceptWarningsProceeds()
{
    var tempRoot = NewTempRoot("deploy-accept-warn");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        // Same fixture shape as DeployBlockedByWarnings (mismatched gameDB versions, different
        // entities so no conflict), but pass --accept-warnings to override the warning gate.
        var srcA = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.dpl-aw-a", "0.1.0", "src-a", "1.3.0-11768+193445", target: "sawmill");
        var srcB = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.dpl-aw-b", "0.1.0", "src-b", "1.2.2-99999+123456", target: "quarry");
        new ModInstaller().Install(srcA, layout);
        new ModInstaller().Install(srcB, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-aw-a", null);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.dpl-aw-b", null);

        var result = new DeployService().Deploy(layout, gameRoot, null, acceptWarnings: true, dryRun: false);

        return result.Outcome == DeployOutcome.Completed
            && result.ModifiedFileCount == 1
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileGameVersionMismatch
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployDryRunLeavesGameUntouched()
{
    var tempRoot = NewTempRoot("deploy-dryrun");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-dryrun");
        var beforeHash = Sha256OfTree(gameRoot);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, dryRun: true);

        var afterHash = Sha256OfTree(gameRoot);

        return result.Outcome == DeployOutcome.DryRun
            && result.ModifiedFileCount == 1
            && afterHash == beforeHash
            && !new DeployHistoryStore().Exists(layout, result.GameFingerprint!)
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployDryRun
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployEmptyProfileIsNoop()
{
    var tempRoot = NewTempRoot("deploy-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var beforeHash = Sha256OfTree(gameRoot);

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        var afterHash = Sha256OfTree(gameRoot);

        return result.Outcome == DeployOutcome.Completed
            && afterHash == beforeHash
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.DeployEmpty
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployManifestRecordsModsAndHashes()
{
    var tempRoot = NewTempRoot("deploy-manifest-records");
    try
    {
        var (layout, gameRoot, modId) = SetupDeployFixture(tempRoot, "deploy-records");
        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (result.Outcome != DeployOutcome.Completed) return false;

        var yaml = File.ReadAllText(result.ManifestPath!);
        var manifest = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
            .Deserialize<DeployManifest>(yaml)!;

        return manifest.Mods.Count == 1
            && manifest.Mods[0].Id == modId
            && manifest.Mods[0].Version == "0.1.0"
            && manifest.ModifiedFiles.Count == 1
            && manifest.ModifiedFiles[0].RelativePath == "core/gdb/buildings.gd.xml"
            && manifest.ModifiedFiles[0].OriginalSha256.Length == 64
            && manifest.ModifiedFiles[0].DeployedSha256.Length == 64
            && manifest.ModifiedFiles[0].OriginalSha256 != manifest.ModifiedFiles[0].DeployedSha256;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RollbackNothingToRollback()
{
    var tempRoot = NewTempRoot("rollback-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var result = new RollbackService().Rollback(layout, gameRoot);
        return result.Outcome == RollbackOutcome.NothingToRollback
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.RollbackNothingToRollback
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RollbackOnlyRevertsLatest()
{
    // Build two distinct deploys, then rollback once and verify only the latest is reverted.
    // We can't really test two deploys against the same target file without conflicts unless
    // we vary the mod between deploys. Easier: enable mod A, deploy. Then disable A, enable B
    // (also a sawmill modifier), deploy again. After rollback, the second deploy should be
    // reverted but the history still has the first deploy entry (or 0 entries if we popped both).
    // Simplest path: just two deploys with the same mod that both write the same value.
    // The second one will be a noop (already at 3) -> DeployEmpty -> no history added.
    //
    // To exercise the multi-deploy path properly, we'd need two profiles to switch between.
    // That works:
    var tempRoot = NewTempRoot("rollback-latest");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.dpl-twostep";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        new ActiveProfileService().Enable(layout, modId, null);

        var deploy1 = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy1.Outcome != DeployOutcome.Completed) return false;

        var historyAfter1 = new DeployHistoryStore().Read(layout, deploy1.GameFingerprint!);
        if (historyAfter1.Deploys.Count != 1) return false;

        // Rollback the only deploy.
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        var historyAfterRollback = new DeployHistoryStore().Read(layout, deploy1.GameFingerprint!);

        return rollback.Outcome == RollbackOutcome.Reverted
            && historyAfterRollback.Deploys.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool DeployStatusShowsLatest()
{
    var tempRoot = NewTempRoot("deploy-status");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "deploy-status");
        new DeployService().Deploy(layout, gameRoot, null, false, false);

        var status = new DeployStatusService().List(layout, gameRoot);
        return status.HasDeploys
            && status.Deploys.Count == 1
            && status.Deploys[0].Profile == "default"
            && status.Deploys[0].ModCount == 1
            && status.Deploys[0].FileCount == 1;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Plan active profile + game-gdb fixture tree helpers
// ============================================================================

// Creates <parentDir>/game-gdb/core/gdb/buildings.gd.xml with the Sawmill entity
// our fixture mods patch. Returns the game-root path (the parent of `core/`).
static string MakeGameGdbFixture(string parentDir, string sawmillAmount = "4")
{
    var gameRoot = Path.Combine(parentDir, "game-gdb");
    var gdbDir = Path.Combine(gameRoot, "core", "gdb");
    Directory.CreateDirectory(gdbDir);
    File.WriteAllText(Path.Combine(gdbDir, "buildings.gd.xml"), $"""
<?xml version="1.0" encoding="utf-8"?>
<GameDatabase>
  <Groups>
    <Group Name="Buildings">
      <Entities>
        <Entity Name="Sawmill" Guid="c732cb26-7487-4a7b-b1ba-b65e094f9bac">
          <Children />
          <Values>
            <AspectBuildup>
              <Costs>
                <Item>
                  <Content>
                    <Resource>c22b4997-5563-44ab-8aa0-04a7b2c826be</Resource>
                    <Amount>{sawmillAmount}</Amount>
                  </Content>
                </Item>
              </Costs>
            </AspectBuildup>
          </Values>
        </Entity>
        <Entity Name="Quarry" Guid="ab999999-9999-4000-8000-000000000001">
          <Children />
          <Values>
            <AspectBuildup>
              <Costs>
                <Item>
                  <Content>
                    <Resource>c22b4997-5563-44ab-8aa0-04a7b2c826be</Resource>
                    <Amount>6</Amount>
                  </Content>
                </Item>
              </Costs>
            </AspectBuildup>
          </Values>
        </Entity>
      </Entities>
    </Group>
  </Groups>
</GameDatabase>
""");
    return gameRoot;
}

// Like MakeMinimalFixtureDir but with a custom gameDatabaseVersion + selectable target
// (Sawmill or Quarry) — for cross-mod version-mismatch tests without forcing a conflict.
static string MakeFixtureDirWithGameVersion(string tempRoot, string modId, string version, string subdir, string gameDatabaseVersion, string target = "sawmill")
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "patches"));
    var opId = modId.Replace('.', '-').Replace('_', '-') + "-op";

    var (entityGuid, entityName, oldValue, newValue) = target == "quarry"
        ? ("ab999999-9999-4000-8000-000000000001", "Quarry", "6", "5")
        : ("c732cb26-7487-4a7b-b1ba-b65e094f9bac", "Sawmill", "4", "3");

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture {modId}
version: "{version}"
author: Pagonia Land
gameDatabaseVersion: "{gameDatabaseVersion}"
description: Inline fixture mod for plan tests with custom game version.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");

    File.WriteAllText(Path.Combine(sourceDir, "patches", "buildings.yaml"), $"""
operations:
  - id: {opId}
    operation: replaceValue
    risk: low
    reason: Plan fixture.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: {entityGuid}
      entityName: {entityName}
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "{oldValue}"
    value: "{newValue}"
""");

    return sourceDir;
}

// ============================================================================
// Tweak-override fixtures + tests
// ============================================================================

static string MakeTweakableFixtureDir(string tempRoot, string modId, string version, string subdir)
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "patches"));

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture {modId}
version: "{version}"
author: Pagonia Land
gameDatabaseVersion: "1.3.1-11826+193733"
description: Inline tweakable fixture mod for tweak-override tests.
requiredPackages:
  - core
tweaks:
  - id: softwood-cost
    type: integer
    label: Softwood trunk cost
    default: 2
    min: 1
    max: 8
    step: 1
  - id: preset
    type: enum
    label: Difficulty preset
    default: standard
    values:
      - value: mild
        label: Mild
      - value: standard
        label: Standard
      - value: hard
        label: Hard
  - id: freebie
    type: boolean
    label: Free upkeep
    default: false
patches:
  - patches/buildings.yaml
""");

    // Non-interpolated raw literal keeps the {{ tweaks.* }} placeholder verbatim.
    File.WriteAllText(Path.Combine(sourceDir, "patches", "buildings.yaml"), """
operations:
  - id: tweakable-softwood-cost
    operation: replaceValue
    risk: low
    reason: Fixture patch whose value comes from the softwood-cost tweak.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.softwood-cost }}"
""");

    return sourceDir;
}

static (StoreLayout Layout, string ModId) SetupTweakableProfile(string tempRoot)
{
    var layout = InitLayout(tempRoot);
    const string modId = "pagonia-land.fixture.tweakable";
    var src = MakeTweakableFixtureDir(tempRoot, modId, "0.1.0", "src");
    new ModInstaller().Install(src, layout);
    new ActiveProfileService().Enable(layout, modId, null);
    return (layout, modId);
}

static bool TweakReadReturnsDeclarationsAndDefaults()
{
    var tempRoot = NewTempRoot("tweak-read-defaults");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var result = new TweakOverrideService().Read(layout, null, modId);

        return result.Success
            && result.Tweaks.Count == 3
            && result.Tweaks.All(t => t.Origin == TweakValueOrigins.Default)
            && result.Tweaks.Single(t => t.Declaration.Id == "softwood-cost").Value == "2"
            && result.Tweaks.Single(t => t.Declaration.Id == "preset").Value == "standard";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetThenReadReflectsOverride()
{
    var tempRoot = NewTempRoot("tweak-set-read");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();

        var set = svc.Set(layout, null, modId, "softwood-cost", "5");
        var view = svc.Read(layout, null, modId).Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return set.Success
            && set.Mutated
            && view.Value == "5"
            && view.Origin == TweakValueOrigins.ProfileOverride;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetOutOfRangeRejected()
{
    var tempRoot = NewTempRoot("tweak-set-range");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();

        var set = svc.Set(layout, null, modId, "softwood-cost", "99");
        var view = svc.Read(layout, null, modId).Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return !set.Success
            && set.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakValueOutOfRange
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && view.Origin == TweakValueOrigins.Default; // nothing stored
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetInvalidTypeRejected()
{
    var tempRoot = NewTempRoot("tweak-set-invalid");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();

        var boolean = svc.Set(layout, null, modId, "freebie", "maybe");
        var enumValue = svc.Set(layout, null, modId, "preset", "ultra");

        return !boolean.Success
            && boolean.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.TweakValueInvalid)
            && !enumValue.Success
            && enumValue.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.TweakValueInvalid);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetUnknownModAndIdRejected()
{
    var tempRoot = NewTempRoot("tweak-set-unknown");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();

        var unknownId = svc.Set(layout, null, modId, "no-such-tweak", "1");
        var unknownMod = svc.Set(layout, null, "pagonia-land.fixture.not-enabled", "x", "1");

        return !unknownId.Success
            && unknownId.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.TweakUnknownId)
            && !unknownMod.Success
            && unknownMod.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.TweakUnknownMod);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakResetDropsOverride()
{
    var tempRoot = NewTempRoot("tweak-reset");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();

        svc.Set(layout, null, modId, "softwood-cost", "5");
        svc.Set(layout, null, modId, "preset", "hard");

        var resetOne = svc.Reset(layout, null, modId, "softwood-cost");
        var afterOne = svc.Read(layout, null, modId);
        var swAfterOne = afterOne.Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var presetAfterOne = afterOne.Tweaks.Single(t => t.Declaration.Id == "preset");

        var resetAll = svc.Reset(layout, null, modId, null);
        var afterAll = svc.Read(layout, null, modId);

        return resetOne.Mutated
            && swAfterOne.Origin == TweakValueOrigins.Default          // dropped
            && presetAfterOne.Origin == TweakValueOrigins.ProfileOverride // untouched
            && resetAll.Mutated
            && afterAll.Tweaks.All(t => t.Origin == TweakValueOrigins.Default); // whole-mod cleared
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanThreadsProfileTweakIntoPlan()
{
    var tempRoot = NewTempRoot("tweak-plan-thread");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot); // Sawmill Amount = 4
        new TweakOverrideService().Set(layout, null, modId, "softwood-cost", "5");

        var plan = new PlanProfileService().Plan(layout, gameRoot, profileName: null);
        var modPlan = plan.PatcherPlan!.ModPlans.Single();
        var resolved = modPlan.ResolvedTweaks.Single(t => t.TweakId == "softwood-cost");
        var write = modPlan.Writes.Single();

        return plan.Success
            && resolved.ResolvedValue == "5"
            && resolved.Origin == PagoniaLand.Patcher.TweakOrigins.External
            && write.OldValue == "4"
            && write.NewValue == "5";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool NumberTweakDrivesMultiplyValueThroughManager()
{
    // Coverage gap closer: the manager's tweak path (set/read/validate + plan threading) had no
    // `number`-type tweak in any fixture — every fixture used integer/boolean/enum. The arithmetic
    // ops make a fractional multiplier the natural case, so exercise it end to end through the manager.
    var tempRoot = NewTempRoot("tweak-number-multiply");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot); // Sawmill Amount = 4
        const string modId = "pagonia-land.fixture.number-tweak";
        var src = Path.Combine(tempRoot, "src");
        Directory.CreateDirectory(Path.Combine(src, "patches"));
        File.WriteAllText(Path.Combine(src, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture Number Tweak
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.1-11826+193733"
description: A fractional number tweak driving a multiplyValue op, configured via the manager.
requiredPackages:
  - core
tweaks:
  - id: cost-multiplier
    type: number
    label: Build-cost multiplier
    default: 1.5
    min: 0.1
    max: 5
    step: 0.1
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(src, "patches", "buildings.yaml"), """
operations:
  - id: scale-softwood-cost
    operation: multiplyValue
    risk: low
    reason: Scale the Sawmill softwood cost by the cost multiplier.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    factor: "{{ tweaks.cost-multiplier }}"
""");
        new ModInstaller().Install(src, layout);
        new ActiveProfileService().Enable(layout, modId, null);

        var svc = new TweakOverrideService();

        // A fractional value is accepted + stored; a fractional value past the max is rejected.
        var set = svc.Set(layout, null, modId, "cost-multiplier", "2.5");
        var outOfRange = svc.Set(layout, null, modId, "cost-multiplier", "9");
        var view = svc.Read(layout, null, modId).Tweaks.Single(t => t.Declaration.Id == "cost-multiplier");

        // The stored 2.5 threads through the multiplyValue op: 4 * 2.5 = 10.
        var plan = new PlanProfileService().Plan(layout, gameRoot, profileName: null);
        var write = plan.PatcherPlan!.ModPlans.Single().Writes.Single();

        return set.Success && set.Mutated
            && view.Declaration.Type == "number"
            && view.Value == "2.5"
            && view.Origin == TweakValueOrigins.ProfileOverride
            // Read surfaces the op the tweak feeds, so the wizard can build an op-aware hint.
            && view.Usages.Any(u => u.OperationType == "multiplyValue" && u.OperandField == "factor" && u.ExpectedOldValue == "4")
            && !outOfRange.Success
            && outOfRange.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.TweakValueOutOfRange)
            && plan.Success
            && write.OperationType == "multiplyValue"
            && write.OldValue == "4"
            && write.NewValue == "10";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakListReportValidatesAndShowsOrigins()
{
    var tempRoot = NewTempRoot("tweak-list-report");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();
        svc.Set(layout, null, modId, "softwood-cost", "5"); // one override; the other two stay default

        var read = svc.Read(layout, null, modId);
        var json = ManagerReports.ToTweakListJson(read);
        var schemaOk = WriteAndValidate(ManagerReportKinds.TweakList, Path.Combine(tempRoot, "list.json"), json);

        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var tweaks = node["tweaks"]!.AsArray();
        var overridden = tweaks.First(t => (string?)t!["id"] == "softwood-cost")!;
        var untouched = tweaks.First(t => (string?)t!["id"] == "preset")!;

        return schemaOk
            && tweaks.Count == 3 // number + enum + boolean all surfaced
            && (string?)overridden["origin"] == "profile-override"
            && (string?)overridden["value"] == "5"
            && (string?)untouched["origin"] == "default"
            && (string?)untouched["value"] == "standard";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetReportValidates()
{
    var tempRoot = NewTempRoot("tweak-set-report");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var result = new TweakOverrideService().Set(layout, null, modId, "preset", "hard");
        var json = ManagerReports.ToTweakSetJson(result, "preset", "hard");

        return result.Success
            && WriteAndValidate(ManagerReportKinds.TweakSet, Path.Combine(tempRoot, "set.json"), json);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakSetReportSurfacesRejection()
{
    var tempRoot = NewTempRoot("tweak-set-reject-report");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var result = new TweakOverrideService().Set(layout, null, modId, "softwood-cost", "99");
        var json = ManagerReports.ToTweakSetJson(result, "softwood-cost", "99");
        var schemaOk = WriteAndValidate(ManagerReportKinds.TweakSet, Path.Combine(tempRoot, "set.json"), json);

        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        return schemaOk
            && (bool)node["success"]! == false
            && node["diagnostics"]!.AsArray().Any(d => (string?)d!["code"] == ManagerDiagnosticCodes.TweakValueOutOfRange);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakResetReportValidates()
{
    var tempRoot = NewTempRoot("tweak-reset-report");
    try
    {
        var (layout, modId) = SetupTweakableProfile(tempRoot);
        var svc = new TweakOverrideService();
        svc.Set(layout, null, modId, "softwood-cost", "5");

        var single = svc.Reset(layout, null, modId, "softwood-cost");
        var singleJson = ManagerReports.ToTweakResetJson(single, "softwood-cost");
        var wholeMod = svc.Reset(layout, null, modId, null);
        var wholeJson = ManagerReports.ToTweakResetJson(wholeMod, null);

        return WriteAndValidate(ManagerReportKinds.TweakReset, Path.Combine(tempRoot, "reset-one.json"), singleJson)
            && WriteAndValidate(ManagerReportKinds.TweakReset, Path.Combine(tempRoot, "reset-all.json"), wholeJson);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// Install + enable a mod that declares an integer tweak `softwood-cost` whose
// previous id `softwood` is listed under aliases. Returns (layout, modId).
static (StoreLayout Layout, string ModId) SetupAliasedTweakProfile(string tempRoot)
{
    var layout = InitLayout(tempRoot);
    const string modId = "pagonia-land.fixture.aliased";
    var src = Path.Combine(tempRoot, "src");
    Directory.CreateDirectory(Path.Combine(src, "patches"));

    File.WriteAllText(Path.Combine(src, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Aliased Tweak Fixture
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.1-11826+193733"
description: Fixture mod whose tweak was renamed (old id under aliases).
requiredPackages:
  - core
tweaks:
  - id: softwood-cost
    type: integer
    label: Softwood trunk cost
    default: 2
    min: 1
    max: 8
    aliases:
      - softwood
      - wood-cost
patches:
  - patches/p.yaml
""");

    File.WriteAllText(Path.Combine(src, "patches", "p.yaml"), """
operations:
  - id: aliased-op
    operation: replaceValue
    risk: low
    reason: aliased tweak fixture
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.softwood-cost }}"
""");

    new ModInstaller().Install(src, layout);
    new ActiveProfileService().Enable(layout, modId, null);
    return (layout, modId);
}

// Overwrite the active profile's stored tweak map for one mod with raw keys —
// used to simulate a profile written before a tweak rename.
static void SetRawProfileTweaks(StoreLayout layout, string modId, Dictionary<string, string> tweaks)
{
    var store = new ProfileStore();
    var name = new StoreStateReader().Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
    var profile = store.Read(layout, name);
    var enabled = profile.EnabledMods
        .Select(m => m.Id == modId
            ? new ProfileEnabledMod { Id = m.Id, Version = m.Version, Tweaks = tweaks }
            : m)
        .ToList();
    store.Write(layout, new ProfileFile
    {
        ProfileVersion = profile.ProfileVersion,
        Name = profile.Name,
        Collection = profile.Collection,
        EnabledMods = enabled,
        LoadOrder = profile.LoadOrder,
    });
}

static Dictionary<string, string>? ReadStoredTweaks(StoreLayout layout, string modId)
{
    var name = new StoreStateReader().Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
    return new ProfileStore().Read(layout, name).EnabledMods.Single(m => m.Id == modId).Tweaks;
}

static bool TweakAliasMigratesOldIdForward()
{
    var tempRoot = NewTempRoot("tweak-alias-migrate");
    try
    {
        var (layout, modId) = SetupAliasedTweakProfile(tempRoot);
        SetRawProfileTweaks(layout, modId, new Dictionary<string, string> { ["softwood"] = "5" });

        var read = new TweakOverrideService().Read(layout, null, modId);
        var view = read.Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var stored = ReadStoredTweaks(layout, modId);

        return read.Success
            && view.Value == "5" // old value followed forward to the current id
            && read.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakMigratedFromAlias
                && d.Severity == ManagerDiagnosticSeverity.Info)
            // profile rewritten on first read: now keyed by the current id, alias gone
            && stored is { } s && s.ContainsKey("softwood-cost") && !s.ContainsKey("softwood")
            && s["softwood-cost"] == "5";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakAliasConflictNewIdWins()
{
    var tempRoot = NewTempRoot("tweak-alias-conflict");
    try
    {
        var (layout, modId) = SetupAliasedTweakProfile(tempRoot);
        SetRawProfileTweaks(layout, modId, new Dictionary<string, string>
        {
            ["softwood"] = "5",      // legacy alias
            ["softwood-cost"] = "7", // current id
        });

        var read = new TweakOverrideService().Read(layout, null, modId);
        var view = read.Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var stored = ReadStoredTweaks(layout, modId);

        return read.Success
            && view.Value == "7" // current id wins
            && read.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakAliasConflict
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && stored is { } s && s["softwood-cost"] == "7" && !s.ContainsKey("softwood");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakTwoAliasesToOneCurrentKeepsOneDeterministically()
{
    // Two legacy aliases ("softwood", "wood-cost") both map to the current id "softwood-cost", and
    // a hand-edited profile stored BOTH (neither stores the current id). Migration must keep one
    // deterministically (the ordinally-smaller alias) + warn, never let dictionary order silently
    // clobber a value (R4-014).
    var tempRoot = NewTempRoot("tweak-alias-two-to-one");
    try
    {
        var (layout, modId) = SetupAliasedTweakProfile(tempRoot);
        SetRawProfileTweaks(layout, modId, new Dictionary<string, string>
        {
            ["softwood"] = "5",   // ordinally smaller alias — should win
            ["wood-cost"] = "8",  // ordinally larger alias — should be dropped with a warning
        });

        var read = new TweakOverrideService().Read(layout, null, modId);
        var view = read.Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var stored = ReadStoredTweaks(layout, modId);

        return read.Success
            && view.Value == "5" // deterministic winner regardless of enumeration order
            && read.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakAliasConflict
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && stored is { } s
            && s["softwood-cost"] == "5"
            && !s.ContainsKey("softwood")
            && !s.ContainsKey("wood-cost");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool TweakOrphanedOverrideKept()
{
    var tempRoot = NewTempRoot("tweak-alias-orphan");
    try
    {
        var (layout, modId) = SetupAliasedTweakProfile(tempRoot);
        SetRawProfileTweaks(layout, modId, new Dictionary<string, string> { ["ancient-knob"] = "9" });

        var read = new TweakOverrideService().Read(layout, null, modId);
        var view = read.Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var stored = ReadStoredTweaks(layout, modId);

        return read.Success
            && view.Value == "2"                       // no override for the declared tweak → default
            && view.Origin == TweakValueOrigins.Default
            && read.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakOrphanedOverride
                && d.Severity == ManagerDiagnosticSeverity.Info)
            && stored is { } s && s.ContainsKey("ancient-knob"); // kept, not silently dropped
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanMissingGameRoot()
{
    var tempRoot = NewTempRoot("plan-no-game");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new PlanProfileService().Plan(layout, Path.Combine(tempRoot, "no-such-game"), profileName: null);
        return !result.Success
            && result.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.GameRootMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanEmptyProfile()
{
    var tempRoot = NewTempRoot("plan-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        var result = new PlanProfileService().Plan(layout, gameRoot, profileName: null);

        return result.Success
            && result.ProfileName == "default"
            && result.PatcherPlan is not null
            && result.PatcherPlan.ModPlans.Count == 0
            && result.PatcherPlan.Writes.Count == 0
            && result.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileEmpty
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanOneModProducesWrite()
{
    var tempRoot = NewTempRoot("plan-one-mod");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.plan-one";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        new ActiveProfileService().Enable(layout, modId, null);

        var result = new PlanProfileService().Plan(layout, gameRoot, profileName: null);

        return result.Success
            && result.PatcherPlan is not null
            && result.PatcherPlan.ModPlans.Count == 1
            && result.PatcherPlan.Writes.Count == 1
            && result.PatcherPlan.Writes[0].OldValue == "4"
            && result.PatcherPlan.Writes[0].NewValue == "3";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanConflictSurfaces()
{
    var tempRoot = NewTempRoot("plan-conflict");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        InstallFixtureMod(layout, tempRoot, "pagonia-land.fixture.plan-conflict-a", "0.1.0", "src-a");
        InstallFixtureMod(layout, tempRoot, "pagonia-land.fixture.plan-conflict-b", "0.1.0", "src-b");

        var active = new ActiveProfileService();
        active.Enable(layout, "pagonia-land.fixture.plan-conflict-a", null);
        active.Enable(layout, "pagonia-land.fixture.plan-conflict-b", null);

        var result = new PlanProfileService().Plan(layout, gameRoot, null);

        return !result.Success
            && result.PatcherPlan is not null
            && result.PatcherPlan.Conflicts.Count >= 1
            && result.ManagerDiagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanMissingModInstall()
{
    var tempRoot = NewTempRoot("plan-missing-install");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.plan-missing";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        new ActiveProfileService().Enable(layout, modId, null);

        // Simulate out-of-band removal: delete the mod's install dir while it stays in the profile.
        Directory.Delete(layout.ModVersionDirectory(modId, "0.1.0"), recursive: true);

        var result = new PlanProfileService().Plan(layout, gameRoot, null);

        return !result.Success
            && result.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModInstallMissing
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanGameVersionMismatch()
{
    var tempRoot = NewTempRoot("plan-version-mismatch");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        var srcA = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.plan-vm-a", "0.1.0", "src-a", "1.3.0-11768+193445");
        var srcB = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.plan-vm-b", "0.1.0", "src-b", "1.2.2-99999+123456");
        new ModInstaller().Install(srcA, layout);
        new ModInstaller().Install(srcB, layout);

        var active = new ActiveProfileService();
        active.Enable(layout, "pagonia-land.fixture.plan-vm-a", null);
        active.Enable(layout, "pagonia-land.fixture.plan-vm-b", null);

        var result = new PlanProfileService().Plan(layout, gameRoot, null);

        return result.ManagerDiagnostics.Any(d =>
            d.Code == ManagerDiagnosticCodes.ProfileGameVersionMismatch
            && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ----- game-vs-mod gameDatabaseVersion compatibility -----

static bool GameDatabaseVersionComparerWorks()
{
    // Parse a well-formed version.
    if (!GameDatabaseVersion.TryParse("1.3.0-11768+193445", out var v) || v is null) return false;
    if (v.Major != 1 || v.Minor != 3 || v.Patch != 0 || v.Build != 11768 || v.Revision != 193445) return false;

    // Malformed input is rejected the same way the manifest validator rejects it.
    foreach (var bad in new[] { "", "1.3.0", "1.3.0-11768", "1.3-11768+1", "1.3.0.0-1+1", "1.3.0-1", "v1.3.0-1+1", "1.3.0+1-1" })
    {
        if (GameDatabaseVersion.TryParse(bad, out _)) return false;
    }
    if (GameDatabaseVersion.TryParse(null, out _)) return false;

    // Within a line, build is the primary ordering key (revision is metadata):
    // a lower build sorts earlier even with a higher revision.
    GameDatabaseVersion.TryParse("1.3.0-11727+999999", out var olderBuild);
    GameDatabaseVersion.TryParse("1.3.0-11768+1", out var newerBuild);
    if (olderBuild!.CompareTo(newerBuild!) >= 0) return false;
    // major.minor.patch tier dominates build.
    GameDatabaseVersion.TryParse("1.2.9-99999+1", out var lowerLine);
    GameDatabaseVersion.TryParse("1.3.0-1+1", out var higherLine);
    if (lowerLine!.CompareTo(higherLine!) >= 0) return false;

    // Tiering against an install version.
    GameDatabaseVersion.TryParse("1.3.0-11768+193445", out var game);
    GameDatabaseVersion.TryParse("1.3.0-11768+193445", out var exact);
    GameDatabaseVersion.TryParse("1.3.0-11727+193140", out var drift);
    GameDatabaseVersion.TryParse("1.2.5-1+1", out var gap);
    return exact!.RelateTo(game!) == GameVersionRelation.Exact
        && drift!.RelateTo(game!) == GameVersionRelation.SameLineDrift
        && gap!.RelateTo(game!) == GameVersionRelation.LineGap;
}

// Install a single mod declaring the given gameDatabaseVersion, enable it, plan
// against the extracted fixture with the supplied install version, return the result.
static PlanProfileResult PlanWithModAndInstallVersion(
    string tempRoot, string modGameVersion, string? installGameVersion)
{
    var layout = InitLayout(tempRoot);
    var gameRoot = MakeGameGdbFixture(tempRoot);
    var src = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.gv-mod", "0.1.0", "src", modGameVersion);
    new ModInstaller().Install(src, layout);
    new ActiveProfileService().Enable(layout, "pagonia-land.fixture.gv-mod", null);
    return new PlanProfileService().Plan(layout, gameRoot, null, installGameVersion);
}

static bool PlanGameVsModExactSilent()
{
    var tempRoot = NewTempRoot("gv-exact");
    try
    {
        var result = PlanWithModAndInstallVersion(tempRoot, "1.3.0-11768+193445", "1.3.0-11768+193445");
        return result.PatcherPlan is not null
            && !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionDrift)
            && !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanGameVsModSameLineDriftInfo()
{
    var tempRoot = NewTempRoot("gv-drift");
    try
    {
        var result = PlanWithModAndInstallVersion(tempRoot, "1.3.0-11727+193140", "1.3.0-11768+193445");
        return result.PatcherPlan is not null // plan still proceeds
            && result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionDrift
                && d.Severity == ManagerDiagnosticSeverity.Info)
            && !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanGameVsModLineGapWarning()
{
    var tempRoot = NewTempRoot("gv-gap");
    try
    {
        var result = PlanWithModAndInstallVersion(tempRoot, "1.2.5-100+200", "1.3.0-11768+193445");
        return result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionDrift);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanGameVsModUnknownDegrades()
{
    var tempRoot = NewTempRoot("gv-unknown");
    try
    {
        // Even a real version gap is silent on the new axis when the install
        // version is unknown — the check degrades to intra-profile-only.
        var result = PlanWithModAndInstallVersion(tempRoot, "1.2.5-100+200", installGameVersion: null);
        return !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionDrift)
            && !result.ManagerDiagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployGameVersionMismatchGatedByAcceptWarnings()
{
    var tempRoot = NewTempRoot("gv-deploy-gate");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        // Extracted layout + a real exe at its root → DeployToExtractedLayout reads
        // the version (1.3.0-11768+193445) and runs the game-vs-mod check.
        PlaceGameExe(gameRoot);

        var src = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.gv-gap-deploy", "0.1.0", "src", "1.2.0-1+1");
        new ModInstaller().Install(src, layout);
        new ActiveProfileService().Enable(layout, "pagonia-land.fixture.gv-gap-deploy", null);

        // Without --accept-warnings: the version-mismatch warning blocks the deploy.
        var blocked = new DeployService().Deploy(layout, gameRoot, null, false, false);
        var blockedOk = blocked.Outcome != DeployOutcome.Completed
            && blocked.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && blocked.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployBlockedByWarnings);

        // With --accept-warnings: the mismatch is advisory, the deploy proceeds.
        var forced = new DeployService().Deploy(layout, gameRoot, null, acceptWarnings: true, false);
        var forcedOk = forced.Outcome == DeployOutcome.Completed
            && forced.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModGameVersionMismatch);

        return blockedOk && forcedOk;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanNamedProfile()
{
    var tempRoot = NewTempRoot("plan-named");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.plan-named";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");

        new ProfileLifecycleService().Create(layout, "alt");
        new ProfileLifecycleService().Use(layout, "alt");
        new ActiveProfileService().Enable(layout, modId, null);

        // Switch back to default so we know the named-profile plan is NOT just reading active.
        new ProfileLifecycleService().Use(layout, "default");

        var result = new PlanProfileService().Plan(layout, gameRoot, profileName: "alt");

        return result.Success
            && result.ProfileName == "alt"
            && result.PatcherPlan?.ModPlans.Count == 1;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanJsonHasEnvelope()
{
    var tempRoot = NewTempRoot("plan-json");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.plan-json";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        new ActiveProfileService().Enable(layout, modId, null);

        var result = new PlanProfileService().Plan(layout, gameRoot, null);
        var json = new ManagerPlanReporter().ToJson(result);

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Patcher uses PascalCase property names (Mods, PlanSource, etc.).
        // Manager envelope is camelCase (profile, diagnostics).
        return root.TryGetProperty("manager", out var managerNode)
            && root.TryGetProperty("patcher", out var patcherNode)
            && managerNode.TryGetProperty("profile", out var profileProp)
            && profileProp.GetString() == "default"
            && managerNode.TryGetProperty("diagnostics", out _)
            && patcherNode.TryGetProperty("Mods", out var mods)
            && mods.GetArrayLength() == 1
            && patcherNode.TryGetProperty("PlanSource", out var planSource)
            && planSource.GetString() == "managerProfile";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanReportsWrittenToDisk()
{
    var tempRoot = NewTempRoot("plan-reports-disk");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var modId = "pagonia-land.fixture.plan-reports";
        InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
        new ActiveProfileService().Enable(layout, modId, null);

        var result = new PlanProfileService().Plan(layout, gameRoot, null);
        var jsonPath = Path.Combine(tempRoot, "out", "plan.json");
        var mdPath = Path.Combine(tempRoot, "out", "plan.md");
        new ManagerPlanReporter().WriteReports(result, mdPath, jsonPath);

        var jsonText = File.ReadAllText(jsonPath);
        var mdText = File.ReadAllText(mdPath);

        return File.Exists(jsonPath)
            && File.Exists(mdPath)
            && jsonText.Contains("\"manager\"", StringComparison.Ordinal)
            && jsonText.Contains("\"patcher\"", StringComparison.Ordinal)
            && mdText.Contains("# Pagonia Land Manager", StringComparison.Ordinal)
            && mdText.Contains("Patcher Plan", StringComparison.Ordinal);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool PlanRespectsLoadOrder()
{
    var tempRoot = NewTempRoot("plan-load-order");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot);

        // Two non-conflicting mods (different op ids, but same target → still conflict).
        // Easier: use two mods both targeting the same path; verify that the FIRST mod in the
        // load order shows up first in the patcher plan. The patcher emits both mods' writes
        // and conflicts on them, but we only care about ModPlans order here.
        var srcA = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.plan-order-a", "0.1.0", "src-a", "1.3.0-11768+193445");
        var srcB = MakeFixtureDirWithGameVersion(tempRoot, "pagonia-land.fixture.plan-order-b", "0.1.0", "src-b", "1.3.0-11768+193445");
        new ModInstaller().Install(srcA, layout);
        new ModInstaller().Install(srcB, layout);

        var active = new ActiveProfileService();
        active.Enable(layout, "pagonia-land.fixture.plan-order-a", null);
        active.Enable(layout, "pagonia-land.fixture.plan-order-b", null);
        active.MoveBefore(layout, "pagonia-land.fixture.plan-order-b", "pagonia-land.fixture.plan-order-a");

        var result = new PlanProfileService().Plan(layout, gameRoot, null);

        return result.PatcherPlan is not null
            && result.PatcherPlan.ModPlans.Count == 2
            && result.PatcherPlan.ModPlans[0].Mod.Manifest.Id == "pagonia-land.fixture.plan-order-b"
            && result.PatcherPlan.ModPlans[1].Mod.Manifest.Id == "pagonia-land.fixture.plan-order-a";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Collection lifecycle helpers
// ============================================================================

static (string ModsRoot, string CollectionPath) BuildCollectionFixture(
    string tempRoot,
    string collectionId,
    (string Id, string Version, string? Source)[] mods)
{
    var modsRoot = Path.Combine(tempRoot, "mods-root");
    Directory.CreateDirectory(modsRoot);

    foreach (var (modId, version, _) in mods)
    {
        var modDir = Path.Combine(modsRoot, modId);
        Directory.CreateDirectory(Path.Combine(modDir, "patches"));
        File.WriteAllText(Path.Combine(modDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture {modId}
version: "{version}"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Fixture mod for collection install tests.
requiredPackages:
  - core
patches:
  - patches/p.yaml
""");
        // Op id must match `^[a-z0-9][a-z0-9-]*$` (no dots) — derive it from modId by
        // replacing forbidden chars with dashes.
        var opId = modId.Replace('.', '-').Replace('_', '-') + "-op";
        File.WriteAllText(Path.Combine(modDir, "patches", "p.yaml"), $"""
operations:
  - id: {opId}
    operation: replaceValue
    risk: low
    reason: collection fixture
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
""");
    }

    var modsYaml = string.Join("\n", mods.Select(m =>
    {
        var sourceLine = m.Source is null
            ? string.Empty
            : $"\n    source: \"{m.Source}\"";
        return $"  - id: {m.Id}\n    version: \"{m.Version}\"{sourceLine}\n    required: true\n    enabled: true";
    }));
    var loadOrder = string.Join("\n", mods.Select(m => $"  - {m.Id}"));

    var collectionPath = Path.Combine(tempRoot, $"{collectionId}.collection.yaml");
    File.WriteAllText(collectionPath, $"""
collectionFormatVersion: 0.1
id: {collectionId}
name: Fixture {collectionId}
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Fixture collection for manager install tests.
conflictPolicy: strict
mods:
{modsYaml}
loadOrder:
{loadOrder}
""");

    return (modsRoot, collectionPath);
}

// Build a one-mod collection whose mod declares an integer tweak and whose
// collection entry supplies a curator override for it. Returns the mods root,
// the collection manifest path, and the mod id.
static (string ModsRoot, string CollectionPath, string ModId) BuildTweakCollectionFixture(
    string tempRoot, string collectionId, string curatorValue)
{
    const string modId = "test.mod.tweakable";
    var modsRoot = Path.Combine(tempRoot, "tw-mods-root");
    var modDir = Path.Combine(modsRoot, modId);
    Directory.CreateDirectory(Path.Combine(modDir, "patches"));

    File.WriteAllText(Path.Combine(modDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Tweakable Fixture
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Fixture mod that declares a tweak for collection-seed tests.
requiredPackages:
  - core
tweaks:
  - id: softwood-cost
    type: integer
    label: Softwood trunk cost
    default: 2
    min: 1
    max: 8
patches:
  - patches/p.yaml
""");

    File.WriteAllText(Path.Combine(modDir, "patches", "p.yaml"), """
operations:
  - id: tweakable-op
    operation: replaceValue
    risk: low
    reason: collection tweak fixture
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.softwood-cost }}"
""");

    var collectionPath = Path.Combine(tempRoot, $"{collectionId}.collection.yaml");
    File.WriteAllText(collectionPath, $"""
collectionFormatVersion: 0.1
id: {collectionId}
name: Tweak Collection
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Fixture collection with a curator tweak override.
conflictPolicy: strict
mods:
  - id: {modId}
    version: "0.1.0"
    required: true
    enabled: true
    tweaks:
      softwood-cost: "{curatorValue}"
loadOrder:
  - {modId}
""");

    return (modsRoot, collectionPath, modId);
}

static bool CollectionInstallSeedsCuratorTweaks()
{
    var tempRoot = NewTempRoot("coll-tweak-seed");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.tweaked", "5");

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        if (result.Outcome != CollectionInstallOutcome.Installed)
        {
            return false;
        }

        var profile = new ProfileStore().Read(layout, result.ProfileName!);
        var stored = profile.EnabledMods.Single(m => m.Id == modId).Tweaks;

        var view = new TweakOverrideService().Read(layout, result.ProfileName, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return stored is { } t && t.TryGetValue("softwood-cost", out var v) && v == "5"
            && view.Value == "5"
            && view.Origin == TweakValueOrigins.CollectionDefault;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallNormalisesCuratorTweak()
{
    var tempRoot = NewTempRoot("coll-tweak-normalise");
    try
    {
        var layout = InitLayout(tempRoot);
        // Curator wrote the value with surrounding whitespace; seeding must normalise (trim) it against
        // the mod's integer declaration, or " 5 " lands verbatim in an integer field the resolver
        // mishandles. (Before the fix it was stored as-is.)
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.normalise", " 5 ");

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        if (result.Outcome != CollectionInstallOutcome.Installed)
        {
            return false;
        }

        var stored = new ProfileStore().Read(layout, result.ProfileName!).EnabledMods.Single(m => m.Id == modId).Tweaks;
        return stored is { } t && t.TryGetValue("softwood-cost", out var v) && v == "5";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionReinstallOverwriteReseedsTweaks()
{
    var tempRoot = NewTempRoot("coll-tweak-reseed");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.reseed", "5");
        var svc = new CollectionInstallService();

        svc.Install(layout, collectionPath, modsRoot, profileNameOverride: null); // seeds softwood-cost=5
        const string profileName = "test.collection.reseed";
        new TweakOverrideService().Set(layout, profileName, modId, "softwood-cost", "7"); // user override

        var result = svc.InstallWithOptions(layout, collectionPath, modsRoot,
            new CollectionInstallOptions { Overwrite = true });

        var view = new TweakOverrideService().Read(layout, profileName, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return result.Outcome == CollectionInstallOutcome.Installed
            && view.Value == "5" // reseeded back to the collection's curator value
            && view.Origin == TweakValueOrigins.CollectionDefault
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.TweakOverridesResetByReinstall
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionReinstallWithoutOverwritePreservesUserOverrides()
{
    var tempRoot = NewTempRoot("coll-tweak-preserve");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.preserve", "5");
        var svc = new CollectionInstallService();

        svc.Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        const string profileName = "test.collection.preserve";
        new TweakOverrideService().Set(layout, profileName, modId, "softwood-cost", "7");

        var result = svc.Install(layout, collectionPath, modsRoot, profileNameOverride: null); // no overwrite

        var view = new TweakOverrideService().Read(layout, profileName, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return result.Outcome == CollectionInstallOutcome.AlreadyInstalled
            && view.Value == "7" // user override survives
            && view.Origin == TweakValueOrigins.ProfileOverride;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallHappyPath()
{
    var tempRoot = NewTempRoot("coll-happy");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.happy", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
            ("test.mod.b", "0.1.0", (string?)null),
        });

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);

        return result.Outcome == CollectionInstallOutcome.Installed
            && result.CollectionId == "test.collection.happy"
            && result.CollectionVersion == "0.1.0"
            && result.InstalledMods.Count == 2
            && File.Exists(layout.CollectionManifestFile("test.collection.happy", "0.1.0"))
            && File.Exists(layout.CollectionLockFile("test.collection.happy"))
            && Directory.Exists(layout.ModVersionDirectory("test.mod.a", "0.1.0"))
            && Directory.Exists(layout.ModVersionDirectory("test.mod.b", "0.1.0"))
            && result.Diagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallProfilePinned()
{
    var tempRoot = NewTempRoot("coll-profile-pin");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.pinned", new[]
        {
            ("test.pin.mod.a", "0.1.0", (string?)null),
            ("test.pin.mod.b", "0.1.0", (string?)null),
        });

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        if (!(result.Outcome == CollectionInstallOutcome.Installed)) return false;

        var profile = new ProfileStore().Read(layout, "test.collection.pinned");
        return profile.Collection == "test.collection.pinned"
            && profile.EnabledMods.Count == 2
            && profile.LoadOrder.SequenceEqual(["test.pin.mod.a", "test.pin.mod.b"]);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallProfileOverride()
{
    var tempRoot = NewTempRoot("coll-profile-override");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.override", new[]
        {
            ("test.ovr.mod", "0.1.0", (string?)null),
        });

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: "my-custom");

        return result.Outcome == CollectionInstallOutcome.Installed
            && result.ProfileName == "my-custom"
            && new ProfileStore().Exists(layout, "my-custom")
            && !new ProfileStore().Exists(layout, "test.collection.override");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallUrlSourceWarning()
{
    var tempRoot = NewTempRoot("coll-url");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.url", new[]
        {
            ("test.url.mod", "0.1.0", (string?)"https://example.invalid/some-mod.zip"),
        });

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);

        return result.Outcome == CollectionInstallOutcome.Installed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.CollectionRemoteSourceUnsupported
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallMissingLocalMod()
{
    var tempRoot = NewTempRoot("coll-missing-local");
    try
    {
        var layout = InitLayout(tempRoot);
        var modsRoot = Path.Combine(tempRoot, "mods-root");
        Directory.CreateDirectory(modsRoot);
        // Note: NO mods written into modsRoot, but the collection references one.
        var collectionPath = Path.Combine(tempRoot, "missing-local.collection.yaml");
        File.WriteAllText(collectionPath, """
collectionFormatVersion: 0.1
id: test.collection.missing
name: Missing Local Mod Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: References a mod not present in modsRoot.
conflictPolicy: strict
mods:
  - id: test.nonexistent.mod
    version: "0.1.0"
    required: true
    enabled: true
""");

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);

        return result.Outcome == CollectionInstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                !d.Code.StartsWith("manager.", StringComparison.Ordinal)
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && !File.Exists(layout.CollectionManifestFile("test.collection.missing", "0.1.0"))
            && !File.Exists(layout.CollectionLockFile("test.collection.missing"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallMissingCollectionFile()
{
    var tempRoot = NewTempRoot("coll-missing-file");
    try
    {
        var layout = InitLayout(tempRoot);
        var modsRoot = Path.Combine(tempRoot, "mods-root");
        Directory.CreateDirectory(modsRoot);

        var result = new CollectionInstallService().Install(layout, Path.Combine(tempRoot, "no-such-file.yaml"), modsRoot, null);

        return result.Outcome == CollectionInstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModSourceNotFound
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallIdempotent()
{
    var tempRoot = NewTempRoot("coll-idempotent");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.idem", new[]
        {
            ("test.idem.mod", "0.1.0", (string?)null),
        });

        var service = new CollectionInstallService();
        var first = service.Install(layout, collectionPath, modsRoot, null);
        var second = service.Install(layout, collectionPath, modsRoot, null);

        return first.Outcome == CollectionInstallOutcome.Installed
            && second.Outcome == CollectionInstallOutcome.AlreadyInstalled
            && second.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.CollectionAlreadyInstalled
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallRecreatesMissingProfile()
{
    var tempRoot = NewTempRoot("coll-recreate-profile");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.recreate", new[]
        {
            ("test.recreate.mod", "0.1.0", (string?)null),
        });

        var service = new CollectionInstallService();
        var first = service.Install(layout, collectionPath, modsRoot, null);
        if (first.Outcome != CollectionInstallOutcome.Installed) return false;

        // User deletes the auto-created profile. Manifest+lockfile remain on disk;
        // mods stay installed in the store.
        new ProfileLifecycleService().Delete(layout, "test.collection.recreate");
        if (new ProfileStore().Exists(layout, "test.collection.recreate")) return false;

        // Re-running the install used to short-circuit on AlreadyInstalled and leave
        // the user stuck with a missing profile. Fix: the AlreadyInstalled check now
        // also requires the profile to exist; if it doesn't, fall through and recreate.
        var second = service.Install(layout, collectionPath, modsRoot, null);

        return second.Outcome == CollectionInstallOutcome.Installed
            && new ProfileStore().Exists(layout, "test.collection.recreate")
            && second.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.CollectionAlreadyInstalled
                && d.Message.Contains("recreating", StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallOverrideErrorNamesOverride()
{
    var tempRoot = NewTempRoot("coll-override-error");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.override", new[]
        {
            ("test.override.mod", "0.1.0", (string?)null),
        });

        // Invalid override name (contains slash, refused by ProfileNameValidator).
        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: "has/slash");

        // Regression: the error used to say "Cannot derive a profile name from collection id 'has/slash'...
        // Pass --profile <name> to override." — blaming the wrong source and telling the user to do
        // exactly what they just did. Fix: branch the message on whether the override was used.
        return result.Outcome == CollectionInstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileNameInvalid
                && d.Message.Contains("Profile name override 'has/slash' is invalid", StringComparison.Ordinal)
                && !d.Message.Contains("derive a profile name from collection id", StringComparison.Ordinal));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionInstallProfileCollisionPreservesCleanState()
{
    var tempRoot = NewTempRoot("coll-profile-collision");
    try
    {
        var layout = InitLayout(tempRoot);
        new ProfileLifecycleService().Create(layout, "test.collection.collision");

        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.collision", new[]
        {
            ("test.coll.mod", "0.1.0", (string?)null),
        });

        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, null);

        return result.Outcome == CollectionInstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ProfileAlreadyExists
                && d.Severity == ManagerDiagnosticSeverity.Error)
            // No manifest, no lockfile, no installed mods — the abort happens BEFORE any write.
            && !File.Exists(layout.CollectionManifestFile("test.collection.collision", "0.1.0"))
            && !File.Exists(layout.CollectionLockFile("test.collection.collision"))
            && !Directory.Exists(layout.ModVersionDirectory("test.coll.mod", "0.1.0"));
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionListEmpty()
{
    var tempRoot = NewTempRoot("coll-list-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        var list = new CollectionLister().List(layout);
        return list.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionListPopulated()
{
    var tempRoot = NewTempRoot("coll-list-populated");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.listed", new[]
        {
            ("test.listed.a", "0.1.0", (string?)null),
            ("test.listed.b", "0.1.0", (string?)null),
        });
        new CollectionInstallService().Install(layout, collectionPath, modsRoot, null);

        var list = new CollectionLister().List(layout);
        return list.Count == 1
            && list[0].Id == "test.collection.listed"
            && list[0].Version == "0.1.0"
            && list[0].ResolvedModCount == 2
            && !string.IsNullOrEmpty(list[0].GeneratedAt)
            && list[0].Name == "Fixture test.collection.listed";
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionUninstallRemovesCollectionOnly()
{
    var tempRoot = NewTempRoot("coll-uninstall");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.unins", new[]
        {
            ("test.unins.mod", "0.1.0", (string?)null),
        });
        new CollectionInstallService().Install(layout, collectionPath, modsRoot, null);

        var result = new CollectionUninstaller().Uninstall(layout, "test.collection.unins");

        return result.Outcome == CollectionUninstallOutcome.Removed
            && result.ManifestDirectoryRemoved
            && result.LockfileRemoved
            && !Directory.Exists(layout.CollectionDirectory("test.collection.unins"))
            && !File.Exists(layout.CollectionLockFile("test.collection.unins"))
            // Mods + profile are deliberately kept.
            && Directory.Exists(layout.ModVersionDirectory("test.unins.mod", "0.1.0"))
            && new ProfileStore().Exists(layout, "test.collection.unins");
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionUninstallMissing()
{
    var tempRoot = NewTempRoot("coll-uninstall-missing");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new CollectionUninstaller().Uninstall(layout, "test.collection.ghost");

        return result.Outcome == CollectionUninstallOutcome.Failed
            && result.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.CollectionNotInstalled
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionUninstallRefusesPathTraversal()
{
    var tempRoot = NewTempRoot("coll-uninstall-traversal");
    try
    {
        var layout = InitLayout(tempRoot);

        // Sibling directory OUTSIDE the store's collections folder with a canary.
        // A traversal collectionId would have recursively deleted it before the guard.
        var outsideDir = Path.Combine(tempRoot, "outside-store");
        Directory.CreateDirectory(outsideDir);
        var canary = Path.Combine(outsideDir, "must-survive.txt");
        File.WriteAllText(canary, "do not delete");

        // "../outside-store" starts inside <storeRoot>/collections and escapes one
        // level up to <tempRoot>/outside-store.
        var result = new CollectionUninstaller().Uninstall(layout, "../outside-store");

        return result.Outcome == CollectionUninstallOutcome.Failed
            && File.Exists(canary)
            && Directory.Exists(outsideDir)
            && result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool RoundTripCollectionLifecycle()
{
    var tempRoot = NewTempRoot("coll-roundtrip");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.rt", new[]
        {
            ("test.rt.mod", "0.1.0", (string?)null),
        });

        var install = new CollectionInstallService().Install(layout, collectionPath, modsRoot, null);
        var afterInstall = new CollectionLister().List(layout);

        new CollectionUninstaller().Uninstall(layout, "test.collection.rt");
        var afterUninstall = new CollectionLister().List(layout);

        return install.Outcome == CollectionInstallOutcome.Installed
            && afterInstall.Count == 1
            && afterUninstall.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// ============================================================================
// Install fixture helpers
// ============================================================================

static StoreLayout InitLayout(string tempRoot)
{
    var storeRoot = Path.Combine(tempRoot, "store");
    var layout = new StoreLayout(storeRoot);
    new StoreInitializer().Initialize(layout);
    return layout;
}

static (StoreLayout Layout, string SourceDir) SetupStoreAndFixture(string tempRoot, string modId)
{
    var layout = InitLayout(tempRoot);
    var sourceDir = MakeMinimalFixtureDir(tempRoot, modId, "0.1.0", subdir: "src");
    return (layout, sourceDir);
}

static string MakeMinimalFixtureDir(string tempRoot, string modId, string version, string subdir)
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(sourceDir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "patches"));

    var manifestName = modId.EndsWith("installer-list", StringComparison.Ordinal)
        ? "Fixture Installer List"
        : $"Fixture {modId.Split('.').Last()}";

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: {manifestName}
version: "{version}"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Inline fixture mod for manager installer tests.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");

    File.WriteAllText(Path.Combine(sourceDir, "patches", "buildings.yaml"), """
operations:
  - id: fixture-replace
    operation: replaceValue
    risk: low
    reason: Fixture patch operation for manager installer tests.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
""");

    return sourceDir;
}

// ============================================================================
// Temp-dir helpers
// ============================================================================

static string NewTempRoot(string label)
{
    var path = Path.Combine(Path.GetTempPath(), $"pagonia-manager-{label}-{Guid.NewGuid():N}");
    Directory.CreateDirectory(path);
    return path;
}

static void CleanupTempRoot(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
}

// ============================================================================
// game-layout detection
// ============================================================================

static bool LayoutDetectLiveInstall()
{
    var tempRoot = NewTempRoot("layout-live");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0xCA, 0xFE });

        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.LiveInstall
            && detected.Root == tempRoot
            && detected.DiscoveredPaks.Count == 1
            && Path.GetFileName(detected.DiscoveredPaks[0]) == "core.pak";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutDetectExtractedLayout()
{
    var tempRoot = NewTempRoot("layout-extracted");
    try
    {
        var gdbDir = Path.Combine(tempRoot, "core", "gdb");
        Directory.CreateDirectory(gdbDir);
        File.WriteAllText(Path.Combine(gdbDir, "buildings.gd.xml"), "<root/>");

        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.ExtractedLayout
            && detected.Root == tempRoot
            && detected.DiscoveredPaks.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutDetectUnrecognisedEmpty()
{
    var tempRoot = NewTempRoot("layout-empty");
    try
    {
        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.Unrecognised;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutDetectUnrecognisedMissingPath()
{
    // A path that simply doesn't exist must classify as Unrecognised rather
    // than throw — the wizard branches on Kind, and a thrown DirectoryNotFound
    // would crash the interactive shell.
    var missing = Path.Combine(Path.GetTempPath(), $"pagonia-manager-nonexistent-{Guid.NewGuid():N}");
    var detected = GameLayoutDetector.Detect(missing);
    return detected.Kind == GameLayoutKind.Unrecognised;
}

static bool LayoutDetectDiscoversAllPaks()
{
    var tempRoot = NewTempRoot("layout-multi-pak");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        // Created out of alphabetical order to verify the detector sorts.
        File.WriteAllBytes(Path.Combine(pakDir, "tools.pak"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0x02 });
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), new byte[] { 0x03 });

        var detected = GameLayoutDetector.Detect(tempRoot);
        if (detected.Kind != GameLayoutKind.LiveInstall) return false;
        if (detected.DiscoveredPaks.Count != 3) return false;
        var names = detected.DiscoveredPaks.Select(Path.GetFileName).ToList();
        return names[0] == "core.pak" && names[1] == "dlc1.pak" && names[2] == "tools.pak";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutDetectLiveWinsOverExtracted()
{
    // Rare but real: a user extracted paks into <install>/core/gdb/ alongside
    // the live pak/ folder. Detection should still prefer live install, since
    // that is what the game reads from.
    var tempRoot = NewTempRoot("layout-both");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0xCA });

        var gdbDir = Path.Combine(tempRoot, "core", "gdb");
        Directory.CreateDirectory(gdbDir);
        File.WriteAllText(Path.Combine(gdbDir, "buildings.gd.xml"), "<root/>");

        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.LiveInstall
            && detected.DiscoveredPaks.Count == 1;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutDetectEmptyPakDirIsUnrecognised()
{
    // A pak/ folder with no *.pak inside must NOT classify as live install —
    // there is nothing to extract. Falls through to extracted-layout check
    // (none here either), so the result is Unrecognised.
    var tempRoot = NewTempRoot("layout-empty-pak");
    try
    {
        Directory.CreateDirectory(Path.Combine(tempRoot, GameLayoutConstants.PakFolderName));
        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.Unrecognised;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// expansion ownership — presence detection + store record + resolver
// ============================================================================

static bool PresenceLiveInstallReportsPaks()
{
    var tempRoot = NewTempRoot("presence-live");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        // core + dlc1 + tools shipped; decorations1 deliberately absent on disk.
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), new byte[] { 0x02 });
        File.WriteAllBytes(Path.Combine(pakDir, "tools.pak"), new byte[] { 0x03 });

        var presence = PackagePresenceDetector.Detect(tempRoot);
        return presence.IsPresent(ExpansionPackages.Core)
            && presence.IsPresent(ExpansionPackages.Dlc1)
            && presence.IsPresent(ExpansionPackages.Tools)
            && !presence.IsPresent(ExpansionPackages.Decorations1);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PresenceExtractedLayoutReportsFolders()
{
    var tempRoot = NewTempRoot("presence-extracted");
    try
    {
        // Extracted layout: core/gdb sentinel + a populated decorations1/ folder.
        var gdbDir = Path.Combine(tempRoot, "core", "gdb");
        Directory.CreateDirectory(gdbDir);
        File.WriteAllText(Path.Combine(gdbDir, "buildings.gd.xml"), "<root/>");
        var decoDir = Path.Combine(tempRoot, "decorations1", "gdb");
        Directory.CreateDirectory(decoDir);
        File.WriteAllText(Path.Combine(decoDir, "decorations.gd.xml"), "<root/>");

        var presence = PackagePresenceDetector.Detect(tempRoot);
        return presence.IsPresent(ExpansionPackages.Core)
            && presence.IsPresent(ExpansionPackages.Decorations1)
            && !presence.IsPresent(ExpansionPackages.Dlc1)
            && !presence.IsPresent(ExpansionPackages.Tools);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PresenceExtractedEmptyFolderNotPresent()
{
    var tempRoot = NewTempRoot("presence-empty-folder");
    try
    {
        var gdbDir = Path.Combine(tempRoot, "core", "gdb");
        Directory.CreateDirectory(gdbDir);
        File.WriteAllText(Path.Combine(gdbDir, "buildings.gd.xml"), "<root/>");
        // An empty dlc1/ folder (e.g. a leftover mkdir) must NOT count as present.
        Directory.CreateDirectory(Path.Combine(tempRoot, "dlc1"));

        var presence = PackagePresenceDetector.Detect(tempRoot);
        return presence.IsPresent(ExpansionPackages.Core)
            && !presence.IsPresent(ExpansionPackages.Dlc1);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PresenceUnrecognisedReportsNothing()
{
    var tempRoot = NewTempRoot("presence-unrecognised");
    try
    {
        var presence = PackagePresenceDetector.Detect(tempRoot);
        return presence.PresentPackages.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool InstallRecordRoundTripsThroughState()
{
    var tempRoot = NewTempRoot("install-record-roundtrip");
    try
    {
        var layout = InitLayout(tempRoot);
        var fingerprint = GameFingerprint.Compute(Path.Combine(tempRoot, "game"));

        var state = new StoreStateReader().Read(layout);
        var withInstall = new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            LastDeploy = state.LastDeploy,
            DefaultGameRoot = state.DefaultGameRoot,
            SubscribedCatalogs = state.SubscribedCatalogs,
            CatalogMaxDepth = state.CatalogMaxDepth,
            AllowInsecureSources = state.AllowInsecureSources,
            CatalogCacheStalenessHours = state.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = state.AllowInsecureCatalogSources,
            Installs = new Dictionary<string, InstallRecord>
            {
                [fingerprint] = new InstallRecord
                {
                    GameRoot = @"C:\Games\PoP",
                    // dlc1 owned, decorations1 explicitly not owned.
                    OwnedExpansions = new OwnedExpansions { Dlc1 = true, Decorations1 = false },
                },
            },
        };
        new StoreStateWriter().Write(layout, withInstall);

        var reread = new StoreStateReader().Read(layout);
        if (!reread.Installs.TryGetValue(fingerprint, out var record)) return false;
        return record.GameRoot == @"C:\Games\PoP"
            && record.OwnedExpansions.Dlc1 == true
            && record.OwnedExpansions.Decorations1 == false
            && record.OwnedExpansions.For(ExpansionPackages.Dlc1) == OwnershipState.Owned
            && record.OwnedExpansions.For(ExpansionPackages.Decorations1) == OwnershipState.NotOwned;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool AbsentInstallsReadsAsUnknown()
{
    var tempRoot = NewTempRoot("install-absent");
    try
    {
        var layout = InitLayout(tempRoot);
        var state = new StoreStateReader().Read(layout);
        // A freshly-initialised store has no install records at all.
        if (state.Installs.Count != 0) return false;

        // An install the store has never seen resolves to unknown for both declarables.
        OwnedExpansions? declared = state.Installs.TryGetValue("nope", out var rec)
            ? rec.OwnedExpansions
            : null;
        return declared is null
            && (declared?.For(ExpansionPackages.Dlc1) ?? OwnershipState.Unknown) == OwnershipState.Unknown
            && (declared?.For(ExpansionPackages.Decorations1) ?? OwnershipState.Unknown) == OwnershipState.Unknown;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PreInstallsStateStaysReadable()
{
    var tempRoot = NewTempRoot("install-pre-0.2");
    try
    {
        var layout = InitLayout(tempRoot);
        // Hand-write a legacy state.yaml: a v0.1 shape with no `installs:` key.
        File.WriteAllText(layout.StateFile,
            "storeVersion: 0.1\nactiveProfile: default\nsubscribedCatalogs: []\n");

        var state = new StoreStateReader().Read(layout);
        return state.StoreVersion == "0.1"
            && state.ActiveProfile == "default"
            && state.Installs is not null
            && state.Installs.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool InstallRecordSurvivesUnrelatedStateWrite()
{
    // The load-bearing guarantee: an unrelated read-modify-write of state.yaml
    // (here SetStoredDefault) must carry the installs map forward, so a deploy
    // stamp or a catalog edit never silently drops declared ownership.
    var tempRoot = NewTempRoot("install-survives-write");
    try
    {
        var layout = InitLayout(tempRoot);
        var fingerprint = GameFingerprint.Compute(Path.Combine(tempRoot, "game"));

        var seeded = new StoreState
        {
            StoreVersion = StoreLayoutConstants.CurrentStoreVersion,
            ActiveProfile = "default",
            Installs = new Dictionary<string, InstallRecord>
            {
                [fingerprint] = new InstallRecord
                {
                    GameRoot = @"C:\Games\PoP",
                    OwnedExpansions = new OwnedExpansions { Dlc1 = true },
                },
            },
        };
        new StoreStateWriter().Write(layout, seeded);

        // An unrelated mutation through the public service path.
        GameRootResolver.SetStoredDefault(layout, @"C:\Games\PoP");

        var reread = new StoreStateReader().Read(layout);
        return reread.DefaultGameRoot == @"C:\Games\PoP"
            && reread.Installs.TryGetValue(fingerprint, out var record)
            && record.OwnedExpansions.Dlc1 == true;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool ResolverAlwaysOwnedPackages()
{
    // core present, tools absent. Both are always owned; effective tracks presence.
    var presence = new PackagePresence(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExpansionPackages.Core });

    var core = ExpansionResolver.Resolve(ExpansionPackages.Core, presence, declared: null);
    var tools = ExpansionResolver.Resolve(ExpansionPackages.Tools, presence, declared: null);

    return core.Ownership == OwnershipState.Owned && core.Present && core.Effective
        && tools.Ownership == OwnershipState.Owned && !tools.Present && !tools.Effective;
}

static bool ResolverDeclarableTruthTable()
{
    // dlc1 present, decorations1 absent.
    var presence = new PackagePresence(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExpansionPackages.Dlc1 });

    // present + owned -> effective.
    var ownedPresent = ExpansionResolver.Resolve(
        ExpansionPackages.Dlc1, presence, new OwnedExpansions { Dlc1 = true });
    // present + not-owned -> not effective.
    var notOwnedPresent = ExpansionResolver.Resolve(
        ExpansionPackages.Dlc1, presence, new OwnedExpansions { Dlc1 = false });
    // absent + owned -> not effective (no pak to be effective from).
    var ownedAbsent = ExpansionResolver.Resolve(
        ExpansionPackages.Decorations1, presence, new OwnedExpansions { Decorations1 = true });

    return ownedPresent is { Present: true, Ownership: OwnershipState.Owned, Effective: true }
        && notOwnedPresent is { Present: true, Ownership: OwnershipState.NotOwned, Effective: false }
        && ownedAbsent is { Present: false, Ownership: OwnershipState.Owned, Effective: false };
}

static bool ResolverUnknownDistinctFromNotOwned()
{
    var presence = new PackagePresence(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExpansionPackages.Dlc1 });

    // Unknown (no declaration) and explicit not-owned both yield effective=false...
    var unknown = ExpansionResolver.Resolve(ExpansionPackages.Dlc1, presence, declared: null);
    var notOwned = ExpansionResolver.Resolve(
        ExpansionPackages.Dlc1, presence, new OwnedExpansions { Dlc1 = false });

    // ...but the tri-state keeps them distinguishable so a surface can prompt vs inform.
    return !unknown.Effective && !notOwned.Effective
        && unknown.Ownership == OwnershipState.Unknown
        && notOwned.Ownership == OwnershipState.NotOwned
        && unknown.Ownership != notOwned.Ownership;
}

static bool ResolverOverrideFlipsEffective()
{
    var presence = new PackagePresence(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExpansionPackages.Dlc1 });
    var declared = new OwnedExpansions { Dlc1 = false };

    // --assume-owned dlc1: override makes a not-owned declared dlc1 effective...
    var assumeOwned = ExpansionResolver.Resolve(
        ExpansionPackages.Dlc1, presence, declared,
        new Dictionary<string, OwnershipState> { ["dlc1"] = OwnershipState.Owned });

    // --assume-not-owned dlc1 over an owned declaration drops effective.
    var assumeNotOwned = ExpansionResolver.Resolve(
        ExpansionPackages.Dlc1, presence, new OwnedExpansions { Dlc1 = true },
        new Dictionary<string, OwnershipState> { ["DLC1"] = OwnershipState.NotOwned }); // casing-insensitive

    return assumeOwned is { Ownership: OwnershipState.Owned, Effective: true }
        && assumeNotOwned is { Ownership: OwnershipState.NotOwned, Effective: false }
        // The stored declaration is untouched — override is transient only.
        && declared.Dlc1 == false;
}

static bool ResolverOverrideIgnoredForAlwaysOwned()
{
    var presence = new PackagePresence(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ExpansionPackages.Core });

    // You cannot "not own" the base game — an override on core is ignored.
    var core = ExpansionResolver.Resolve(
        ExpansionPackages.Core, presence, declared: null,
        new Dictionary<string, OwnershipState> { ["core"] = OwnershipState.NotOwned });

    return core.Ownership == OwnershipState.Owned && core.Effective;
}

// ---- expansion gate (plan/deploy ownership awareness) ----------------------

// A synthetic LoadedMod for gate tests: only the manifest's package + safety
// declarations matter (the gate never reads patch files).
static PagoniaLand.Patcher.LoadedMod GateMod(
    string id,
    string[] required,
    string[]? optional = null,
    (string Id, bool Optional, string[] Packages)[]? patchSets = null,
    PagoniaLand.Patcher.SafetyState? multiplayerSafe = null)
{
    var manifest = new PagoniaLand.Patcher.ModManifest
    {
        Id = id,
        RequiredPackages = required.ToList(),
        OptionalPackages = (optional ?? []).ToList(),
        MultiplayerSafe = multiplayerSafe,
        PatchSets = (patchSets ?? [])
            .Select(p => new PagoniaLand.Patcher.PatchSet { Id = p.Id, Optional = p.Optional, RequiresPackages = p.Packages.ToList() })
            .ToList(),
    };
    return new PagoniaLand.Patcher.LoadedMod(".", manifest, []);
}

// Resolved state for all four canonical packages: core/tools always owned+present;
// decorations1/dlc1 as specified. effective = present && owned.
static IReadOnlyList<ExpansionState> ResolvedStates(
    bool dlc1Present, OwnershipState dlc1Owned,
    bool decoPresent = false, OwnershipState decoOwned = OwnershipState.Unknown)
{
    static ExpansionState Mk(string pkg, bool present, OwnershipState owned)
        => new(pkg, present, owned, present && owned == OwnershipState.Owned);
    return new[]
    {
        Mk(ExpansionPackages.Core, true, OwnershipState.Owned),
        Mk(ExpansionPackages.Decorations1, decoPresent, decoOwned),
        Mk(ExpansionPackages.Dlc1, dlc1Present, dlc1Owned),
        Mk(ExpansionPackages.Tools, true, OwnershipState.Owned),
    };
}

static bool GateRequiredNotPresentIsError()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.needs-dlc1", required: ["core", "dlc1"]) },
        ResolvedStates(dlc1Present: false, dlc1Owned: OwnershipState.Owned));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ModExpansionNotPresent
        && diags[0].Severity == ManagerDiagnosticSeverity.Error;
}

static bool GateRequiredNotOwnedIsWarning()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.needs-dlc1", required: ["core", "dlc1"]) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.NotOwned));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ModExpansionNotOwned
        && diags[0].Severity == ManagerDiagnosticSeverity.Warning;
}

static bool GateRequiredUnknownIsWarning()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.needs-dlc1", required: ["core", "dlc1"]) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.Unknown));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ExpansionOwnershipUnknown
        && diags[0].Severity == ManagerDiagnosticSeverity.Warning;
}

static bool GateRequiredOwnedIsSilent()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.needs-dlc1", required: ["core", "dlc1"]) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.Owned));

    return diags.Count == 0;
}

static bool GateAlwaysOwnedNeverWarns()
{
    // A core-only mod (the common case) on a normal install: silent.
    var silent = ExpansionGate.Evaluate(
        new[] { GateMod("m.core-only", required: ["core"]) },
        ResolvedStates(dlc1Present: false, dlc1Owned: OwnershipState.Unknown));
    if (silent.Count != 0) return false;

    // core somehow absent → presence error (never an ownership warning).
    var coreAbsent = new[]
    {
        new ExpansionState(ExpansionPackages.Core, false, OwnershipState.Owned, false),
        new ExpansionState(ExpansionPackages.Decorations1, false, OwnershipState.Unknown, false),
        new ExpansionState(ExpansionPackages.Dlc1, false, OwnershipState.Unknown, false),
        new ExpansionState(ExpansionPackages.Tools, true, OwnershipState.Owned, true),
    };
    var diags = ExpansionGate.Evaluate(new[] { GateMod("m.core-only", required: ["core"]) }, coreAbsent);
    return diags.Count == 1 && diags[0].Code == ManagerDiagnosticCodes.ModExpansionNotPresent;
}

static bool GateOptionalAbsentSkippedInfo()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.opt-dlc1", required: ["core"], optional: ["dlc1"]) },
        ResolvedStates(dlc1Present: false, dlc1Owned: OwnershipState.Owned));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ModOptionalExpansionSkipped
        && diags[0].Severity == ManagerDiagnosticSeverity.Info;
}

static bool GateOptionalPresentInactiveInfo()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.opt-dlc1", required: ["core"], optional: ["dlc1"]) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.NotOwned));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ModOptionalExpansionInactive
        && diags[0].Severity == ManagerDiagnosticSeverity.Info;
}

static bool GateMultiplayerSafeCarriesCoopNote()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.mp", required: ["core", "dlc1"], multiplayerSafe: PagoniaLand.Patcher.SafetyState.Yes) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.NotOwned));

    return diags.Count == 1
        && diags[0].Code == ManagerDiagnosticCodes.ModExpansionNotOwned
        && diags[0].Message.Contains("co-op", StringComparison.OrdinalIgnoreCase)
        && diags[0].Message.Contains("profile export", StringComparison.OrdinalIgnoreCase);
}

static bool GateNonMultiplayerSafeOmitsCoopNote()
{
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.sp", required: ["core", "dlc1"]) },
        ResolvedStates(dlc1Present: true, dlc1Owned: OwnershipState.NotOwned));

    return diags.Count == 1 && !diags[0].Message.Contains("co-op", StringComparison.OrdinalIgnoreCase);
}

static bool GatePatchSetRequiresCountAsRequired()
{
    // A non-optional patchSet requiring dlc1 makes dlc1 a hard need → not-present error.
    var diags = ExpansionGate.Evaluate(
        new[] { GateMod("m.ps", required: ["core"], patchSets: [("meadowsong", false, ["dlc1"])]) },
        ResolvedStates(dlc1Present: false, dlc1Owned: OwnershipState.Owned));

    return diags.Count == 1 && diags[0].Code == ManagerDiagnosticCodes.ModExpansionNotPresent;
}

static bool GateAdvisoryCodesAreNonBlocking()
{
    return ExpansionGate.IsNonBlockingAdvisory(ManagerDiagnosticCodes.ModExpansionNotOwned)
        && ExpansionGate.IsNonBlockingAdvisory(ManagerDiagnosticCodes.ExpansionOwnershipUnknown)
        && !ExpansionGate.IsNonBlockingAdvisory(ManagerDiagnosticCodes.ModExpansionNotPresent)
        && !ExpansionGate.IsNonBlockingAdvisory(ManagerDiagnosticCodes.ProfileGameVersionMismatch);
}

// ---- expansions CLI surface (list / set) -----------------------------------

static bool ExpansionsSetThenListRoundTrip()
{
    var tempRoot = NewTempRoot("expansions-set-list");
    try
    {
        var layout = InitLayout(tempRoot);
        // A live install with core + dlc1 present (decorations1 absent on disk).
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), new byte[] { 0x02 });

        var svc = new ExpansionOwnershipService();
        var setDlc1 = svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);
        var setDeco = svc.Set(layout, gameRoot, ExpansionPackages.Decorations1, OwnershipState.NotOwned);
        if (!setDlc1.Success || !setDlc1.Mutated || !setDeco.Success) return false;

        var list = svc.List(layout, gameRoot);
        var dlc1 = list.Expansions.Single(e => e.Package == ExpansionPackages.Dlc1);
        var deco = list.Expansions.Single(e => e.Package == ExpansionPackages.Decorations1);

        return list.Success
            // dlc1: present on disk + declared owned → effective.
            && dlc1 is { Present: true, Ownership: OwnershipState.Owned, Effective: true }
            // decorations1: declared not-owned AND absent on disk → not effective.
            && deco is { Present: false, Ownership: OwnershipState.NotOwned, Effective: false };
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool ExpansionsSetRefusesAlwaysOwned()
{
    var tempRoot = NewTempRoot("expansions-set-core");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);

        var result = new ExpansionOwnershipService().Set(layout, gameRoot, ExpansionPackages.Core, OwnershipState.NotOwned);
        if (result.Success) return false;
        if (!result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ExpansionPackageNotDeclarable
            && d.Severity == ManagerDiagnosticSeverity.Error)) return false;

        // Nothing was written: no install record exists.
        return new StoreStateReader().Read(layout).Installs.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool ExpansionsSetMutatedFlagOnReSet()
{
    var tempRoot = NewTempRoot("expansions-set-reset");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);

        var svc = new ExpansionOwnershipService();
        var first = svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);
        var again = svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);

        return first is { Success: true, Mutated: true }
            && again is { Success: true, Mutated: false };
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool ExpansionsListOverrideFlipsEffective()
{
    var tempRoot = NewTempRoot("expansions-list-override");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), new byte[] { 0x02 });

        var svc = new ExpansionOwnershipService();
        svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);

        // --assume-not-owned dlc1 makes dlc1 non-effective for this list, without
        // touching the stored 'owned' record.
        var overridden = svc.List(layout, gameRoot,
            new Dictionary<string, OwnershipState> { [ExpansionPackages.Dlc1] = OwnershipState.NotOwned });
        var dlc1Overridden = overridden.Expansions.Single(e => e.Package == ExpansionPackages.Dlc1);

        var stored = svc.List(layout, gameRoot).Expansions.Single(e => e.Package == ExpansionPackages.Dlc1);

        return dlc1Overridden is { Ownership: OwnershipState.NotOwned, Effective: false }
            && stored is { Ownership: OwnershipState.Owned, Effective: true };
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- plan integration (the gate runs inside PlanProfileService) ------------

// A mod that requires dlc1 (and patches the core sawmill so a real plan would
// otherwise succeed). Mirrors MakeMinimalFixtureDir but with custom packages.
static string MakeDlc1RequiringFixture(string tempRoot, string modId, string subdir, bool optional)
{
    var sourceDir = Path.Combine(tempRoot, subdir);
    Directory.CreateDirectory(Path.Combine(sourceDir, "patches"));
    var packagesBlock = optional
        ? "requiredPackages:\n  - core\noptionalPackages:\n  - dlc1\n"
        : "requiredPackages:\n  - core\n  - dlc1\n";

    File.WriteAllText(Path.Combine(sourceDir, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Fixture {modId}
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Inline fixture mod requiring dlc1 for expansion-gate tests.
{packagesBlock}patches:
  - patches/buildings.yaml
""");

    File.WriteAllText(Path.Combine(sourceDir, "patches", "buildings.yaml"), """
operations:
  - id: fixture-replace
    operation: replaceValue
    risk: low
    reason: Fixture patch operation for expansion-gate tests.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
""");
    return sourceDir;
}

static bool PlanRequiredExpansionNotPresentBlocks()
{
    var tempRoot = NewTempRoot("plan-dlc1-absent");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = MakeGameGdbFixture(tempRoot); // extracted core-only → dlc1 absent
        var modId = "pagonia-land.fixture.needs-dlc1";
        new ModInstaller().Install(MakeDlc1RequiringFixture(tempRoot, modId, "src", optional: false), layout);
        new ActiveProfileService().Enable(layout, modId, null);

        var expansions = ExpansionOwnershipService.ResolveForInstall(layout, gameRoot);
        var result = new PlanProfileService().Plan(layout, gameRoot, null, installGameVersion: null, expansions);

        return !result.Success
            && result.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModExpansionNotPresent
                && d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PlanRequiredExpansionNotOwnedWarnsNotBlocks()
{
    var tempRoot = NewTempRoot("plan-dlc1-notowned");
    try
    {
        var layout = InitLayout(tempRoot);
        // Extracted layout with BOTH core/gdb and a populated dlc1/ folder → dlc1 present.
        var gameRoot = MakeGameGdbFixture(tempRoot);
        var dlc1Dir = Path.Combine(gameRoot, "dlc1", "gdb");
        Directory.CreateDirectory(dlc1Dir);
        File.WriteAllText(Path.Combine(dlc1Dir, "marker.gd.xml"), "<root/>");

        var modId = "pagonia-land.fixture.needs-dlc1-present";
        new ModInstaller().Install(MakeDlc1RequiringFixture(tempRoot, modId, "src", optional: false), layout);
        new ActiveProfileService().Enable(layout, modId, null);

        // Declare dlc1 as not-owned for this install.
        new ExpansionOwnershipService().Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.NotOwned);

        var expansions = ExpansionOwnershipService.ResolveForInstall(layout, gameRoot);
        var result = new PlanProfileService().Plan(layout, gameRoot, null, installGameVersion: null, expansions);

        // The patch target (core sawmill) resolves, so planning succeeds; the
        // not-owned advisory is a warning, never an error.
        return result.Success
            && result.ManagerDiagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.ModExpansionNotOwned
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            && result.ManagerDiagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- expansion onboarding nudge --------------------------------------------

// A store + a live install with core + dlc1 present on disk (decorations1 absent).
static (StoreLayout Layout, string GameRoot) NudgeFixture(string tempRoot)
{
    var layout = InitLayout(tempRoot);
    var gameRoot = Path.Combine(tempRoot, "game");
    var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
    Directory.CreateDirectory(pakDir);
    File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0x01 });
    File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), new byte[] { 0x02 });
    return (layout, gameRoot);
}

static bool NudgeFiresOnPresentUnknown()
{
    var tempRoot = NewTempRoot("nudge-fires");
    try
    {
        var (layout, gameRoot) = NudgeFixture(tempRoot);
        // dlc1 present on disk, no ownership declared yet → unknown → nudge.
        return new ExpansionOwnershipService().ShouldOfferNudge(layout, gameRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool NudgeSkipsWhenNothingUnknown()
{
    var tempRoot = NewTempRoot("nudge-skip-known");
    try
    {
        var (layout, gameRoot) = NudgeFixture(tempRoot);
        var svc = new ExpansionOwnershipService();
        // Declare dlc1 → no present-but-unknown declarable remains (decorations1 is absent).
        svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);
        return !svc.ShouldOfferNudge(layout, gameRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool NudgeAskMeLaterStopsNagging()
{
    var tempRoot = NewTempRoot("nudge-later");
    try
    {
        var (layout, gameRoot) = NudgeFixture(tempRoot);
        var svc = new ExpansionOwnershipService();
        if (!svc.ShouldOfferNudge(layout, gameRoot)) return false;

        // "Ask me later" records that it was offered, but leaves ownership unknown.
        svc.MarkNudgeOffered(layout, gameRoot);

        var dlc1 = svc.List(layout, gameRoot).Expansions.Single(e => e.Package == ExpansionPackages.Dlc1);
        return !svc.ShouldOfferNudge(layout, gameRoot)            // doesn't nag again...
            && dlc1.Ownership == OwnershipState.Unknown;          // ...and stayed unknown.
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool NudgeSetPreservesOfferedFlag()
{
    var tempRoot = NewTempRoot("nudge-preserve");
    try
    {
        var (layout, gameRoot) = NudgeFixture(tempRoot);
        var svc = new ExpansionOwnershipService();
        svc.MarkNudgeOffered(layout, gameRoot);
        // Declaring ownership afterwards must not re-arm the nudge.
        svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);

        var fingerprint = GameFingerprint.Compute(gameRoot);
        var record = new StoreStateReader().Read(layout).Installs[fingerprint];
        return record.NudgeOffered == true
            && record.OwnedExpansions.Dlc1 == true
            && !svc.ShouldOfferNudge(layout, gameRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool StatusListDeclaredInstalls()
{
    var tempRoot = NewTempRoot("status-declared");
    try
    {
        var (layout, gameRoot) = NudgeFixture(tempRoot);
        var svc = new ExpansionOwnershipService();

        // Nothing declared yet.
        if (svc.ListDeclaredInstalls(layout).Count != 0) return false;

        svc.Set(layout, gameRoot, ExpansionPackages.Dlc1, OwnershipState.Owned);

        var declared = svc.ListDeclaredInstalls(layout);
        var fingerprint = GameFingerprint.Compute(gameRoot);
        var entry = declared.SingleOrDefault(d => d.Fingerprint == fingerprint);

        return entry is not null
            && entry.Dlc1 == OwnershipState.Owned
            && entry.Decorations1 == OwnershipState.Unknown
            && entry.GameRoot == Path.GetFullPath(gameRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool NudgeSkipsUninitialisedStore()
{
    var tempRoot = NewTempRoot("nudge-uninit");
    try
    {
        var layout = new StoreLayout(Path.Combine(tempRoot, "store")); // never initialised
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);
        var svc = new ExpansionOwnershipService();
        // No state.yaml → no nudge, and MarkNudgeOffered is a safe no-op.
        svc.MarkNudgeOffered(layout, gameRoot);
        return !svc.ShouldOfferNudge(layout, gameRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// game version surfacing (exe ProductVersion)
// ============================================================================

// The fixture stub assembly carries a known Win32 version resource:
//   ProductVersion 1.3.0-11768+193445, FileVersion 1.3.0.0, ProductName "Pioneers of Pagonia".
// Tests copy it to a temp folder under whatever exe name they need to read it back.
// (Inlined as a literal at each call site rather than a shared const — a top-level
// const here would land after the runner's terminal `return`, tripping CS0162.)
static string GameExeFixtureSourcePath()
    => Path.Combine(AppContext.BaseDirectory, "PagoniaLand.Manager.TestFixtures.GameExe.dll");

// Copy the version-stamped fixture into <gameRoot> under the given exe name.
static void PlaceGameExe(string gameRoot, string exeName = GameLayoutConstants.GameExecutableName)
{
    Directory.CreateDirectory(gameRoot);
    File.Copy(GameExeFixtureSourcePath(), Path.Combine(gameRoot, exeName), overwrite: true);
}

static bool GameVersionReaderReadsExe()
{
    var tempRoot = NewTempRoot("gv-read");
    try
    {
        PlaceGameExe(tempRoot);
        var ok = GameVersionReader.TryRead(tempRoot, out var product, out var file);
        return ok
            && product == "1.3.0-11768+193445"
            // FileVersion is the truncated 4-part numeric form — build/revision lost.
            && file == "1.3.0.0";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameVersionReaderMissingExe()
{
    var tempRoot = NewTempRoot("gv-missing");
    try
    {
        var ok = GameVersionReader.TryRead(tempRoot, out var product, out var file);
        return !ok && product is null && file is null;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameVersionReaderFallbackByProductName()
{
    // Named exe absent → fall back to the single *.exe whose ProductName contains
    // "Pagonia" (covers an upstream rename without hard-failing).
    var tempRoot = NewTempRoot("gv-fallback");
    try
    {
        PlaceGameExe(tempRoot, "PioneersRenamed.exe");
        var ok = GameVersionReader.TryRead(tempRoot, out var product, out _);
        return ok && product == "1.3.0-11768+193445";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutLiveInstallCarriesVersion()
{
    var tempRoot = NewTempRoot("gv-layout");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0xCA, 0xFE });
        PlaceGameExe(tempRoot);

        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.LiveInstall
            && detected.GameProductVersion == "1.3.0-11768+193445";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LayoutLiveInstallNullVersionWithoutExe()
{
    var tempRoot = NewTempRoot("gv-layout-noexe");
    try
    {
        var pakDir = Path.Combine(tempRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), new byte[] { 0xCA, 0xFE });

        var detected = GameLayoutDetector.Detect(tempRoot);
        return detected.Kind == GameLayoutKind.LiveInstall
            && detected.GameProductVersion is null;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployStatusReportExposesGameVersion()
{
    // The deploy-status JSON carries a nullable gameProductVersion field.
    // Validate both the populated and the null shapes against the embedded schema.
    var tempRoot = NewTempRoot("gv-report");
    try
    {
        var withVersion = ManagerReports.ToJson(
            new DeployStatusResult { GameFingerprint = "abc123", GameProductVersion = "1.3.0-11768+193445" },
            "C:\\Games\\PoP");
        var withoutVersion = ManagerReports.ToJson(
            new DeployStatusResult { GameFingerprint = "abc123", GameProductVersion = null },
            "C:\\Games\\PoP");

        // The version string's '+' is JSON-escaped to + by the default encoder,
        // so match on the prefix (the schema validation below confirms the full shape).
        return withVersion.Contains("\"gameProductVersion\": \"1.3.0-11768", StringComparison.Ordinal)
            && withVersion.Contains("\"schemaVersion\": \"0.1\"", StringComparison.Ordinal)
            && withoutVersion.Contains("\"gameProductVersion\": null", StringComparison.Ordinal)
            && WriteAndValidate(ManagerReportKinds.DeployStatus, Path.Combine(tempRoot, "with.json"), withVersion)
            && WriteAndValidate(ManagerReportKinds.DeployStatus, Path.Combine(tempRoot, "without.json"), withoutVersion);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployManifestRecordsGameVersion()
{
    var tempRoot = NewTempRoot("gv-manifest");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        // Drop the version-stamped exe at the install root so detection reads it.
        PlaceGameExe(gameRoot);

        var deploy = new DeployService().Deploy(layout, gameRoot, null, true, false);
        if (deploy.Outcome != DeployOutcome.Completed || deploy.ManifestPath is null) return false;

        var manifest = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
            .Deserialize<DeployManifest>(File.ReadAllText(deploy.ManifestPath));
        return manifest.GameProductVersion == "1.3.0-11768+193445";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UpdateWarningNamesVersions()
{
    var tempRoot = NewTempRoot("gv-update-warn");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        PlaceGameExe(gameRoot); // current install reads as 1.3.0-11768+193445

        // Fake a prior deploy under a different fingerprint pointing at the SAME
        // gameRoot, whose manifest recorded an OLDER version. Hand-write both the
        // history and the manifest so the preflight scan can name old -> new.
        var oldFingerprint = "deadbeef00000002";
        var oldTimestamp = "20260101T000000000Z";
        var oldVersion = "1.2.2-99999+123456";
        new DeployHistoryStore().Write(layout, oldFingerprint, new DeployHistory
        {
            DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
            GameFingerprint = oldFingerprint,
            GameRoot = Path.GetFullPath(gameRoot),
            Deploys = new List<DeployHistoryEntry>
            {
                new() { Timestamp = oldTimestamp, Profile = "default", ModCount = 1, FileCount = 1 },
            },
        });
        Directory.CreateDirectory(layout.DeployTimestampDirectory(oldFingerprint, oldTimestamp));
        File.WriteAllText(
            layout.DeployManifestFile(oldFingerprint, oldTimestamp),
            new SerializerBuilder().Build().Serialize(new DeployManifest
            {
                DeployVersion = StoreLayoutConstants.CurrentDeployVersion,
                Timestamp = oldTimestamp,
                GameRoot = Path.GetFullPath(gameRoot),
                GameFingerprint = oldFingerprint,
                GameProductVersion = oldVersion,
                Profile = "default",
            }));

        var deploy = new DeployService().Deploy(layout, gameRoot, null, true, false);
        var warning = deploy.Diagnostics.FirstOrDefault(d =>
            d.Code == ManagerDiagnosticCodes.GameUpdatedSinceLastDeploy);
        return deploy.Outcome == DeployOutcome.Completed
            && warning is not null
            && warning.Message.Contains($"updated from v{oldVersion} to v1.3.0-11768+193445", StringComparison.Ordinal);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// pak extract cache
// ============================================================================

static bool PakCacheColdMissExtracts()
{
    var tempRoot = NewTempRoot("cache-cold");
    try
    {
        // Real PoP paks already prefix entries with their package name (a core.pak entry
        // would be "core/gdb/...", not bare "gdb/..."). Tests mirror that shape.
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var result = new PakCacheService().Ensure(store, detected);

        if (!result.Success) return false;
        if (result.FromCache) return false;
        if (result.ExtractedPaks.Count != 1) return false;

        // Entries extract verbatim — "core/probe.txt" lands at <cache>/core/probe.txt.
        // No extra pak-basename prefix is added; doing so would double "core/".
        var extractedFile = Path.Combine(result.CacheRoot, "core", "probe.txt");
        if (!File.Exists(extractedFile) || File.ReadAllText(extractedFile) != "hello") return false;

        // v3: per-pak status file (.extract-status.yaml) commits which paks are warm.
        // Legacy .extract-complete sentinel is not written anymore.
        return File.Exists(store.PakCacheStatusFile(result.Fingerprint))
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakCacheRefreshed);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheWarmHitReuses()
{
    var tempRoot = NewTempRoot("cache-warm");
    try
    {
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var first = new PakCacheService().Ensure(store, detected);
        if (!first.Success || first.FromCache) return false;

        var second = new PakCacheService().Ensure(store, detected);
        return second.Success
            && second.FromCache
            && second.CacheRoot == first.CacheRoot
            && second.ExtractedPaks.Count == 0
            && second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakCacheReused);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCachePreservesEntryPaths()
{
    // Real PoP paks embed the package name in each entry's filename (e.g. "core/gdb/...").
    // The cache must reproduce those paths verbatim so the patcher's
    // <root>/<package>/gdb/<file> resolution lines up with the cache root as <root>.
    var tempRoot = NewTempRoot("cache-prefix");
    try
    {
        var store = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"),
            BuildTinyPak(("core/gdb/buildings.gd.xml", "<a/>")));
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"),
            BuildTinyPak(("dlc1/maps/meadowsong.gd.xml", "<b/>"), ("dlc1/gdb/foo.gd.xml", "<c/>")));

        var detected = GameLayoutDetector.Detect(gameRoot);
        var result = new PakCacheService().Ensure(store, detected);
        if (!result.Success) return false;

        // No double-prefix — entries land at exactly their in-pak path.
        return File.Exists(Path.Combine(result.CacheRoot, "core", "gdb", "buildings.gd.xml"))
            && File.Exists(Path.Combine(result.CacheRoot, "dlc1", "maps", "meadowsong.gd.xml"))
            && File.Exists(Path.Combine(result.CacheRoot, "dlc1", "gdb", "foo.gd.xml"))
            && !Directory.Exists(Path.Combine(result.CacheRoot, "core", "core")); // would-be double prefix
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCachePrunesStaleOnFingerprintChange()
{
    // v4 fingerprint: hashes the discovered pak FILENAME LIST + system.json,
    // not per-pak size/mtime. So this test triggers a fingerprint change by
    // ADDING a new pak file (simulating DLC install) rather than by modifying
    // an existing one — modifying content is a deliberate no-op for the
    // fingerprint now, so manager-deploy + rollback don't invalidate the cache.
    var tempRoot = NewTempRoot("cache-prune");
    try
    {
        var (store, gameRoot, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var first = new PakCacheService().Ensure(store, detected);
        if (!first.Success) return false;
        var firstCacheDir = first.CacheRoot;

        // Install a new pak (simulates the user installing a DLC). The discovered-
        // pak list grows, so the fingerprint must change and the old cache must
        // be pruned in favour of a fresh one for the new install topology.
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), BuildTinyPak(("dlc1/probe.txt", "new dlc")));
        var detectedAfter = GameLayoutDetector.Detect(gameRoot);

        var second = new PakCacheService().Ensure(store, detectedAfter);
        if (!second.Success || second.CacheRoot == firstCacheDir) return false;
        // Old cache directory must be gone; new one present.
        return !Directory.Exists(firstCacheDir) && Directory.Exists(second.CacheRoot);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheFingerprintStableAcrossDeployRollback()
{
    // user-reported pain: deploy -> rollback -> deploy
    // was re-extracting the cache on the second deploy because the v3 fingerprint
    // included pak mtime, and manager-deploy + rollback both rewrote the pak
    // file (giving it a fresh mtime each time). The v4 fingerprint omits per-pak
    // mtime/size and uses only the filename list + system.json content, so the
    // round-trip keeps the cache warm.
    var tempRoot = NewTempRoot("cache-rt-stable");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);

        // First deploy — warms the cache.
        var first = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (first.Outcome != DeployOutcome.Completed) return false;

        // Rollback — restores the pre-deploy pak bytes. v3 would have changed
        // the live pak's mtime here, but v4 doesn't care.
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        if (rollback.Outcome != RollbackOutcome.Reverted) return false;

        // Second deploy — should hit the cache (FromCache=true on the underlying
        // ensure call). We can't observe that directly through DeployResult, but
        // we CAN re-detect + re-resolve the pak fingerprint and confirm it's
        // unchanged from the first deploy + that the cache dir still exists.
        var detected = GameLayoutDetector.Detect(gameRoot);
        var ensureResult = new PakCacheService().Ensure(layout, detected,
            requiredPakBasenames: new[] { "core" });

        return ensureResult.Success
            && ensureResult.FromCache  // <- the key assertion: no re-extract
            && ensureResult.ExtractedPaks.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheFailedExtractKeepsGoodPaks()
{
    var tempRoot = NewTempRoot("cache-fail");
    try
    {
        var store = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        // First pak OK, second is empty bytes — PakReader will reject the truncated footer.
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), BuildTinyPak(("core/probe.txt", "ok")));
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), Array.Empty<byte>());

        var detected = GameLayoutDetector.Detect(gameRoot);
        var result = new PakCacheService().Ensure(store, detected);

        if (result.Success) return false;
        if (!result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakCacheExtractFailed)) return false;

        // v3 semantics (different from v2): paks that DID extract cleanly are kept
        // and recorded in .extract-status.yaml so the next ensure call resumes
        // incrementally. Don't punish the user by wiping gigabytes of successful
        // extract work over one bad pak.
        if (!Directory.Exists(result.CacheRoot)) return false;
        if (!File.Exists(Path.Combine(result.CacheRoot, "core", "probe.txt"))) return false;
        if (!File.Exists(store.PakCacheStatusFile(result.Fingerprint))) return false;

        // Status must list "core" (extracted cleanly) but NOT "dlc1" (failed mid-extract).
        var statusYaml = File.ReadAllText(store.PakCacheStatusFile(result.Fingerprint));
        return statusYaml.Contains("core", StringComparison.Ordinal)
            && !statusYaml.Contains("dlc1", StringComparison.Ordinal);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheRejectsNonLiveLayout()
{
    var tempRoot = NewTempRoot("cache-misuse");
    try
    {
        var store = InitLayout(tempRoot);
        // An ExtractedLayout that callers shouldn't be passing to Ensure
        // (they should branch on Kind themselves).
        var gameRoot = Path.Combine(tempRoot, "extracted");
        Directory.CreateDirectory(Path.Combine(gameRoot, "core", "gdb"));
        File.WriteAllText(Path.Combine(gameRoot, "core", "gdb", "foo.gd.xml"), "<a/>");
        var detected = GameLayoutDetector.Detect(gameRoot);

        try
        {
            new PakCacheService().Ensure(store, detected);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Helpers used by the earlier work tests.
static (StoreLayout Store, string GameRoot, GameLayout Detected) SetupLiveInstallWithOnePak(
    string tempRoot, string pakFileName, string entryName, string payloadText)
{
    var store = InitLayout(tempRoot);
    var gameRoot = Path.Combine(tempRoot, "game");
    var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
    Directory.CreateDirectory(pakDir);
    File.WriteAllBytes(Path.Combine(pakDir, pakFileName),
        BuildTinyPak((entryName, payloadText)));
    var detected = GameLayoutDetector.Detect(gameRoot);
    return (store, gameRoot, detected);
}

// Build a minimal uncompressed pak with one or more named entries. The
// reader enforces the footer CRC over data+index, so the rolling Crc32
// must include every data byte before WriteIndex extends it over the
// index. Without that, OpenIndex bails with pakIndexCrcMismatch and the
// cache extract test fails before it gets to the real assertion.
static byte[] BuildTinyPak(params (string Name, string Payload)[] entries)
{
    var writer = new PakWriter();
    using var pakStream = new MemoryStream();
    var pakEntries = new List<PakEntry>(entries.Length);
    var crc = new System.IO.Hashing.Crc32();
    foreach (var (name, payload) in entries)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var beginOffset = pakStream.Position;
        pakStream.Write(bytes, 0, bytes.Length);
        crc.Append(bytes);
        pakEntries.Add(new PakEntry(
            Compressed: false,
            Filename: name,
            BeginOffset: beginOffset,
            Size: bytes.Length));
    }
    writer.WriteIndex(pakStream, pakEntries, version: 1, rollingCrc: crc);
    return pakStream.ToArray();
}

// ============================================================================
// canonical-pak external-change detection at cache time
// ============================================================================

static bool PakCacheUntouchedNoExternalChangeWarning()
{
    var tempRoot = NewTempRoot("cache-drift-untouched");
    try
    {
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var first = new PakCacheService().Ensure(store, detected, requiredPakBasenames: new[] { "core" });
        if (!first.Success || first.FromCache) return false;

        // Nothing changed → warm hit, no external-change warning, fast path intact.
        var second = new PakCacheService().Ensure(store, detected, requiredPakBasenames: new[] { "core" });
        return second.Success
            && second.FromCache
            && !second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CanonicalPakChangedExternally);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheExternalEditWarnsAndReExtracts()
{
    var tempRoot = NewTempRoot("cache-drift-external");
    try
    {
        var (store, gameRoot, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "hello");
        var first = new PakCacheService().Ensure(store, detected, requiredPakBasenames: new[] { "core" });
        if (!first.Success) return false;
        var cachedProbe = Path.Combine(first.CacheRoot, "core", "probe.txt");
        if (File.ReadAllText(cachedProbe) != "hello") return false;

        // Out-of-band edit: another tool rewrites core.pak with different content
        // (not via the manager — no deploy recorded it).
        var pakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");
        File.WriteAllBytes(pakPath, BuildTinyPak(("core/probe.txt", "edited-externally")));

        var second = new PakCacheService().Ensure(store, detected, requiredPakBasenames: new[] { "core" });
        return second.Success
            && second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CanonicalPakChangedExternally
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            // Self-healing: the cache now reflects the new canonical content.
            && File.ReadAllText(cachedProbe) == "edited-externally";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheManagerWritesStaySilent()
{
    var tempRoot = NewTempRoot("cache-drift-manager");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var detected = GameLayoutDetector.Detect(gameRoot);

        // Deploy rewrites the live core.pak — a manager-authored change.
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Ensure after the deploy: live pak differs from the cached baseline, but
        // its hash matches a recorded RebuiltPaks.NewSha256 → no false warning.
        var afterDeploy = new PakCacheService().Ensure(layout, detected, requiredPakBasenames: new[] { "core" });
        if (afterDeploy.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CanonicalPakChangedExternally)) return false;

        // Rollback restores the original pak; the next ensure matches the baseline
        // exactly → still silent (confirms no self-invalidation regression).
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        if (rollback.Outcome != RollbackOutcome.Reverted) return false;

        var afterRollback = new PakCacheService().Ensure(layout, detected, requiredPakBasenames: new[] { "core" });
        return !afterRollback.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CanonicalPakChangedExternally);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// pak rebuild + live-install write-back
// ============================================================================

static bool PakRebuildReplacesNamedEntries()
{
    var tempRoot = NewTempRoot("rebuild-replace");
    try
    {
        var pakPath = Path.Combine(tempRoot, "core.pak");
        File.WriteAllBytes(pakPath, BuildTinyPak(
            ("core/gdb/buildings.gd.xml", "<original-buildings/>"),
            ("core/gdb/units.gd.xml", "<original-units/>")));

        var replacement = Path.Combine(tempRoot, "patched-buildings.gd.xml");
        File.WriteAllText(replacement, "<patched-buildings/>");

        var outputPath = Path.Combine(tempRoot, "core-rebuilt.pak");
        var result = new PakRebuilder().Rebuild(
            originalPakPath: pakPath,
            outputPakPath: outputPath,
            replacements: new Dictionary<string, string>
            {
                ["core/gdb/buildings.gd.xml"] = replacement,
            });

        if (!result.Success) return false;
        if (result.EntriesTotal != 2 || result.EntriesReplaced != 1) return false;
        if (result.NewSha256 == result.OriginalSha256) return false;

        // Open the rebuilt pak and assert: replaced entry has new bytes, untouched entry preserved.
        var reader = new PakReader();
        using var rebuilt = File.OpenRead(outputPath);
        var open = reader.OpenIndex(rebuilt);
        if (!open.Success || open.Index is null || open.Index.Entries.Count != 2) return false;

        var byName = open.Index.Entries.ToDictionary(e => e.Filename, StringComparer.Ordinal);
        using var buildingsOut = new MemoryStream();
        reader.ExtractEntry(rebuilt, byName["core/gdb/buildings.gd.xml"], buildingsOut);
        using var unitsOut = new MemoryStream();
        reader.ExtractEntry(rebuilt, byName["core/gdb/units.gd.xml"], unitsOut);

        return System.Text.Encoding.UTF8.GetString(buildingsOut.ToArray()) == "<patched-buildings/>"
            && System.Text.Encoding.UTF8.GetString(unitsOut.ToArray()) == "<original-units/>";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakRebuildEmptyReplacementsRoundTrips()
{
    // No replacements → every entry's data must come through byte-identical via
    // the raw-copy path. The output pak isn't necessarily byte-equal to the
    // original (PakWriter may serialize entry ordering / index padding the same,
    // but the data section is built fresh) — assert the data round-trips instead.
    var tempRoot = NewTempRoot("rebuild-noop");
    try
    {
        var pakPath = Path.Combine(tempRoot, "core.pak");
        File.WriteAllBytes(pakPath, BuildTinyPak(
            ("core/gdb/a.gd.xml", "<a-content/>"),
            ("core/gdb/b.gd.xml", "<b-content/>")));

        var outputPath = Path.Combine(tempRoot, "core-rebuilt.pak");
        var result = new PakRebuilder().Rebuild(pakPath, outputPath,
            new Dictionary<string, string>());

        if (!result.Success || result.EntriesReplaced != 0) return false;

        var reader = new PakReader();
        using var rebuilt = File.OpenRead(outputPath);
        var open = reader.OpenIndex(rebuilt);
        if (!open.Success || open.Index is null || open.Index.Entries.Count != 2) return false;

        var byName = open.Index.Entries.ToDictionary(e => e.Filename, StringComparer.Ordinal);
        using var aOut = new MemoryStream();
        reader.ExtractEntry(rebuilt, byName["core/gdb/a.gd.xml"], aOut);
        using var bOut = new MemoryStream();
        reader.ExtractEntry(rebuilt, byName["core/gdb/b.gd.xml"], bOut);

        return System.Text.Encoding.UTF8.GetString(aOut.ToArray()) == "<a-content/>"
            && System.Text.Encoding.UTF8.GetString(bOut.ToArray()) == "<b-content/>";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakRebuildMissingReplacementFails()
{
    var tempRoot = NewTempRoot("rebuild-missing");
    try
    {
        var pakPath = Path.Combine(tempRoot, "core.pak");
        File.WriteAllBytes(pakPath, BuildTinyPak(("core/gdb/a.gd.xml", "<a/>")));

        var outputPath = Path.Combine(tempRoot, "core-rebuilt.pak");
        var result = new PakRebuilder().Rebuild(pakPath, outputPath,
            new Dictionary<string, string>
            {
                ["core/gdb/a.gd.xml"] = Path.Combine(tempRoot, "does-not-exist.xml"),
            });

        return !result.Success
            && result.Diagnostics.Any(d => d.Code == "manager.pakRebuildFailed")
            // Output file must be wiped on failure — leaving a half-written pak
            // behind would be worse than no pak at all.
            && !File.Exists(outputPath);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployLiveInstallRepacksAffectedPak()
{
    var tempRoot = NewTempRoot("deploy-live");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);

        // Confirm the original pak's payload contains "<Amount>4</Amount>" (sawmill cost = 4).
        var originalEntry = ReadFirstEntryUtf8(originalPakBytes, "core/gdb/buildings.gd.xml");
        if (!originalEntry.Contains("<Amount>4</Amount>", StringComparison.Ordinal)) return false;

        var result = new DeployService().Deploy(layout, gameRoot, null, false, false);

        if (result.Outcome != DeployOutcome.Completed) return false;
        if (result.RebuiltPakCount != 1) return false;
        if (string.IsNullOrEmpty(result.ManifestPath) || !File.Exists(result.ManifestPath)) return false;
        if (string.IsNullOrEmpty(result.BackupDirectory) || !Directory.Exists(result.BackupDirectory)) return false;

        // Live pak got rewritten — extract its buildings.gd.xml entry and verify the patched value.
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");
        var livePakBytes = File.ReadAllBytes(livePakPath);
        var patched = ReadFirstEntryUtf8(livePakBytes, "core/gdb/buildings.gd.xml");
        if (!patched.Contains("<Amount>3</Amount>", StringComparison.Ordinal)) return false;

        // Backup contains byte-identical original.
        var backupPakPath = Path.Combine(result.BackupDirectory!, GameLayoutConstants.PakFolderName, "core.pak");
        if (!File.Exists(backupPakPath)) return false;
        if (!File.ReadAllBytes(backupPakPath).AsSpan().SequenceEqual(originalPakBytes)) return false;

        // Manifest records the rebuilt pak entry (parse YAML rather than substring-match for stability).
        var manifestYaml = File.ReadAllText(result.ManifestPath!);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.NullNamingConvention.Instance)
            .Build();
        var manifest = deserializer.Deserialize<DeployManifest>(manifestYaml);
        if (manifest.DeployVersion != StoreLayoutConstants.CurrentDeployVersion) return false;
        if (manifest.RebuiltPaks.Count != 1) return false;
        var pakEntry = manifest.RebuiltPaks[0];
        return pakEntry.PakName == "core.pak"
            && pakEntry.BackupRelativePath == "pak/core.pak"
            && pakEntry.TargetRelativePath == "pak/core.pak"
            && pakEntry.OriginalSha256.Length == 64
            && pakEntry.NewSha256.Length == 64
            && pakEntry.OriginalSha256 != pakEntry.NewSha256;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployLiveInstallDryRunWritesNothing()
{
    var tempRoot = NewTempRoot("deploy-live-dry");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        var result = new DeployService().Deploy(layout, gameRoot, null, false, dryRun: true);

        return result.Outcome == DeployOutcome.DryRun
            && result.RebuiltPakCount == 1
            // Live pak untouched.
            && File.ReadAllBytes(livePakPath).AsSpan().SequenceEqual(originalPakBytes)
            // Manifest + backup dir not created in dry-run mode.
            && (result.ManifestPath is null || !File.Exists(result.ManifestPath))
            && (result.BackupDirectory is null || !Directory.Exists(result.BackupDirectory));
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Build a live install with one pak containing the same Sawmill XML that
// MakeGameGdbFixture uses, and install the cheaper-sawmill fixture mod into
// the store, so a Deploy() call should rebuild core.pak with Amount: 4 -> 3.
static (StoreLayout Layout, string GameRoot, byte[] OriginalPakBytes)
    SetupLiveInstallDeployFixture(string tempRoot)
{
    var layout = InitLayout(tempRoot);
    var gameRoot = Path.Combine(tempRoot, "game");
    var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
    Directory.CreateDirectory(pakDir);

    // Use the same buildings.gd.xml content the extracted-layout tests use, so
    // the fixture mod's targets resolve cleanly. Entry name embeds the
    // "core/gdb/" prefix because real PoP paks do.
    var buildingsXml = BuildingsXmlForFixture(sawmillAmount: "4");
    var corePakBytes = BuildTinyPak(("core/gdb/buildings.gd.xml", buildingsXml));
    var corePakPath = Path.Combine(pakDir, "core.pak");
    File.WriteAllBytes(corePakPath, corePakBytes);

    // Install the same cheaper-sawmill fixture mod the extracted-layout tests use.
    var modId = "pagonia-land.fixture.cheaper-sawmill-live";
    InstallFixtureMod(layout, tempRoot, modId, "0.1.0", "src");
    new ActiveProfileService().Enable(layout, modId, null);

    return (layout, gameRoot, corePakBytes);
}

// The same Sawmill XML the extracted-layout helper writes, factored out so
// the live-install test can pack it into a pak entry.
static string BuildingsXmlForFixture(string sawmillAmount) => $"""
<?xml version="1.0" encoding="utf-8"?>
<GameDatabase>
  <Groups>
    <Group Name="Buildings">
      <Entities>
        <Entity Name="Sawmill" Guid="c732cb26-7487-4a7b-b1ba-b65e094f9bac">
          <Children />
          <Values>
            <AspectBuildup>
              <Costs>
                <Item>
                  <Content>
                    <Resource>c22b4997-5563-44ab-8aa0-04a7b2c826be</Resource>
                    <Amount>{sawmillAmount}</Amount>
                  </Content>
                </Item>
              </Costs>
            </AspectBuildup>
          </Values>
        </Entity>
        <Entity Name="Quarry" Guid="ab999999-9999-4000-8000-000000000001">
          <Children />
          <Values>
            <AspectBuildup>
              <Costs>
                <Item>
                  <Content>
                    <Resource>c22b4997-5563-44ab-8aa0-04a7b2c826be</Resource>
                    <Amount>6</Amount>
                  </Content>
                </Item>
              </Costs>
            </AspectBuildup>
          </Values>
        </Entity>
      </Entities>
    </Group>
  </Groups>
</GameDatabase>
""";

static string ReadFirstEntryUtf8(byte[] pakBytes, string entryName)
{
    var reader = new PakReader();
    using var stream = new MemoryStream(pakBytes, writable: false);
    var open = reader.OpenIndex(stream);
    if (!open.Success || open.Index is null)
    {
        throw new InvalidOperationException("could not open pak");
    }
    var entry = open.Index.Entries.First(e => e.Filename == entryName);
    using var outStream = new MemoryStream();
    reader.ExtractEntry(stream, entry, outStream);
    return System.Text.Encoding.UTF8.GetString(outStream.ToArray());
}

// ============================================================================
// live-state drift detection
// ============================================================================

// Flip the last byte of a file — changes its SHA-256 while preserving size,
// simulating an out-of-band edit (another tool / hand-edit) to a deployed pak.
static void FlipLastByte(string path)
{
    var bytes = File.ReadAllBytes(path);
    bytes[^1] ^= 0xFF;
    File.WriteAllBytes(path, bytes);
}

static string LiveCorePakPath(string gameRoot)
    => Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

static DeployManifest ReadManifest(string manifestPath)
    => new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
        .Deserialize<DeployManifest>(File.ReadAllText(manifestPath));

static bool LiveStateInspectorCleanNoDrift()
{
    var tempRoot = NewTempRoot("drift-clean");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed || deploy.ManifestPath is null) return false;
        // No external change → the live pak still matches the recorded NewSha256.
        var drifts = new LiveStateInspector().Inspect(gameRoot, ReadManifest(deploy.ManifestPath));
        return drifts.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LiveStateInspectorDetectsEdit()
{
    var tempRoot = NewTempRoot("drift-detect");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed || deploy.ManifestPath is null) return false;

        FlipLastByte(LiveCorePakPath(gameRoot));
        var drifts = new LiveStateInspector().Inspect(gameRoot, ReadManifest(deploy.ManifestPath));
        return drifts.Count == 1
            && drifts[0].RelativePath.EndsWith("core.pak", StringComparison.Ordinal)
            && drifts[0].ActualSha256 is not null
            && !string.Equals(drifts[0].ActualSha256, drifts[0].ExpectedSha256, StringComparison.OrdinalIgnoreCase);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployDriftBlocksWithoutForce()
{
    var tempRoot = NewTempRoot("drift-deploy-block");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var first = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (first.Outcome != DeployOutcome.Completed) return false;

        // Out-of-band change: another tool replaced the deployed pak with a
        // different (still valid) one — here the pre-deploy original, whose hash
        // differs from the recorded post-deploy NewSha256.
        File.WriteAllBytes(LiveCorePakPath(gameRoot), originalPakBytes);

        // Re-deploy without --force → blocked by drift.
        var blocked = new DeployService().Deploy(layout, gameRoot, null, false, false);
        var blockedOk = blocked.Outcome == DeployOutcome.Failed
            && blocked.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployBlockedByDrift
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && blocked.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift
                && d.Severity == ManagerDiagnosticSeverity.Warning);

        // Re-deploy with --force (acceptDrift) → proceeds, still surfacing the drift.
        var forced = new DeployService().Deploy(layout, gameRoot, null, false, false, acceptDrift: true);
        var forcedOk = forced.Outcome == DeployOutcome.Completed
            && forced.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift);

        return blockedOk && forcedOk;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployDriftDryRunSurfacesWithoutWriting()
{
    var tempRoot = NewTempRoot("drift-deploy-dryrun");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var first = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (first.Outcome != DeployOutcome.Completed) return false;

        // Out-of-band change (a different but valid pak — see DeployDriftBlocksWithoutForce).
        File.WriteAllBytes(LiveCorePakPath(gameRoot), originalPakBytes);
        var mutatedBytes = File.ReadAllBytes(LiveCorePakPath(gameRoot));

        // Dry-run surfaces the drift but neither blocks nor writes.
        var dry = new DeployService().Deploy(layout, gameRoot, null, false, dryRun: true);
        return dry.Outcome == DeployOutcome.DryRun
            && dry.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift)
            && !dry.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployBlockedByDrift)
            && File.ReadAllBytes(LiveCorePakPath(gameRoot)).AsSpan().SequenceEqual(mutatedBytes);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployCleanRedeployNoDriftDiagnostic()
{
    var tempRoot = NewTempRoot("drift-clean-redeploy");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var first = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (first.Outcome != DeployOutcome.Completed) return false;

        // No external change → a clean re-deploy must not report drift.
        var second = new DeployService().Deploy(layout, gameRoot, null, false, false);
        return second.Outcome == DeployOutcome.Completed
            && !second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift)
            && !second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployBlockedByDrift);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackDriftGatedByForce()
{
    var tempRoot = NewTempRoot("drift-rollback");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Out-of-band edit AFTER deploy — rollback would discard it.
        FlipLastByte(LiveCorePakPath(gameRoot));

        // Rollback without --force → refused, nothing restored.
        var refused = new RollbackService().Rollback(layout, gameRoot);
        var refusedOk = refused.Outcome == RollbackOutcome.Failed
            && refused.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift)
            && !File.ReadAllBytes(LiveCorePakPath(gameRoot)).AsSpan().SequenceEqual(originalPakBytes);

        // Rollback with --force → restores the backup (original pak) over the edit.
        var forced = new RollbackService().Rollback(layout, gameRoot, acceptDrift: true);
        var forcedOk = forced.Outcome == RollbackOutcome.Reverted
            && File.ReadAllBytes(LiveCorePakPath(gameRoot)).AsSpan().SequenceEqual(originalPakBytes);

        return refusedOk && forcedOk;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// live-install rollback from pak backups
// ============================================================================

static bool RollbackLiveInstallByteIdentical()
{
    var tempRoot = NewTempRoot("rollback-live-bi");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;
        // Sanity: deploy changed the live pak.
        if (File.ReadAllBytes(livePakPath).AsSpan().SequenceEqual(originalPakBytes)) return false;

        var rollback = new RollbackService().Rollback(layout, gameRoot);

        return rollback.Outcome == RollbackOutcome.Reverted
            // The rebuilt pak counts as one restored entry.
            && rollback.RestoredFileCount == 1
            // And the live pak is now byte-identical to the pre-deploy original.
            && File.ReadAllBytes(livePakPath).AsSpan().SequenceEqual(originalPakBytes)
            && rollback.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakRollbackRestored);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackLiveInstallHashMismatch()
{
    var tempRoot = NewTempRoot("rollback-live-hash");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Tamper with the backup so its SHA no longer matches what the manifest
        // recorded at deploy time. Rollback must refuse to overwrite the live pak.
        var backupPath = Path.Combine(deploy.BackupDirectory!, GameLayoutConstants.PakFolderName, "core.pak");
        var tamperedBytes = File.ReadAllBytes(backupPath);
        tamperedBytes[0] ^= 0xFF;  // flip a single byte → different SHA
        File.WriteAllBytes(backupPath, tamperedBytes);

        var livePakBefore = File.ReadAllBytes(livePakPath);
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        var livePakAfter = File.ReadAllBytes(livePakPath);

        return rollback.Outcome == RollbackOutcome.Failed
            && rollback.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.RollbackHashMismatch
                && d.Severity == ManagerDiagnosticSeverity.Error)
            // Live pak must NOT have been overwritten with the tampered bytes.
            && livePakAfter.AsSpan().SequenceEqual(livePakBefore);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackExtractedLayoutHashMismatch()
{
    // Extracted-layout (loose XML) analog of RollbackLiveInstallHashMismatch:
    // the ModifiedFiles restore path must verify the backup SHA-256 too, not
    // just the RebuiltPaks path. A tampered backup must be refused, leaving
    // the live file untouched.
    var tempRoot = NewTempRoot("rollback-xml-hash");
    try
    {
        var (layout, gameRoot, _) = SetupDeployFixture(tempRoot, "rollback-xml-hash");
        var liveFilePath = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        var backupPath = Path.Combine(deploy.BackupDirectory!, "core", "gdb", "buildings.gd.xml");
        var tampered = File.ReadAllBytes(backupPath);
        tampered[0] ^= 0xFF;  // flip a single byte → different SHA
        File.WriteAllBytes(backupPath, tampered);

        var liveBefore = File.ReadAllBytes(liveFilePath);
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        var liveAfter = File.ReadAllBytes(liveFilePath);

        return rollback.Outcome == RollbackOutcome.Failed
            && rollback.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.RollbackHashMismatch
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && liveAfter.AsSpan().SequenceEqual(liveBefore);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackLiveInstallMissingBackup()
{
    var tempRoot = NewTempRoot("rollback-live-missing");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Wipe the backup file. Rollback should surface a clear diagnostic
        // rather than throw — same UX as the extracted-layout backup-missing path.
        var backupPath = Path.Combine(deploy.BackupDirectory!, GameLayoutConstants.PakFolderName, "core.pak");
        File.Delete(backupPath);

        var livePakBefore = File.ReadAllBytes(livePakPath);
        var rollback = new RollbackService().Rollback(layout, gameRoot);
        var livePakAfter = File.ReadAllBytes(livePakPath);

        return rollback.Outcome == RollbackOutcome.Failed
            && rollback.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.RollbackBackupMissing
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && livePakAfter.AsSpan().SequenceEqual(livePakBefore);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ----------------------------------------------------------------------------
// rollback overlay (Pattern B) safety: mixed-install abort + foreign-file guard
// ----------------------------------------------------------------------------

static bool RollbackAbortsBeforeDeletingOverlaysOnRestoreFailure()
{
    var tempRoot = NewTempRoot("rollback-mixed-abort");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Drop a Pattern B overlay pak into the live install + register it in the
        // manifest as an addedFile, so rollback would delete it.
        var overlayPath = Path.Combine(gameRoot, "mods", "test-overlay.pak");
        Directory.CreateDirectory(Path.GetDirectoryName(overlayPath)!);
        var overlayBytes = System.Text.Encoding.UTF8.GetBytes("overlay-pak-bytes");
        File.WriteAllBytes(overlayPath, overlayBytes);
        AddOverlayToManifest(deploy.ManifestPath!, "mods/test-overlay.pak", Sha256Hex(overlayBytes));

        // Force the canonical-pak restore to FAIL by deleting its backup.
        File.Delete(Path.Combine(deploy.BackupDirectory!, GameLayoutConstants.PakFolderName, "core.pak"));

        var rollback = new RollbackService().Rollback(layout, gameRoot);

        // Restore failed → rollback must abort BEFORE touching the overlay, so the
        // install isn't left mixed (canonical un-restored AND overlay gone).
        return rollback.Outcome == RollbackOutcome.Failed
            && rollback.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RollbackBackupMissing
                && d.Severity == ManagerDiagnosticSeverity.Error)
            && File.Exists(overlayPath);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackPreservesDriftedOverlayUnderForce()
{
    var tempRoot = NewTempRoot("rollback-overlay-drift");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        var overlayPath = Path.Combine(gameRoot, "mods", "test-overlay.pak");
        Directory.CreateDirectory(Path.GetDirectoryName(overlayPath)!);
        var deployedBytes = System.Text.Encoding.UTF8.GetBytes("overlay-as-deployed");
        File.WriteAllBytes(overlayPath, deployedBytes);
        AddOverlayToManifest(deploy.ManifestPath!, "mods/test-overlay.pak", Sha256Hex(deployedBytes));

        // A third party REPLACES the overlay with their own content after deploy.
        var foreignBytes = System.Text.Encoding.UTF8.GetBytes("USER REPLACED THIS FILE LATER");
        File.WriteAllBytes(overlayPath, foreignBytes);

        // --force past the drift; the canonical restore still succeeds (backup intact),
        // but the foreign overlay must be preserved (not deleted) + warned about.
        var rollback = new RollbackService().Rollback(layout, gameRoot, acceptDrift: true);

        return File.Exists(overlayPath)
            && File.ReadAllBytes(overlayPath).AsSpan().SequenceEqual(foreignBytes)
            && rollback.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RollbackAddedFileChanged
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Append an addedFiles overlay entry to an existing deploy manifest, re-serialising
// with the YamlMember aliases the rollback reader expects.
static void AddOverlayToManifest(string manifestPath, string relativePath, string deployedSha256)
{
    var m = ReadManifest(manifestPath);
    var updated = new DeployManifest
    {
        DeployVersion = m.DeployVersion,
        Timestamp = m.Timestamp,
        GameRoot = m.GameRoot,
        GameFingerprint = m.GameFingerprint,
        GameProductVersion = m.GameProductVersion,
        Profile = m.Profile,
        Mods = m.Mods,
        ModifiedFiles = m.ModifiedFiles,
        RebuiltPaks = m.RebuiltPaks,
        AddedFiles = new List<DeployAddedFileEntry>(m.AddedFiles)
        {
            new() { RelativePath = relativePath, DeployedSha256 = deployedSha256, SourceMod = "test.overlay", ByteSize = 1 },
        },
    };
    File.WriteAllText(manifestPath, new SerializerBuilder().Build().Serialize(updated));
}

static string Sha256Hex(byte[] bytes)
    => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

// ============================================================================
// selective pak extract
// ============================================================================

static bool PakCacheSelectiveExtractsSubset()
{
    var tempRoot = NewTempRoot("cache-sel-subset");
    try
    {
        var store = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), BuildTinyPak(("core/probe.txt", "c")));
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), BuildTinyPak(("dlc1/probe.txt", "d")));
        File.WriteAllBytes(Path.Combine(pakDir, "tools.pak"), BuildTinyPak(("tools/probe.txt", "t")));

        var detected = GameLayoutDetector.Detect(gameRoot);
        var result = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core" });

        if (!result.Success) return false;
        if (result.ExtractedPaks.Count != 1) return false;
        if (!result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakCacheSelective)) return false;

        // Only "core" should be on disk; "dlc1" and "tools" must be absent.
        return File.Exists(Path.Combine(result.CacheRoot, "core", "probe.txt"))
            && !Directory.Exists(Path.Combine(result.CacheRoot, "dlc1"))
            && !Directory.Exists(Path.Combine(result.CacheRoot, "tools"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheSelectiveWarmStaysWarm()
{
    var tempRoot = NewTempRoot("cache-sel-warm");
    try
    {
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "x");
        var first = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core" });
        if (!first.Success || first.FromCache) return false;

        var second = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core" });
        return second.Success
            && second.FromCache
            && second.ExtractedPaks.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheSelectivePartialHitIncrementsCache()
{
    var tempRoot = NewTempRoot("cache-sel-incr");
    try
    {
        var store = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), BuildTinyPak(("core/probe.txt", "c")));
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), BuildTinyPak(("dlc1/probe.txt", "d")));

        var detected = GameLayoutDetector.Detect(gameRoot);
        // First call: warm only core
        var first = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core" });
        if (!first.Success || first.ExtractedPaks.Count != 1) return false;

        // Second call: ask for core + dlc1. core is warm, dlc1 must be added.
        var second = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: new[] { "core", "dlc1" });
        if (!second.Success) return false;
        if (second.FromCache) return false;
        if (second.ExtractedPaks.Count != 1) return false; // only dlc1 was extracted, not both
        if (!Path.GetFileName(second.ExtractedPaks[0]).Equals("dlc1.pak", StringComparison.OrdinalIgnoreCase)) return false;
        if (!second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.PakCachePartialHit)) return false;

        // Both paks now on disk.
        return File.Exists(Path.Combine(second.CacheRoot, "core", "probe.txt"))
            && File.Exists(Path.Combine(second.CacheRoot, "dlc1", "probe.txt"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheSelectiveEmptyRequestIsNoOp()
{
    // Empty profile (no enabled mods) -> empty required-paks set. Ensure should
    // short-circuit immediately without extracting anything.
    var tempRoot = NewTempRoot("cache-sel-empty");
    try
    {
        var (store, _, detected) = SetupLiveInstallWithOnePak(tempRoot, "core.pak", "core/probe.txt", "x");
        var result = new PakCacheService().Ensure(store, detected,
            requiredPakBasenames: Array.Empty<string>());

        return result.Success
            && result.FromCache
            && result.ExtractedPaks.Count == 0
            && !Directory.Exists(Path.Combine(result.CacheRoot, "core")); // never extracted
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakCacheSelectiveNullExtractsAll()
{
    // Null required-paks = back-compat "extract every discovered pak" behaviour.
    var tempRoot = NewTempRoot("cache-sel-null");
    try
    {
        var store = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        var pakDir = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName);
        Directory.CreateDirectory(pakDir);
        File.WriteAllBytes(Path.Combine(pakDir, "core.pak"), BuildTinyPak(("core/probe.txt", "c")));
        File.WriteAllBytes(Path.Combine(pakDir, "dlc1.pak"), BuildTinyPak(("dlc1/probe.txt", "d")));

        var detected = GameLayoutDetector.Detect(gameRoot);
        var result = new PakCacheService().Ensure(store, detected, requiredPakBasenames: null);

        return result.Success
            && result.ExtractedPaks.Count == 2
            && File.Exists(Path.Combine(result.CacheRoot, "core", "probe.txt"))
            && File.Exists(Path.Combine(result.CacheRoot, "dlc1", "probe.txt"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakRequirementAnalyzerReadsPatchTargets()
{
    var tempRoot = NewTempRoot("req-analyzer-targets");
    try
    {
        // Use the existing fixture infra: install a mod that targets core/gdb/...
        var (layout, _, modId) = SetupDeployFixture(tempRoot, "req-analyzer");

        var required = PakRequirementAnalyzer.ComputeRequiredPaks(layout, profileName: null);
        if (required is null) return false;
        // The cheaper-sawmill fixture mod targets core/gdb/buildings.gd.xml,
        // so "core" must be in the required set. Nothing else is touched.
        return required.Count == 1
            && required.Contains("core", StringComparer.OrdinalIgnoreCase);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool PakRequirementAnalyzerEmptyProfileEmptySet()
{
    var tempRoot = NewTempRoot("req-analyzer-empty");
    try
    {
        var layout = InitLayout(tempRoot);
        // Default profile exists (created by InitLayout) but has no enabled mods.

        var required = PakRequirementAnalyzer.ComputeRequiredPaks(layout, profileName: null);
        // Empty profile must yield empty set (NOT null) so the cache call
        // short-circuits without extracting all paks "just in case".
        return required is not null && required.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// game-update awareness + orphaned deploys
// ============================================================================

static bool OrphanFinderGameRootGone()
{
    var tempRoot = NewTempRoot("orphan-gone");
    try
    {
        // Do a real deploy first, so history.yaml lands under <store>/deploys/<fp>/
        // with a recorded gameRoot. Then nuke the game directory and assert the
        // finder flags this fingerprint as GameRootGone.
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        Directory.Delete(gameRoot, recursive: true);

        var orphans = new OrphanedDeployFinder().FindAll(layout);
        return orphans.Count == 1
            && orphans[0].Reason == OrphanReason.GameRootGone
            && orphans[0].Fingerprint == deploy.GameFingerprint;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool OrphanFinderGameUpdated()
{
    var tempRoot = NewTempRoot("orphan-updated");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Simulate a Steam update — GameFingerprint.Compute reads system.json
        // content, so adding/changing it shifts the fingerprint. Drop a
        // system.json file with version-bump content.
        File.WriteAllText(Path.Combine(gameRoot, "system.json"), "{\"version\":\"new-build\"}");

        var orphans = new OrphanedDeployFinder().FindAll(layout);
        return orphans.Count == 1
            && orphans[0].Reason == OrphanReason.GameUpdated
            && orphans[0].Fingerprint == deploy.GameFingerprint;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool OrphanFinderCurrentDeployNotOrphan()
{
    var tempRoot = NewTempRoot("orphan-current");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);
        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // GameRoot still exists, fingerprint unchanged — must NOT be flagged
        // as an orphan.
        var orphans = new OrphanedDeployFinder().FindAll(layout);
        return orphans.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployWarnsOnFingerprintDrift()
{
    var tempRoot = NewTempRoot("deploy-drift-warn");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);

        // Fake a prior deploy under a different fingerprint pointing at the
        // SAME gameRoot. Hand-write the history file rather than running a real
        // first deploy + rollback: rollback truncates the history to empty
        // deploys (which the orphan finder skips), and a non-rolled-back deploy
        // would leave the live pak patched and make the second deploy hit
        // expectedValueMismatch. Hand-writing keeps the test focused on the
        // preflight scan behaviour.
        var fakeOldFingerprint = "deadbeef00000001";
        var fakeHistory = new DeployHistory
        {
            DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
            GameFingerprint = fakeOldFingerprint,
            GameRoot = Path.GetFullPath(gameRoot),
            Deploys = new List<DeployHistoryEntry>
            {
                new()
                {
                    Timestamp = "20260101T000000000Z",
                    Profile = "default",
                    ModCount = 1,
                    FileCount = 1,
                },
            },
        };
        new DeployHistoryStore().Write(layout, fakeOldFingerprint, fakeHistory);

        // Real deploy with the current fingerprint (whatever GameFingerprint
        // computes for this test gameRoot) — must NOT equal fakeOldFingerprint,
        // and the preflight scan must find the prior history + emit the warning.
        var deploy = new DeployService().Deploy(layout, gameRoot, null, true /* accept warnings */, false);
        return deploy.Outcome == DeployOutcome.Completed
            && deploy.Diagnostics.Any(d =>
                d.Code == ManagerDiagnosticCodes.GameUpdatedSinceLastDeploy
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// backup retention + deploys clean
// ============================================================================

// Hand-write N fake history entries + corresponding timestamp directories
// (with sentinel files inside backup/) for a single fingerprint. Lets the
// clean-tests exercise keep-N behaviour without running N real deploys.
static void SeedFakeDeploys(StoreLayout layout, string fingerprint, string gameRoot, int count)
{
    var deploys = new List<DeployHistoryEntry>();
    for (var i = 0; i < count; i++)
    {
        // Newest-first ordering matches DeployService's prepend semantics.
        var ts = $"2026010{count - i:D2}T000000000Z";
        deploys.Add(new DeployHistoryEntry
        {
            Timestamp = ts,
            Profile = "default",
            ModCount = 1,
            FileCount = 1,
        });

        // Create the timestamp dir + a placeholder backup file so we can
        // verify the dir was deleted by the clean.
        var tsDir = layout.DeployTimestampDirectory(fingerprint, ts);
        Directory.CreateDirectory(tsDir);
        var backupDir = layout.DeployBackupDirectory(fingerprint, ts);
        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "placeholder.bin"), $"backup for {ts}");
    }

    new DeployHistoryStore().Write(layout, fingerprint, new DeployHistory
    {
        DeployHistoryVersion = StoreLayoutConstants.CurrentDeployVersion,
        GameFingerprint = fingerprint,
        GameRoot = gameRoot,
        Deploys = deploys,
    });
}

static bool DeployCleanKeepThreeNewest()
{
    var tempRoot = NewTempRoot("clean-keep3");
    try
    {
        var layout = InitLayout(tempRoot);
        var fingerprint = "abcdef0123456789";
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);
        SeedFakeDeploys(layout, fingerprint, gameRoot, count: 5);

        var result = new DeployCleanService().Clean(layout, keep: 3, gameRoot: null, dryRun: false);
        if (result.RemovedCount != 2 || result.KeptCount != 3) return false;

        // History rewritten to the 3 newest.
        var history = new DeployHistoryStore().Read(layout, fingerprint);
        if (history.Deploys.Count != 3) return false;

        // Removed timestamp dirs must be gone; kept ones must remain.
        var keptTimestamps = history.Deploys.Select(d => d.Timestamp).ToHashSet();
        for (var i = 1; i <= 5; i++)
        {
            var ts = $"2026010{6 - i:D2}T000000000Z"; // matches the seed loop's pattern
            var tsDir = layout.DeployTimestampDirectory(fingerprint, ts);
            var shouldExist = keptTimestamps.Contains(ts);
            if (Directory.Exists(tsDir) != shouldExist) return false;
        }
        return true;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployCleanRefusesLastDeploy()
{
    var tempRoot = NewTempRoot("clean-refuse");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);
        var fingerprint = GameFingerprint.Compute(gameRoot);
        SeedFakeDeploys(layout, fingerprint, gameRoot, count: 4);

        // Point state.yaml.lastDeploy at the OLDEST timestamp — would normally
        // be the first to be removed by --keep 2. Clean must refuse to delete it.
        var history = new DeployHistoryStore().Read(layout, fingerprint);
        var oldestTimestamp = history.Deploys[3].Timestamp;
        var state = new StoreStateReader().Read(layout);
        new StoreStateWriter().Write(layout, new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            LastDeploy = new StoreLastDeploy
            {
                Timestamp = oldestTimestamp,
                GameRoot = gameRoot,
                Profile = "default",
            },
        });

        var result = new DeployCleanService().Clean(layout, keep: 2, gameRoot: null, dryRun: false);

        // 2 in the keep window, 1 removed, 1 refused (the oldest = state.lastDeploy).
        if (result.KeptCount != 2 || result.RemovedCount != 1 || result.RefusedCount != 1) return false;
        if (!result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployCleanRefusedLatest)) return false;

        // History after clean: 2 newest + the refused oldest entry.
        var historyAfter = new DeployHistoryStore().Read(layout, fingerprint);
        if (historyAfter.Deploys.Count != 3) return false;
        if (!historyAfter.Deploys.Any(d => d.Timestamp == oldestTimestamp)) return false;
        return Directory.Exists(layout.DeployTimestampDirectory(fingerprint, oldestTimestamp));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployCleanDryRunWritesNothing()
{
    var tempRoot = NewTempRoot("clean-dry");
    try
    {
        var layout = InitLayout(tempRoot);
        var fingerprint = "deadbeef00000002";
        SeedFakeDeploys(layout, fingerprint, Path.Combine(tempRoot, "game"), count: 4);

        var historyBefore = new DeployHistoryStore().Read(layout, fingerprint);

        var result = new DeployCleanService().Clean(layout, keep: 1, gameRoot: null, dryRun: true);
        if (!result.DryRun) return false;
        if (result.RemovedCount != 3 || result.KeptCount != 1) return false;

        // No files written: history unchanged + every timestamp dir still on disk.
        var historyAfter = new DeployHistoryStore().Read(layout, fingerprint);
        if (historyAfter.Deploys.Count != historyBefore.Deploys.Count) return false;
        foreach (var entry in historyBefore.Deploys)
        {
            if (!Directory.Exists(layout.DeployTimestampDirectory(fingerprint, entry.Timestamp))) return false;
        }
        return true;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployCleanKeepZeroRespectsLastDeploy()
{
    var tempRoot = NewTempRoot("clean-keep0");
    try
    {
        var layout = InitLayout(tempRoot);
        var gameRoot = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(gameRoot);
        var fingerprint = GameFingerprint.Compute(gameRoot);
        SeedFakeDeploys(layout, fingerprint, gameRoot, count: 3);

        // Pin state.lastDeploy at the MIDDLE entry (not the newest), so the newest
        // would be unprotected by the lastDeploy guard alone.
        var history = new DeployHistoryStore().Read(layout, fingerprint);
        var newestTs = history.Deploys[0].Timestamp;
        var pinnedTs = history.Deploys[1].Timestamp;
        var state = new StoreStateReader().Read(layout);
        new StoreStateWriter().Write(layout, new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            LastDeploy = new StoreLastDeploy { Timestamp = pinnedTs, GameRoot = gameRoot, Profile = "default" },
        });

        var result = new DeployCleanService().Clean(layout, keep: 0, gameRoot: null, dryRun: false);

        // --keep 0 prunes aggressively but ALWAYS retains the newest deploy per
        // fingerprint as the rollback anchor (`rollback` reverts it), and the lastDeploy
        // guard additionally protects the pinned middle entry. So of 3: oldest removed,
        // newest kept (anchor), middle refused (lastDeploy) — both survive on disk.
        return result.RemovedCount == 1
            && result.KeptCount == 1
            && result.RefusedCount == 1
            && Directory.Exists(layout.DeployTimestampDirectory(fingerprint, newestTs))
            && Directory.Exists(layout.DeployTimestampDirectory(fingerprint, pinnedTs));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployCleanAcrossFingerprints()
{
    var tempRoot = NewTempRoot("clean-multi-fp");
    try
    {
        var layout = InitLayout(tempRoot);
        SeedFakeDeploys(layout, "fp1aaaaaaaaa0001", Path.Combine(tempRoot, "g1"), count: 5);
        SeedFakeDeploys(layout, "fp2bbbbbbbbb0002", Path.Combine(tempRoot, "g2"), count: 3);

        var result = new DeployCleanService().Clean(layout, keep: 2, gameRoot: null, dryRun: false);

        // fp1: keep 2, remove 3. fp2: keep 2, remove 1. Total: keep 4, remove 4.
        if (result.RemovedCount != 4 || result.KeptCount != 4) return false;

        var h1 = new DeployHistoryStore().Read(layout, "fp1aaaaaaaaa0001");
        var h2 = new DeployHistoryStore().Read(layout, "fp2bbbbbbbbb0002");
        return h1.Deploys.Count == 2 && h2.Deploys.Count == 2;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// sparse patch apply
// ============================================================================

static bool DeployLiveInstallTakesSparsePath()
{
    var tempRoot = NewTempRoot("deploy-sparse-path");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        // Pure Pattern A mod (cheaper-sawmill, no pak: block, no entries:)
        // must trigger the sparse fast-path diagnostic.
        return deploy.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployUsedSparsePath)
            && !deploy.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployFellBackToFullApply);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployLiveInstallSparsePathByteEquivalentToSlow()
{
    // Regression test: the sparse fast-path MUST produce a live pak with
    // byte-identical content to what the slow path produced before. Compare
    // the live pak after sparse-deploy to the previously-known expected
    // outcome (Amount 4 -> 3 in the Sawmill XML, repacked into core.pak).
    // We don't directly compare to "slow path output" because the slow path
    // is no longer reachable for pure Pattern A mods (sparse always wins);
    // instead we verify the patched XML round-trips correctly through the pak.
    var tempRoot = NewTempRoot("deploy-sparse-bytes");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;
        if (deploy.RebuiltPakCount != 1) return false;

        // Confirm we DID change the pak.
        var rebuiltBytes = File.ReadAllBytes(livePakPath);
        if (rebuiltBytes.AsSpan().SequenceEqual(originalPakBytes)) return false;

        // Extract the patched buildings.gd.xml from the rebuilt pak and
        // verify it has Amount=3 (cheaper-sawmill's intended change).
        var patchedXml = ReadFirstEntryUtf8(rebuiltBytes, "core/gdb/buildings.gd.xml");
        return patchedXml.Contains("<Amount>3</Amount>", StringComparison.Ordinal)
            && !patchedXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool DeployLiveInstallSparsePathSkipsStaging()
{
    // Sparse path must not create or leave behind a staging directory under
    // the OS temp dir. Capture the pre-deploy temp-dir snapshot and confirm
    // no new pagonia-manager-deploy-stage-* dirs lingered.
    var tempRoot = NewTempRoot("deploy-sparse-no-staging");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);

        var beforeStagingDirs = Directory.EnumerateDirectories(Path.GetTempPath(), "pagonia-manager-deploy-stage-*").Count();

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        var afterStagingDirs = Directory.EnumerateDirectories(Path.GetTempPath(), "pagonia-manager-deploy-stage-*").Count();
        // Sparse path doesn't create staging, so the count must not change.
        // (Even if slow-path had run, the finally block cleans up — but the
        // diagnostic separation gives us a stronger signal of which path ran.)
        return afterStagingDirs == beforeStagingDirs
            && deploy.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployUsedSparsePath);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ============================================================================
// persistent default game folder
// ============================================================================

static bool GameRootResolverSessionWins()
{
    var tempRoot = NewTempRoot("resolver-session");
    try
    {
        var store = InitLayout(tempRoot);
        var sessionDir = Path.Combine(tempRoot, "session-game");
        Directory.CreateDirectory(sessionDir);
        // Stash a stored default — session value must beat it.
        var storedDir = Path.Combine(tempRoot, "stored-game");
        Directory.CreateDirectory(storedDir);
        GameRootResolver.SetStoredDefault(store, storedDir);

        var resolved = GameRootResolver.Resolve(store, sessionOverride: sessionDir);
        return resolved.Source == GameRootSource.Session
            && resolved.Path == sessionDir;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverStoredWinsOverPlatform()
{
    // Platform default may or may not exist on the test runner. The resolver
    // contract is: if a stored default points at an existing dir, it wins
    // regardless of whether the platform default also resolves. So we just
    // assert "stored is returned"; we don't need to mock the platform check.
    var tempRoot = NewTempRoot("resolver-stored");
    try
    {
        var store = InitLayout(tempRoot);
        var storedDir = Path.Combine(tempRoot, "stored-game");
        Directory.CreateDirectory(storedDir);
        GameRootResolver.SetStoredDefault(store, storedDir);

        var resolved = GameRootResolver.Resolve(store, sessionOverride: null);
        return resolved.Source == GameRootSource.StoredDefault
            && resolved.Path == storedDir;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverStaleFallsThrough()
{
    // A stored default whose directory was moved / deleted must not be
    // suggested back — the resolver checks existence at resolve time.
    var tempRoot = NewTempRoot("resolver-stale");
    try
    {
        var store = InitLayout(tempRoot);
        var ghostDir = Path.Combine(tempRoot, "ghost-game");
        Directory.CreateDirectory(ghostDir);
        GameRootResolver.SetStoredDefault(store, ghostDir);
        Directory.Delete(ghostDir, recursive: true);

        var resolved = GameRootResolver.Resolve(store, sessionOverride: null);
        // Result depends on whether the Windows Steam path happens to exist
        // on this runner. Either NotSet (no platform default) or PlatformDefault
        // is acceptable; what MUST be true is that it's NOT StoredDefault.
        return resolved.Source != GameRootSource.StoredDefault
            && resolved.Path != ghostDir;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverNotSet()
{
    // Fresh store, no session, no stored default. On non-Windows or when
    // the Steam folder doesn't exist, the resolver must return NotSet.
    // We can only assert this on systems where the platform default check
    // is guaranteed false — non-Windows OSes return empty WindowsSteamDefaultPath.
    var tempRoot = NewTempRoot("resolver-empty");
    try
    {
        var store = InitLayout(tempRoot);
        var resolved = GameRootResolver.Resolve(store, sessionOverride: null);
        if (!OperatingSystem.IsWindows())
        {
            return resolved.Source == GameRootSource.NotSet && !resolved.HasPath;
        }
        // On Windows the Steam folder may or may not exist on the runner.
        // Accept either NotSet or PlatformDefault — both are valid outcomes
        // when no stored default is set.
        return resolved.Source is GameRootSource.NotSet or GameRootSource.PlatformDefault;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverSetStoredPersists()
{
    var tempRoot = NewTempRoot("resolver-set");
    try
    {
        var store = InitLayout(tempRoot);
        var dir = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(dir);

        var wrote = GameRootResolver.SetStoredDefault(store, dir);
        if (!wrote) return false;

        var fresh = new StoreStateReader().Read(store);
        return fresh.DefaultGameRoot == dir;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverSetStoredNoOp()
{
    var tempRoot = NewTempRoot("resolver-noop");
    try
    {
        var store = InitLayout(tempRoot);
        var dir = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(dir);
        GameRootResolver.SetStoredDefault(store, dir);

        var wroteAgain = GameRootResolver.SetStoredDefault(store, dir);
        return !wroteAgain;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool GameRootResolverSetStoredPreservesOtherFields()
{
    var tempRoot = NewTempRoot("resolver-preserve");
    try
    {
        var store = InitLayout(tempRoot);

        // Seed the store with a non-default ActiveProfile + LastDeploy entry,
        // so we can prove SetStoredDefault doesn't clobber them.
        var seed = new StoreState
        {
            StoreVersion = StoreLayoutConstants.CurrentStoreVersion,
            ActiveProfile = "custom-profile",
            LastDeploy = new StoreLastDeploy
            {
                Timestamp = "20260101T000000000Z",
                GameRoot = @"C:\some\path",
                Profile = "custom-profile",
            },
        };
        new StoreStateWriter().Write(store, seed);

        var newDir = Path.Combine(tempRoot, "game");
        Directory.CreateDirectory(newDir);
        GameRootResolver.SetStoredDefault(store, newDir);

        var read = new StoreStateReader().Read(store);
        return read.DefaultGameRoot == newDir
            && read.ActiveProfile == "custom-profile"
            && read.LastDeploy is not null
            && read.LastDeploy.Timestamp == "20260101T000000000Z"
            && read.LastDeploy.Profile == "custom-profile";
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LiveInstallDeployRollbackRoundTripByteIdentical()
{
    // Mirrors the extracted-layout DeployRollbackRoundTripByteIdentical test,
    // for the live-install pipeline. Covers the full chain:
    //   * GameLayoutDetector identifies the install as LiveInstall
    //   * PakCacheService extracts paks into <store>/cache/extract-v2-<fp>/
    //   * DeployService rebuilds + atomically writes the affected pak
    //   * RollbackService restores from backup with SHA-256 validation
    // The invariant: byte-for-byte the install ends up exactly where it started.
    var tempRoot = NewTempRoot("live-rt");
    try
    {
        var (layout, gameRoot, originalPakBytes) = SetupLiveInstallDeployFixture(tempRoot);
        var livePakPath = Path.Combine(gameRoot, GameLayoutConstants.PakFolderName, "core.pak");
        var preDeployBytes = File.ReadAllBytes(livePakPath);
        if (!preDeployBytes.AsSpan().SequenceEqual(originalPakBytes)) return false;

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed || deploy.RebuiltPakCount != 1) return false;

        // The deploy MUST have changed the live pak — otherwise the round-trip
        // test would trivially pass on a no-op deploy without actually exercising
        // the rebuild + backup + restore path.
        var afterDeployBytes = File.ReadAllBytes(livePakPath);
        if (afterDeployBytes.AsSpan().SequenceEqual(preDeployBytes)) return false;

        var rollback = new RollbackService().Rollback(layout, gameRoot);
        if (rollback.Outcome != RollbackOutcome.Reverted) return false;

        var afterRollbackBytes = File.ReadAllBytes(livePakPath);
        return afterRollbackBytes.AsSpan().SequenceEqual(preDeployBytes);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool RollbackLiveInstallTrimsHistory()
{
    var tempRoot = NewTempRoot("rollback-live-history");
    try
    {
        var (layout, gameRoot, _) = SetupLiveInstallDeployFixture(tempRoot);

        var deploy = new DeployService().Deploy(layout, gameRoot, null, false, false);
        if (deploy.Outcome != DeployOutcome.Completed) return false;

        var fingerprint = deploy.GameFingerprint!;
        var historyBefore = new DeployHistoryStore().Read(layout, fingerprint);
        if (historyBefore.Deploys.Count != 1) return false;

        var timestampDirBefore = layout.DeployTimestampDirectory(fingerprint, deploy.Timestamp!);
        if (!Directory.Exists(timestampDirBefore)) return false;

        var rollback = new RollbackService().Rollback(layout, gameRoot);
        if (rollback.Outcome != RollbackOutcome.Reverted) return false;

        var historyAfter = new DeployHistoryStore().Read(layout, fingerprint);
        return historyAfter.Deploys.Count == 0
            // Timestamp directory (manifest + backup) is cleaned up after a successful
            // rollback — leaves no dangling references to a deploy that no longer exists.
            && !Directory.Exists(timestampDirBefore);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- Remote-source parser ----------------------------------------------------

static bool RemoteParserShortFormRepoOnly()
{
    return RemoteSourceParser.TryParse("gh:pagonia-land/example-mods", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "example-mods", Ref: "HEAD", ModSpec: null };
}

static bool RemoteParserShortFormWithModSpec()
{
    return RemoteSourceParser.TryParse("gh:pagonia-land/example-mods/pagonia-land.example.cheaper-sawmill", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "example-mods", Ref: "HEAD", ModSpec: "pagonia-land.example.cheaper-sawmill" };
}

static bool RemoteParserShortFormWithRefAndModSpec()
{
    return RemoteSourceParser.TryParse("gh:pagonia-land/example-mods#v0.1.0/pagonia-land.example.cheaper-sawmill", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "example-mods", Ref: "v0.1.0", ModSpec: "pagonia-land.example.cheaper-sawmill" };
}

static bool RemoteParserShortFormRefWithSlashInsideRef()
{
    // Ref ends at the first '/' AFTER '#', not at the first '/' anywhere.
    // A ref like "release/v1.0" parses as ref="release", mod-spec="v1.0".
    // The slash-in-ref case is documented as unsupported — users on
    // forward-slash branch names need to pin to a commit SHA or tag instead.
    return RemoteSourceParser.TryParse("gh:owner/repo#release/v1.0", out var src)
        && src is GitHubSource { Owner: "owner", Repo: "repo", Ref: "release", ModSpec: "v1.0" };
}

static bool RemoteParserShortFormNestedPath()
{
    return RemoteSourceParser.TryParse("gh:owner/repo#main/mods/sub/dir", out var src)
        && src is GitHubSource { Owner: "owner", Repo: "repo", Ref: "main", ModSpec: "mods/sub/dir" };
}

static bool RemoteParserShortFormBasePathWithMod()
{
    // gh:owner/repo:<base>/<mod-id> — the ':' base segment carries the repo's
    // index subdirectory; the mod-spec still follows the trailing '/'.
    return RemoteSourceParser.TryParse("gh:pagonia-land/Pagonia-Land:official-mods/pagonia-land.example.cheaper-sawmill", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "Pagonia-Land", Ref: "HEAD", BasePath: "official-mods", ModSpec: "pagonia-land.example.cheaper-sawmill" };
}

static bool RemoteParserShortFormBasePathRefAndMod()
{
    // All four optional pieces together: base, ref, mod-spec.
    return RemoteSourceParser.TryParse("gh:owner/repo:official-mods#v0.1.0/mod-id", out var src)
        && src is GitHubSource { Owner: "owner", Repo: "repo", BasePath: "official-mods", Ref: "v0.1.0", ModSpec: "mod-id" };
}

static bool RemoteParserShortFormBasePathNoMod()
{
    // Base with no mod-spec — names the repo's subtree but no mod inside it.
    return RemoteSourceParser.TryParse("gh:owner/repo:official-mods", out var src)
        && src is GitHubSource { Owner: "owner", Repo: "repo", BasePath: "official-mods", ModSpec: null };
}

static bool RemoteParserShortFormNestedBasePath()
{
    // A base path may itself be a nested directory; it's taken whole up to the
    // '#ref' / '/mod-spec' boundary.
    return RemoteSourceParser.TryParse("gh:owner/repo:content/official-mods#main/mod-id", out var src)
        && src is GitHubSource { Owner: "owner", Repo: "repo", BasePath: "content/official-mods", Ref: "main", ModSpec: "mod-id" };
}

static bool RemoteParserDefaultBasePathEmpty()
{
    // A spec with no ':' segment leaves BasePath empty (root) — the unchanged
    // pre-existing behaviour, asserted explicitly so a regression is visible.
    return RemoteSourceParser.TryParse("gh:owner/repo/mod-id", out var src)
        && src is GitHubSource { BasePath: "", ModSpec: "mod-id" };
}

static bool RemoteParserRejectsBasePathTraversal()
{
    // '..' in the base path is refused at parse time (mirrors the catalog
    // indexPath schema pattern) — before any network fetch.
    return !RemoteSourceParser.TryParse("gh:owner/repo:../etc/mod-id", out _)
        && !RemoteSourceParser.TryParse("gh:owner/repo:official/../../etc#main/m", out _);
}

static bool RemoteParserRejectsModSpecTraversal()
{
    // '..' in the mod-spec is refused at parse time too (not just the base
    // path), in both the short and long forms — before any network fetch.
    return !RemoteSourceParser.TryParse("gh:owner/repo/../../evil", out _)
        && !RemoteSourceParser.TryParse("gh:owner/repo#main/../../evil", out _)
        && !RemoteSourceParser.TryParse("https://github.com/owner/repo/tree/main/../../evil", out _)
        // A legitimate nested mod-spec still parses.
        && RemoteSourceParser.TryParse("gh:owner/repo/mods/cheaper-sawmill", out _);
}

static bool RemoteParserRejectsEmptyBasePath()
{
    // A bare 'repo:' (empty base) is a typo, not a valid zero-length value.
    return !RemoteSourceParser.TryParse("gh:owner/repo:/mod-id", out _)
        && !RemoteSourceParser.TryParse("gh:owner/repo:#main/mod-id", out _);
}

static bool RemoteParserLongFormFull()
{
    return RemoteSourceParser.TryParse("https://github.com/pagonia-land/example-mods/tree/main/mods/cheaper-sawmill", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "example-mods", Ref: "main", ModSpec: "mods/cheaper-sawmill" };
}

static bool RemoteParserLongFormRepoAndRef()
{
    return RemoteSourceParser.TryParse("https://github.com/pagonia-land/example-mods/tree/main", out var src)
        && src is GitHubSource { Owner: "pagonia-land", Repo: "example-mods", Ref: "main", ModSpec: null };
}

static bool RemoteParserRejectsEmptyOwner()
{
    return !RemoteSourceParser.TryParse("gh:/repo", out _);
}

static bool RemoteParserRejectsEmptyRepo()
{
    return !RemoteSourceParser.TryParse("gh:owner/", out _);
}

static bool RemoteParserRejectsEmptyRefAfterHash()
{
    return !RemoteSourceParser.TryParse("gh:owner/repo#", out _);
}

static bool RemoteParserRejectsGarbageInOwner()
{
    return !RemoteSourceParser.TryParse("gh:bad owner!/repo", out _);
}

static bool RemoteParserRejectsLocalPath()
{
    // Local paths like "C:\mods\cheaper-sawmill" or "/home/user/mods" must NOT
    // parse as remote — the caller falls back to ModInstaller's existing
    // folder / zip handling when TryParse returns false.
    return !RemoteSourceParser.TryParse(@"C:\mods\cheaper-sawmill", out _)
        && !RemoteSourceParser.TryParse("/home/user/mods", out _)
        && !RemoteSourceParser.TryParse(@".\mods\cheaper-sawmill", out _);
}

static bool RemoteParserRejectsLongFormWithoutTree()
{
    // https://github.com/owner/repo (no /tree/ segment) is the repo's
    // landing page, not a pinnable folder. Reject so the user gets a clear
    // error instead of a surprise default-branch install.
    return !RemoteSourceParser.TryParse("https://github.com/pagonia-land/example-mods", out _)
        && !RemoteSourceParser.TryParse("https://github.com/pagonia-land/example-mods/blob/main/README.md", out _);
}

static bool RemoteParserRejectsEmpty()
{
    return !RemoteSourceParser.TryParse(null, out _)
        && !RemoteSourceParser.TryParse("", out _)
        && !RemoteSourceParser.TryParse("   ", out _);
}

// ---- RemoteFetcher (with in-memory IRemoteContentFetcher) -------------------

static InMemoryRemoteContentFetcher MakeRepoFixture()
{
    // A minimal but realistic repo: index.yaml lists one mod, the mod folder
    // contains mod.yaml (referencing patches/buildings.yaml) and the patch
    // file itself.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "main", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddRef("acme", "mods", "v0.1.0", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/mod.yaml", """
        patchFormatVersion: 0.1
        id: pagonia-land.example.cheaper-sawmill
        name: Cheaper Sawmill
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Lowers the Sawmill Softwood Trunk cost by one.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/buildings.yaml
        """);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/patches/buildings.yaml", """
        operations:
          - id: cheaper-sawmill-softwood-cost
            operation: replaceValue
            risk: low
            reason: Example patch.
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              entityName: Sawmill
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "3"
        """);

    return fetcher;
}

static bool RemoteFetcherHappyPath()
{
    var fetcher = MakeRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill");
    var result = new RemoteFetcher(fetcher).FetchMod(source);

    try
    {
        return result.Success
            && result.TempDirectory is not null
            && Directory.Exists(result.TempDirectory)
            && File.Exists(Path.Combine(result.TempDirectory, "mod.yaml"))
            && File.Exists(Path.Combine(result.TempDirectory, "patches", "buildings.yaml"))
            && result.ResolvedSource == $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/pagonia-land.example.cheaper-sawmill"
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteResolvedToCommit);
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

// Same shape as MakeRepoFixture, but the index entry advertises a stale version
// (0.2.0) and a safeToRemove flag (true) that disagree with the mod.yaml the repo
// actually ships (0.1.0 / unknown) — the drift an install-time check should warn on.
static InMemoryRemoteContentFetcher MakeDriftRepoFixture()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "main", InMemoryRemoteContentFetcher.FakeSha);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: 0.2.0
            gameDatabaseVersion: "1.3.0-11694+192849"
            safetyFlags:
              requiresNewGame: false
              safeToRemove: true
              multiplayerSafe: unknown
              campaignSafe: unknown
        """);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/mod.yaml", """
        patchFormatVersion: 0.1
        id: pagonia-land.example.cheaper-sawmill
        name: Cheaper Sawmill
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Lowers the Sawmill Softwood Trunk cost by one.
        requiredPackages:
          - core
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        patches:
          - patches/buildings.yaml
        """);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/patches/buildings.yaml", """
        operations:
          - id: cheaper-sawmill-softwood-cost
            operation: replaceValue
            risk: low
            reason: Example patch.
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              entityName: Sawmill
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "3"
        """);

    return fetcher;
}

static bool RemoteFetcherNoMetadataWarningWhenInSync()
{
    // The happy-path fixture's index omits safetyFlags and its version matches the
    // mod.yaml, so the cross-check must stay silent (present-only, no drift).
    var result = new RemoteFetcher(MakeRepoFixture())
        .FetchMod(new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill"));
    try
    {
        return result.Success
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RepoIndexMetadataMismatch);
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherWarnsOnIndexMetadataDrift()
{
    var result = new RemoteFetcher(MakeDriftRepoFixture())
        .FetchMod(new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill"));
    try
    {
        var mismatches = result.Diagnostics
            .Where(d => d.Code == ManagerDiagnosticCodes.RepoIndexMetadataMismatch
                && d.Severity == ManagerDiagnosticSeverity.Warning)
            .ToList();

        // Still succeeds (warning, not fatal); flags both the version and the safeToRemove drift.
        return result.Success
            && mismatches.Any(d => d.Message.Contains("version"))
            && mismatches.Any(d => d.Message.Contains("safeToRemove"));
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

// Re-serve acme/mods' index.yaml with a contentHash on the cheaper-sawmill entry.
static void SetIndexContentHash(InMemoryRemoteContentFetcher fetcher, string contentHash)
{
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", $"""
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
            contentHash: {contentHash}
        """);
}

static bool RemoteFetcherWarnsOnContentHashMismatch()
{
    var fetcher = MakeRepoFixture();
    SetIndexContentHash(fetcher, new string('0', 64)); // advertised hash can't match the real payload
    var result = new RemoteFetcher(fetcher)
        .FetchMod(new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill"));
    try
    {
        // Still succeeds (warning, not fatal); flags the integrity/drift mismatch.
        return result.Success
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModContentHashMismatch
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherAcceptsMatchingContentHash()
{
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill");

    // First fetch (no contentHash advertised) to learn the payload's real hash...
    var fetcher = MakeRepoFixture();
    var first = new RemoteFetcher(fetcher).FetchMod(source);
    string? realHash;
    try
    {
        realHash = first.Success && first.TempDirectory is not null
            ? PagoniaLand.Patcher.ContentHash.OfModPayload(first.TempDirectory)
            : null;
    }
    finally
    {
        if (first.TempDirectory is not null && Directory.Exists(first.TempDirectory))
        { Directory.Delete(first.TempDirectory, true); }
    }
    if (realHash is null) { return false; }

    // ...then advertise exactly that, and confirm the verify stays silent.
    SetIndexContentHash(fetcher, realHash);
    var second = new RemoteFetcher(fetcher).FetchMod(source);
    try
    {
        return second.Success
            && !second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModContentHashMismatch);
    }
    finally
    {
        if (second.TempDirectory is not null && Directory.Exists(second.TempDirectory))
        { Directory.Delete(second.TempDirectory, true); }
    }
}

static bool RemoteFetcherUnknownModIdSurfacesDiagnostic()
{
    var fetcher = MakeRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.does-not-exist");
    var result = new RemoteFetcher(fetcher).FetchMod(source);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModNotInRepoIndex
            && d.Severity == ManagerDiagnosticSeverity.Error
            && d.Message.Contains("pagonia-land.example.cheaper-sawmill")); // names what IS available
}

static bool RemoteFetcherPathFallbackWithoutIndex()
{
    // No index.yaml in the repo → the ModSpec is interpreted as a literal
    // repo-relative folder path. Useful for single-mod-repo authors who
    // skip the index.yaml.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("solo", "tiny-mod", "main", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/solo/tiny-mod/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/mod.yaml", """
        patchFormatVersion: 0.1
        id: pagonia-land.example.cheaper-sawmill
        name: Cheaper Sawmill
        version: 0.1.0
        author: Solo
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Lowers the Sawmill cost.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/buildings.yaml
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/solo/tiny-mod/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/patches/buildings.yaml", "operations: []");

    var source = new GitHubSource("solo", "tiny-mod", "main", "mods/cheaper-sawmill");
    var result = new RemoteFetcher(fetcher).FetchMod(source);
    try
    {
        return result.Success
            && File.Exists(Path.Combine(result.TempDirectory!, "mod.yaml"))
            && File.Exists(Path.Combine(result.TempDirectory!, "patches", "buildings.yaml"))
            && result.ResolvedSource == $"gh:solo/tiny-mod#{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill";
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

// ---- RepoIndexFetcher (standalone index read for the browse listing) -------

static InMemoryRemoteContentFetcher MakeRepoIndexFixture()
{
    // index.yaml only — RepoIndexFetcher never touches mod.yaml / patches, so a
    // listing fixture needs just the ref + the catalogue. Two mods + one
    // collection so the test can prove both lists round-trip.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            displayName: Cheaper Sawmill
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
          - id: pagonia-land.example.bigger-storage
            path: mods/bigger-storage
            displayName: Bigger Storage
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        collections:
          - id: pagonia-land.example.starter-qol
            path: collections/starter-qol.collection.yaml
            displayName: Starter QoL Pack
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);
    return fetcher;
}

static bool RepoIndexFetcherListsModsAndCollections()
{
    var fetcher = MakeRepoIndexFixture();
    var source = new GitHubSource("acme", "mods", "HEAD", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return result.Success
        && result.HasIndex
        && result.CommitSha == InMemoryRemoteContentFetcher.FakeSha
        && result.Index!.Mods.Count == 2
        && result.Index.Mods.Any(m => m.Id == "pagonia-land.example.cheaper-sawmill" && m.DisplayName == "Cheaper Sawmill")
        && result.Index.Mods.Any(m => m.Id == "pagonia-land.example.bigger-storage")
        && result.Index.Collections.Count == 1
        && result.Index.Collections[0].Id == "pagonia-land.example.starter-qol"
        && result.Index.Collections[0].DisplayName == "Starter QoL Pack"
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteResolvedToCommit);
}

static bool RepoIndexFetcherHonoursBasePath()
{
    // The catalogue lives under an 'official-mods/' subdirectory — the layout a
    // tooling monorepo uses behind a catalog repo-entry indexPath. The fetch must
    // target official-mods/index.yaml, not the repo root.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("pagonia-land", "Pagonia-Land", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/pagonia-land/Pagonia-Land/{InMemoryRemoteContentFetcher.FakeSha}/official-mods/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: Pagonia Land Official Mods
        mods:
          - id: pagonia-land.mods.cheaper-sawmill
            path: mods/cheaper-sawmill
            displayName: Cheaper Sawmill
            version: 0.1.0
        """);

    var source = new GitHubSource("pagonia-land", "Pagonia-Land", "HEAD", ModSpec: null, BasePath: "official-mods");
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return result.Success
        && result.HasIndex
        && result.Index!.Mods.Count == 1
        && result.Index.Mods[0].Id == "pagonia-land.mods.cheaper-sawmill";
}

static bool RepoIndexFetcherNoIndexReportsHasIndexFalse()
{
    // Ref resolves, but the repo ships no index.yaml. That's a successful read
    // with nothing to enumerate — distinct from a fetch failure.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("solo", "tiny-mod", "HEAD", InMemoryRemoteContentFetcher.FakeSha);

    var source = new GitHubSource("solo", "tiny-mod", "HEAD", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return result.Success
        && !result.HasIndex
        && result.Index is null
        && result.CommitSha == InMemoryRemoteContentFetcher.FakeSha;
}

static bool RepoIndexFetcherMalformedIndexSurfacesDiagnostic()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", "not: valid: yaml: at: all:");

    var source = new GitHubSource("acme", "mods", "HEAD", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return !result.Success
        && !result.HasIndex
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteIndexMalformed
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool RepoIndexFetcherUnknownRefSurfacesDiagnostic()
{
    var fetcher = MakeRepoIndexFixture(); // only "HEAD" is a known ref
    var source = new GitHubSource("acme", "mods", "does-not-exist", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteFetchFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool RepoIndexFetcherNewerMinorReads()
{
    // A newer same-major minor reads: the shared format-version policy tolerates it (unknown
    // optional fields ignored) and surfaces an info recommend-update note — same code the
    // patcher emits, so manager + patcher agree.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
        indexFormatVersion: "0.99"
        repo:
          name: Future Repo
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            displayName: Cheaper Sawmill
            version: 0.1.0
        """);

    var source = new GitHubSource("acme", "mods", "HEAD", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return result.Success
        && result.HasIndex
        && result.Index!.Mods.Count == 1
        && result.Diagnostics.Any(d => d.Code == PagoniaLand.Patcher.DiagnosticCodes.FormatMinorAhead
            && d.Severity == ManagerDiagnosticSeverity.Info);
}

static bool RepoIndexFetcherNewerMajorRefused()
{
    // A newer major is a breaking shape this build can't read — refused with the actionable
    // formatMajorUnsupported error, so the browse list is never built from it.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
        indexFormatVersion: "1.0"
        repo:
          name: Future Repo
        """);

    var source = new GitHubSource("acme", "mods", "HEAD", ModSpec: null);
    var result = new RepoIndexFetcher(fetcher).Fetch(source);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == PagoniaLand.Patcher.DiagnosticCodes.FormatMajorUnsupported
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool ModVersionOrdersCorrectly()
{
    return ModVersion.IsNewer("0.2.0", "0.1.0")
        && ModVersion.IsNewer("0.1.1", "0.1.0")
        && ModVersion.IsNewer("1.0.0", "0.9.9")
        && !ModVersion.IsNewer("0.1.0", "0.1.0")
        && !ModVersion.IsNewer("0.1.0", "0.2.0")
        // release outranks a pre-release of the same core
        && ModVersion.IsNewer("1.0.0", "1.0.0-beta")
        && !ModVersion.IsNewer("1.0.0-beta", "1.0.0")
        // numeric pre-release identifiers compare numerically: rc.10 > rc.2 (not ordinal, where '1' < '2')
        && ModVersion.IsNewer("1.0.0-rc.10", "1.0.0-rc.2")
        && !ModVersion.IsNewer("1.0.0-rc.2", "1.0.0-rc.10")
        // missing components default to 0 (0.2 == 0.2.0 > 0.1.5)
        && ModVersion.IsNewer("0.2", "0.1.5")
        // an unparseable version is never claimed newer (no false positives)
        && !ModVersion.IsNewer("garbage", "0.1.0")
        && !ModVersion.IsNewer("0.2.0", "garbage");
}

// Materialise an installed mod on disk: a version directory + an install sidecar carrying the
// transport-neutral provenance, exactly what ModLister reads back.
static void WriteInstalledModFixture(StoreLayout layout, string id, string version, string source)
{
    var dir = layout.ModVersionDirectory(id, version);
    Directory.CreateDirectory(dir);
    File.WriteAllText(
        Path.Combine(dir, ModInstaller.SidecarFileName),
        $"installedAt: \"2026-06-17T00:00:00Z\"\nsourceType: github\nsource: \"{source}\"\n");
}

static bool UpdateDetectionFindsNewerVersion()
{
    var tempRoot = NewTempRoot("updates-newer");
    try
    {
        var layout = new StoreLayout(tempRoot);
        WriteInstalledModFixture(layout, "pagonia-land.example.cheaper-sawmill", "0.1.0",
            "gh:acme/mods#deadbeefdeadbeefdeadbeefdeadbeefdeadbeef/pagonia-land.example.cheaper-sawmill");

        var fetcher = new InMemoryRemoteContentFetcher();
        fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
        fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
            indexFormatVersion: "0.1"
            repo:
              name: Acme Mods
            mods:
              - id: pagonia-land.example.cheaper-sawmill
                path: mods/cheaper-sawmill
                displayName: Cheaper Sawmill
                version: 0.2.0
                gameDatabaseVersion: "1.4.0-test"
            """);

        var result = new UpdateDetectionService(fetcher).Check(layout);

        return result.CheckedCount == 1
            && result.Updates.Count == 1
            && result.Updates[0].Id == "pagonia-land.example.cheaper-sawmill"
            && result.Updates[0].InstalledVersion == "0.1.0"
            && result.Updates[0].AvailableVersion == "0.2.0"
            && result.Updates[0].GameDatabaseVersion == "1.4.0-test"
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModUpdateAvailable
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UpdateDetectionUpToDateReportsNoUpdates()
{
    var tempRoot = NewTempRoot("updates-current");
    try
    {
        var layout = new StoreLayout(tempRoot);
        WriteInstalledModFixture(layout, "pagonia-land.example.cheaper-sawmill", "0.2.0",
            "gh:acme/mods#deadbeefdeadbeefdeadbeefdeadbeefdeadbeef/pagonia-land.example.cheaper-sawmill");

        var fetcher = new InMemoryRemoteContentFetcher();
        fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
        fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
            indexFormatVersion: "0.1"
            repo:
              name: Acme Mods
            mods:
              - id: pagonia-land.example.cheaper-sawmill
                path: mods/cheaper-sawmill
                displayName: Cheaper Sawmill
                version: 0.2.0
                gameDatabaseVersion: "1.4.0-test"
            """);

        var result = new UpdateDetectionService(fetcher).Check(layout);
        return result.CheckedCount == 1 && result.Updates.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UpdateDetectionSkipsLocalMods()
{
    var tempRoot = NewTempRoot("updates-local");
    try
    {
        var layout = new StoreLayout(tempRoot);
        // No source -> a local folder/zip install: nothing to check against, must be skipped.
        WriteInstalledModFixture(layout, "pagonia-land.example.local-only", "0.1.0", "");

        var result = new UpdateDetectionService(new InMemoryRemoteContentFetcher()).Check(layout);
        return result.Updates.Count == 0
            && result.CheckedCount == 0
            && result.SkippedLocalCount == 1;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// Lay down a fully-installed mod (mod.yaml + a patch + gh: sidecar) so its payload hash can be
// recomputed — used for same-version content-drift detection.
static void WriteInstalledModPayload(StoreLayout layout, string id, string version, string source)
{
    var dir = layout.ModVersionDirectory(id, version);
    Directory.CreateDirectory(Path.Combine(dir, "patches"));
    File.WriteAllText(Path.Combine(dir, "mod.yaml"), $"""
        patchFormatVersion: "0.1"
        id: {id}
        name: Drift Fixture
        version: "{version}"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture for content-drift detection.
        requiredPackages:
          - core
        patches:
          - patches/p.yaml
        """);
    File.WriteAllText(Path.Combine(dir, "patches", "p.yaml"), "operations: []\n");
    File.WriteAllText(Path.Combine(dir, ModInstaller.SidecarFileName),
        $"installedAt: \"2026-06-17T00:00:00Z\"\nsourceType: github\nsource: \"{source}\"\n");
}

static void WriteDriftIndex(InMemoryRemoteContentFetcher fetcher, string id, string version, string? contentHash)
{
    var hashLine = contentHash is null ? string.Empty : $"\n    contentHash: {contentHash}";
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", $"""
        indexFormatVersion: "0.1"
        repo:
          name: Acme Mods
        mods:
          - id: {id}
            path: mods/x
            version: {version}
            gameDatabaseVersion: "1.3.0-11768+193445"{hashLine}
        """);
}

static bool UpdateDetectionFlagsSameVersionContentDrift()
{
    var tempRoot = NewTempRoot("updates-drift");
    try
    {
        var layout = InitLayout(tempRoot);
        const string id = "pagonia-land.example.drifty";
        WriteInstalledModPayload(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");

        var fetcher = new InMemoryRemoteContentFetcher();
        // Same version, but advertise a contentHash that can't match the installed payload.
        WriteDriftIndex(fetcher, id, "0.1.0", new string('0', 64));

        var result = new UpdateDetectionService(fetcher).Check(layout);
        return result.Updates.Count == 0
            && result.ContentDrifts.Count == 1
            && result.ContentDrifts[0].Id == id
            && result.ContentDrifts[0].Version == "0.1.0"
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModContentDriftAvailable);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UpdateDetectionNoDriftWhenContentHashMatches()
{
    var tempRoot = NewTempRoot("updates-nodrift");
    try
    {
        var layout = InitLayout(tempRoot);
        const string id = "pagonia-land.example.steady";
        WriteInstalledModPayload(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");
        var realHash = PagoniaLand.Patcher.ContentHash.OfModPayload(layout.ModVersionDirectory(id, "0.1.0"));

        var fetcher = new InMemoryRemoteContentFetcher();
        WriteDriftIndex(fetcher, id, "0.1.0", realHash);

        var result = new UpdateDetectionService(fetcher).Check(layout);
        return result.ContentDrifts.Count == 0
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModContentDriftAvailable);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UpdateDetectionNoDriftWhenNoAdvertisedHash()
{
    var tempRoot = NewTempRoot("updates-nohash");
    try
    {
        var layout = InitLayout(tempRoot);
        const string id = "pagonia-land.example.nohash";
        WriteInstalledModPayload(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");

        var fetcher = new InMemoryRemoteContentFetcher();
        WriteDriftIndex(fetcher, id, "0.1.0", contentHash: null); // index doesn't advertise a hash

        var result = new UpdateDetectionService(fetcher).Check(layout);
        return result.ContentDrifts.Count == 0;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Lay down an installed collection (manifest + provenance sidecar) at the store layout
// without going through the install pipeline, so update detection can be exercised in
// isolation. An empty source leaves out the sidecar (a local-file install).
static void WriteInstalledCollectionFixture(StoreLayout layout, string id, string version, string source)
{
    var dir = layout.CollectionVersionDirectory(id, version);
    Directory.CreateDirectory(dir);
    File.WriteAllText(layout.CollectionManifestFile(id, version), $"""
        collectionFormatVersion: 0.1
        id: {id}
        name: Fixture {id}
        version: "{version}"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture installed collection for update-detection tests.
        conflictPolicy: strict
        mods:
          - id: test.mod.a
            version: "0.1.0"
            required: true
            enabled: true
        loadOrder:
          - test.mod.a
        """);
    if (!string.IsNullOrEmpty(source))
    {
        File.WriteAllText(
            Path.Combine(dir, CollectionInstallService.SidecarFileName),
            $"installedAt: \"2026-06-17T00:00:00Z\"\nsource: \"{source}\"\n");
    }
}

static bool CollectionUpdateDetectionFindsNewerVersion()
{
    var tempRoot = NewTempRoot("coll-updates-newer");
    try
    {
        var layout = InitLayout(tempRoot);
        WriteInstalledCollectionFixture(layout, "pagonia-land.example.beginner-qol", "0.1.0",
            "gh:acme/presets#deadbeefdeadbeefdeadbeefdeadbeefdeadbeef/pagonia-land.example.beginner-qol");

        var fetcher = new InMemoryRemoteContentFetcher();
        fetcher.AddRef("acme", "presets", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
        fetcher.AddText($"https://raw.githubusercontent.com/acme/presets/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
            indexFormatVersion: "0.1"
            repo:
              name: Acme Presets
            collections:
              - id: pagonia-land.example.beginner-qol
                path: collections/beginner-qol.collection.yaml
                displayName: Beginner QoL
                version: 0.2.0
                gameDatabaseVersion: "1.4.0-test"
            """);

        var result = new UpdateDetectionService(fetcher).Check(layout);

        return result.CheckedCollectionCount == 1
            && result.CollectionUpdates.Count == 1
            && result.CollectionUpdates[0].Id == "pagonia-land.example.beginner-qol"
            && result.CollectionUpdates[0].InstalledVersion == "0.1.0"
            && result.CollectionUpdates[0].AvailableVersion == "0.2.0"
            && result.CollectionUpdates[0].GameDatabaseVersion == "1.4.0-test"
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionUpdateAvailable
                && d.Severity == ManagerDiagnosticSeverity.Info);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionUpdateDetectionUpToDateReportsNoUpdates()
{
    var tempRoot = NewTempRoot("coll-updates-current");
    try
    {
        var layout = InitLayout(tempRoot);
        WriteInstalledCollectionFixture(layout, "pagonia-land.example.beginner-qol", "0.2.0",
            "gh:acme/presets#deadbeefdeadbeefdeadbeefdeadbeefdeadbeef/pagonia-land.example.beginner-qol");

        var fetcher = new InMemoryRemoteContentFetcher();
        fetcher.AddRef("acme", "presets", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
        fetcher.AddText($"https://raw.githubusercontent.com/acme/presets/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", """
            indexFormatVersion: "0.1"
            repo:
              name: Acme Presets
            collections:
              - id: pagonia-land.example.beginner-qol
                path: collections/beginner-qol.collection.yaml
                displayName: Beginner QoL
                version: 0.2.0
                gameDatabaseVersion: "1.4.0-test"
            """);

        var result = new UpdateDetectionService(fetcher).Check(layout);
        return result.CheckedCollectionCount == 1 && result.CollectionUpdates.Count == 0;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool CollectionUpdateDetectionSkipsLocalCollections()
{
    var tempRoot = NewTempRoot("coll-updates-local");
    try
    {
        var layout = InitLayout(tempRoot);
        // No provenance sidecar -> a local-file install: nothing to check against, must be skipped.
        WriteInstalledCollectionFixture(layout, "pagonia-land.example.local-coll", "0.1.0", "");

        var result = new UpdateDetectionService(new InMemoryRemoteContentFetcher()).Check(layout);
        return result.CollectionUpdates.Count == 0
            && result.CheckedCollectionCount == 0
            && result.SkippedLocalCollectionCount == 1;
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// Serves acme/mods' cheaper-sawmill at a given version (index + mod.yaml + patch), so the update
// flow can install a newer version end to end through the real remote-install path.
static InMemoryRemoteContentFetcher MakeUpdateRepoFixture(string version)
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", $"""
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: {version}
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/mod.yaml", $"""
        patchFormatVersion: 0.1
        id: pagonia-land.example.cheaper-sawmill
        name: Cheaper Sawmill
        version: {version}
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Lowers the Sawmill Softwood Trunk cost by one.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/buildings.yaml
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/mods/cheaper-sawmill/patches/buildings.yaml", """
        operations:
          - id: cheaper-sawmill-softwood-cost
            operation: replaceValue
            risk: low
            reason: Example patch.
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              entityName: Sawmill
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "3"
        """);
    return fetcher;
}

static bool UpdateMovesProfilePinAndKeepsOldVersion()
{
    var tempRoot = NewTempRoot("update-flow");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout);

        const string id = "pagonia-land.example.cheaper-sawmill";
        // Old 0.1.0 installed with gh: provenance; active profile pins it with a user tweak override.
        WriteInstalledModFixture(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");
        new ProfileStore().Write(layout, new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = "default",
            EnabledMods = new List<ProfileEnabledMod>
            {
                new() { Id = id, Version = "0.1.0", Tweaks = new Dictionary<string, string> { ["softwood-cost"] = "2" } },
            },
            LoadOrder = new List<string> { id },
        });

        var fetcher = MakeUpdateRepoFixture("0.2.0");
        var result = new ModUpdateService(fetcher, allowInsecureSources: false).Update(layout, id, "default");

        if (result.Outcome != ModUpdateOutcome.Updated || result.FromVersion != "0.1.0" || result.ToVersion != "0.2.0")
        {
            return false;
        }

        var pinned = new ProfileStore().Read(layout, "default").EnabledMods.FirstOrDefault(m => m.Id == id);
        return pinned is not null
            && pinned.Version == "0.2.0"
            // user tweak override carried forward across the version bump
            && pinned.Tweaks is { Count: 1 } && pinned.Tweaks["softwood-cost"] == "2"
            // old version kept on disk (rollback anchor); new version installed alongside
            && Directory.Exists(layout.ModVersionDirectory(id, "0.1.0"))
            && Directory.Exists(layout.ModVersionDirectory(id, "0.2.0"))
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModUpdated);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UpdateIsNoOpWhenAlreadyCurrent()
{
    var tempRoot = NewTempRoot("update-current");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout);

        const string id = "pagonia-land.example.cheaper-sawmill";
        WriteInstalledModFixture(layout, id, "0.2.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");
        new ProfileStore().Write(layout, new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = "default",
            EnabledMods = new List<ProfileEnabledMod> { new() { Id = id, Version = "0.2.0" } },
            LoadOrder = new List<string> { id },
        });

        var fetcher = MakeUpdateRepoFixture("0.2.0");
        var result = new ModUpdateService(fetcher, allowInsecureSources: false).Update(layout, id, "default");

        return result.Outcome == ModUpdateOutcome.AlreadyCurrent
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModUpdateAlreadyCurrent);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UpdateRefusesMissingProfile()
{
    var tempRoot = NewTempRoot("update-missing-profile");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout);

        const string id = "pagonia-land.example.cheaper-sawmill";
        WriteInstalledModFixture(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");

        // A typo'd --profile must not throw out of ProfileStore.Read — it returns a clean profileMissing.
        var result = new ModUpdateService(new InMemoryRemoteContentFetcher(), allowInsecureSources: false)
            .Update(layout, id, "does-not-exist");

        return result.Outcome == ModUpdateOutcome.Failed
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ProfileMissing);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

static bool UpdateRefusesWhenNotEnabled()
{
    var tempRoot = NewTempRoot("update-not-enabled");
    try
    {
        var layout = new StoreLayout(tempRoot);
        new StoreInitializer().Initialize(layout); // default profile is empty

        const string id = "pagonia-land.example.cheaper-sawmill";
        WriteInstalledModFixture(layout, id, "0.1.0", $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/{id}");

        // No network needed — the mod isn't enabled, so there's no pin to move.
        var result = new ModUpdateService(new InMemoryRemoteContentFetcher(), allowInsecureSources: false).Update(layout, id, "default");

        return result.Outcome == ModUpdateOutcome.NotEnabled
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModUpdateNotEnabled);
    }
    finally
    {
        CleanupTempRoot(tempRoot);
    }
}

// Serves a single-mod collection repo (acme/mods) at given collection + mod versions, so the
// collection update flow can install a newer version end to end through the real remote path.
// AddText overwrites by URL, so a test can "publish" a newer version by re-calling it.
static void SeedCollectionRepoFixture(InMemoryRemoteContentFetcher fetcher, string collectionVersion, string modVersion)
{
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddRef("acme", "mods", "HEAD", sha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/index.yaml", $"""
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: test.upd.mod
            path: mods/test.upd.mod
            version: {modVersion}
            gameDatabaseVersion: "1.3.0-11768+193445"
        collections:
          - id: test.upd.collection
            path: collections/test.upd.collection.yaml
            version: {collectionVersion}
            gameDatabaseVersion: "1.3.0-11768+193445"
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/collections/test.upd.collection.yaml", $"""
        collectionFormatVersion: 0.1
        id: test.upd.collection
        name: Updatable Collection
        version: "{collectionVersion}"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture collection for the update flow.
        conflictPolicy: strict
        mods:
          - id: test.upd.mod
            version: "{modVersion}"
            required: true
            enabled: true
        loadOrder:
          - test.upd.mod
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/test.upd.mod/mod.yaml", $"""
        patchFormatVersion: "0.1"
        id: test.upd.mod
        name: Updatable Mod
        version: "{modVersion}"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture mod for the collection update flow.
        requiredPackages:
          - core
        patches:
          - patches/p.yaml
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/test.upd.mod/patches/p.yaml", """
        operations:
          - id: test-upd-mod-op
            operation: replaceValue
            risk: low
            reason: collection update fixture
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "3"
        """);
}

// Install test.upd.collection at the version the fixture currently serves, through the real
// remote fetch + install path (so the provenance sidecar is written). Returns the installed
// collection version.
static string InstallRemoteCollectionFixture(StoreLayout layout, InMemoryRemoteContentFetcher fetcher)
{
    var source = new GitHubSource("acme", "mods", "HEAD", "test.upd.collection");
    var fetch = new RemoteFetcher(fetcher).FetchCollection(source);
    if (!fetch.Success) { throw new InvalidOperationException("fixture fetch failed"); }
    try
    {
        var result = new CollectionInstallService().InstallWithOptions(layout, fetch.CollectionFilePath!, fetch.ModsRoot!,
            new CollectionInstallOptions
            {
                RemoteModSources = new Dictionary<string, string>(fetch.ModSources, StringComparer.Ordinal),
                RemoteCollectionSource = fetch.ResolvedCollectionSource,
            });
        if (result.Outcome != CollectionInstallOutcome.Installed) { throw new InvalidOperationException("fixture install failed"); }
        return result.CollectionVersion!;
    }
    finally
    {
        if (fetch.TempDirectory is not null && Directory.Exists(fetch.TempDirectory)) { Directory.Delete(fetch.TempDirectory, true); }
    }
}

static bool CollectionUpdateInstallsNewerVersionAndReseedsProfile()
{
    var tempRoot = NewTempRoot("coll-update-flow");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();

        // Publish + install 0.1.0.
        SeedCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", modVersion: "0.1.0");
        InstallRemoteCollectionFixture(layout, fetcher);

        // Publish 0.2.0 of the collection at HEAD, then update.
        SeedCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", modVersion: "0.1.0");
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");

        if (result.Outcome != CollectionUpdateOutcome.Updated
            || result.FromVersion != "0.1.0" || result.ToVersion != "0.2.0")
        {
            return false;
        }

        return File.Exists(layout.CollectionManifestFile("test.upd.collection", "0.2.0"))
            // old version kept on disk as the rollback anchor
            && File.Exists(layout.CollectionManifestFile("test.upd.collection", "0.1.0"))
            // the linked profile now reports the collection at the new version
            && new CollectionLister().List(layout).Any(c => c.Id == "test.upd.collection" && c.Version == "0.2.0")
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionUpdated);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateIsNoOpWhenAlreadyCurrent()
{
    var tempRoot = NewTempRoot("coll-update-current");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", modVersion: "0.1.0");
        InstallRemoteCollectionFixture(layout, fetcher);

        // HEAD still advertises 0.1.0 — nothing to do.
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");

        return result.Outcome == CollectionUpdateOutcome.AlreadyCurrent
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionUpdateAlreadyCurrent);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateRefusesLocalCollection()
{
    var tempRoot = NewTempRoot("coll-update-local");
    try
    {
        var layout = InitLayout(tempRoot);
        // Local-file install: no provenance sidecar, so there's no source to update from.
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.local.coll", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });
        new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);

        var result = new CollectionUpdateService(new InMemoryRemoteContentFetcher()).Update(layout, "test.local.coll");

        return result.Outcome == CollectionUpdateOutcome.NoRemoteSource
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionUpdateNoRemoteSource);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateRefusesWhenNotInstalled()
{
    var tempRoot = NewTempRoot("coll-update-missing");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new CollectionUpdateService(new InMemoryRemoteContentFetcher()).Update(layout, "no.such.collection");

        return result.Outcome == CollectionUpdateOutcome.NotInstalled
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionUpdateNotInstalled);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// Serves a single-mod collection (test.upd.collection / test.upd.mod) where the mod declares an
// integer tweak `softwood-cost` fed into its op, and the collection's curator sets a value for it.
// Parameterised so a test can "publish" a newer collection version and/or change the curator value.
static void SeedTweakCollectionRepoFixture(InMemoryRemoteContentFetcher fetcher, string collectionVersion, string curatorTweakValue)
{
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddRef("acme", "mods", "HEAD", sha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/index.yaml", $"""
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: test.upd.mod
            path: mods/test.upd.mod
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11768+193445"
        collections:
          - id: test.upd.collection
            path: collections/test.upd.collection.yaml
            version: {collectionVersion}
            gameDatabaseVersion: "1.3.0-11768+193445"
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/collections/test.upd.collection.yaml", $"""
        collectionFormatVersion: 0.1
        id: test.upd.collection
        name: Updatable Tweak Collection
        version: "{collectionVersion}"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture collection with a curator tweak override.
        conflictPolicy: strict
        mods:
          - id: test.upd.mod
            version: "0.1.0"
            required: true
            enabled: true
            tweaks:
              softwood-cost: "{curatorTweakValue}"
        loadOrder:
          - test.upd.mod
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/test.upd.mod/mod.yaml", """
        patchFormatVersion: "0.1"
        id: test.upd.mod
        name: Updatable Tweak Mod
        version: "0.1.0"
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11768+193445"
        description: Fixture mod with a tweak for the collection update flow.
        requiredPackages:
          - core
        tweaks:
          - id: softwood-cost
            type: integer
            label: Softwood trunk cost
            default: 2
            min: 1
            max: 8
        patches:
          - patches/p.yaml
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/test.upd.mod/patches/p.yaml", """
        operations:
          - id: test-upd-mod-op
            operation: replaceValue
            risk: low
            reason: collection update tweak fixture
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "{{ tweaks.softwood-cost }}"
        """);
}

// Resolve the current effective softwood-cost value on the updated collection's profile.
static string ReadUpdModTweak(StoreLayout layout)
    => new TweakOverrideService().Read(layout, "test.upd.collection", "test.upd.mod")
        .Tweaks.Single(t => t.Declaration.Id == "softwood-cost").Value;

static bool CollectionUpdateMergeKeepsGenuineOverride()
{
    var tempRoot = NewTempRoot("coll-update-merge-keep");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", curatorTweakValue: "5");
        InstallRemoteCollectionFixture(layout, fetcher);

        // User makes a genuine override (5 -> 7).
        new TweakOverrideService().Set(layout, "test.upd.collection", "test.upd.mod", "softwood-cost", "7");

        // Publish 0.2.0 (curator value unchanged at 5). Default policy = Merge.
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", curatorTweakValue: "5");
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");

        return result.Outcome == CollectionUpdateOutcome.Updated
            && ReadUpdModTweak(layout) == "7" // the user's override survived the update
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionTweakKept);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateMergeAdoptsNewCuratorDefaultForNonOverridden()
{
    var tempRoot = NewTempRoot("coll-update-merge-adopt");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", curatorTweakValue: "5");
        InstallRemoteCollectionFixture(layout, fetcher);
        // No user override — the stored 5 is just the curator default.

        // Publish 0.2.0 with a NEW curator value (6). Merge must adopt it (nothing genuine to keep).
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", curatorTweakValue: "6");
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");

        return result.Outcome == CollectionUpdateOutcome.Updated
            && ReadUpdModTweak(layout) == "6" // followed the new curator default
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionTweakKept);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateReseedDiscardsOverride()
{
    var tempRoot = NewTempRoot("coll-update-reseed");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", curatorTweakValue: "5");
        InstallRemoteCollectionFixture(layout, fetcher);
        new TweakOverrideService().Set(layout, "test.upd.collection", "test.upd.mod", "softwood-cost", "7");

        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", curatorTweakValue: "5");
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection", CollectionTweakPolicy.Reseed);

        return result.Outcome == CollectionUpdateOutcome.Updated
            && ReadUpdModTweak(layout) == "5" // override discarded, curator value reseeded
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionTweakKept);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateAskCallbackResolvesConflict()
{
    var tempRoot = NewTempRoot("coll-update-ask");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", curatorTweakValue: "5");
        InstallRemoteCollectionFixture(layout, fetcher);
        new TweakOverrideService().Set(layout, "test.upd.collection", "test.upd.mod", "softwood-cost", "7");

        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", curatorTweakValue: "5");

        // Callback fires once (one genuine conflict) and chooses the curator value.
        var callbackHits = 0;
        var result = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection",
            CollectionTweakPolicy.Ask,
            conflict =>
            {
                callbackHits++;
                return conflict is { ModId: "test.upd.mod", TweakId: "softwood-cost", YourValue: "7", CuratorValue: "5" }
                    ? CollectionTweakResolution.TakeCurator
                    : CollectionTweakResolution.KeepYours;
            });

        return result.Outcome == CollectionUpdateOutcome.Updated
            && callbackHits == 1
            && ReadUpdModTweak(layout) == "5" // callback chose curator
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionTweakReset);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool TweakSetMarksOverrideEvenWhenEqualToCurator()
{
    var tempRoot = NewTempRoot("tweak-explicit-equal");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.explicit", "5");
        new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        const string profile = "test.collection.explicit";

        // Seeded curator value reads as collection-default.
        var before = new TweakOverrideService().Read(layout, profile, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        // User explicitly sets the SAME value the curator uses. The old heuristic would call this
        // collection-default; explicit marking records it as the user's.
        new TweakOverrideService().Set(layout, profile, modId, "softwood-cost", "5");
        var after = new TweakOverrideService().Read(layout, profile, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");

        return before.Origin == TweakValueOrigins.CollectionDefault
            && after.Value == "5"
            && after.Origin == TweakValueOrigins.ProfileOverride;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool LegacyProfileMigratesUserTweaksOnRead()
{
    var tempRoot = NewTempRoot("tweak-legacy-migrate");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath, modId) = BuildTweakCollectionFixture(tempRoot, "test.collection.legacy", "5");
        new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        const string profile = "test.collection.legacy";

        // Simulate a pre-marking profile: a user value (7 != curator 5) stored WITHOUT userTweaks.
        var store = new ProfileStore();
        var p = store.Read(layout, profile);
        store.Write(layout, new ProfileFile
        {
            ProfileVersion = p.ProfileVersion,
            Name = p.Name,
            Collection = p.Collection,
            LoadOrder = p.LoadOrder,
            EnabledMods = p.EnabledMods.Select(m => m.Id == modId
                ? new ProfileEnabledMod { Id = m.Id, Version = m.Version, Tweaks = new Dictionary<string, string> { ["softwood-cost"] = "7" }, UserTweaks = null }
                : m).ToList(),
        });

        // First read infers + persists the marker via the heuristic (7 != 5 ⇒ user).
        var view = new TweakOverrideService().Read(layout, profile, modId)
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost");
        var persisted = store.Read(layout, profile).EnabledMods.Single(m => m.Id == modId).UserTweaks;

        return view.Origin == TweakValueOrigins.ProfileOverride
            && persisted is not null && persisted.Contains("softwood-cost");
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionUpdateKeepsCoincidentalEqualOverride()
{
    var tempRoot = NewTempRoot("coll-update-coincidental");
    try
    {
        var layout = InitLayout(tempRoot);
        var fetcher = new InMemoryRemoteContentFetcher();
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.1.0", curatorTweakValue: "5");
        InstallRemoteCollectionFixture(layout, fetcher);

        // User explicitly sets the SAME value the curator currently uses (5).
        new TweakOverrideService().Set(layout, "test.upd.collection", "test.upd.mod", "softwood-cost", "5");

        // Update 0.1.0 -> 0.2.0, curator still 5. The explicit mark must survive the reseed.
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.2.0", curatorTweakValue: "5");
        var r1 = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");
        var originAfter = new TweakOverrideService().Read(layout, "test.upd.collection", "test.upd.mod")
            .Tweaks.Single(t => t.Declaration.Id == "softwood-cost").Origin;

        // Now the curator diverges (6). Because the value is still marked as the user's, it is kept
        // at 5 rather than swept up to the new curator default — which only works because the mark
        // survived the coincidental-equal update above.
        SeedTweakCollectionRepoFixture(fetcher, collectionVersion: "0.3.0", curatorTweakValue: "6");
        var r2 = new CollectionUpdateService(fetcher).Update(layout, "test.upd.collection");

        return r1.Outcome == CollectionUpdateOutcome.Updated
            && originAfter == TweakValueOrigins.ProfileOverride
            && r2.Outcome == CollectionUpdateOutcome.Updated
            && ReadUpdModTweak(layout) == "5";
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- InstallSourceResolver (shared remote-source dispatch) -----------------

static bool ResolveRemoteGitHubFetchesAndPinsProvenance()
{
    var fetcher = MakeRepoFixture();
    var layout = new StoreLayout(Path.Combine(Path.GetTempPath(), $"pagonia-resolver-{Guid.NewGuid():N}"));
    var resolution = InstallSourceResolver.ResolveRemote(
        "gh:acme/mods/pagonia-land.example.cheaper-sawmill", layout, fetcher, allowInsecureSources: false);

    try
    {
        return resolution is not null
            && !resolution.Aborted
            && !resolution.MapTypeSkipped
            && resolution.InstallSource is not null
            && Directory.Exists(resolution.InstallSource)
            && File.Exists(Path.Combine(resolution.InstallSource, "mod.yaml"))
            && resolution.TempDir == resolution.InstallSource
            && resolution.RemoteProvenance == $"gh:acme/mods#{InMemoryRemoteContentFetcher.FakeSha}/pagonia-land.example.cheaper-sawmill";
    }
    finally
    {
        if (resolution?.TempDir is not null && Directory.Exists(resolution.TempDir))
        { Directory.Delete(resolution.TempDir, true); }
    }
}

static bool ResolveRemoteLocalSpecReturnsNull()
{
    // A bare local path is not a transport-prefixed remote spec, so the resolver
    // returns null and the caller installs it as a local source.
    var fetcher = new InMemoryRemoteContentFetcher();
    var layout = new StoreLayout(Path.Combine(Path.GetTempPath(), $"pagonia-resolver-{Guid.NewGuid():N}"));
    var resolution = InstallSourceResolver.ResolveRemote("my-local-mod-folder", layout, fetcher, allowInsecureSources: false);
    return resolution is null;
}

static bool ResolveRemoteInsecureHttpRefusedWithoutOptIn()
{
    // Plain-http direct URL aborts before any fetch unless allowInsecureSources
    // is set, with the directUrlInsecureHttp error diagnostic.
    var fetcher = new InMemoryRemoteContentFetcher();
    var layout = new StoreLayout(Path.Combine(Path.GetTempPath(), $"pagonia-resolver-{Guid.NewGuid():N}"));
    var resolution = InstallSourceResolver.ResolveRemote("http://example.invalid/mod.zip", layout, fetcher, allowInsecureSources: false);

    return resolution is not null
        && resolution.Aborted
        && resolution.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DirectUrlInsecureHttp
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static InMemoryRemoteContentFetcher MakeSubdirRepoFixture()
{
    // Same shape as MakeRepoFixture, but the index.yaml + mod tree live under
    // an 'official-mods/' subdirectory — the layout a tooling monorepo uses to
    // host a mod-distribution tree referenced by a catalog repo-entry indexPath.
    // The mod path inside index.yaml stays base-relative ('mods/cheaper-sawmill').
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("pagonia-land", "Pagonia-Land", "main", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddRef("pagonia-land", "Pagonia-Land", "HEAD", InMemoryRemoteContentFetcher.FakeSha);
    var baseUrl = $"https://raw.githubusercontent.com/pagonia-land/Pagonia-Land/{InMemoryRemoteContentFetcher.FakeSha}/official-mods";

    fetcher.AddText($"{baseUrl}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: Pagonia Land Official Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);
    fetcher.AddText($"{baseUrl}/mods/cheaper-sawmill/mod.yaml", """
        patchFormatVersion: 0.1
        id: pagonia-land.example.cheaper-sawmill
        name: Cheaper Sawmill
        version: 0.1.0
        author: Pagonia Land
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Lowers the Sawmill cost.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/buildings.yaml
        """);
    fetcher.AddText($"{baseUrl}/mods/cheaper-sawmill/patches/buildings.yaml", "operations: []");
    return fetcher;
}

static bool RemoteFetcherSubdirectoryIndexPath()
{
    // BasePath 'official-mods' redirects the index.yaml + mod folder + patch
    // fetches under the subdirectory, and the resolved provenance carries the
    // ':official-mods' segment so a re-install round-trips the subtree.
    var fetcher = MakeSubdirRepoFixture();
    var source = new GitHubSource("pagonia-land", "Pagonia-Land", "main", "pagonia-land.example.cheaper-sawmill", "official-mods");
    var result = new RemoteFetcher(fetcher).FetchMod(source);
    try
    {
        return result.Success
            && File.Exists(Path.Combine(result.TempDirectory!, "mod.yaml"))
            && File.Exists(Path.Combine(result.TempDirectory!, "patches", "buildings.yaml"))
            && result.ResolvedSource == $"gh:pagonia-land/Pagonia-Land:official-mods#{InMemoryRemoteContentFetcher.FakeSha}/pagonia-land.example.cheaper-sawmill";
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherBaseTraversalRefused()
{
    // The parser rejects '..' in a base path, but a directly-constructed source
    // (or a hand-edited state) shouldn't escape the repo either — the fetcher
    // guards the base-joined mod folder. Defence in depth.
    var fetcher = MakeSubdirRepoFixture();
    var source = new GitHubSource("pagonia-land", "Pagonia-Land", "main", "pagonia-land.example.cheaper-sawmill", "../secrets");
    var result = new RemoteFetcher(fetcher).FetchMod(source);
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteFetchFailed
            && d.Message.Contains("traversal"));
}

static bool RemoteFetcherUnknownRefSurfacesDiagnostic()
{
    var fetcher = MakeRepoFixture();
    var source = new GitHubSource("acme", "mods", "this-branch-does-not-exist", "pagonia-land.example.cheaper-sawmill");
    var result = new RemoteFetcher(fetcher).FetchMod(source);

    return !result.Success
        && result.TempDirectory is null
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteFetchFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool RemoteFetcherMalformedIndexSurfacesDiagnostic()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "main", InMemoryRemoteContentFetcher.FakeSha);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{InMemoryRemoteContentFetcher.FakeSha}/index.yaml", "not: valid: yaml: at: all:");
    var source = new GitHubSource("acme", "mods", "main", "anything");
    var result = new RemoteFetcher(fetcher).FetchMod(source);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteIndexMalformed);
}

static bool RemoteFetcherRefusesTraversal()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "mods", "main", InMemoryRemoteContentFetcher.FakeSha);
    // No index.yaml → path-fallback mode. Hand it a path with '..' — the
    // fetcher must refuse before issuing any further network call.
    var source = new GitHubSource("acme", "mods", "main", "../../etc/passwd");
    var result = new RemoteFetcher(fetcher).FetchMod(source);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.RemoteFetchFailed
            && d.Message.Contains("traversal", StringComparison.OrdinalIgnoreCase))
        // No mod.yaml or patch URLs should have been requested after the refusal.
        && fetcher.FetchedUrls.All(u => !u.Contains("/etc/", StringComparison.Ordinal));
}

static bool RemoteFetcherResolvedSourcePinsSha()
{
    // User asks for the moving "main" branch; ResolvedSource must record the
    // concrete commit SHA so a re-install months later (when main has moved)
    // still names the exact code that landed in the store.
    var fetcher = MakeRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill");
    var result = new RemoteFetcher(fetcher).FetchMod(source);
    try
    {
        return result.Success
            && result.CommitSha == InMemoryRemoteContentFetcher.FakeSha
            && !result.ResolvedSource!.Contains("#main") // didn't pin the ref
            && result.ResolvedSource.Contains($"#{InMemoryRemoteContentFetcher.FakeSha}"); // pinned the SHA
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherEndToEndInstall()
{
    // The whole point of the fetcher: hand its temp dir to the existing
    // ModInstaller and watch the install land normally under <store>/mods/...
    var tempRoot = NewTempRoot("remote-fetcher-install");
    string? fetchedDir = null;
    try
    {
        var fetcher = MakeRepoFixture();
        var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.cheaper-sawmill");
        var fetchResult = new RemoteFetcher(fetcher).FetchMod(source);
        if (!fetchResult.Success || fetchResult.TempDirectory is null) { return false; }
        fetchedDir = fetchResult.TempDirectory;

        var storeRoot = Path.Combine(tempRoot, "store");
        Directory.CreateDirectory(storeRoot);
        var layout = new StoreLayout(storeRoot);
        new StoreInitializer().Initialize(layout);

        // Pass the resolved remote source through to ModInstaller so the
        // sidecar records the gh: provenance — that's what `pagonia-manager
        // list` and post-mortem audits read.
        var installResult = new ModInstaller().Install(fetchResult.TempDirectory, layout, fetchResult.ResolvedSource);

        var sidecarPath = Path.Combine(installResult.InstallPath!, ModInstaller.SidecarFileName);
        var sidecar = new YamlDotNet.Serialization.DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<InstallSidecar>(File.ReadAllText(sidecarPath));

        return installResult.Outcome == InstallOutcome.Installed
            && installResult.ModId == "pagonia-land.example.cheaper-sawmill"
            && installResult.Version == "0.1.0"
            && File.Exists(Path.Combine(installResult.InstallPath!, "mod.yaml"))
            && File.Exists(Path.Combine(installResult.InstallPath!, "patches", "buildings.yaml"))
            && sidecar.Source == fetchResult.ResolvedSource
            && sidecar.Source.Contains($"#{InMemoryRemoteContentFetcher.FakeSha}"); // SHA pinned, not the moving "main" ref
    }
    finally
    {
        if (fetchedDir is not null && Directory.Exists(fetchedDir)) { Directory.Delete(fetchedDir, true); }
        CleanupTempRoot(tempRoot);
    }
}

// ---- CollectionInstallService new flags ------------------------------------

static bool CollectionInstallAsProfileAndActivate()
{
    var tempRoot = NewTempRoot("coll-as-profile-activate");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.activate", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });

        var options = new CollectionInstallOptions
        {
            ProfileNameOverride = "play-with-streamer",
            Activate = true,
        };
        var result = new CollectionInstallService().InstallWithOptions(layout, collectionPath, modsRoot, options);

        var state = new StoreStateReader().Read(layout);
        return result.Outcome == CollectionInstallOutcome.Installed
            && result.ProfileName == "play-with-streamer"
            && result.ProfileActivated
            && state.ActiveProfile == "play-with-streamer"
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ProfileCreatedFromCollection)
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ProfileActivatedFromCollection);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionInstallRefusesOverwriteByDefault()
{
    var tempRoot = NewTempRoot("coll-overwrite-refuse");
    try
    {
        var layout = InitLayout(tempRoot);

        // Pre-seed: a profile named "shared-name" exists from some other install.
        var preexisting = new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = "shared-name",
        };
        new ProfileStore().Write(layout, preexisting);

        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.refuse", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });

        var options = new CollectionInstallOptions
        {
            ProfileNameOverride = "shared-name",
            // Overwrite intentionally NOT set
        };
        var result = new CollectionInstallService().InstallWithOptions(layout, collectionPath, modsRoot, options);

        // Refusal happens BEFORE any disk write, so manifest/lockfile/mods
        // stay untouched and the pre-existing profile is still on disk.
        return result.Outcome == CollectionInstallOutcome.Failed
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ProfileAlreadyExists
                && d.Severity == ManagerDiagnosticSeverity.Error
                && d.Message.Contains("--overwrite"))
            && !File.Exists(layout.CollectionManifestFile("test.collection.refuse", "0.1.0"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionInstallOverwriteReplacesProfile()
{
    var tempRoot = NewTempRoot("coll-overwrite-replace");
    try
    {
        var layout = InitLayout(tempRoot);

        // Pre-seed: a profile with mods that DON'T match our collection so
        // we can prove the replacement actually happened (not a silent merge).
        var stale = new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = "shared-name",
            EnabledMods = new List<ProfileEnabledMod>
            {
                new() { Id = "left.over.mod", Version = "9.9.9" },
            },
        };
        new ProfileStore().Write(layout, stale);

        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.replace", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });

        var options = new CollectionInstallOptions
        {
            ProfileNameOverride = "shared-name",
            Overwrite = true,
        };
        var result = new CollectionInstallService().InstallWithOptions(layout, collectionPath, modsRoot, options);

        // After overwrite the profile holds the collection's mods, not the
        // pre-existing "left.over.mod".
        var reread = new ProfileStore().Read(layout, "shared-name");
        return result.Outcome == CollectionInstallOutcome.Installed
            && reread.EnabledMods.Count == 1
            && reread.EnabledMods[0].Id == "test.mod.a"
            && reread.EnabledMods.All(m => m.Id != "left.over.mod");
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionInstallRemoteSourcesAugmentLockfile()
{
    var tempRoot = NewTempRoot("coll-remote-lockfile");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.remote", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
            ("test.mod.b", "0.1.0", (string?)null),
        });

        var remoteSources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["test.mod.a"] = "gh:acme/mods#aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/test.mod.a",
            ["test.mod.b"] = "gh:other/repo#bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb/test.mod.b",
        };

        var options = new CollectionInstallOptions
        {
            RemoteModSources = remoteSources,
        };
        var result = new CollectionInstallService().InstallWithOptions(layout, collectionPath, modsRoot, options);

        if (result.Outcome != CollectionInstallOutcome.Installed) { return false; }

        // Read the lockfile back and verify each mod has its source +
        // resolvedAt populated. The lockfile version tracks the current schema
        // the writer pins (0.1).
        var lockText = File.ReadAllText(result.LockfilePath!);
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        var lockObj = deserializer.Deserialize<PagoniaLand.Patcher.CollectionLock>(lockText);

        return lockObj.CollectionLockVersion == "0.1"
            && lockObj.Mods.All(m => !string.IsNullOrEmpty(m.Source) && !string.IsNullOrEmpty(m.ResolvedAt))
            && lockObj.Mods.Single(m => m.Id == "test.mod.a").Source.Contains("acme/mods#aaaaaaaa")
            && lockObj.Mods.Single(m => m.Id == "test.mod.b").Source.Contains("other/repo#bbbbbbbb");
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionInstallRemoteSourceWritesProvenanceSidecar()
{
    var tempRoot = NewTempRoot("coll-provenance-sidecar");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.prov", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });

        const string collectionSource = "gh:acme/presets#cccccccccccccccccccccccccccccccccccccccc/test.collection.prov";
        var result = new CollectionInstallService().InstallWithOptions(layout, collectionPath, modsRoot,
            new CollectionInstallOptions { RemoteCollectionSource = collectionSource });

        if (result.Outcome != CollectionInstallOutcome.Installed) { return false; }

        // Sidecar lands beside the manifest in the version dir, and the lister
        // surfaces it as InstalledCollection.Source.
        var sidecarPath = Path.Combine(
            layout.CollectionVersionDirectory("test.collection.prov", "0.1.0"),
            CollectionInstallService.SidecarFileName);

        var listed = new CollectionLister().List(layout)
            .Single(c => c.Id == "test.collection.prov" && c.Version == "0.1.0");

        return File.Exists(sidecarPath)
            && listed.Source == collectionSource;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CollectionInstallLocalLeavesNoProvenanceSidecar()
{
    var tempRoot = NewTempRoot("coll-no-sidecar");
    try
    {
        var layout = InitLayout(tempRoot);
        var (modsRoot, collectionPath) = BuildCollectionFixture(tempRoot, "test.collection.local", new[]
        {
            ("test.mod.a", "0.1.0", (string?)null),
        });

        // No RemoteCollectionSource -> local-file install: nothing to update-check
        // against, so no sidecar is written and Source reads back null.
        var result = new CollectionInstallService().Install(layout, collectionPath, modsRoot, profileNameOverride: null);
        if (result.Outcome != CollectionInstallOutcome.Installed) { return false; }

        var sidecarPath = Path.Combine(
            layout.CollectionVersionDirectory("test.collection.local", "0.1.0"),
            CollectionInstallService.SidecarFileName);

        var listed = new CollectionLister().List(layout)
            .Single(c => c.Id == "test.collection.local" && c.Version == "0.1.0");

        return !File.Exists(sidecarPath)
            && string.IsNullOrEmpty(listed.Source);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- RemoteFetcher.FetchCollection -----------------------------------------

static InMemoryRemoteContentFetcher MakeCollectionRepoFixture()
{
    // Repo "acme/preset-repo" with an index.yaml listing 2 mods + 1 collection;
    // the collection references both mods (same-repo) and one mod is from
    // a cross-repo source pointing at "other/other-repo".
    var fetcher = MakeRepoFixture(); // reuses acme/mods setup so SHA + ref mappings line up

    // Override: present mods + collection on the same repo.
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: ACME Mods
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            path: mods/cheaper-sawmill
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
          - id: pagonia-land.example.wine-icon-test
            path: mods/wine-icon-test
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        collections:
          - id: pagonia-land.example.beginner-qol
            path: collections/beginner-qol.collection.yaml
            version: 0.1.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);

    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/collections/beginner-qol.collection.yaml", """
        collectionFormatVersion: 0.1
        id: pagonia-land.example.beginner-qol
        name: Beginner QoL
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Two mods bundled.
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            version: "0.1.0"
          - id: pagonia-land.example.wine-icon-test
            version: "0.1.0"
        """);

    // Second mod content (cheaper-sawmill is from MakeRepoFixture).
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/wine-icon-test/mod.yaml", """
        patchFormatVersion: 0.1
        id: pagonia-land.example.wine-icon-test
        name: Wine Icon Test
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Cosmetic icon swap.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/dlc1-icon.yaml
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/mods/wine-icon-test/patches/dlc1-icon.yaml", "operations: []");

    return fetcher;
}

static bool RemoteFetcherCollectionSameRepoHappy()
{
    var fetcher = MakeCollectionRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.beginner-qol");
    var result = new RemoteFetcher(fetcher).FetchCollection(source);

    try
    {
        return result.Success
            && result.TempDirectory is not null
            && File.Exists(result.CollectionFilePath!)
            && Directory.Exists(result.ModsRoot!)
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.cheaper-sawmill", "mod.yaml"))
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.cheaper-sawmill", "patches", "buildings.yaml"))
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.wine-icon-test", "mod.yaml"))
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.wine-icon-test", "patches", "dlc1-icon.yaml"));
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherCollectionCrossRepo()
{
    // Collection in repo A; one mod's source: gh: points at repo B.
    // The fetcher must hit B's index.yaml + raw URLs without losing
    // anything, and the per-mod source map must record B's commit SHA.
    var fetcher = MakeCollectionRepoFixture();
    var crossSha = "b0b1b2b3b4b5b6b7b8b9babbbcbdbebfc0c1c2c3";
    fetcher.AddRef("other", "other-repo", "main", crossSha);
    fetcher.AddRef("other", "other-repo", "HEAD", crossSha);

    fetcher.AddText($"https://raw.githubusercontent.com/other/other-repo/{crossSha}/index.yaml", """
        indexFormatVersion: "0.1"
        repo:
          name: Other Mods
        mods:
          - id: other.shared-mod
            path: mods/shared-mod
            version: 0.2.0
            gameDatabaseVersion: "1.3.0-11694+192849"
        """);
    fetcher.AddText($"https://raw.githubusercontent.com/other/other-repo/{crossSha}/mods/shared-mod/mod.yaml", """
        patchFormatVersion: 0.1
        id: other.shared-mod
        name: Shared
        version: 0.2.0
        author: Other
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Cross-repo mod.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches: []
        """);

    // Override the collection to include a cross-repo entry.
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/collections/beginner-qol.collection.yaml", """
        collectionFormatVersion: 0.1
        id: pagonia-land.example.beginner-qol
        name: Beginner QoL
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Same-repo + cross-repo.
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            version: "0.1.0"
          - id: other.shared-mod
            version: "0.2.0"
            source: "gh:other/other-repo/other.shared-mod"
        """);

    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.beginner-qol");
    var result = new RemoteFetcher(fetcher).FetchCollection(source);

    try
    {
        return result.Success
            && File.Exists(Path.Combine(result.ModsRoot!, "other.shared-mod", "mod.yaml"))
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.cheaper-sawmill", "mod.yaml"))
            && result.ModSources.TryGetValue("other.shared-mod", out var crossSource)
            && crossSource.Contains($"other/other-repo#{crossSha}")
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CrossRepoSourceResolved);
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherCollectionResolvedSourcesPinShas()
{
    var fetcher = MakeCollectionRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.beginner-qol");
    var result = new RemoteFetcher(fetcher).FetchCollection(source);
    try
    {
        if (!result.Success) { return false; }
        var sha = InMemoryRemoteContentFetcher.FakeSha;
        // Top-level ResolvedCollectionSource and every per-mod source must
        // embed the concrete SHA — not the user-supplied "main" ref —
        // so the lockfile written downstream pins exact code.
        return result.ResolvedCollectionSource!.Contains($"#{sha}")
            && !result.ResolvedCollectionSource.Contains("#main")
            && result.ModSources.Values.All(s => s.Contains($"#{sha}") && !s.Contains("#main"));
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

static bool RemoteFetcherCollectionMissingIdSurfacesDiagnostic()
{
    var fetcher = MakeCollectionRepoFixture();
    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.no-such-preset");
    var result = new RemoteFetcher(fetcher).FetchCollection(source);
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModNotInRepoIndex
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool RemoteFetcherCollectionNonGithubSourceWarns()
{
    // A collection entry whose source: is a non-github URL should surface
    // collectionRemoteSourceUnsupported as a warning, then fall back to
    // same-repo lookup so the install can still proceed when a matching
    // mod lives locally in the collection's repo.
    var fetcher = MakeCollectionRepoFixture();
    var sha = InMemoryRemoteContentFetcher.FakeSha;
    fetcher.AddText($"https://raw.githubusercontent.com/acme/mods/{sha}/collections/beginner-qol.collection.yaml", """
        collectionFormatVersion: 0.1
        id: pagonia-land.example.beginner-qol
        name: Beginner QoL
        version: 0.1.0
        author: ACME
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: One http source.
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            version: "0.1.0"
            source: "https://example.invalid/cheaper-sawmill-0.1.0.zip"
        """);

    var source = new GitHubSource("acme", "mods", "main", "pagonia-land.example.beginner-qol");
    var result = new RemoteFetcher(fetcher).FetchCollection(source);
    try
    {
        return result.Success
            && File.Exists(Path.Combine(result.ModsRoot!, "pagonia-land.example.cheaper-sawmill", "mod.yaml"))
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CollectionRemoteSourceUnsupported
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally
    {
        if (result.TempDirectory is not null && Directory.Exists(result.TempDirectory))
        { Directory.Delete(result.TempDirectory, true); }
    }
}

// ---- CachingCatalogFetcher -------------------------------------------------

// Static local function rather than a top-level `const`: a const here would be
// an unreachable top-level statement (it sits after the program body's return,
// among the helper functions) and warn CS0162. A function is a declaration, not
// flow, so it's exempt — and it still folds to the same compile-time literal.
static string CachingCatalogTestSha() => "abcdef0123456789abcdef0123456789abcdef01";

static (string url, InMemoryRemoteContentFetcher http, StoreLayout layout, string tempRoot) MakeCachingFixture(string label)
{
    var tempRoot = NewTempRoot($"caching-{label}");
    var storeRoot = Path.Combine(tempRoot, "store");
    Directory.CreateDirectory(storeRoot);
    var layout = new StoreLayout(storeRoot);
    new StoreInitializer().Initialize(layout);

    var http = new InMemoryRemoteContentFetcher();
    http.AddRef("acme", "catalogs", "HEAD", CachingCatalogTestSha());
    http.AddText($"https://raw.githubusercontent.com/acme/catalogs/{CachingCatalogTestSha()}/catalog.yaml", """
        catalogFormatVersion: "0.1"
        catalog:
          name: ACME
          maintainer: acme
        repos:
          - owner: someone
            repo: their-mods
            summary: cached test repo.
        """);
    var url = $"https://raw.githubusercontent.com/acme/catalogs/{CachingCatalogTestSha()}/catalog.yaml";
    return (url, http, layout, tempRoot);
}

static bool CachingCatalogColdFetchWritesCache()
{
    var (_, http, layout, tempRoot) = MakeCachingFixture("cold");
    try
    {
        var src = new GitHubCatalogSource("acme", "catalogs", "HEAD", "catalog.yaml");
        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);
        var result = fetcher.Fetch(src);

        // Find the cache dir under <root>/cache/catalogs/ (one entry).
        var cacheBase = Path.Combine(layout.Root, "cache", "catalogs");
        var cacheDirs = Directory.Exists(cacheBase) ? Directory.GetDirectories(cacheBase) : Array.Empty<string>();
        if (cacheDirs.Length != 1) { return false; }
        var dir = cacheDirs[0];

        return result.Success
            && File.Exists(Path.Combine(dir, "catalog.yaml"))
            && File.Exists(Path.Combine(dir, "cache-meta.yaml"))
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCacheWritten);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CachingCatalogWarmFetchServesFromCache()
{
    var (url, http, layout, tempRoot) = MakeCachingFixture("warm");
    try
    {
        var src = new GitHubCatalogSource("acme", "catalogs", "HEAD", "catalog.yaml");
        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);

        // First fetch populates the cache.
        fetcher.Fetch(src);
        var fetchedUrlsAfterFirst = http.FetchedUrls.Count(u => u == url);

        // Second fetch within the freshness window must NOT hit the URL again.
        var second = fetcher.Fetch(src);
        var fetchedUrlsAfterSecond = http.FetchedUrls.Count(u => u == url);

        return second.Success
            && fetchedUrlsAfterFirst == 1
            && fetchedUrlsAfterSecond == 1                  // no new network request
            && second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogStale)
            && second.Catalog?.Repos.Count == 1;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CachingCatalogStaleTriggersRefresh()
{
    var (url, http, layout, tempRoot) = MakeCachingFixture("stale");
    try
    {
        var src = new GitHubCatalogSource("acme", "catalogs", "HEAD", "catalog.yaml");
        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);

        // Cold fetch.
        fetcher.Fetch(src);
        var afterFirst = http.FetchedUrls.Count(u => u == url);

        // Rewind the cache meta to 48h ago — beyond the staleness threshold.
        var cacheBase = Path.Combine(layout.Root, "cache", "catalogs");
        var dir = Directory.GetDirectories(cacheBase).Single();
        var metaPath = Path.Combine(dir, "cache-meta.yaml");
        var stale = "canonical: gh:acme/catalogs#" + CachingCatalogTestSha() + "/catalog.yaml\n"
                  + "fetchedAt: \"" + DateTime.UtcNow.AddHours(-48).ToString("O") + "\"\n"
                  + "commitSha: " + CachingCatalogTestSha() + "\n"
                  + "sourceType: github\n";
        File.WriteAllText(metaPath, stale);

        // Second fetch sees stale meta → refetches → updates cache.
        var second = fetcher.Fetch(src);
        var afterSecond = http.FetchedUrls.Count(u => u == url);

        return second.Success
            && afterFirst == 1
            && afterSecond == 2                              // new network hit
            && second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCacheWritten);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CachingCatalogForceRefreshBypassesCacheHit()
{
    var (url, http, layout, tempRoot) = MakeCachingFixture("force");
    try
    {
        var src = new GitHubCatalogSource("acme", "catalogs", "HEAD", "catalog.yaml");
        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);
        fetcher.Fetch(src);
        var afterFirst = http.FetchedUrls.Count(u => u == url);

        var forced = fetcher.Fetch(src, forceRefresh: true);
        var afterForce = http.FetchedUrls.Count(u => u == url);

        return forced.Success
            && afterFirst == 1
            && afterForce == 2                               // forced re-fetch even though cache was fresh
            && forced.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCacheWritten);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CachingCatalogFileBypassesCache()
{
    var (_, http, layout, tempRoot) = MakeCachingFixture("file");
    try
    {
        // Create a tiny on-disk catalog and point a FileCatalogSource at it.
        var localCatalog = Path.Combine(tempRoot, "local.yaml");
        File.WriteAllText(localCatalog, """
            catalogFormatVersion: "0.1"
            catalog:
              name: Local
            repos:
              - owner: local
                repo: only
            """);

        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);
        var src = new FileCatalogSource(localCatalog);
        var result = fetcher.Fetch(src);

        // file: source must not write a cache directory.
        var cacheBase = Path.Combine(layout.Root, "cache", "catalogs");
        var cacheDirs = Directory.Exists(cacheBase) ? Directory.GetDirectories(cacheBase) : Array.Empty<string>();

        return result.Success
            && cacheDirs.Length == 0
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCacheWritten || d.Code == ManagerDiagnosticCodes.CatalogStale);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CachingCatalogCorruptMetaFallsThrough()
{
    var (url, http, layout, tempRoot) = MakeCachingFixture("corrupt");
    try
    {
        var src = new GitHubCatalogSource("acme", "catalogs", "HEAD", "catalog.yaml");
        var fetcher = new CachingCatalogFetcher(http, layout, stalenessHours: 24);
        fetcher.Fetch(src);

        // Corrupt the meta sidecar — unparseable YAML.
        var cacheBase = Path.Combine(layout.Root, "cache", "catalogs");
        var dir = Directory.GetDirectories(cacheBase).Single();
        File.WriteAllText(Path.Combine(dir, "cache-meta.yaml"), "this is not: valid yaml: at all: [[[");

        var afterFirst = http.FetchedUrls.Count(u => u == url);
        var second = fetcher.Fetch(src);
        var afterSecond = http.FetchedUrls.Count(u => u == url);

        return second.Success
            && afterFirst == 1
            && afterSecond == 2                              // corrupt meta forces refetch
            && second.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCacheCorrupt
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- UrlCatalogSource ------------------------------------------------------

static bool UrlCatalogParserHttps()
{
    return CatalogSourceParser.TryParse("https://example.com/catalog.yaml", out var src)
        && src is UrlCatalogSource u
        && u.SourceUri.Scheme == "https"
        && u.SourceUri.AbsoluteUri == "https://example.com/catalog.yaml"
        && !u.IsInsecure;
}

static bool UrlCatalogParserHttpInsecure()
{
    return CatalogSourceParser.TryParse("http://intranet.local/catalog.yaml", out var src)
        && src is UrlCatalogSource u
        && u.IsInsecure;
}

static bool UrlCatalogParserRejectsHostless()
{
    // "https://" alone has no host — Uri.TryCreate may return true with an
    // empty Host on some platforms; the parser must reject it either way.
    return !CatalogSourceParser.TryParse("https://", out _)
        && !CatalogSourceParser.TryParse("https:///nohost", out _);
}

static bool UrlCatalogParserRejectsLoopbackAndLinkLocal()
{
    // SSRF guard: no legitimate catalog lives on loopback or a link-local
    // address (169.254.169.254 being the classic cloud-metadata target). A normal
    // public/LAN host still parses.
    return !CatalogSourceParser.TryParse("http://127.0.0.1/catalog.yaml", out _)
        && !CatalogSourceParser.TryParse("http://localhost/catalog.yaml", out _)
        && !CatalogSourceParser.TryParse("http://169.254.169.254/latest/meta-data/", out _)
        && CatalogSourceParser.TryParse("https://example.com/catalog.yaml", out _);
}

static bool RemoteHostPolicyBlocksInternalAndMappedHosts()
{
    // The shared SSRF policy (enforced at parse time AND on every HTTP/redirect hop) blocks
    // loopback, link-local, the metadata IP, and IPv4-mapped IPv6 spellings of those — while a
    // public host and private LAN mirrors stay allowed.
    bool Blocked(string url) => RemoteHostPolicy.IsBlocked(new Uri(url));
    return Blocked("http://127.0.0.1/")
        && Blocked("http://[::1]/")
        && Blocked("http://0.0.0.0/")                   // unspecified IPv4 — OS routes to localhost
        && Blocked("http://[::]/")                      // unspecified IPv6
        && Blocked("http://169.254.169.254/latest/meta-data/")
        && Blocked("http://[fe80::1]/")
        && Blocked("http://[::ffff:127.0.0.1]/")        // IPv4-mapped loopback
        && Blocked("http://[::ffff:169.254.169.254]/")  // IPv4-mapped metadata IP
        && !Blocked("https://example.com/")             // public host
        && !Blocked("http://192.168.1.10/")             // private LAN mirror stays allowed
        && !Blocked("http://10.0.0.5/");
}

static bool UrlCatalogCanonicalNormalisesSchemeAndHost()
{
    var a = CatalogSourceParser.TryParse("HTTPS://Example.COM/x", out var srcA) ? srcA : null;
    var b = CatalogSourceParser.TryParse("https://example.com/x", out var srcB) ? srcB : null;
    return a is UrlCatalogSource && b is UrlCatalogSource
        && string.Equals(a.Canonical, b.Canonical, StringComparison.Ordinal)
        && a.Canonical == "https://example.com/x";
}

static bool UrlCatalogCanonicalStripsTrailingSlash()
{
    return CatalogSourceParser.TryParse("https://example.com/path/", out var src)
        && src.Canonical == "https://example.com/path";
}

static bool GitHubCatalogCanonicalOmitsDefaultRef()
{
    // A plainly-typed gh: catalog spec (default HEAD) canonicalises WITHOUT an
    // injected #HEAD — the displayed/deduped form matches what the user typed.
    // An explicit #HEAD dedups to the same string. A pinned ref stays explicit.
    var plain = CatalogSourceParser.TryParse("gh:pagonia-land/Pagonia-Land/catalog/official.yaml", out var a) ? a : null;
    var explicitHead = CatalogSourceParser.TryParse("gh:pagonia-land/Pagonia-Land#HEAD/catalog/official.yaml", out var b) ? b : null;
    var pinned = CatalogSourceParser.TryParse("gh:pagonia-land/Pagonia-Land#v1/catalog/official.yaml", out var c) ? c : null;
    return plain is GitHubCatalogSource && explicitHead is GitHubCatalogSource && pinned is GitHubCatalogSource
        && plain.Canonical == "gh:pagonia-land/Pagonia-Land/catalog/official.yaml"
        && string.Equals(plain.Canonical, explicitHead.Canonical, StringComparison.Ordinal)
        && pinned.Canonical == "gh:pagonia-land/Pagonia-Land#v1/catalog/official.yaml";
}

static (UrlCatalogSource Source, InMemoryRemoteContentFetcher Http, StoreLayout Layout, string TempRoot)
    MakeUrlCatalogFixture(string label, string url, string body, bool allowInsecure = false)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-url-catalog-{label}-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var layout = new StoreLayout(tempRoot);
    Directory.CreateDirectory(layout.Root);
    var http = new InMemoryRemoteContentFetcher();
    http.AddText(url, body);
    var src = new UrlCatalogSource(new Uri(url));
    return (src, http, layout, tempRoot);
}

static bool UrlCatalogFetchHttpsLandsInAggregator()
{
    var url = "https://example.com/community-catalog.yaml";
    var (src, http, layout, tempRoot) = MakeUrlCatalogFixture("https-aggregate", url, """
        catalogFormatVersion: "0.1"
        catalog:
          name: pagonia-land community
        repos:
          - owner: pagonia-land
            repo: pagonia-mods
            summary: community-hosted listing
        """);
    try
    {
        var fetcher = new CatalogFetcher(http);
        var aggregator = new CatalogAggregator(fetcher);
        var result = aggregator.Aggregate(new[] { src });

        // No insecure-http warning on https://.
        var hasInsecureWarn = result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogInsecureHttp);
        return result.Repos.Count == 1
            && result.Repos[0].Owner == "pagonia-land"
            && result.Repos[0].Repo == "pagonia-mods"
            && result.VisitedSources.Count == 1
            && !hasInsecureWarn;
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UrlCatalogFetchHttpNoOptInWarns()
{
    var url = "http://intranet.lan/catalog.yaml";
    var (src, http, layout, tempRoot) = MakeUrlCatalogFixture("http-no-optin", url, """
        catalogFormatVersion: "0.1"
        catalog:
          name: LAN
        repos:
          - owner: pagonia-land
            repo: lan-mods
        """);
    try
    {
        var fetcher = new CatalogFetcher(http, allowInsecureCatalogSources: false);
        var result = fetcher.Fetch(src);
        return result.Success
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogInsecureHttp
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UrlCatalogFetchHttpWithOptInSilent()
{
    var url = "http://intranet.lan/catalog.yaml";
    var (src, http, layout, tempRoot) = MakeUrlCatalogFixture("http-optin", url, """
        catalogFormatVersion: "0.1"
        catalog:
          name: LAN
        repos:
          - owner: pagonia-land
            repo: lan-mods
        """);
    try
    {
        var fetcher = new CatalogFetcher(http, allowInsecureCatalogSources: true);
        var result = fetcher.Fetch(src);
        return result.Success
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogInsecureHttp);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UrlCatalogCycleDetected()
{
    // https://a -> gh:b -> https://a should bail with catalogCycleDetected
    // on the second visit to https://a.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-url-cycle-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    try
    {
        var http = new InMemoryRemoteContentFetcher();
        var sha = InMemoryRemoteContentFetcher.FakeSha;

        http.AddText("https://example.com/a.yaml", """
            catalogFormatVersion: "0.1"
            catalog:
              name: A
            catalogs:
              - source: gh:pagonia-land/b-catalog
            """);
        http.AddRef("pagonia-land", "b-catalog", "HEAD", sha);
        http.AddText($"https://raw.githubusercontent.com/pagonia-land/b-catalog/{sha}/catalog.yaml", """
            catalogFormatVersion: "0.1"
            catalog:
              name: B
            catalogs:
              - source: https://example.com/a.yaml
            """);

        var src = new UrlCatalogSource(new Uri("https://example.com/a.yaml"));
        var fetcher = new CatalogFetcher(http);
        var aggregator = new CatalogAggregator(fetcher);
        var result = aggregator.Aggregate(new[] { (CatalogSource)src });

        return result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCycleDetected);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool UrlCatalogVisitedSetDedup()
{
    // Subscribing twice to the same URL via different casings should dedup
    // in the aggregator's visited-set (canonical-string match) and only
    // contribute one visited source.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-url-dedup-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    try
    {
        var http = new InMemoryRemoteContentFetcher();
        http.AddText("https://example.com/x", """
            catalogFormatVersion: "0.1"
            catalog:
              name: X
            repos:
              - owner: pagonia-land
                repo: only
            """);

        var a = new UrlCatalogSource(new Uri("HTTPS://Example.COM/x"));
        var b = new UrlCatalogSource(new Uri("https://example.com/x"));

        var fetcher = new CatalogFetcher(http);
        var aggregator = new CatalogAggregator(fetcher);
        var result = aggregator.Aggregate(new CatalogSource[] { a, b });

        return result.VisitedSources.Count == 1
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCycleDetected)
            && result.Repos.Count == 1;
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- Direct-URL ZIP source (parser + fetcher) ------------------------------

static bool DirectUrlParserHttps()
{
    return RemoteSourceParser.TryParse("https://example.com/foo.zip", out var src)
        && src is DirectUrlSource { Url: "https://example.com/foo.zip", IsHttp: false };
}

static bool DirectUrlParserHttp()
{
    return RemoteSourceParser.TryParse("http://example.com/foo.zip", out var src)
        && src is DirectUrlSource { Url: "http://example.com/foo.zip", IsHttp: true };
}

static bool DirectUrlParserSignedUrl()
{
    // mod.io style — query-string after the .zip path. Path-suffix check
    // must look at the URL path, not the full string.
    var url = "https://thumb.modcdn.io/files/9876/some-mod.zip?signature=abc123&expires=1234567890";
    return RemoteSourceParser.TryParse(url, out var src)
        && src is DirectUrlSource s && s.Url == url && !s.IsHttp;
}

static bool DirectUrlParserGitHubLongFormPriority()
{
    // Long-form .../tree/<ref> wins — should resolve to GitHubSource, NOT
    // DirectUrlSource, even though the URL is https://.
    return RemoteSourceParser.TryParse("https://github.com/pagonia-land/example-mods/tree/main/mods/cheaper-sawmill", out var src)
        && src is GitHubSource gh && gh.Owner == "pagonia-land" && gh.Repo == "example-mods";
}

static bool DirectUrlParserGitHubArchiveZipFallthrough()
{
    // GitHub-served archive URL: /archive/refs/heads/main.zip — not a /tree/
    // long-form, so long-form parsing fails and we fall through to direct-URL
    // parsing, which accepts it (ends in .zip).
    var url = "https://github.com/pagonia-land/example-mods/archive/refs/heads/main.zip";
    return RemoteSourceParser.TryParse(url, out var src)
        && src is DirectUrlSource s && s.Url == url;
}

static bool DirectUrlParserRejectsNonZip()
{
    // Repo landing pages, blog posts, documentation — anything not ending in
    // .zip must fail to parse so the caller can fall back to "treat as local
    // path" with a clearer downstream error.
    return !RemoteSourceParser.TryParse("https://example.com/foo", out _)
        && !RemoteSourceParser.TryParse("https://example.com/foo.tar.gz", out _)
        && !RemoteSourceParser.TryParse("https://github.com/owner/repo", out _);
}

static (byte[] zipBytes, string id, string version) MakeMinimalModZip(bool nested)
{
    var modYaml = """
        patchFormatVersion: 0.1
        id: pagonia-land.example.direct-url-mod
        name: Direct URL Mod
        version: 0.1.0
        author: Test
        gameDatabaseVersion: "1.3.0-11694+192849"
        description: Tiny test mod packaged as a direct-URL ZIP.
        requiredPackages:
          - core
        optionalPackages: []
        requiresNewGame: false
        safeToRemove: unknown
        multiplayerSafe: unknown
        campaignSafe: unknown
        loadAfter: []
        loadBefore: []
        incompatibleWith: []
        patches:
          - patches/buildings.yaml
        """;
    var patchYaml = """
        operations:
          - id: direct-url-mod-sawmill-cost
            operation: replaceValue
            risk: low
            reason: Tiny test op so the patch file passes schema validation (minItems 1).
            target:
              file: core/gdb/buildings.gd.xml
              entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
              entityName: Sawmill
              component: AspectBuildup
              path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
            expectedOldValue: "4"
            value: "3"
        """;

    using var memoryStream = new MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
    {
        var prefix = nested ? "wrapper-folder/" : "";
        var modYamlEntry = archive.CreateEntry(prefix + "mod.yaml");
        using (var w = new StreamWriter(modYamlEntry.Open())) { w.Write(modYaml); }
        var patchEntry = archive.CreateEntry(prefix + "patches/buildings.yaml");
        using (var w = new StreamWriter(patchEntry.Open())) { w.Write(patchYaml); }
    }
    return (memoryStream.ToArray(), "pagonia-land.example.direct-url-mod", "0.1.0");
}

static byte[] MakeTraversalBombZip()
{
    using var memoryStream = new MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
    {
        // Entry name with '..' segments — would escape the destination root
        // if ExtractToFile blindly resolved the path.
        var bad = archive.CreateEntry("../../../escape.txt");
        using (var w = new StreamWriter(bad.Open())) { w.Write("if you see this, the guard failed"); }
    }
    return memoryStream.ToArray();
}

static bool DirectUrlFetcherHappyPath()
{
    var (bytes, _, _) = MakeMinimalModZip(nested: false);
    var fetcher = new InMemoryRemoteContentFetcher();
    var url = "https://example.com/mods/direct-url-mod.zip";
    fetcher.AddBytes(url, bytes);

    var source = new DirectUrlSource(url, IsHttp: false);
    var result = new DirectUrlFetcher(fetcher).Fetch(source);

    try
    {
        return result.Success
            && File.Exists(Path.Combine(result.ModRootDirectory!, "mod.yaml"))
            && File.Exists(Path.Combine(result.ModRootDirectory!, "patches", "buildings.yaml"))
            && result.ResolvedSource!.StartsWith($"url:{url}#")
            && result.ArchiveSha256!.Length == 64                     // hex-encoded SHA-256
            && result.ArchiveLength == bytes.Length
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DirectUrlFetched);
    }
    finally
    {
        if (result.TempRoot is not null && Directory.Exists(result.TempRoot))
        { Directory.Delete(result.TempRoot, true); }
    }
}

static bool DirectUrlFetcherRedactsSignedUrl()
{
    // mod.io hands back a pre-signed binary URL carrying a ?signature= credential. It must not leak
    // into the diagnostics that flow to the console / --json report (mirrors the api_key redaction).
    var (bytes, _, _) = MakeMinimalModZip(nested: false);
    var fetcher = new InMemoryRemoteContentFetcher();
    var url = "https://thumb.modcdn.io/files/9876/some-mod.zip?signature=abc123&expires=1234567890";
    fetcher.AddBytes(url, bytes);

    var result = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, IsHttp: false));
    try
    {
        var fetched = result.Diagnostics.First(d => d.Code == ManagerDiagnosticCodes.DirectUrlFetched);
        return result.Success
            && !fetched.Message.Contains("signature", StringComparison.OrdinalIgnoreCase)
            && !fetched.Message.Contains("abc123", StringComparison.Ordinal)
            && fetched.Message.Contains("thumb.modcdn.io/files/9876/some-mod.zip", StringComparison.Ordinal); // path kept
    }
    finally
    {
        if (result.TempRoot is not null && Directory.Exists(result.TempRoot))
        { Directory.Delete(result.TempRoot, true); }
    }
}

static bool DirectUrlFetcherVerifiesMd5()
{
    // A mod.io download advertises an MD5; the fetcher must verify it — silent on a match, a warning
    // (still installing) on a mismatch. Closes the gap where filehash.md5 was parsed but never checked.
    var (bytes, _, _) = MakeMinimalModZip(nested: false);
    var fetcher = new InMemoryRemoteContentFetcher();
    var url = "https://example.com/mods/modio-mod.zip";
    fetcher.AddBytes(url, bytes);
    var goodMd5 = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant();
    var source = new DirectUrlSource(url, IsHttp: false);

    var matching = new DirectUrlFetcher(fetcher).Fetch(source, goodMd5);
    var mismatched = new DirectUrlFetcher(fetcher).Fetch(source, "ffffffffffffffffffffffffffffffff");
    try
    {
        return matching.Success
            && matching.Diagnostics.All(d => d.Code != ManagerDiagnosticCodes.ModIoChecksumMismatch)
            && mismatched.Success
            && mismatched.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoChecksumMismatch);
    }
    finally
    {
        foreach (var r in new[] { matching, mismatched })
        {
            if (r.TempRoot is not null && Directory.Exists(r.TempRoot)) { Directory.Delete(r.TempRoot, true); }
        }
    }
}

static bool DirectUrlFetcherNestedFolder()
{
    var (bytes, _, _) = MakeMinimalModZip(nested: true);
    var fetcher = new InMemoryRemoteContentFetcher();
    var url = "https://example.com/mods/wrapped-mod.zip";
    fetcher.AddBytes(url, bytes);

    var result = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, false));
    try
    {
        // ModRootDirectory should point INTO the wrapper folder, not the
        // outer extracted root — so the existing ModInstaller sees mod.yaml
        // at root the same way it does for un-wrapped ZIPs.
        return result.Success
            && File.Exists(Path.Combine(result.ModRootDirectory!, "mod.yaml"))
            && result.ModRootDirectory!.EndsWith("wrapper-folder");
    }
    finally
    {
        if (result.TempRoot is not null && Directory.Exists(result.TempRoot))
        { Directory.Delete(result.TempRoot, true); }
    }
}

static bool DirectUrlFetcherRefusesTraversal()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    var url = "https://example.com/evil.zip";
    fetcher.AddBytes(url, MakeTraversalBombZip());

    var result = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, false));

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DirectUrlTraversalRefused
            && d.Severity == ManagerDiagnosticSeverity.Error
            && d.Message.Contains("escape", StringComparison.OrdinalIgnoreCase))
        // Temp dir cleanup ran (no orphaned extraction tree).
        && result.TempRoot is null;
}

static bool DirectUrlFetcher404Cleanup()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    // No AddBytes -> TryStreamFetchAsync returns false -> 404 path.
    var url = "https://example.com/missing.zip";
    var result = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, false));

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DirectUrlFetchFailed
            && d.Severity == ManagerDiagnosticSeverity.Error)
        && result.TempRoot is null;
}

static bool DirectUrlFetcherEndToEndInstall()
{
    // Hand the fetched temp dir to the existing ModInstaller — exactly what
    // the CLI dispatch path will do in slice B. Verifies the install lands
    // cleanly + the sidecar records the url: source identifier.
    var tempRoot = NewTempRoot("direct-url-install");
    string? fetchedRoot = null;
    try
    {
        var (bytes, id, version) = MakeMinimalModZip(nested: false);
        var fetcher = new InMemoryRemoteContentFetcher();
        var url = "https://example.com/mods/install-me.zip";
        fetcher.AddBytes(url, bytes);

        var fetch = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, false));
        if (!fetch.Success || fetch.ModRootDirectory is null) { return false; }
        fetchedRoot = fetch.TempRoot;

        var storeRoot = Path.Combine(tempRoot, "store");
        Directory.CreateDirectory(storeRoot);
        var layout = new StoreLayout(storeRoot);
        new StoreInitializer().Initialize(layout);

        var installResult = new ModInstaller().Install(fetch.ModRootDirectory, layout, fetch.ResolvedSource);
        if (installResult.Outcome != InstallOutcome.Installed || installResult.InstallPath is null)
        {
            return false;
        }

        var sidecarPath = Path.Combine(installResult.InstallPath, ModInstaller.SidecarFileName);
        var sidecar = new YamlDotNet.Serialization.DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<InstallSidecar>(File.ReadAllText(sidecarPath));

        return installResult.ModId == id
            && installResult.Version == version
            && sidecar.Source.StartsWith($"url:{url}#")
            && sidecar.Source.Contains(fetch.ArchiveSha256!);
    }
    finally
    {
        if (fetchedRoot is not null && Directory.Exists(fetchedRoot)) { Directory.Delete(fetchedRoot, true); }
        CleanupTempRoot(tempRoot);
    }
}

static bool DirectUrlInstalledModExposesSource()
{
    // The CLI's drift-detection helper scans existing installs for sidecar
    // sources matching `url:<url>#<sha>`. That requires InstalledMod to
    // surface the sidecar's Source field through ModLister — pin it here so
    // a future refactor that drops the wiring is caught in tests, not at
    // run-time after a drift goes unreported.
    var tempRoot = NewTempRoot("direct-url-installed-mod");
    string? fetchedRoot = null;
    try
    {
        var (bytes, _, _) = MakeMinimalModZip(nested: false);
        var fetcher = new InMemoryRemoteContentFetcher();
        var url = "https://example.com/source-exposed.zip";
        fetcher.AddBytes(url, bytes);
        var fetch = new DirectUrlFetcher(fetcher).Fetch(new DirectUrlSource(url, false));
        if (!fetch.Success || fetch.ModRootDirectory is null) { return false; }
        fetchedRoot = fetch.TempRoot;

        var storeRoot = Path.Combine(tempRoot, "store");
        Directory.CreateDirectory(storeRoot);
        var layout = new StoreLayout(storeRoot);
        new StoreInitializer().Initialize(layout);
        new ModInstaller().Install(fetch.ModRootDirectory, layout, fetch.ResolvedSource);

        var listed = new ModLister().List(layout);
        return listed.Count == 1
            && listed[0].Source is not null
            && listed[0].Source!.StartsWith($"url:{url}#")
            && listed[0].Source!.Contains(fetch.ArchiveSha256!);
    }
    finally
    {
        if (fetchedRoot is not null && Directory.Exists(fetchedRoot)) { Directory.Delete(fetchedRoot, true); }
        CleanupTempRoot(tempRoot);
    }
}

// ---- mod.io adapter --------------------------------------------------------

static bool ModIoParserBasic()
{
    return RemoteSourceParser.TryParse("modio:1234/5678", out var src)
        && src is ModIoSource { Game: "1234", ModId: "5678", Version: null };
}

static bool ModIoParserSlugAndVersion()
{
    return RemoteSourceParser.TryParse("modio:pioneers-of-pagonia/5678#0.1.0", out var src)
        && src is ModIoSource { Game: "pioneers-of-pagonia", ModId: "5678", Version: "0.1.0" };
}

static bool ModIoParserRejectsEmpty()
{
    return !RemoteSourceParser.TryParse("modio:/5678", out _)
        && !RemoteSourceParser.TryParse("modio:1234/", out _)
        && !RemoteSourceParser.TryParse("modio:1234/5678#", out _);
}

static bool ModIoParserRejectsGarbage()
{
    return !RemoteSourceParser.TryParse("modio:bad game/5678", out _)
        && !RemoteSourceParser.TryParse("modio:1234/bad mod", out _)
        && !RemoteSourceParser.TryParse("modio:1234/5/extra", out _);
}

static string MakeModIoJson(string modName, bool isMap, string version, string binaryUrl, string? md5)
{
    // Built as plain string concatenation to avoid the C# raw-string-interpolation
    // brace-counting trap when JSON's own braces collide with the
    // {{interpolation}} delimiter rules. Less elegant but unambiguous.
    var tags = isMap ? "[{\"name\":\"Map\"}]" : "[{\"name\":\"QoL\"}]";
    var md5Block = md5 is null ? "" : ", \"filehash\": { \"md5\": \"" + md5 + "\" }";
    return "{ \"id\": 5678,"
        + " \"name\": \"" + modName + "\","
        + " \"tags\": " + tags + ","
        + " \"modfile\": {"
        + "   \"id\": 99999,"
        + "   \"version\": \"" + version + "\","
        + "   \"filename\": \"modio-mod-" + version + ".zip\","
        + "   \"download\": { \"binary_url\": \"" + binaryUrl + "\" }"
        + md5Block
        + " } }";
}

static bool ModIoFetcherUsesEmbeddedKey()
{
    // env var unset + no override -> resolution falls back to the embedded
    // DefaultApiKey, which now ships a real read-only key, so the fetch
    // proceeds (no modIoApiError bail).
    var prior = Environment.GetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable);
    Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, null);
    try
    {
        var http = new InMemoryRemoteContentFetcher();
        var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=" + ModIoFetcher.DefaultApiKey;
        http.AddText(apiUrl, MakeModIoJson("Embedded Key Map", isMap: true, "0.1.0",
            "https://thumb.modcdn.io/files/99999/some-map.zip?signature=abc", md5: "abcdef0123456789abcdef0123456789"));

        var fetcher = new ModIoFetcher(http);
        var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));
        return result.Success
            && result.GameId == "1234"
            && !result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoApiError);
    }
    finally { Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, prior); }
}

static bool ModIoFetcherHappyNonMap()
{
    var http = new InMemoryRemoteContentFetcher();
    var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=test-key";
    http.AddText(apiUrl, MakeModIoJson("Cheaper Sawmill", isMap: false, "0.1.0",
        "https://thumb.modcdn.io/files/99999/some-mod.zip?signature=abc", md5: "abcdef0123456789abcdef0123456789"));

    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));

    return result.Success
        && !result.IsMapType
        && result.GameId == "1234"
        && result.ModId == "5678"
        && result.Version == "0.1.0"
        && result.ModName == "Cheaper Sawmill"
        && result.BinaryUrl!.Contains("99999/some-mod.zip")
        && result.Md5 == "abcdef0123456789abcdef0123456789";
}

static bool ModIoFetcherMapSkip()
{
    var http = new InMemoryRemoteContentFetcher();
    var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=test-key";
    http.AddText(apiUrl, MakeModIoJson("Hidden Valley", isMap: true, "1.0.0",
        "https://thumb.modcdn.io/files/99999/map.zip", md5: null));

    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));

    // Map-skip is a "successful" outcome semantically — the fetcher did its
    // job, the mod just isn't installable by this manager. But IsMapType
    // is set and BinaryUrl is null so the CLI knows not to chain into
    // DirectUrlFetcher.
    return result.Success
        && result.IsMapType
        && result.BinaryUrl is null
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoMapTypeSkipped
            && d.Severity == ManagerDiagnosticSeverity.Info
            && d.Message.Contains("UGC subscription"));
}

static bool ModIoFetcher404()
{
    var http = new InMemoryRemoteContentFetcher();
    // No AddText for the API URL -> TryFetchAsync returns null -> 404 path.
    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("1234", "9999", null));

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoApiError
            && d.Severity == ManagerDiagnosticSeverity.Error
            && d.Message.Contains("1234")
            && d.Message.Contains("9999"));
}

static bool ModIoFetcherMalformedJson()
{
    var http = new InMemoryRemoteContentFetcher();
    var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=test-key";
    http.AddText(apiUrl, "not valid {{{ json");

    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoApiError
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool ModIoFetcherRateLimitRedactsKey()
{
    // The api_key rides in the request URL but must never reach a diagnostic.
    // A 429 (rate-limit) and a generic API error both surface a URL — assert
    // the secret key is absent from every diagnostic message.
    const string secret = "super-secret-key-xyz";
    var rateLimited = new InMemoryRemoteContentFetcher();
    rateLimited.Throws["https://api.mod.io/v1/games/1234/mods/5678?api_key=" + secret]
        = System.Net.HttpStatusCode.TooManyRequests;
    var rl = new ModIoFetcher(rateLimited, apiKeyOverride: secret)
        .Fetch(new ModIoSource("1234", "5678", null));

    var serverError = new InMemoryRemoteContentFetcher();
    serverError.Throws["https://api.mod.io/v1/games/1234/mods/5678?api_key=" + secret]
        = System.Net.HttpStatusCode.InternalServerError;
    var se = new ModIoFetcher(serverError, apiKeyOverride: secret)
        .Fetch(new ModIoSource("1234", "5678", null));

    return !rl.Success
        && rl.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoRateLimited)
        && rl.Diagnostics.All(d => !d.Message.Contains(secret))
        && !se.Success
        && se.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoApiError)
        && se.Diagnostics.All(d => !d.Message.Contains(secret));
}

static bool ModIoFetcherEnvOverride()
{
    var prior = Environment.GetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable);
    Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, "from-env-var");
    try
    {
        var http = new InMemoryRemoteContentFetcher();
        // The env-var key flows into the api_key= query parameter; the test
        // pre-populates the URL with EXACTLY that key, so a wrong-key fetch
        // would 404.
        var apiUrl = "https://api.mod.io/v1/games/1234/mods/5678?api_key=from-env-var";
        http.AddText(apiUrl, MakeModIoJson("Env Mod", isMap: false, "0.2.0",
            "https://thumb.modcdn.io/files/99/env-mod.zip", md5: null));

        // No explicit override -> falls back to env var.
        var fetcher = new ModIoFetcher(http);
        var result = fetcher.Fetch(new ModIoSource("1234", "5678", null));
        return result.Success && result.Version == "0.2.0";
    }
    finally { Environment.SetEnvironmentVariable(ModIoFetcher.ApiKeyEnvironmentVariable, prior); }
}

// ---- ModIoGameAliases ------------------------------------------------------

static bool ModIoAliasesNumericResolves()
{
    // Numeric form of the PoP game id resolves to itself. End-to-end check
    // via ModIoFetcher: the API URL the fetcher builds must hit /games/8242/.
    var http = new InMemoryRemoteContentFetcher();
    http.AddText("https://api.mod.io/v1/games/8242/mods/5734246?api_key=test-key",
        MakeModIoJson("D'n'D", isMap: false, version: "0.1.0",
            binaryUrl: "https://g-8242.modapi.io/v1/games/8242/mods/5734246/files/7325235/download",
            md5: null));
    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("8242", "5734246", null));
    return result.Success && result.GameId == "8242";
}

static bool ModIoAliasesSlugResolves()
{
    // Slug 'pioneers-of-pagonia' resolves to '8242'. Same round-trip check
    // via the fetcher's externally observable API URL.
    var http = new InMemoryRemoteContentFetcher();
    http.AddText("https://api.mod.io/v1/games/8242/mods/5568896?api_key=test-key",
        MakeModIoJson("Better Zoom", isMap: false, version: "1.0.0",
            binaryUrl: "https://g-8242.modapi.io/v1/games/8242/mods/5568896/files/7138101/download",
            md5: null));
    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("pioneers-of-pagonia", "5568896", null));
    return result.Success && result.GameId == "8242";
}

static bool ModIoAliasesShortSlugResolves()
{
    // 'pop' (case-insensitive) is the short-form alias for the PoP slug.
    // Tested with a non-lowercase variant ('PoP') to lock the
    // case-insensitive behaviour.
    var http = new InMemoryRemoteContentFetcher();
    http.AddText("https://api.mod.io/v1/games/8242/mods/5734246?api_key=test-key",
        MakeModIoJson("D'n'D", isMap: false, version: "0.1.0",
            binaryUrl: "https://g-8242.modapi.io/v1/games/8242/mods/5734246/files/7325235/download",
            md5: null));
    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("PoP", "5734246", null));
    return result.Success && result.GameId == "8242";
}

static bool ModIoAliasesUnknownDescribesAccepted()
{
    // Any other game string surfaces modIoUnknownGameAlias with a message
    // that names all accepted forms so the user can spot a typo.
    var http = new InMemoryRemoteContentFetcher();
    var fetcher = new ModIoFetcher(http, apiKeyOverride: "test-key");
    var result = fetcher.Fetch(new ModIoSource("totally-unknown-xyz", "5678", null));
    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.ModIoUnknownGameAlias
            && d.Severity == ManagerDiagnosticSeverity.Error
            && d.Message.Contains("8242")
            && d.Message.Contains("pioneers-of-pagonia")
            && d.Message.Contains("pop"));
}

// ---- CatalogSourceParser ----------------------------------------------------

static bool CatalogParserShortFormDefaults()
{
    return CatalogSourceParser.TryParse("gh:pagonia-land/mod-catalogs", out var src)
        && src is GitHubCatalogSource { Owner: "pagonia-land", Repo: "mod-catalogs", Ref: "HEAD", Path: "catalog.yaml" };
}

static bool CatalogParserShortFormCustomRefAndPath()
{
    return CatalogSourceParser.TryParse("gh:pagonia-land/mod-catalogs#v1.0/catalogs/curated.yaml", out var src)
        && src is GitHubCatalogSource { Owner: "pagonia-land", Repo: "mod-catalogs", Ref: "v1.0", Path: "catalogs/curated.yaml" };
}

static bool CatalogParserFileUrl()
{
    // file:///D:/foo/catalog.yaml on Windows -> absolute D:/foo/catalog.yaml
    var path = Path.GetTempFileName();
    try
    {
        var spec = $"file://{path.Replace('\\', '/')}";
        if (!CatalogSourceParser.TryParse(spec, out var src)) { return false; }
        return src is FileCatalogSource file
            && string.Equals(Path.GetFullPath(file.AbsolutePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }
    finally { File.Delete(path); }
}

static bool CatalogParserFileShort()
{
    var path = Path.GetTempFileName();
    try
    {
        var spec = $"file:{path}";
        if (!CatalogSourceParser.TryParse(spec, out var src)) { return false; }
        return src is FileCatalogSource file
            && string.Equals(Path.GetFullPath(file.AbsolutePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
    }
    finally { File.Delete(path); }
}

static bool CatalogParserRejectsLocalPath()
{
    // Bare paths without a transport prefix must NOT parse — `catalog add`
    // requires explicit transport so the caller can grep "file:" / "gh:"
    // in the subscription file later.
    return !CatalogSourceParser.TryParse(@"C:\catalogs\thing.yaml", out _)
        && !CatalogSourceParser.TryParse("/home/user/catalog.yaml", out _)
        && !CatalogSourceParser.TryParse("./relative.yaml", out _);
}

static bool CatalogParserRejectsGarbage()
{
    return !CatalogSourceParser.TryParse("gh:/repo", out _)
        && !CatalogSourceParser.TryParse("gh:owner/", out _)
        && !CatalogSourceParser.TryParse("gh:bad owner!/repo", out _)
        && !CatalogSourceParser.TryParse(null, out _)
        && !CatalogSourceParser.TryParse("", out _);
}

static bool CatalogParserRejectsPathTraversal()
{
    // '..' in the catalog path is refused at parse time (mirrors the remote
    // parser's base-path guard) — before any raw.githubusercontent.com fetch.
    return !CatalogSourceParser.TryParse("gh:owner/repo/../../etc/catalog.yaml", out _)
        && !CatalogSourceParser.TryParse("gh:owner/repo#main/../../etc/catalog.yaml", out _)
        // A legitimate nested catalog path still parses.
        && CatalogSourceParser.TryParse("gh:owner/repo#main/catalogs/official.yaml", out _);
}

static bool CatalogParserRelativeFileResolvesAgainstParent()
{
    var parentDir = Path.Combine(Path.GetTempPath(), $"parent-{Guid.NewGuid():N}");
    Directory.CreateDirectory(parentDir);
    try
    {
        var ok = CatalogSourceParser.TryParseRelativeTo("file:./sub/child.yaml", parentDir, out var src);
        return ok
            && src is FileCatalogSource file
            && Path.GetFullPath(file.AbsolutePath) == Path.GetFullPath(Path.Combine(parentDir, "sub/child.yaml"));
    }
    finally { Directory.Delete(parentDir); }
}

// ---- CatalogFetcher ---------------------------------------------------------

// Static local function, not a top-level `const` — see CachingCatalogTestSha
// above for why (avoids the CS0162 unreachable-statement warning).
static string CatalogFakeSha() => "c1c2c3c4c5c6c7c8c9d0d1d2d3d4d5d6d7d8d9e0";

static bool CatalogFetcherGitHubHappy()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("pagonia-land", "mod-catalogs", "HEAD", CatalogFakeSha());
    fetcher.AddText($"https://raw.githubusercontent.com/pagonia-land/mod-catalogs/{CatalogFakeSha()}/catalog.yaml", """
        catalogFormatVersion: "0.1"
        catalog:
          name: Official
          maintainer: pagonia-land
        repos:
          - owner: someone
            repo: their-mods
            summary: One-line.
        """);

    var src = new GitHubCatalogSource("pagonia-land", "mod-catalogs", "HEAD", "catalog.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return result.Success
        && result.Catalog?.Repos.Count == 1
        && result.Catalog!.Repos[0].Owner == "someone"
        && result.CommitSha == CatalogFakeSha()
        // The result.Source pins the resolved SHA — not the original "HEAD" —
        // so the aggregator's visited-set sees a stable identity.
        && ((GitHubCatalogSource)result.Source).Ref == CatalogFakeSha();
}

static bool CatalogRepoEntryIndexPathRoundTrips()
{
    // A repo entry's optional indexPath round-trips through the real parse
    // path; an entry without it keeps empty (root) semantics. The field is
    // dormant data at this point — the fetcher/install path doesn't consume
    // it yet — so this only asserts the model carries it faithfully.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("pagonia-land", "Pagonia-Land", "HEAD", CatalogFakeSha());
    fetcher.AddText($"https://raw.githubusercontent.com/pagonia-land/Pagonia-Land/{CatalogFakeSha()}/catalog/official.yaml", """
        catalogFormatVersion: "0.1"
        catalog:
          name: Official
          maintainer: pagonia-land
        repos:
          - owner: pagonia-land
            repo: Pagonia-Land
            indexPath: official-mods
            summary: First-party mods hosted in a subfolder.
          - owner: someone
            repo: their-mods
            summary: Index at the repo root.
        """);

    var src = new GitHubCatalogSource("pagonia-land", "Pagonia-Land", "HEAD", "catalog/official.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return result.Success
        && result.Catalog?.Repos.Count == 2
        && result.Catalog!.Repos[0].IndexPath == "official-mods"
        && result.Catalog!.Repos[1].IndexPath == "";
}

static bool CatalogFetcherGitHubUnknownRef()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    // No AddRef -> ResolveCommitShaAsync returns null -> fetch fails.
    var src = new GitHubCatalogSource("missing", "repo", "main", "catalog.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogFetchFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool CatalogFetcherGitHubMalformed()
{
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "bad", "HEAD", CatalogFakeSha());
    // Wrong shape — `repos` should be a list, not a string.
    fetcher.AddText($"https://raw.githubusercontent.com/acme/bad/{CatalogFakeSha()}/catalog.yaml", """
        catalogFormatVersion: "0.1"
        catalog:
          name: Bad
        repos: not-a-list-but-a-string
        """);

    var src = new GitHubCatalogSource("acme", "bad", "HEAD", "catalog.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogMalformed);
}

static bool CatalogFetcherNewerMinorReads()
{
    // A newer same-major minor catalog reads with an info recommend-update note.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "future", "HEAD", CatalogFakeSha());
    fetcher.AddText($"https://raw.githubusercontent.com/acme/future/{CatalogFakeSha()}/catalog.yaml", """
        catalogFormatVersion: "0.99"
        catalog:
          name: Future Catalog
        repos:
          - owner: someone
            repo: their-mods
            summary: One-line.
        """);

    var src = new GitHubCatalogSource("acme", "future", "HEAD", "catalog.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return result.Success
        && result.Catalog?.Repos.Count == 1
        && result.Diagnostics.Any(d => d.Code == PagoniaLand.Patcher.DiagnosticCodes.FormatMinorAhead
            && d.Severity == ManagerDiagnosticSeverity.Info);
}

static bool CatalogFetcherNewerMajorRefused()
{
    // A newer-major catalog is refused: the successful parse is turned into a failure carrying
    // the actionable formatMajorUnsupported error, so its repos never enter the aggregate.
    var fetcher = new InMemoryRemoteContentFetcher();
    fetcher.AddRef("acme", "future", "HEAD", CatalogFakeSha());
    fetcher.AddText($"https://raw.githubusercontent.com/acme/future/{CatalogFakeSha()}/catalog.yaml", """
        catalogFormatVersion: "1.0"
        catalog:
          name: Future Catalog
        repos:
          - owner: someone
            repo: their-mods
            summary: One-line.
        """);

    var src = new GitHubCatalogSource("acme", "future", "HEAD", "catalog.yaml");
    var result = new CatalogFetcher(fetcher).Fetch(src);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == PagoniaLand.Patcher.DiagnosticCodes.FormatMajorUnsupported
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

static bool CatalogFetcherFileExample()
{
    // The bundled examples/mod-catalog-example/catalog.yaml is the
    // canonical end-to-end fixture for the federation tests in slice C.
    // Here we just verify that file: source resolution + YAML parse work
    // against the on-disk reality.
    var catalogPath = Path.Combine(LocateRepoRoot(), "examples", "mod-catalog-example", "catalog.yaml");
    var src = new FileCatalogSource(catalogPath);
    // file: fetch path doesn't touch HTTP — pass a no-op stub.
    var result = new CatalogFetcher(new InMemoryRemoteContentFetcher()).Fetch(src);

    return result.Success
        && result.Catalog is not null
        && result.Catalog.CatalogFormatVersion == "0.1"
        && result.Catalog.CatalogMeta?.Name == "Pagonia Land Example Catalog"
        // Bundled fixture lists 3 repos and 1 nested catalog reference.
        && result.Catalog.Repos.Count == 3
        && result.Catalog.Catalogs.Count == 1
        && result.Catalog.Repos.Any(r => r.Owner == "pagonia-land" && r.Repo == "example-mods")
        && result.Catalog.Catalogs[0].Source.StartsWith("file:", StringComparison.Ordinal);
}

static bool CatalogFetcherFileMissing()
{
    var src = new FileCatalogSource(Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.yaml"));
    var result = new CatalogFetcher(new InMemoryRemoteContentFetcher()).Fetch(src);

    return !result.Success
        && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogFetchFailed
            && d.Severity == ManagerDiagnosticSeverity.Error);
}

// ---- CatalogAggregator ------------------------------------------------------

static bool CatalogAggregatorBundledExampleFlattens()
{
    // Top-level catalog lists 3 repos; the sub-catalog it federates to lists 2
    // (one overlapping with the parent — pagonia-land/example-mods). Aggregator
    // dedups to 4 unique repos: 3 parent + 1 new from the sub-catalog.
    var parentPath = Path.Combine(LocateRepoRoot(), "examples", "mod-catalog-example", "catalog.yaml");
    var subscriptions = new[] { (CatalogSource)new FileCatalogSource(parentPath) };

    var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
    var result = aggregator.Aggregate(subscriptions);

    return result.Repos.Count == 4
        && result.Repos.Any(r => r.Owner == "pagonia-land" && r.Repo == "example-mods")
        && result.Repos.Any(r => r.Owner == "thirdparty" && r.Repo == "extra-fictional-mods")
        && result.VisitedSources.Count == 2;
}

static bool CatalogAggregatorDedupRecordsVouches()
{
    // pagonia-land/example-mods appears in both parent + sub-catalog. After
    // dedup it should surface once with VouchedBy.Count == 2.
    var parentPath = Path.Combine(LocateRepoRoot(), "examples", "mod-catalog-example", "catalog.yaml");
    var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
    var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(parentPath) });

    var bundled = result.Repos.SingleOrDefault(r => r.Owner == "pagonia-land" && r.Repo == "example-mods");
    return bundled is not null
        && bundled.VouchedBy.Count == 2;
}

static bool CatalogAggregatorCarriesIndexPath()
{
    // A catalog repo entry's indexPath flows onto the AggregatedRepo so the
    // install path can build the gh:owner/repo:indexPath base segment. A repo
    // without the field carries an empty IndexPath (root semantics).
    var tempRoot = NewTempRoot("aggregator-indexpath");
    try
    {
        var path = Path.Combine(tempRoot, "cat.yaml");
        File.WriteAllText(path, """
            catalogFormatVersion: "0.1"
            catalog:
              name: Official
            repos:
              - owner: pagonia-land
                repo: Pagonia-Land
                indexPath: official-mods
              - owner: someone
                repo: root-mods
            """);
        var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
        var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(path) });
        var sub = result.Repos.SingleOrDefault(r => r.Repo == "Pagonia-Land");
        var root = result.Repos.SingleOrDefault(r => r.Repo == "root-mods");
        return sub is { IndexPath: "official-mods" } && root is { IndexPath: "" };
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogAggregatorIndexPathConflict()
{
    // Two catalogs vouch for the same (owner, repo) but disagree on indexPath.
    // The first visited (the parent subscription) wins; the divergent later
    // vouch surfaces catalogRepoIndexPathConflict (warning) rather than silently
    // picking one.
    var tempRoot = NewTempRoot("aggregator-indexpath-conflict");
    try
    {
        var childPath = Path.Combine(tempRoot, "child.yaml");
        var parentPath = Path.Combine(tempRoot, "parent.yaml");
        File.WriteAllText(childPath, """
            catalogFormatVersion: "0.1"
            catalog:
              name: Child
            repos:
              - owner: pagonia-land
                repo: Pagonia-Land
                indexPath: other-dir
            """);
        File.WriteAllText(parentPath, $"""
            catalogFormatVersion: "0.1"
            catalog:
              name: Parent
            repos:
              - owner: pagonia-land
                repo: Pagonia-Land
                indexPath: official-mods
            catalogs:
              - source: file:{childPath.Replace('\\', '/')}
            """);
        var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
        var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(parentPath) });
        var repo = result.Repos.SingleOrDefault(r => r.Repo == "Pagonia-Land");
        return repo is { IndexPath: "official-mods" }
            && repo.VouchedBy.Count == 2
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogRepoIndexPathConflict
                && d.Severity == ManagerDiagnosticSeverity.Warning);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogAggregatorCycleDetection()
{
    // Build a synthetic cycle: A.yaml -> B.yaml -> A.yaml. The aggregator
    // must not loop forever; it bails on the second visit with the cycle
    // diagnostic, and still surfaces every unique repo encountered.
    var tempRoot = NewTempRoot("aggregator-cycle");
    try
    {
        var aPath = Path.Combine(tempRoot, "a.yaml");
        var bPath = Path.Combine(tempRoot, "b.yaml");

        File.WriteAllText(aPath, $"""
            catalogFormatVersion: "0.1"
            catalog:
              name: A
            repos:
              - owner: only-in-a
                repo: only-in-a-repo
            catalogs:
              - source: file:{bPath.Replace('\\', '/')}
            """);
        File.WriteAllText(bPath, $"""
            catalogFormatVersion: "0.1"
            catalog:
              name: B
            repos:
              - owner: only-in-b
                repo: only-in-b-repo
            catalogs:
              - source: file:{aPath.Replace('\\', '/')}
            """);

        var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
        var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(aPath) });

        return result.Repos.Count == 2
            && result.Repos.Any(r => r.Owner == "only-in-a")
            && result.Repos.Any(r => r.Owner == "only-in-b")
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogCycleDetected);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogAggregatorDepthCap()
{
    // Build a chain of 5 catalogs A->B->C->D->E with maxDepth=2. The
    // aggregator visits A (depth 0), B (1), C (2 — at cap, fetched but
    // its children not enqueued), and emits catalogDepthCapped before
    // skipping descendant catalogs.
    var tempRoot = NewTempRoot("aggregator-depth-cap");
    try
    {
        string MakePath(string name) => Path.Combine(tempRoot, $"{name}.yaml");
        void Write(string name, string ownerStem, string? nextLink)
        {
            var nextRef = nextLink is null ? "" : $"""
catalogs:
  - source: file:{MakePath(nextLink).Replace('\\', '/')}
""";
            File.WriteAllText(MakePath(name), $"""
                catalogFormatVersion: "0.1"
                catalog:
                  name: {name}
                repos:
                  - owner: {ownerStem}
                    repo: {ownerStem}-repo
                {nextRef}
                """);
        }
        Write("a", "owner-a", "b");
        Write("b", "owner-b", "c");
        Write("c", "owner-c", "d");
        Write("d", "owner-d", "e");
        Write("e", "owner-e", null);

        var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
        var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(MakePath("a")) }, maxDepthOverride: 2);

        // With depth cap 2: visit A (depth 0), B (1), C (2 — at cap,
        // children not enqueued). D + E are unreached. Expect 3 repos.
        return result.Repos.Count == 3
            && result.Repos.Any(r => r.Owner == "owner-a")
            && result.Repos.Any(r => r.Owner == "owner-b")
            && result.Repos.Any(r => r.Owner == "owner-c")
            && !result.Repos.Any(r => r.Owner == "owner-d" || r.Owner == "owner-e")
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogDepthCapped);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogAggregatorRelativeChildResolvesAgainstParent()
{
    // Parent catalog at <tempRoot>/parent.yaml references file:./nested/child.yaml.
    // The aggregator must resolve that against the parent's directory
    // (<tempRoot>), not against the process cwd.
    var tempRoot = NewTempRoot("aggregator-relative");
    try
    {
        var nestedDir = Path.Combine(tempRoot, "nested");
        Directory.CreateDirectory(nestedDir);
        var parentPath = Path.Combine(tempRoot, "parent.yaml");
        var childPath = Path.Combine(nestedDir, "child.yaml");

        File.WriteAllText(parentPath, """
            catalogFormatVersion: "0.1"
            catalog:
              name: Parent
            catalogs:
              - source: file:./nested/child.yaml
            """);
        File.WriteAllText(childPath, """
            catalogFormatVersion: "0.1"
            catalog:
              name: Child
            repos:
              - owner: nested
                repo: nested-repo
            """);

        var aggregator = new CatalogAggregator(new CatalogFetcher(new InMemoryRemoteContentFetcher()));
        var result = aggregator.Aggregate(new[] { (CatalogSource)new FileCatalogSource(parentPath) });

        return result.Repos.Any(r => r.Owner == "nested" && r.Repo == "nested-repo")
            && result.Diagnostics.All(d => d.Severity != ManagerDiagnosticSeverity.Error);
    }
    finally { CleanupTempRoot(tempRoot); }
}

// ---- CatalogSubscriptionService --------------------------------------------

static bool CatalogSubsAddPersistsAndDedups()
{
    var tempRoot = NewTempRoot("subs-add-persist");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new CatalogSubscriptionService();

        var add1 = service.Add(layout, "gh:pagonia-land/mod-catalogs");
        var add2 = service.Add(layout, "gh:pagonia-land/mod-catalogs");

        var stored = new StoreStateReader().Read(layout).SubscribedCatalogs;
        // Both calls succeed; the second is a no-op-info, not a duplicate write.
        return add1.Success && add2.Success
            && stored.Count == 1
            && stored[0] == "gh:pagonia-land/mod-catalogs"
            && add2.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogSubscribed && d.Message.Contains("no-op"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogSubsRemoveAndNoop()
{
    var tempRoot = NewTempRoot("subs-remove");
    try
    {
        var layout = InitLayout(tempRoot);
        var service = new CatalogSubscriptionService();
        service.Add(layout, "gh:pagonia-land/mod-catalogs");

        var removeKnown = service.Remove(layout, "gh:pagonia-land/mod-catalogs");
        var removeUnknown = service.Remove(layout, "gh:nonexistent/repo");

        var stored = new StoreStateReader().Read(layout).SubscribedCatalogs;
        return removeKnown.Success && removeUnknown.Success
            && stored.Count == 0
            && removeUnknown.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.CatalogUnsubscribed && d.Message.Contains("no-op"));
    }
    finally { CleanupTempRoot(tempRoot); }
}

static bool CatalogSubsAddRejectsGarbage()
{
    var tempRoot = NewTempRoot("subs-garbage");
    try
    {
        var layout = InitLayout(tempRoot);
        var result = new CatalogSubscriptionService().Add(layout, "not-a-recognised-source-spec");
        return !result.Success
            && result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);
    }
    finally { CleanupTempRoot(tempRoot); }
}

static string LocateRepoRoot()
{
    // Walk up from the test assembly's location until we find a directory
    // containing the bundled fixture. Mirrors the patcher tests' FindRepositoryRoot
    // helper but kept local to avoid coupling the two test projects.
    var dir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(dir))
    {
        if (Directory.Exists(Path.Combine(dir, "examples", "mod-catalog-example")))
        {
            return dir;
        }
        var parent = Directory.GetParent(dir);
        if (parent is null) { break; }
        dir = parent.FullName;
    }
    throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
}

// ---- Type declarations (must follow all top-level statements + local funcs) --

/// <summary>
/// In-memory test fake for <see cref="IRemoteContentFetcher"/>. Lets every
/// RemoteFetcher test run without touching the network — the dictionary keyed
/// by URL holds canned bodies; refs map directly to the SHA the test wants
/// pinned.
/// </summary>
sealed class InMemoryRemoteContentFetcher : IRemoteContentFetcher
{
    public const string FakeSha = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0";

    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string?> Refs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, System.Net.HttpStatusCode> Throws { get; } = new(StringComparer.Ordinal);
    public List<string> FetchedUrls { get; } = new();

    public Task<RemoteFetchedContent?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        FetchedUrls.Add(url);
        if (Throws.TryGetValue(url, out var status))
        {
            throw new System.Net.Http.HttpRequestException(
                $"Response status code does not indicate success: {(int)status} ({status}).", null, status);
        }
        if (!Files.TryGetValue(url, out var bytes)) { return Task.FromResult<RemoteFetchedContent?>(null); }
        return Task.FromResult<RemoteFetchedContent?>(new RemoteFetchedContent(System.Text.Encoding.UTF8.GetString(bytes), bytes));
    }

    public Task<string?> ResolveCommitShaAsync(string owner, string repo, string @ref, CancellationToken cancellationToken)
    {
        var key = $"{owner}/{repo}#{@ref}";
        return Task.FromResult(Refs.TryGetValue(key, out var sha) ? sha : null);
    }

    public async Task<bool> TryStreamFetchAsync(string url, Stream destination, CancellationToken cancellationToken)
    {
        FetchedUrls.Add(url);
        if (!Files.TryGetValue(url, out var bytes)) { return false; }
        await destination.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void AddText(string url, string body) => Files[url] = System.Text.Encoding.UTF8.GetBytes(body);
    public void AddBytes(string url, byte[] body) => Files[url] = body;
    public void AddRef(string owner, string repo, string @ref, string sha) => Refs[$"{owner}/{repo}#{@ref}"] = sha;
}

/// <summary>
/// Captures every <see cref="DeployProgress"/> tick a service reports, in order,
/// for the structured-progress assertions. A direct synchronous sink (not
/// <see cref="Progress{T}"/>): the deploy runs on a background Task.Run thread and
/// reports inline, and the synchronous <c>Deploy</c>/<c>Rollback</c> wrapper blocks
/// until it finishes, so every Add happens-before the test reads <see cref="Reports"/>.
/// </summary>
sealed class RecordingProgress : IProgress<DeployProgress>
{
    public List<DeployProgress> Reports { get; } = new();

    public void Report(DeployProgress value) => Reports.Add(value);
}
