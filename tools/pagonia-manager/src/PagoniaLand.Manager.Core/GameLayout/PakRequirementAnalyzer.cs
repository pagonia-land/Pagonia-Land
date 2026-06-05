using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// figures out which canonical paks the active profile's
/// mods actually touch, so <see cref="PakCacheService"/> only extracts those
/// instead of every <c>pak/*.pak</c> in the install. Reads each enabled mod's
/// patch files via <see cref="ManifestReader"/> and collects the first path
/// segment of every operation's <c>target.file</c> (e.g.
/// <c>"core/gdb/buildings.gd.xml"</c> → <c>"core"</c>).
/// <para>Conservative when in doubt: returns <c>null</c> on any error
/// (profile missing, mod missing, manifest unparseable) so the caller falls
/// back to PakCacheService's "extract everything" behaviour rather than
/// silently extracting nothing.</para>
/// </summary>
public static class PakRequirementAnalyzer
{
    /// <summary>
    /// Compute the set of pak basenames the named profile (or the active
    /// profile if <paramref name="profileName"/> is null) needs warm.
    /// Returns <c>null</c> to signal "I don't know — extract everything" on
    /// any failure path.
    /// </summary>
    public static IReadOnlyCollection<string>? ComputeRequiredPaks(StoreLayout layout, string? profileName)
    {
        try
        {
            var stateReader = new StoreStateReader();
            if (!stateReader.Exists(layout)) return null;

            var resolvedName = string.IsNullOrWhiteSpace(profileName)
                ? stateReader.Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName
                : profileName!;

            var profileStore = new ProfileStore();
            if (!profileStore.Exists(layout, resolvedName)) return null;

            var profile = profileStore.Read(layout, resolvedName);
            if (profile.EnabledMods.Count == 0)
            {
                // Empty profile: no mods, no paks needed. Return an empty set
                // (NOT null) so the cache call short-circuits without
                // extracting all paks.
                return Array.Empty<string>();
            }

            var manifestReader = new ManifestReader();
            var paks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var enabled in profile.EnabledMods)
            {
                var modDir = layout.ModVersionDirectory(enabled.Id, enabled.Version);
                if (!Directory.Exists(modDir)) return null; // mod missing → fall back

                var loaded = manifestReader.ReadMod(modDir);
                if (loaded.Value is null) return null; // manifest unparseable → fall back

                foreach (var patchFile in loaded.Value.PatchFiles)
                {
                    foreach (var op in patchFile.PatchFile.Operations)
                    {
                        var basename = FirstPathSegment(op.Target.File);
                        if (!string.IsNullOrEmpty(basename))
                        {
                            paks.Add(basename);
                        }
                    }
                }

                // Pattern B overlay paks (pak: block in manifest) don't need an
                // extract — they get built fresh from the mod scaffold. Skip
                // them from the required-paks set.
            }

            return paks;
        }
        catch
        {
            // Unknown error → conservative fallback to "extract everything".
            return null;
        }
    }

    private static string FirstPathSegment(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var normalized = path.Replace('\\', '/');
        var slash = normalized.IndexOf('/');
        return slash < 0 ? normalized : normalized[..slash];
    }
}
