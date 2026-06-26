namespace PagoniaLand.Paker;

/// <summary>
/// Stable identifiers used in <see cref="PakDiagnostic.Code"/>. Mod managers
/// and CI scripts can match against these constants instead of parsing raw
/// strings.
/// Codes here are unprefixed camelCase, scoped to this tool; the manager prefixes its own codes with "manager." because it aggregates diagnostics from all three tools and needs to disambiguate.
/// </summary>
public static class DiagnosticCodes
{
    // Format-level read errors
    public const string PakFooterTruncated = "pakFooterTruncated";
    public const string PakIndexOffsetInvalid = "pakIndexOffsetInvalid";
    public const string PakIndexTruncated = "pakIndexTruncated";
    public const string PakIndexCrcMismatch = "pakIndexCrcMismatch";
    public const string PakEntryTruncated = "pakEntryTruncated";
    public const string PakEntryLongFilenameMarkerMissing = "pakEntryLongFilenameMarkerMissing";
    public const string PakEntryFilenameInvalidUtf8 = "pakEntryFilenameInvalidUtf8";

    // Pack-time errors
    public const string PakInfoJsonInvalid = "pakInfoJsonInvalid";
    public const string PakInfoEmpty = "pakInfoEmpty";
    public const string PackSourceMissing = "packSourceMissing";
    public const string PackSourceUnreadable = "packSourceUnreadable";
    public const string PackEntryFilenameEmpty = "packEntryFilenameEmpty";

    // Patch-time errors
    public const string PatchInputMissing = "patchInputMissing";
    public const string PatchSourceMissing = "patchSourceMissing";
    // Reserved: not currently emitted — a positional path that matches no existing
    // entry is treated as an Add, not an error. Kept as a stable identifier in case
    // a future strict-patch mode wants to reject unknown targets.
    public const string PatchEntryNotFound = "patchEntryNotFound";
    public const string PatchDuplicateSource = "patchDuplicateSource";
    public const string PatchDeleteTargetMissing = "patchDeleteTargetMissing";
    public const string PatchAddConflictsWithExisting = "patchAddConflictsWithExisting";
    public const string PatchGdBinUpdateFailed = "patchGdbinUpdateFailed";

    // gd.bin index errors
    public const string GdBinHeaderInvalid = "gdbinHeaderInvalid";
    public const string GdBinEntryTruncated = "gdbinEntryTruncated";
    public const string GdBinPathDecodingFailed = "gdbinPathDecodingFailed";

    // loca blob errors
    public const string LocaEntryTruncated = "locaEntryTruncated";
    public const string LocaStringDecodingFailed = "locaStringDecodingFailed";

    // Classify
    public const string ClassifyMultipleModules = "classifyMultipleModules";
    public const string ClassifyManifestUnreadable = "classifyManifestUnreadable";

    // Successful reads / writes (info level)
    public const string PakIndexRead = "pakIndexRead";
    public const string PakIndexWrite = "pakIndexWrite";
    public const string PakPackWritten = "pakPackWritten";
    public const string PakPatchWritten = "pakPatchWritten";
    public const string PakEntryAdded = "pakEntryAdded";
    public const string PakEntryDeleted = "pakEntryDeleted";
    public const string GdBinRead = "gdbinRead";
    public const string LocaRead = "locaRead";
    public const string PakPatchGdBinUpdated = "pakPatchGdbinUpdated";
    public const string PakClassified = "pakClassified";
}
