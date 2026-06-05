namespace PagoniaLand.Manager;

public sealed class StoreInfo
{
    public string Root { get; init; } = string.Empty;
    public bool Initialised { get; init; }
    public string? StoreVersion { get; init; }
    public string? ActiveProfile { get; init; }
    public int InstalledModCount { get; init; }
    public int ProfileCount { get; init; }
    public int CollectionCount { get; init; }
}

public sealed class StoreInspector
{
    public StoreInfo Inspect(StoreLayout layout)
    {
        if (!File.Exists(layout.StateFile))
        {
            return new StoreInfo { Root = layout.Root, Initialised = false };
        }

        var state = new StoreStateReader().Read(layout);

        return new StoreInfo
        {
            Root = layout.Root,
            Initialised = true,
            StoreVersion = state.StoreVersion,
            ActiveProfile = state.ActiveProfile,
            InstalledModCount = CountInstalledMods(layout),
            ProfileCount = CountProfiles(layout),
            CollectionCount = CountCollections(layout)
        };
    }

    private static int CountInstalledMods(StoreLayout layout)
    {
        if (!Directory.Exists(layout.ModsDirectory))
        {
            return 0;
        }

        return Directory.EnumerateDirectories(layout.ModsDirectory).Count();
    }

    private static int CountProfiles(StoreLayout layout)
        => AtomicFile.EnumerateFilesIgnoringTemp(layout.ProfilesDirectory,
            "*" + StoreLayoutConstants.ProfileFileSuffix).Count();

    private static int CountCollections(StoreLayout layout)
    {
        if (!Directory.Exists(layout.CollectionsDirectory))
        {
            return 0;
        }

        return Directory.EnumerateDirectories(layout.CollectionsDirectory)
            .Count(directory => !string.Equals(
                Path.GetFileName(directory),
                StoreLayoutConstants.CollectionLocksFolderName,
                StringComparison.Ordinal));
    }
}
