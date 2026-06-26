using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Loads the manifests of a profile's enabled mods that are installed on disk — the input the
/// dependency / load-order analyses operate on. Best-effort: an enabled mod that isn't installed, or
/// whose manifest can't be read, is skipped (the plan / doctor paths report those separately).
/// </summary>
public static class EnabledModSet
{
    public static List<LoadedMod> Load(StoreLayout layout, ProfileFile profile)
    {
        var reader = new ManifestReader();
        var result = new List<LoadedMod>();
        foreach (var enabled in profile.EnabledMods)
        {
            var directory = layout.ModVersionDirectory(enabled.Id, enabled.Version);
            if (!Directory.Exists(directory))
            {
                continue;
            }
            var read = reader.ReadMod(directory);
            if (read.Value is not null)
            {
                result.Add(read.Value);
            }
        }
        return result;
    }
}
