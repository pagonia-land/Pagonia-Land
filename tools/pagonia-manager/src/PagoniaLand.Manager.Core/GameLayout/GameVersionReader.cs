using System.Diagnostics;

namespace PagoniaLand.Manager;

/// <summary>
/// Reads the Pioneers of Pagonia version stamped into the game executable's
/// Win32 version resource. Uses <see cref="FileVersionInfo.GetVersionInfo(string)"/>,
/// whose managed PE reader parses the resource itself rather than calling the OS —
/// so a Linux/macOS CI host reads the Windows exe's resource identically, and the
/// AOT win-x64/linux-x64/osx-x64 binaries all behave the same.
/// </summary>
public static class GameVersionReader
{
    /// <summary>
    /// Try to read the version from the game executable under <paramref name="gameRoot"/>.
    /// Prefers <see cref="GameLayoutConstants.GameExecutableName"/>; if that file is
    /// absent, falls back to the single game-root <c>*.exe</c> whose ProductName
    /// (when readable) contains "Pagonia" — covers an upstream rename without
    /// hard-failing. Returns <c>false</c> (never throws) when no executable is found
    /// or it carries no version resource.
    /// </summary>
    /// <param name="productVersion">The full ProductVersion string — e.g.
    /// <c>1.3.0-11768+193445</c>, byte-for-byte a mod manifest's
    /// <c>gameDatabaseVersion</c>. Every comparison + display path keys off this.</param>
    /// <param name="fileVersion">The 4-part numeric FileVersion — e.g. <c>1.3.0.0</c>.
    /// Build/revision are lost here, so it is returned for diagnostics only and
    /// never compared.</param>
    public static bool TryRead(string gameRoot, out string? productVersion, out string? fileVersion)
    {
        productVersion = null;
        fileVersion = null;

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return false;
        }

        var exePath = ResolveExecutable(gameRoot);
        if (exePath is null)
        {
            return false;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            // ProductVersion carries the full <major>.<minor>.<patch>-<build>+<revision>
            // string. FileVersion comes back as the truncated 4-part numeric form
            // (1.3.0.0) — kept for diagnostics, never used for comparison/display.
            productVersion = NormaliseOrNull(info.ProductVersion);
            fileVersion = NormaliseOrNull(info.FileVersion);
            return productVersion is not null;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? ResolveExecutable(string gameRoot)
    {
        var named = Path.Combine(gameRoot, GameLayoutConstants.GameExecutableName);
        if (File.Exists(named))
        {
            return named;
        }

        // Fallback: upstream renamed the exe. Accept the single game-root *.exe
        // whose ProductName contains "Pagonia". Only when exactly one such exe
        // exists — never guess among several candidates (e.g. unrelated tools).
        string? candidate = null;
        foreach (var exe in Directory.EnumerateFiles(
            gameRoot, "*" + GameLayoutConstants.ExecutableExtension, SearchOption.TopDirectoryOnly))
        {
            string? product;
            try
            {
                product = FileVersionInfo.GetVersionInfo(exe).ProductName;
            }
            catch
            {
                // Unreadable resource — not a candidate.
                continue;
            }

            if (product is not null
                && product.Contains("Pagonia", StringComparison.OrdinalIgnoreCase))
            {
                if (candidate is not null)
                {
                    // Ambiguous — refuse to guess.
                    return null;
                }
                candidate = exe;
            }
        }
        return candidate;
    }

    private static string? NormaliseOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
