using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public sealed class CollectionLister
{
    private readonly ManifestReader _reader = new();

    public IReadOnlyList<InstalledCollection> List(StoreLayout layout)
    {
        var result = new List<InstalledCollection>();

        if (!Directory.Exists(layout.CollectionsDirectory))
        {
            return result;
        }

        foreach (var collectionDirectory in Directory.EnumerateDirectories(layout.CollectionsDirectory)
                     .Where(directory => !string.Equals(
                         Path.GetFileName(directory),
                         StoreLayoutConstants.CollectionLocksFolderName,
                         StringComparison.Ordinal))
                     .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase))
        {
            var collectionId = Path.GetFileName(collectionDirectory);
            if (string.IsNullOrEmpty(collectionId))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(collectionDirectory)
                         .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase))
            {
                var version = Path.GetFileName(versionDirectory);
                if (string.IsNullOrEmpty(version))
                {
                    continue;
                }

                var manifestPath = layout.CollectionManifestFile(collectionId, version);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var manifestResult = _reader.ReadCollectionManifest(manifestPath);
                var manifest = manifestResult.Value;

                var lockfilePath = layout.CollectionLockFile(collectionId);
                CollectionLock? collectionLock = null;
                if (File.Exists(lockfilePath))
                {
                    var lockResult = _reader.ReadCollectionLock(lockfilePath);
                    collectionLock = lockResult.Value;
                }

                result.Add(new InstalledCollection
                {
                    Id = collectionId,
                    Version = version,
                    Name = manifest?.Name,
                    Author = manifest?.Author,
                    GameDatabaseVersion = manifest?.GameDatabaseVersion,
                    Description = manifest?.Description,
                    ResolvedModCount = collectionLock?.Mods.Count ?? 0,
                    ManifestPath = manifestPath,
                    LockfilePath = File.Exists(lockfilePath) ? lockfilePath : null,
                    GeneratedAt = collectionLock?.GeneratedAt,
                });
            }
        }

        return result;
    }
}
