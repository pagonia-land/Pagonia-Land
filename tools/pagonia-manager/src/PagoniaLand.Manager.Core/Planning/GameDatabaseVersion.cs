using System.Text.RegularExpressions;

namespace PagoniaLand.Manager;

/// <summary>How a mod's declared GameDatabase version relates to the install's.</summary>
public enum GameVersionRelation
{
    /// <summary>Identical down to build + revision.</summary>
    Exact,

    /// <summary>Same <c>major.minor.patch</c> line, different build/revision — the
    /// mod targets a different snapshot of the same patch line. Almost always
    /// still applies; the patcher's apply-time check is the hard safety net.</summary>
    SameLineDrift,

    /// <summary>Different <c>major.minor.patch</c> — a real version gap; the mod
    /// may not apply cleanly.</summary>
    LineGap,
}

/// <summary>
/// A parsed Pioneers of Pagonia GameDatabase version —
/// <c>major.minor.patch-build+revision</c> (e.g. <c>1.3.0-11768+193445</c>).
/// Both the exe's ProductVersion and a mod manifest's <c>gameDatabaseVersion</c>
/// use this exact shape (established in the version-surfacing step), so one parser
/// compares the two. <see cref="Build"/> is the monotonic counter and the primary
/// ordering key within a matching <c>major.minor.patch</c>; <see cref="Revision"/>
/// is changeset metadata.
/// </summary>
public sealed partial class GameDatabaseVersion : IComparable<GameDatabaseVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public int Build { get; }
    public int Revision { get; }

    private GameDatabaseVersion(int major, int minor, int patch, int build, int revision)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Build = build;
        Revision = revision;
    }

    // Mirrors the patcher's gameDatabaseVersion validation regex, with capture
    // groups. Anything the manifest validator rejects is rejected here too.
    [GeneratedRegex(@"^([0-9]+)\.([0-9]+)\.([0-9]+)-([0-9]+)\+([0-9]+)$")]
    private static partial Regex VersionPattern();

    /// <summary>Parse a validated version string. Returns false (not throw) for
    /// null/empty/malformed input or any component that overflows <see cref="int"/>.</summary>
    public static bool TryParse(string? value, out GameDatabaseVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = VersionPattern().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (int.TryParse(match.Groups[1].Value, out var major)
            && int.TryParse(match.Groups[2].Value, out var minor)
            && int.TryParse(match.Groups[3].Value, out var patch)
            && int.TryParse(match.Groups[4].Value, out var build)
            && int.TryParse(match.Groups[5].Value, out var revision))
        {
            version = new GameDatabaseVersion(major, minor, patch, build, revision);
            return true;
        }

        return false;
    }

    /// <summary>The <c>major.minor.patch</c> line as a string (e.g. <c>1.3.0</c>).</summary>
    public string Line => $"{Major}.{Minor}.{Patch}";

    /// <summary>True when <c>major.minor.patch</c> match — the "same line" test.</summary>
    public bool SameLine(GameDatabaseVersion other)
        => Major == other.Major && Minor == other.Minor && Patch == other.Patch;

    /// <summary>Classify how this (a mod's target) relates to <paramref name="game"/>
    /// (the install's actual version).</summary>
    public GameVersionRelation RelateTo(GameDatabaseVersion game)
    {
        if (!SameLine(game))
        {
            return GameVersionRelation.LineGap;
        }
        return Build == game.Build && Revision == game.Revision
            ? GameVersionRelation.Exact
            : GameVersionRelation.SameLineDrift;
    }

    public int CompareTo(GameDatabaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        c = Minor.CompareTo(other.Minor);
        if (c != 0) return c;
        c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        // Build is the monotonic counter — primary ordering within a line.
        c = Build.CompareTo(other.Build);
        if (c != 0) return c;
        return Revision.CompareTo(other.Revision);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}-{Build}+{Revision}";
}
