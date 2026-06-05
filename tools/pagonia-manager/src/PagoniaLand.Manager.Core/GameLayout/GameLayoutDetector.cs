namespace PagoniaLand.Manager;

/// <summary>
/// What kind of game-side directory a path refers to. Plan + Deploy needs
/// this distinction because the patcher resolves operations against
/// extracted XML files but Pioneers of Pagonia ships its data inside
/// <c>pak/*.pak</c>; without detection, pointing at a live install fails
/// silently with <c>patcher.targetFileMissing</c>.
/// </summary>
public enum GameLayoutKind
{
    /// <summary>Live install — <c>&lt;root&gt;/pak/*.pak</c> present. Pak-aware
    /// deploy extracts these into a fingerprinted
    /// cache, patches against the cache, then repacks back.</summary>
    LiveInstall,

    /// <summary>Pre-extracted layout — <c>&lt;root&gt;/core/gdb/*.gd.xml</c>
    /// present, same shape as the repo's local <c>game-gdb/</c> folder. This
    /// is the only layout deploy currently supports end-to-end.</summary>
    ExtractedLayout,

    /// <summary>Neither — surface a clear "not a recognised PoP folder" message
    /// instead of letting the patcher fail later with a generic file-not-found.</summary>
    Unrecognised,
}

/// <summary>Detected layout for a game-side path. <see cref="DiscoveredPaks"/> is
/// the full list of <c>*.pak</c> file paths under <c>&lt;root&gt;/pak/</c> when
/// <see cref="Kind"/> is <see cref="GameLayoutKind.LiveInstall"/>; empty otherwise.
/// <see cref="GameProductVersion"/> is the executable's ProductVersion for a
/// <see cref="GameLayoutKind.LiveInstall"/> (the real <c>gameDatabaseVersion</c>),
/// or <c>null</c> when there is no readable exe — every consumer treats null as
/// "unknown" and renders a dash.</summary>
public sealed record GameLayout(
    GameLayoutKind Kind,
    string Root,
    IReadOnlyList<string> DiscoveredPaks,
    string? GameProductVersion = null);

/// <summary>
/// Probes a path to decide whether it's a live game install, a pre-extracted
/// XML layout, or neither. Cheap — directory + file enumeration only, no pak
/// reads. Returns a value object that downstream wizards branch on.
/// </summary>
public static class GameLayoutDetector
{
    /// <summary>
    /// Inspect <paramref name="root"/> and return what kind of layout it is.
    /// If both a live <c>pak/</c> folder and an extracted <c>core/gdb/</c> folder
    /// are present (rare — typically a manual extraction left behind alongside
    /// the original install), live install wins: deploying to the place the
    /// game actually reads from is almost always what the user wanted.
    /// </summary>
    public static GameLayout Detect(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return new GameLayout(GameLayoutKind.Unrecognised, root ?? string.Empty, Array.Empty<string>());
        }

        var pakDir = Path.Combine(root, GameLayoutConstants.PakFolderName);
        if (Directory.Exists(pakDir))
        {
            var paks = Directory
                .EnumerateFiles(pakDir, "*" + GameLayoutConstants.PakExtension, SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            if (paks.Count > 0)
            {
                // Surface the game version the OS hands us for free via the exe's
                // ProductVersion. Null (no readable exe) just means "unknown" —
                // never a detection failure.
                GameVersionReader.TryRead(root, out var productVersion, out _);
                return new GameLayout(GameLayoutKind.LiveInstall, root, paks, productVersion);
            }
        }

        // Use core/gdb/ as the extracted-layout sentinel — every Pioneers of
        // Pagonia install since release has core.pak with XML under core/gdb/,
        // so an extracted layout always has this directory populated. Other
        // packages (decorations1, dlc1, tools) may or may not be present
        // depending on DLC ownership.
        var coreGdb = Path.Combine(root, "core", "gdb");
        if (Directory.Exists(coreGdb)
            && Directory.EnumerateFiles(coreGdb, "*.gd.xml", SearchOption.TopDirectoryOnly).Any())
        {
            return new GameLayout(GameLayoutKind.ExtractedLayout, root, Array.Empty<string>());
        }

        return new GameLayout(GameLayoutKind.Unrecognised, root, Array.Empty<string>());
    }
}
