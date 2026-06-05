using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class ProfileStore
{
    // YamlDotNet uses reflection on these types via Deserialize<ProfileFile>(). Tell the AOT
    // linker to keep their public constructors + properties whenever ProfileStore is reachable.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(ProfileFile))]
    [DynamicDependency(Shape, typeof(ProfileEnabledMod))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<ProfileEnabledMod>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<string>))]
    public ProfileStore()
    {
    }

    public bool Exists(StoreLayout layout, string profileName)
        => File.Exists(layout.ProfileFile(profileName));

    public ProfileFile Read(StoreLayout layout, string profileName)
    {
        var path = layout.ProfileFile(profileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"[{ManagerDiagnosticCodes.ProfileMissing}] " +
                $"Profile '{profileName}' not found at '{path}'.");
        }

        var yaml = File.ReadAllText(path);
        var profile = ManagerYaml.CreateDeserializer().Deserialize<ProfileFile>(yaml);
        if (profile is null)
        {
            throw new InvalidOperationException(
                $"[{ManagerDiagnosticCodes.ProfileMissing}] " +
                $"Profile file '{path}' is empty or invalid.");
        }

        return profile;
    }

    public void Write(StoreLayout layout, ProfileFile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile.Name must not be empty.", nameof(profile));
        }

        var path = layout.ProfileFile(profile.Name);
        AtomicFile.WriteAllText(path, ManagerYaml.CreateSerializer().Serialize(profile));
    }
}
