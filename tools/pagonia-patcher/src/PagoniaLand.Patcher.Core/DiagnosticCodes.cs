namespace PagoniaLand.Patcher;

/// <summary>
/// Stable identifiers used in <see cref="PatchDiagnostic.Code"/>. Mod managers and CI scripts can
/// match against these constants instead of comparing raw strings.
/// Codes here are unprefixed camelCase, scoped to this tool; the manager prefixes its own codes with "manager." because it aggregates diagnostics from all three tools and needs to disambiguate.
/// </summary>
public static class DiagnosticCodes
{
    // Reading
    public const string FileNotFound = "fileNotFound";
    public const string FileRead = "fileRead";
    public const string ModManifestReadFailed = "modManifestReadFailed";
    public const string PatchFileReadFailed = "patchFileReadFailed";
    public const string CollectionReadFailed = "collectionReadFailed";
    public const string CollectionLockReadFailed = "collectionLockReadFailed";

    // Manifest validation
    public const string MissingPatchFormatVersion = "missingPatchFormatVersion";
    public const string MissingId = "missingId";
    public const string InvalidId = "invalidId";
    public const string MissingName = "missingName";
    public const string MissingVersion = "missingVersion";
    public const string MissingAuthor = "missingAuthor";
    public const string MissingGameDatabaseVersion = "missingGameDatabaseVersion";
    public const string InvalidGameDatabaseVersion = "invalidGameDatabaseVersion";
    public const string MissingDescription = "missingDescription";
    public const string MissingRequiredPackages = "missingRequiredPackages";
    public const string UnknownPackage = "unknownPackage";
    public const string MissingPatchSetId = "missingPatchSetId";
    public const string EmptyPatchPath = "emptyPatchPath";
    public const string UnsafePatchPath = "unsafePatchPath";
    public const string MissingOperationId = "missingOperationId";
    public const string DuplicateOperationId = "duplicateOperationId";
    public const string ModManifestValid = "modManifestValid";

    // Tweak declarations (mod.yaml `tweaks:` block)
    public const string InvalidTweakId = "invalidTweakId";
    public const string InvalidTweakType = "invalidTweakType";
    public const string DuplicateTweakId = "duplicateTweakId";
    public const string InvalidTweakAlias = "invalidTweakAlias";
    public const string TweakEnumMissingValues = "tweakEnumMissingValues";
    public const string TweakDefaultOutOfRange = "tweakDefaultOutOfRange";
    public const string TweakDefaultNotInteger = "tweakDefaultNotInteger";
    public const string TweakDefaultNotEnumValue = "tweakDefaultNotEnumValue";
    public const string TweakMinGreaterThanMax = "tweakMinGreaterThanMax";
    public const string TweakStepInvalid = "tweakStepInvalid";
    public const string TweakDeclaredButUnused = "tweakDeclaredButUnused";
    public const string TweakTernaryOnNonBoolean = "tweakTernaryOnNonBoolean";

    // Tweak templating ({{ tweaks.<id> }} resolution at plan time)
    public const string TweakValueResolved = "tweakValueResolved";
    public const string TweakValueOutOfRange = "tweakValueOutOfRange";
    public const string TweakValueInvalid = "tweakValueInvalid";
    public const string TweakUndeclared = "tweakUndeclared";
    public const string TweakSyntaxError = "tweakSyntaxError";
    public const string TweakOverrideMalformed = "tweakOverrideMalformed";
    public const string TweakValuePinnedByLockfile = "tweakValuePinnedByLockfile";

    // Target resolution
    public const string TargetFileMissing = "targetFileMissing";
    public const string TargetFileReadFailed = "targetFileReadFailed";
    public const string TargetEntityMissing = "targetEntityMissing";
    public const string TargetEntityNameMismatch = "targetEntityNameMismatch";
    public const string TargetComponentMissing = "targetComponentMissing";
    public const string TargetPathMissing = "targetPathMissing";
    public const string TargetPathMalformed = "targetPathMalformed";
    public const string ReplaceValueOnContainer = "replaceValueOnContainer";
    public const string TargetAttributeMissing = "targetAttributeMissing";
    public const string TargetListItemMissing = "targetListItemMissing";
    public const string TargetEntityGroupMissing = "targetEntityGroupMissing";
    public const string TargetEntityAlreadyExists = "targetEntityAlreadyExists";
    public const string InvalidPatchOperationEntity = "invalidPatchOperationEntity";
    public const string ExpectedOldValueMismatch = "expectedOldValueMismatch";
    public const string ExpectedOldXmlMismatch = "expectedOldXmlMismatch";
    public const string MissingPatchOperationField = "missingPatchOperationField";
    public const string InvalidPatchOperationXml = "invalidPatchOperationXml";
    public const string TargetResolved = "targetResolved";

    // Arithmetic operations (multiplyValue / addValue)
    public const string ArithmeticTargetNotNumeric = "arithmeticTargetNotNumeric";
    public const string ArithmeticOperandNotNumeric = "arithmeticOperandNotNumeric";
    public const string ArithmeticResultClamped = "arithmeticResultClamped";
    public const string ArithmeticResultNotFinite = "arithmeticResultNotFinite";
    public const string ClampMinGreaterThanMax = "clampMinGreaterThanMax";

