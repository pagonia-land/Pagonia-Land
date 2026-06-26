using System.Diagnostics;

namespace PagoniaLand.Catalog;

/// <summary>
/// Reads the game's version for confirmation: the <c>ProductVersion</c> of
/// <c>Pioneers of Pagonia.exe</c> in a live install — the canonical gameDatabaseVersion (not its
/// FileVersion). Only a live install carries the exe; a pak folder or extracted layout has none.
/// </summary>
public static class GameVersion
{
    private const string ExeName = "Pioneers of Pagonia.exe";

    /// <summary>The install's product version, or null if it can't be determined.</summary>
    public static string? TryRead(string root)
    {
        if (GameInstallLocator.Detect(root) != GameInstallKind.LiveInstall)
        {
            return null;
        }

        var exe = Path.Combine(root, ExeName);
        if (!File.Exists(exe))
        {
            return null;
        }

        try
        {
            var version = FileVersionInfo.GetVersionInfo(exe).ProductVersion;
            return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }
        catch
        {
            return null;
        }
    }
}
