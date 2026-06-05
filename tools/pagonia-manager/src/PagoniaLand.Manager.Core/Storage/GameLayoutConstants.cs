namespace PagoniaLand.Manager;

/// <summary>
/// Game-install-side layout constants — mirrors <see cref="StoreLayoutConstants"/>
/// for paths on the player's Pioneers of Pagonia game install (game-root). Keep
/// this in sync if upstream renames its mods/ folder, its system.json file, or
/// changes the pak extension.
/// </summary>
public static class GameLayoutConstants
{
    /// <summary>Folder inside the game install that holds the canonical paks
    /// (core.pak, decorations1.pak, dlc1.pak, tools.pak, …). the live-install path reads
    /// these to populate the extract cache for pak-aware deploys.</summary>
    public const string PakFolderName = "pak";

    /// <summary>Folder inside the game install that holds Pattern B overlay paks.</summary>
    public const string ModsFolderName = "mods";

    /// <summary>Extension used for Pioneers of Pagonia pak archives.</summary>
    public const string PakExtension = ".pak";

    /// <summary>
    /// The game executable at the install root. Its Win32 ProductVersion is the
    /// authoritative game version — byte-for-byte the same string mods declare as
    /// <c>gameDatabaseVersion</c> (e.g. <c>1.3.0-11768+193445</c>).
    /// <see cref="GameVersionReader"/> reads it; if this exact name is absent it
    /// falls back to the single game-root <c>*.exe</c> whose ProductName contains
    /// "Pagonia", so an upstream rename degrades gracefully instead of hard-failing.
    /// </summary>
    public const string GameExecutableName = "Pioneers of Pagonia.exe";

    /// <summary>Extension used for the game executable.</summary>
    public const string ExecutableExtension = ".exe";

    /// <summary>
    /// File at the game-install root used as the secondary input to
    /// <see cref="GameFingerprint"/>. Includes game version info, so changes here
    /// distinguish installs after game updates.
    /// </summary>
    public const string SystemFingerprintFile = "system.json";

    /// <summary>
    /// Prefix used for transient staging pak files <see cref="DeployService"/>
    /// writes into the per-deploy staging tree before copying to game/mods/.
    /// The leading dot keeps them out of normal sort/listing views.
    /// </summary>
    public const string PakBuildStagingPrefix = ".built-";

    /// <summary>
    /// Path to a Pattern B overlay pak inside the game install, relative to
    /// game-root, using forward slashes (the deploy-manifest convention).
    /// </summary>
    public static string PakTargetRelativePath(string pakName)
        => $"{ModsFolderName}/{pakName}{PakExtension}";

    /// <summary>
    /// Filename for a transient staging pak built by <c>PakBuilder</c> before
    /// copying to <see cref="PakTargetRelativePath"/>.
    /// </summary>
    public static string PakStagingFileName(string pakName)
        => $"{PakBuildStagingPrefix}{pakName}{PakExtension}";

    /// <summary>
    /// Default Steam install path on Windows. Pioneers of Pagonia is steam-only
    /// today, and Steam's per-library install path is configurable but the
    /// default library lives here on the C: drive — covers the vast majority
    /// of Windows installs. <see cref="GameRootResolver"/> only suggests this
    /// when the directory actually exists. Empty string on non-Windows OSes;
    /// the resolver treats empty as "no platform default available".
    /// </summary>
    public static string WindowsSteamDefaultPath { get; } =
        OperatingSystem.IsWindows()
            ? @"C:\Program Files (x86)\Steam\steamapps\common\Pioneers of Pagonia"
            : string.Empty;
}
