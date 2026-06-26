namespace PagoniaLand.Manager;

public enum UninstallOutcome
{
    Failed,
    Removed,
}

public sealed class UninstallResult
{
    public UninstallOutcome Outcome { get; init; } = UninstallOutcome.Failed;
    public string? ModId { get; init; }
    public string? Version { get; init; }
    public string? RemovedPath { get; init; }
    public bool ParentDirectoryPruned { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ModUninstaller
{
    public UninstallResult Uninstall(string modId, string? version, StoreLayout layout)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (string.IsNullOrWhiteSpace(modId))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModNotInstalled,
                "Mod id must not be empty."));
            return new UninstallResult { Diagnostics = diagnostics };
        }

        var modDirectory = Path.Combine(layout.ModsDirectory, modId);

        // Refuse modIds that resolve outside the store's mods directory. Without
        // this check, an id like "../outside" would let Directory.Delete reach
        // arbitrary paths via traversal — a path-traversal data-loss bug.
        if (!IsWithinModsRoot(modDirectory, layout.ModsDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModNotInstalled,
                $"Mod id '{modId}' resolves outside the store and was refused."));
            return new UninstallResult { ModId = modId, Diagnostics = diagnostics };
        }

        if (!Directory.Exists(modDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModNotInstalled,
                $"Mod '{modId}' is not installed in the store."));
            return new UninstallResult { ModId = modId, Diagnostics = diagnostics };
        }

        var installedVersions = Directory.EnumerateDirectories(modDirectory)
            .Select(directory => Path.GetFileName(directory))
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (version is null)
        {
            if (installedVersions.Count == 0)
            {
                // Previously this path deleted modDirectory recursively BEFORE returning the
                // error — a failure-path side-effect that contradicted the error message and
                // wiped any loose files in the mod folder (partial installs, manual edits).
                // Report the failure without touching the directory.
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModNotInstalled,
                    $"Mod '{modId}' is not installed (no version directories under '{modDirectory}')."));
                return new UninstallResult { ModId = modId, Diagnostics = diagnostics };
            }

            if (installedVersions.Count > 1)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModVersionAmbiguous,
                    $"Mod '{modId}' has multiple installed versions ({string.Join(", ", installedVersions)}); pass --version to choose one."));
                return new UninstallResult { ModId = modId, Diagnostics = diagnostics };
            }

            version = installedVersions[0]!;
        }

        var versionDirectory = layout.ModVersionDirectory(modId, version);
        if (!Directory.Exists(versionDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModVersionNotInstalled,
                $"Mod '{modId}' version '{version}' is not installed."));
            return new UninstallResult { ModId = modId, Version = version, Diagnostics = diagnostics };
        }

        // Advisory: warn if the active profile's enabled mods depend on the one being removed —
        // before it silently breaks them. Best-effort + never blocks: any failure to resolve the
        // profile (uninitialised store, missing profile) just skips the check.
        WarnIfDependedUpon(layout, modId, diagnostics);

        Directory.Delete(versionDirectory, recursive: true);

        var parentPruned = false;
        if (!Directory.EnumerateFileSystemEntries(modDirectory).Any())
        {
            Directory.Delete(modDirectory);
            parentPruned = true;
        }

        return new UninstallResult
        {
            Outcome = UninstallOutcome.Removed,
            ModId = modId,
            Version = version,
            RemovedPath = versionDirectory,
            ParentDirectoryPruned = parentPruned,
            Diagnostics = diagnostics,
        };
    }

    private static void WarnIfDependedUpon(StoreLayout layout, string modId, List<ManagerDiagnostic> diagnostics)
    {
        try
        {
            if (!new StoreStateReader().Exists(layout))
            {
                return;
            }
            var state = new StoreStateReader().Read(layout);
            var profileName = string.IsNullOrWhiteSpace(state.ActiveProfile)
                ? StoreLayoutConstants.DefaultProfileName
                : state.ActiveProfile!;
            var profileStore = new ProfileStore();
            if (!profileStore.Exists(layout, profileName))
            {
                return;
            }

            var dependents = ModDependencyDetector.DependentsOf(
                EnabledModSet.Load(layout, profileStore.Read(layout, profileName)), modId);
            if (dependents.Count > 0)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ModDependedUponByOthers,
                    $"Uninstalling '{modId}', but {string.Join(", ", dependents)} (enabled in profile '{profileName}') depend(s) on it — that dependency may be unmet if no other version of '{modId}' remains enabled."));
            }
        }
        catch
        {
            // Advisory only — never let the dependents check abort an uninstall.
        }
    }

    /// <summary>
    /// Returns true if the candidate path (after .. and . normalisation)
    /// resolves to a child of modsRoot. Defence against path traversal: an
    /// untrusted modId fed into Path.Combine can produce paths like "../foo"
    /// or absolute paths that escape the store; this guard refuses them
    /// before any filesystem mutation happens.
    /// </summary>
    private static bool IsWithinModsRoot(string candidate, string modsRoot)
    {
        var rootFull = Path.GetFullPath(modsRoot);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
        {
            rootFull += Path.DirectorySeparatorChar;
        }
        var candidateFull = Path.GetFullPath(candidate);
        return candidateFull.StartsWith(rootFull, StringComparison.Ordinal);
    }
}
