namespace PagoniaLand.Manager;

/// <summary>
/// Which of the four canonical packages are physically present on a given
/// install. <see cref="IsPresent"/> is the one fact deployment hard-gates on
/// (you cannot patch a pak that isn't on disk).
/// </summary>
public sealed record PackagePresence(IReadOnlySet<string> PresentPackages)
{
    public bool IsPresent(string package) =>
        PresentPackages.Contains(package);
}

/// <summary>
/// Reports which of <c>core</c> / <c>decorations1</c> / <c>dlc1</c> / <c>tools</c>
/// are physically present on an install, across both supported layouts (a live
/// <c>pak/*.pak</c> install and a pre-extracted folder layout). Distinct from
/// <see cref="PakRequirementAnalyzer"/>, which derives the paks a mod
/// <em>touches</em> transitively from its patches — presence is a direct
/// on-disk fact, not a function of any mod. Reuses
/// <see cref="GameLayoutDetector"/> for the layout decision; cheap (directory +
/// file enumeration only, no pak reads).
/// </summary>
public static class PackagePresenceDetector
{
    /// <summary>Detect presence by first classifying <paramref name="gameRoot"/>'s layout.</summary>
    public static PackagePresence Detect(string gameRoot) =>
        Detect(GameLayoutDetector.Detect(gameRoot));

    /// <summary>Detect presence against an already-classified layout (avoids a
    /// second <see cref="GameLayoutDetector.Detect"/> when the caller has one).</summary>
    public static PackagePresence Detect(GameLayout layout)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (layout.Kind)
        {
            case GameLayoutKind.LiveInstall:
                // A package is present iff pak/<package>.pak exists. DiscoveredPaks
                // holds the full pak/*.pak file paths; match on basename-without-extension.
                foreach (var pakPath in layout.DiscoveredPaks)
                {
                    var basename = Path.GetFileNameWithoutExtension(pakPath);
                    if (ExpansionPackages.IsKnown(basename))
                    {
                        present.Add(Canonical(basename));
                    }
                }
                break;

            case GameLayoutKind.ExtractedLayout:
                // A package is present iff <root>/<package>/ exists and is non-empty.
                // Mirrors the detector's own core/gdb sentinel, generalised to all four.
                foreach (var package in ExpansionPackages.All)
                {
                    var packageDir = Path.Combine(layout.Root, package);
                    if (Directory.Exists(packageDir)
                        && Directory.EnumerateFileSystemEntries(packageDir).Any())
                    {
                        present.Add(package);
                    }
                }
                break;

            case GameLayoutKind.Unrecognised:
            default:
                // Nothing recognised → nothing present. Callers treat this the
                // same as an absent install (deployment already refuses an
                // unrecognised layout with manager.gameLayoutUnrecognised).
                break;
        }

        return new PackagePresence(present);
    }

    // Map a discovered basename back to its canonical spelling so the result set
    // uses the constants from ExpansionPackages regardless of on-disk casing.
    private static string Canonical(string basename) =>
        ExpansionPackages.All.First(p => string.Equals(p, basename, StringComparison.OrdinalIgnoreCase));
}