    // Planning
    public const string PatchPlanReady = "patchPlanReady";
    public const string CombinedPatchPlanReady = "combinedPatchPlanReady";
    public const string DuplicateWriteTarget = "duplicateWriteTarget";
    public const string UnsupportedOperation = "unsupportedOperation";
    public const string PatchSetSkipped = "patchSetSkipped";
    public const string PatchSetMissingPackage = "patchSetMissingPackage";

    // Applying
    public const string ApplyBlocked = "applyBlocked";
    public const string ApplyOutputOverlapsSource = "applyOutputOverlapsSource";
    public const string ApplyTargetMissing = "applyTargetMissing";
    public const string ApplyOldValueMismatch = "applyOldValueMismatch";
    public const string ApplyListItemMissing = "applyListItemMissing";
    public const string ApplyEntityMissing = "applyEntityMissing";
    public const string ApplyEntityAlreadyExists = "applyEntityAlreadyExists";
    public const string ApplyComponentMissing = "applyComponentMissing";
    public const string PatchApplied = "patchApplied";
    public const string ApplyComplete = "applyComplete";

    // Entry operations (binary pak ops alongside XML patches)
    public const string EntryPlanReady = "entryPlanReady";
    public const string EntrySourceMissing = "entrySourceMissing";
    public const string EntryConflict = "entryConflict";
    public const string EntryReplaced = "entryReplaced";
    public const string EntryAdded = "entryAdded";
    public const string EntryDeleted = "entryDeleted";
    public const string EntrySourceUnreadable = "entrySourceUnreadable";

    // Collection resolution
    public const string ModsRootMissing = "modsRootMissing";
    public const string CollectionModSkipped = "collectionModSkipped";
    public const string CollectionModResolved = "collectionModResolved";
    public const string CollectionModMissing = "collectionModMissing";
    public const string CollectionSetEmpty = "collectionSetEmpty";
    public const string CollectionModDuplicateSkipped = "collectionModDuplicateSkipped";
    public const string CollectionModVersionConflict = "collectionModVersionConflict";
    public const string CollectionGameDatabaseVersionConflict = "collectionGameDatabaseVersionConflict";

    // Lockfile resolution
    public const string LockfileModSkipped = "lockfileModSkipped";
    public const string LockfileModResolved = "lockfileModResolved";
    public const string LockfileModMissing = "lockfileModMissing";
    public const string LockfileArchiveHashMismatch = "lockfileArchiveHashMismatch";
    public const string LockfileVersionUnsupported = "lockfileVersionUnsupported";

    // Collection export
    public const string CollectionExportNoMods = "collectionExportNoMods";
    public const string CollectionExportNoLoadedMods = "collectionExportNoLoadedMods";
    public const string CollectionExportDuplicateMod = "collectionExportDuplicateMod";
    public const string CollectionExportMixedGameDatabaseVersions = "collectionExportMixedGameDatabaseVersions";
    public const string CollectionExportGameDatabaseOverride = "collectionExportGameDatabaseOverride";
    public const string CollectionExportReady = "collectionExportReady";

    // Pattern B overlay-pak scaffold
    public const string ScaffoldWritten = "scaffoldWritten";
    public const string ScaffoldNameMissing = "scaffoldNameMissing";
    public const string ScaffoldNameInvalid = "scaffoldNameInvalid";

    // Schema validation (mod.yaml / patch files / collection / lockfile against schemas/mod-patches/*.schema.json)
    public const string SchemaValidationOk = "schemaValidationOk";
    public const string SchemaValidationFailed = "schemaValidationFailed";

    // Repo-index mirror sync (index.yaml's per-mod copy vs each mod.yaml — see RepoIndexMirror)
    public const string IndexReadFailed = "indexReadFailed";
    public const string IndexMirrorMismatch = "indexMirrorMismatch";
    public const string IndexEntryOrphaned = "indexEntryOrphaned";
    public const string IndexEntryMissing = "indexEntryMissing";
    public const string IndexEntryIdMismatch = "indexEntryIdMismatch";
    public const string IndexMirrorInSync = "indexMirrorInSync";
    public const string IndexMirrorUpdated = "indexMirrorUpdated";
    public const string IndexMirrorManualFixNeeded = "indexMirrorManualFixNeeded";
    public const string IndexMirrorWriteAborted = "indexMirrorWriteAborted";

    // Authoring advisor — conflict-minimising lint over a mod's own overlay *.gd.xml
    public const string OverlayGdbFileMissing = "overlayGdbFileMissing";
    public const string OverlayGdbUnreadable = "overlayGdbUnreadable";
    public const string UsesDestructiveInheritanceMode = "usesDestructiveInheritanceMode";
    public const string UnloadsReferencedEntity = "unloadsReferencedEntity";
    public const string InheritanceConflictRisk = "inheritanceConflictRisk";
    public const string ReplaceCouldBeIncremental = "replaceCouldBeIncremental";
    // --game-root was supplied but loaded no *.gd.xml entities — the base-aware
    // checks silently did nothing; warn so the user doesn't trust a false pass.
    public const string ReferenceGameRootEmpty = "referenceGameRootEmpty";
}
