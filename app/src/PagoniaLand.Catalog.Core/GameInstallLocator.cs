using System.Text.RegularExpressions;

namespace PagoniaLand.Catalog;

/// <summary>What kind of game-side directory a path points at.</summary>
public enum GameInstallKind
{
    /// <summary>A live install — <c>&lt;root&gt;/pak/*.pak</c> present.</summary>
    LiveInstall,

    /// <summary>A folder of <c>*.pak</c> files directly (e.g. the repo's <c>game-paks/</c>).</summary>
    PakDirectory,

    /// <summary>A pre-extracted layout — <c>*.gd.xml</c> under the folder (the <c>game-gdb/</c> shape).</summary>
    ExtractedLayout,

    /// <summary>None of the above.</summary>
    Unrecognised,
}

/// <summary>
/// Decides what a game-side path is (live install / pak folder / extracted layout) and makes
/// a best-effort guess at the default Steam install location. Cheap — directory/file
/// enumeration only, no pak reads. A small, dependency-free reimplementation of the
/// detection the manager does (the manager's version is coupled to its store).
/// </summary>
public static class GameInstallLocator
{
    private const string PakFolderName = "pak";
    private const string SteamRelativePath = "steamapps/common/Pioneers of Pagonia";

    public static GameInstallKind Detect(string? root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return GameInstallKind.Unrecognised;
        }

        var pakDir = Path.Combine(root, PakFolderName);
        if (Directory.Exists(pakDir) && Directory.EnumerateFiles(pakDir, "*.pak").Any())
        {
            return GameInstallKind.LiveInstall;
        }

        if (Directory.EnumerateFiles(root, "*.pak").Any())
        {
            return GameInstallKind.PakDirectory;
        }

        if (Directory.EnumerateFiles(root, "*.gd.xml", SearchOption.AllDirectories).Any())
        {
            return GameInstallKind.ExtractedLayout;
        }

        return GameInstallKind.Unrecognised;
    }

    /// <summary>Best-effort: the first common Steam location that looks like a live install.</summary>
    public static bool TryFindDefaultInstall(out string? root)
    {
        foreach (var candidate in CandidateInstallPaths())
        {
            if (Detect(candidate) == GameInstallKind.LiveInstall)
            {
                root = candidate;
                return true;
            }
        }

        root = null;
        return false;
    }

    private static IEnumerable<string> CandidateInstallPaths()
    {
        var rel = SteamRelativePath.Replace('/', Path.DirectorySeparatorChar);

        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            if (string.IsNullOrEmpty(programFiles))
            {
                continue;
            }

            var steamBase = Path.Combine(programFiles, "Steam");
            yield return Path.Combine(steamBase, rel); // the base library itself

            // Steam records its other library drives in steamapps/libraryfolders.vdf — follow them.
            foreach (var library in SteamLibraryRoots(steamBase))
            {
                yield return Path.Combine(library, rel);
            }
        }

        // Common secondary library locations as a final fallback.
        yield return Path.Combine("C:\\", "SteamLibrary", rel);
        yield return Path.Combine("D:\\", "SteamLibrary", rel);
        yield return Path.Combine("D:\\", "Steam", rel);
    }

    private static IEnumerable<string> SteamLibraryRoots(string steamBase)
    {
        var vdf = Path.Combine(steamBase, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            return ParseSteamLibraryPaths(File.ReadAllText(vdf));
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>Extract the library-folder paths from a Steam <c>libraryfolders.vdf</c>'s content.</summary>
    public static IReadOnlyList<string> ParseSteamLibraryPaths(string vdf)
    {
        var paths = new List<string>();
        foreach (Match match in Regex.Matches(vdf, "\"path\"\\s*\"([^\"]+)\""))
        {
            paths.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }

        return paths;
    }
}
