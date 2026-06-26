using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Guards the manager's own <b>internal</b> format-versioned files — <c>state.yaml</c>
/// (<c>storeVersion</c>) and profiles (<c>profileVersion</c>) — on read. Unlike the public
/// author↔consumer formats (handled by the shared <see cref="FormatVersionPolicy"/> / the
/// patcher), these never cross between authors and consumers: a single installation both
/// writes and reads them. So they don't need a forward-compatible field-ignoring reader —
/// they need the one protection that <em>can't</em> be retrofitted once an old binary is in
/// the wild: refusing a file written by a <b>newer</b> manager, so that older binary doesn't
/// read it, silently drop the fields it doesn't know, and clobber the newer manager's data on
/// the next write.
///
/// <para>
/// The check is deliberately minimal: it refuses <b>only</b> a version clearly newer than this
/// build (any newer major or, within the same major, a newer minor). An unparseable or absent
/// version is treated as legacy and tolerated — the readers never checked it before, and an
/// older same-major minor reads normally (and rises to the current minor whenever the writers
/// start stamping current, which lands with the first real <c>storeVersion</c>/<c>profileVersion</c>
/// bump). It reuses the shared <c>MAJOR.MINOR</c> vocabulary via <see cref="FormatVersionPolicy.TryParse"/>.
/// </para>
/// </summary>
internal static class InternalFormatVersionGuard
{
    /// <summary>
    /// Throws the same <c>[code]</c>-prefixed <see cref="InvalidOperationException"/> the readers
    /// already use for a bad file when <paramref name="declared"/> is newer than <paramref name="current"/>.
    /// Returns quietly otherwise (current, older, or unparseable/legacy).
    /// </summary>
    public static void EnsureNotNewer(string declared, string current, string fieldLabel, string code, string artifactPath)
    {
        if (!FormatVersionPolicy.TryParse(declared, out var fileVersion)
            || !FormatVersionPolicy.TryParse(current, out var buildVersion))
        {
            return;
        }

        var newer = fileVersion.Major > buildVersion.Major
            || (fileVersion.Major == buildVersion.Major && fileVersion.Minor > buildVersion.Minor);
        if (newer)
        {
            throw new InvalidOperationException(
                $"[{code}] {fieldLabel} {fileVersion} at '{artifactPath}' was written by a newer pagonia-manager " +
                $"(this build understands {buildVersion}). Upgrade pagonia-manager to use it.");
        }
    }
}
