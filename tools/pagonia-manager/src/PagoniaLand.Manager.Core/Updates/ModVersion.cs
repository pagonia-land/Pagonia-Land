using System.Globalization;

namespace PagoniaLand.Manager;

/// <summary>
/// A mod / collection content version (author-driven semver: <c>MAJOR.MINOR.PATCH</c> with an
/// optional <c>-prerelease</c> suffix). Just enough semver to answer the one question update
/// detection asks — "is the advertised version strictly newer than what's installed?" — without
/// pulling in a dependency. Build metadata (<c>+…</c>) is ignored for precedence, and a version
/// with a pre-release sorts <em>below</em> the same version without one (so <c>1.0.0-beta &lt; 1.0.0</c>).
/// </summary>
public readonly record struct ModVersion(int Major, int Minor, int Patch, string PreRelease)
    : IComparable<ModVersion>
{
    /// <summary>
    /// Parse a semver-ish string. Accepts 1–3 numeric components (missing ones default to 0) plus
    /// an optional <c>-prerelease</c> and <c>+build</c> tail. Returns false for anything that isn't
    /// a run of non-negative integer components — the caller then treats the version as
    /// non-comparable rather than guessing.
    /// </summary>
    public static bool TryParse(string? raw, out ModVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();

        // Drop build metadata (ignored in precedence), then split off any pre-release tail.
        var plus = text.IndexOf('+');
        if (plus >= 0)
        {
            text = text[..plus];
        }

        var pre = string.Empty;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            pre = text[(dash + 1)..];
            text = text[..dash];
        }

        var parts = text.Split('.');
        if (parts.Length is 0 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            {
                return false;
            }
            numbers[i] = n;
        }

        version = new ModVersion(numbers[0], numbers[1], numbers[2], pre);
        return true;
    }

    public int CompareTo(ModVersion other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        // Per semver: a normal version outranks a pre-release of the same core; two pre-releases
        // compare identifier-by-identifier (numeric parts numerically, so rc.10 > rc.2).
        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    /// <summary>
    /// Compare two pre-release tails per semver §11.4: split on '.', compare each identifier
    /// (numeric identifiers numerically, alphanumeric ordinally, numeric &lt; alphanumeric), and a
    /// longer identifier set outranks a shorter prefix-equal one. An empty tail (a normal version)
    /// outranks any pre-release. This orders multi-digit tags correctly — <c>rc.10 &gt; rc.2</c> —
    /// where a plain ordinal string compare would put <c>rc.10</c> below <c>rc.2</c>.
    /// </summary>
    private static int ComparePreRelease(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 0;
        if (a.Length == 0) return 1;
        if (b.Length == 0) return -1;

        var aIds = a.Split('.');
        var bIds = b.Split('.');
        var shared = Math.Min(aIds.Length, bIds.Length);
        for (var i = 0; i < shared; i++)
        {
            var aNumeric = int.TryParse(aIds[i], NumberStyles.None, CultureInfo.InvariantCulture, out var an);
            var bNumeric = int.TryParse(bIds[i], NumberStyles.None, CultureInfo.InvariantCulture, out var bn);
            int c;
            if (aNumeric && bNumeric) c = an.CompareTo(bn);
            else if (aNumeric) c = -1;       // a numeric identifier has lower precedence than alphanumeric
            else if (bNumeric) c = 1;
            else c = string.CompareOrdinal(aIds[i], bIds[i]);
            if (c != 0) return c;
        }

        return aIds.Length.CompareTo(bIds.Length);
    }

    /// <summary>
    /// True when <paramref name="available"/> is strictly newer than <paramref name="installed"/>.
    /// Both must parse; an unparseable version yields false (we never claim an update we can't prove).
    /// </summary>
    public static bool IsNewer(string? available, string? installed)
        => TryParse(available, out var a) && TryParse(installed, out var i) && a.CompareTo(i) > 0;
}
