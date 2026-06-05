namespace PagoniaLand.Paker;

/// <summary>
/// Stable identifiers for the four pak shapes the classifier recognises.
/// Match against these constants instead of comparing raw strings; they show
/// up in <see cref="PakClassifyResult.Kind"/> and in the JSON report.
/// </summary>
public static class PakKinds
{
    /// <summary>
    /// Database-contributing module (Pattern A territory): `&lt;m&gt;/manifest.json`,
    /// `&lt;m&gt;/files.json` pointing at `&lt;m&gt;/&lt;m&gt;.gd.bin`, and the index
    /// itself. core.pak / dlc1.pak / decorations1.pak / tools.pak match this.
    /// </summary>
    public const string Module = "module";

    /// <summary>
    /// Map pak produced by the in-game editor (Pattern C): `&lt;m&gt;/manifest.json`
    /// + at least one `&lt;m&gt;/usermaps/*.popmap`. No files.json / .gd.bin.
    /// </summary>
    public const string UserMap = "user-map";

    /// <summary>
    /// Side-by-side overlay pak (Pattern B): `&lt;m&gt;/manifest.json` but no
    /// GameDatabase contribution and no popmap. Typically carries override
    /// files at pak root (e.g. `system.json`) or asset additions under
    /// `&lt;m&gt;/`. The camera-zoom mod from mod.io is the canonical example.
    /// </summary>
    public const string Overlay = "overlay";

    /// <summary>
    /// The pak doesn't fit any known shape — no parseable `&lt;m&gt;/manifest.json`,
    /// or multiple inconsistent module folders, or other unexpected layout.
    /// Still classifies (exit code 0); the diagnostics list explains why.
    /// </summary>
    public const string Unknown = "unknown";
}
