using System.Globalization;

namespace PagoniaLand.Patcher;

/// <summary>
/// The public author↔consumer distribution formats governed by the
/// <c>MAJOR.MINOR</c> format-version contract. Each carries a <c>*FormatVersion</c>
/// field and travels between authors, catalogs, and consumers on possibly-different
/// tool versions, so a cross-version reader (not a flat fail-closed <c>enum</c>) is
/// the point — a file written by one party is read by another on a different build.
/// </summary>
public enum ManagedFormat
{
    /// <summary><c>patchFormatVersion</c> in <c>mod.yaml</c>.</summary>
    Mod,

    /// <summary><c>collectionFormatVersion</c> in a collection manifest.</summary>
    Collection,

    /// <summary><c>collectionLockVersion</c> in a resolved collection lockfile.</summary>
    CollectionLock,

    /// <summary><c>indexFormatVersion</c> in a repo <c>index.yaml</c>.</summary>
    RepoIndex,

    /// <summary><c>catalogFormatVersion</c> in a <c>catalog.yaml</c>.</summary>
    Catalog,
}

/// <summary>
/// Where a file's declared format version sits relative to what this build knows.
/// </summary>
public enum FormatVersionTier
{
    /// <summary>Same major, known or older minor — read normally.</summary>
    Current,

    /// <summary>Same major, newer minor — read; ignore unknown optional fields.</summary>
    MinorAhead,

    /// <summary>Newer / unknown major — refuse; the file needs a newer tool.</summary>
    MajorUnsupported,

    /// <summary>Older major we no longer support — refuse with a migration hint.</summary>
    MajorRetired,

    /// <summary>Empty / unparseable — not a <c>MAJOR.MINOR</c> value.</summary>
    Malformed,
}

/// <summary>A parsed <c>MAJOR.MINOR</c> format version.</summary>
public readonly record struct FormatVersion(int Major, int Minor)
{
    public override string ToString() => $"{Major}.{Minor}";
}

/// <summary>
/// The outcome of checking one file's declared version against the policy.
/// </summary>
public sealed record FormatVersionVerdict(
    ManagedFormat Format,
    FormatVersionTier Tier,
    FormatVersion? Version,
    PatchDiagnostic? Diagnostic)
{
    /// <summary>
    /// Reading should proceed (<see cref="FormatVersionTier.Current"/> or
    /// <see cref="FormatVersionTier.MinorAhead"/>). The other tiers refuse.
    /// </summary>
    public bool Accepted => Tier is FormatVersionTier.Current or FormatVersionTier.MinorAhead;

    /// <summary>
    /// A newer-minor file may carry optional fields this build doesn't know;
    /// strict <c>additionalProperties: false</c> validation must be relaxed for it
    /// so the unknown fields are ignored rather than rejected.
    /// </summary>
    public bool TolerateUnknownFields => Tier is FormatVersionTier.MinorAhead;
}

/// <summary>
/// The shared, tiered <c>MAJOR.MINOR</c> reader for the format-version contract. It
/// replaces the old flat fail-closed behaviour (any value other than <c>0.1</c>
/// rejected, no migration path, no actionable message) with a tiered model:
///
/// <list type="bullet">
/// <item>same major, known/older minor → read normally;</item>
/// <item>same major, newer minor → read, ignore unknown optional fields, info
/// <see cref="DiagnosticCodes.FormatMinorAhead"/>;</item>
/// <item>newer/unknown major → refuse, error <see cref="DiagnosticCodes.FormatMajorUnsupported"/>
/// naming where to get a newer tool;</item>
/// <item>older retired major → refuse, error <see cref="DiagnosticCodes.FormatMajorRetired"/>.</item>
/// </list>
///
/// This type is the single source of truth for which version each format is known
/// up to. The JSON Schemas widen to "any same-major minor"; the tier decision
/// (which the schema can't express) is made here, ahead of strict schema validation.
/// The patcher is the reference implementation; the manager consumes the same policy.
/// </summary>
public sealed class FormatVersionPolicy
{
    /// <summary>
    /// The latest <c>MAJOR.MINOR</c> this build understands per format. Bump a
    /// minor here in lockstep with an additive (optional-field) schema change so the
    /// <see cref="DiagnosticCodes.FormatMinorAhead"/> signal stays meaningful; bump a
    /// major only for a breaking shape change. Every format is at <c>0.1</c> today.
    ///
    /// <para>
    /// A <b>major</b> bump here must also widen each schema's <c>*FormatVersion</c>
    /// <c>pattern</c> (it hardcodes the current major, e.g. <c>^0\.[0-9]+$</c>) so the
    /// code gate and the standalone schema contract stay in agreement. A drift test in
    /// the patcher suite asserts the two never diverge, so the schema side can't be
    /// forgotten.
    /// </para>
    /// </summary>
    private static readonly Dictionary<ManagedFormat, FormatVersion> Known = new()
    {
        [ManagedFormat.Mod] = new FormatVersion(0, 1),
        [ManagedFormat.Collection] = new FormatVersion(0, 1),
        [ManagedFormat.CollectionLock] = new FormatVersion(0, 1),
        [ManagedFormat.RepoIndex] = new FormatVersion(0, 1),
        [ManagedFormat.Catalog] = new FormatVersion(0, 1),
    };

