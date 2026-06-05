using System.Diagnostics.CodeAnalysis;

namespace PagoniaLand.Manager;

public sealed class StoreInitializeResult
{
    public string Root { get; init; } = string.Empty;
    public string StoreVersion { get; init; } = string.Empty;
    public bool CreatedState { get; init; }
    public bool CreatedDefaultProfile { get; init; }
    public IReadOnlyList<string> CreatedDirectories { get; init; } = [];

    /// <summary>
    /// True when this call created a brand-new <c>state.yaml</c> AND was asked
    /// to seed the default subscription, so the new store starts subscribed to
    /// the official catalog. False on a re-init (state already existed) or when
    /// seeding wasn't requested — so the caller only announces the seed once,
    /// and a user who opted out by removing it never gets it re-added.
    /// </summary>
    public bool SeededDefaultCatalog { get; init; }
}

public sealed class StoreInitializer
{
    // AOT: StoreInitializer writes both StoreState and a default ProfileFile via
    // YamlDotNet. ProfileStore + StoreStateReader pin these for their own use,
    // but a fresh-install code path that only invokes StoreInitializer (e.g. CI
    // tooling) would otherwise rely on cross-class pinning to survive trimming.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(StoreState))]
    [DynamicDependency(Shape, typeof(InstallRecord))]
    [DynamicDependency(Shape, typeof(OwnedExpansions))]
    [DynamicDependency(Shape, typeof(ProfileFile))]
    public StoreInitializer()
    {
    }

    /// <param name="seedDefaultCatalog">
    /// When true AND this call creates a brand-new <c>state.yaml</c>, the new
    /// store is seeded with the official catalog as its one default subscription
    /// (opt-out — the user can drop it with <c>catalog remove</c>). Defaults to
    /// false so a re-init never re-adds it and so test/CI callers get a blank
    /// store unless they ask otherwise. Only the user-facing <c>store init</c>
    /// path (CLI + interactive first-run) opts in.
    /// </param>
    public StoreInitializeResult Initialize(StoreLayout layout, bool seedDefaultCatalog = false)
    {
        var created = new List<string>();
        EnsureDirectory(layout.Root, created);
        EnsureDirectory(layout.ModsDirectory, created);
        EnsureDirectory(layout.ProfilesDirectory, created);
        EnsureDirectory(layout.CollectionsDirectory, created);
        EnsureDirectory(layout.CollectionLocksDirectory, created);

        var createdState = false;
        var seededDefaultCatalog = false;
        if (!File.Exists(layout.StateFile))
        {
            // Seed the default subscription into the very first state.yaml write
            // (one atomic write, no second pass). Tied to createdState so it
            // only ever happens on a fresh store — re-running init against an
            // existing store, or against one the user emptied via `catalog
            // remove`, leaves subscriptions exactly as they are.
            var state = new StoreState
            {
                StoreVersion = StoreLayoutConstants.CurrentStoreVersion,
                ActiveProfile = StoreLayoutConstants.DefaultProfileName,
                SubscribedCatalogs = seedDefaultCatalog
                    ? new List<string> { CatalogConstants.OfficialCatalogSource }
                    : new List<string>(),
            };
            AtomicFile.WriteAllText(layout.StateFile, ManagerYaml.CreateSerializer().Serialize(state));
            createdState = true;
            seededDefaultCatalog = seedDefaultCatalog;
        }

        var createdDefaultProfile = false;
        var defaultProfilePath = layout.ProfileFile(StoreLayoutConstants.DefaultProfileName);
        if (!File.Exists(defaultProfilePath))
        {
            var profile = new ProfileFile
            {
                ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
                Name = StoreLayoutConstants.DefaultProfileName
            };
            AtomicFile.WriteAllText(defaultProfilePath, ManagerYaml.CreateSerializer().Serialize(profile));
            createdDefaultProfile = true;
        }

        return new StoreInitializeResult
        {
            Root = layout.Root,
            StoreVersion = StoreLayoutConstants.CurrentStoreVersion,
            CreatedState = createdState,
            CreatedDefaultProfile = createdDefaultProfile,
            CreatedDirectories = created,
            SeededDefaultCatalog = seededDefaultCatalog,
        };
    }

    private static void EnsureDirectory(string path, List<string> created)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            created.Add(path);
        }
    }
}
