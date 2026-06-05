namespace PagoniaLand.Manager;

public enum CollectionUninstallOutcome
{
    Failed,
    Removed,
}

public sealed class CollectionUninstallResult
{
    public CollectionUninstallOutcome Outcome { get; init; } = CollectionUninstallOutcome.Failed;
    public string? CollectionId { get; init; }
    public bool LockfileRemoved { get; init; }
    public bool ManifestDirectoryRemoved { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class CollectionUninstaller
{
    public CollectionUninstallResult Uninstall(StoreLayout layout, string collectionId)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (string.IsNullOrWhiteSpace(collectionId))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.CollectionNotInstalled,
                "Collection id must not be empty."));
            return new CollectionUninstallResult { Diagnostics = diagnostics };
        }

        var collectionDirectory = layout.CollectionDirectory(collectionId);
        var lockfilePath = layout.CollectionLockFile(collectionId);

        // `collectionId` comes straight from CLI args and is concatenated into the
        // store path. Without this check an id like "../../x" would let the
        // Directory.Delete / File.Delete below reach arbitrary paths via traversal —
        // the same path-traversal data-loss bug ModUninstaller guards against.
        if (!IsWithinRoot(collectionDirectory, layout.CollectionsDirectory)
            || !IsWithinRoot(lockfilePath, layout.CollectionLocksDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.CollectionNotInstalled,
                $"Collection id '{collectionId}' resolves outside the store and was refused."));
            return new CollectionUninstallResult { CollectionId = collectionId, Diagnostics = diagnostics };
        }

        if (!Directory.Exists(collectionDirectory) && !File.Exists(lockfilePath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.CollectionNotInstalled,
                $"Collection '{collectionId}' is not installed in this store."));
            return new CollectionUninstallResult { CollectionId = collectionId, Diagnostics = diagnostics };
        }

        var manifestDirectoryRemoved = false;
        if (Directory.Exists(collectionDirectory))
        {
            Directory.Delete(collectionDirectory, recursive: true);
            manifestDirectoryRemoved = true;
        }

        var lockfileRemoved = false;
        if (File.Exists(lockfilePath))
        {
            File.Delete(lockfilePath);
            lockfileRemoved = true;
        }

        return new CollectionUninstallResult
        {
            Outcome = CollectionUninstallOutcome.Removed,
            CollectionId = collectionId,
            LockfileRemoved = lockfileRemoved,
            ManifestDirectoryRemoved = manifestDirectoryRemoved,
            Diagnostics = diagnostics,
        };
    }

    // True when `candidate` resolves to a path at or below `root` — the guard that
    // stops a crafted collection id from escaping the store via "../" traversal.
    private static bool IsWithinRoot(string candidate, string root)
    {
        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
        {
            rootFull += Path.DirectorySeparatorChar;
        }
        var candidateFull = Path.GetFullPath(candidate);
        return candidateFull.StartsWith(rootFull, StringComparison.Ordinal);
    }
}
