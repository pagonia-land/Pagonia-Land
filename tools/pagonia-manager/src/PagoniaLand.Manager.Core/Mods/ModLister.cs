using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class ModLister
{
    // AOT: pin the install-sidecar model so YamlDotNet's reflection survives trimming.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields,
        typeof(InstallSidecar))]
    public ModLister()
    {
    }

    public IReadOnlyList<InstalledMod> List(StoreLayout layout)
    {
        var result = new List<InstalledMod>();

        if (!Directory.Exists(layout.ModsDirectory))
        {
            return result;
        }

        foreach (var modDirectory in Directory.EnumerateDirectories(layout.ModsDirectory)
                     .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase))
        {
            var modId = Path.GetFileName(modDirectory);
            if (string.IsNullOrEmpty(modId))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(modDirectory)
                         .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase))
            {
                var version = Path.GetFileName(versionDirectory);
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                var sidecar = TryReadSidecar(Path.Combine(versionDirectory, ModInstaller.SidecarFileName));

                result.Add(new InstalledMod
                {
                    Id = modId,
                    Version = version,
                    InstallPath = versionDirectory,
                    InstalledAt = sidecar?.InstalledAt,
                    SourcePath = sidecar?.SourcePath,
                    SourceType = sidecar?.SourceType,
                    ManifestName = sidecar?.ManifestName,
                    Source = sidecar?.Source,
                });
            }
        }

        return result;
    }

    private static InstallSidecar? TryReadSidecar(string sidecarPath)
    {
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(sidecarPath);
            return ManagerYaml.CreateDeserializer().Deserialize<InstallSidecar>(yaml);
        }
        catch
        {
            return null;
        }
    }
}