    /// <summary>
    /// The public download page named in the <see cref="DiagnosticCodes.FormatMajorUnsupported"/>
    /// message — a file from a future major needs a newer release than this build, and
    /// the exact minimum version is unknowable here (that major didn't exist when this
    /// build shipped), so we point at the releases page.
    /// </summary>
    public const string DownloadUrl = "https://github.com/pagonia-land/Pagonia-Land/releases";

    /// <summary>The latest <c>MAJOR.MINOR</c> this build understands for <paramref name="format"/>.</summary>
    public static FormatVersion KnownVersion(ManagedFormat format) => Known[format];

    /// <summary>
    /// The canonical version string a writer stamps for <paramref name="format"/> (the current
    /// <see cref="KnownVersion"/>). Writers use this instead of a literal so a future minor/major
    /// bump of <see cref="Known"/> flows through every write site automatically (read-old / write-new).
    /// </summary>
    public static string CurrentVersion(ManagedFormat format) => Known[format].ToString();

    /// <summary>
    /// Decide how to treat <paramref name="declared"/> for <paramref name="format"/>.
    /// Returns the tier, the parsed version (null when unparseable), and the single
    /// diagnostic to surface (null only for an exact-match <see cref="FormatVersionTier.Current"/>).
    /// </summary>
    public FormatVersionVerdict Evaluate(ManagedFormat format, string? declared)
    {
        var field = FieldName(format);
        var known = Known[format];

        if (!TryParse(declared, out var parsed))
        {
            var detail = string.IsNullOrWhiteSpace(declared)
                ? "is missing or empty"
                : $"'{declared}' is not a MAJOR.MINOR version";
            return new FormatVersionVerdict(format, FormatVersionTier.Malformed, null, new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.FormatVersionMalformed,
                $"{field} {detail}; expected a MAJOR.MINOR value such as \"{known}\"."));
        }

        if (parsed.Major > known.Major)
        {
            return new FormatVersionVerdict(format, FormatVersionTier.MajorUnsupported, parsed, new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.FormatMajorUnsupported,
                $"{field} {parsed} needs a newer pagonia-* release (this build reads format major {known.Major}). Download the latest at {DownloadUrl}."));
        }

        if (parsed.Major < known.Major)
        {
            return new FormatVersionVerdict(format, FormatVersionTier.MajorRetired, parsed, new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.FormatMajorRetired,
                $"{field} {parsed} uses a retired format major no longer supported by this build (reads major {known.Major}); re-export the file with a current tool."));
        }

        if (parsed.Minor > known.Minor)
        {
            return new FormatVersionVerdict(format, FormatVersionTier.MinorAhead, parsed, new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.FormatMinorAhead,
                $"{field} {parsed} is newer than this build knows ({known}); reading anyway and ignoring any newer optional fields. Updating pagonia-* is recommended."));
        }

        return new FormatVersionVerdict(format, FormatVersionTier.Current, parsed, null);
    }

    /// <summary>
    /// Parse a declared version into <c>MAJOR.MINOR</c>. Accepts the canonical string
    /// form (<c>"0.1"</c>) and the bare-number form YAML may produce (<c>0.1</c> arrives
    /// as the string <c>"0.1"</c> off the model). Requires exactly two non-negative
    /// integer components — <c>"0"</c>, <c>"0.1.0"</c>, signs, and decimals are rejected.
    /// </summary>
    public static bool TryParse(string? declared, out FormatVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(declared))
        {
            return false;
        }

        var parts = declared.Trim().Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor))
        {
            return false;
        }

        version = new FormatVersion(major, minor);
        return true;
    }

    /// <summary>The on-disk field name carrying <paramref name="format"/>'s version.</summary>
    public static string FieldName(ManagedFormat format) => format switch
    {
        ManagedFormat.Mod => "patchFormatVersion",
        ManagedFormat.Collection => "collectionFormatVersion",
        ManagedFormat.CollectionLock => "collectionLockVersion",
        ManagedFormat.RepoIndex => "indexFormatVersion",
        ManagedFormat.Catalog => "catalogFormatVersion",
        _ => "formatVersion",
    };
}
