namespace PagoniaLand.Manager;

public sealed class ProfileOperationResult
{
    public bool Success { get; init; }
    public string? ProfileName { get; init; }
    public ProfileFile? Profile { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ProfileListResult
{
    public bool Success { get; init; }
    public string? ActiveProfile { get; init; }
    public IReadOnlyList<ProfileSummary> Profiles { get; init; } = [];
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ProfileLifecycleService
{
    private readonly StoreStateReader _stateReader = new();
    private readonly StoreStateWriter _stateWriter = new();
    private readonly ProfileStore _profileStore = new();

    public ProfileOperationResult Create(StoreLayout layout, string profileName)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        var preflight = PreflightWithName(layout, profileName, requireExists: false, diagnostics);
        if (preflight is not null)
        {
            return preflight;
        }

        if (_profileStore.Exists(layout, profileName))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileAlreadyExists,
                $"Profile '{profileName}' already exists at '{layout.ProfileFile(profileName)}'."));
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        var profile = new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = profileName,
        };
        _profileStore.Write(layout, profile);

        return new ProfileOperationResult
        {
            Success = true,
            ProfileName = profileName,
            Profile = profile,
            Diagnostics = diagnostics,
        };
    }

    public ProfileListResult List(StoreLayout layout)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ProfileListResult { Diagnostics = diagnostics };
        }

        var state = _stateReader.Read(layout);
        var activeName = state.ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
        var summaries = new List<ProfileSummary>();

        foreach (var profilePath in AtomicFile
                     .EnumerateFilesIgnoringTemp(layout.ProfilesDirectory,
                         "*" + StoreLayoutConstants.ProfileFileSuffix)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(profilePath);
            var name = fileName[..^StoreLayoutConstants.ProfileFileSuffix.Length];

            ProfileFile? profile = null;
            try
            {
                profile = _profileStore.Read(layout, name);
            }
            catch
            {
                // Listing should not fail on a single malformed file; surface it as 0 mods and let
                // the user investigate with `profile show <name>` which raises the real diagnostic.
            }

            summaries.Add(new ProfileSummary
            {
                Name = name,
                IsActive = string.Equals(name, activeName, StringComparison.Ordinal),
                IsDefault = string.Equals(name, StoreLayoutConstants.DefaultProfileName, StringComparison.Ordinal),
                EnabledModCount = profile?.EnabledMods.Count ?? 0,
                Collection = profile?.Collection,
                FilePath = profilePath,
            });
        }

        return new ProfileListResult
        {
            Success = true,
            ActiveProfile = activeName,
            Profiles = summaries,
            Diagnostics = diagnostics,
        };
    }

    public ProfileOperationResult Use(StoreLayout layout, string profileName)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        var preflight = PreflightWithName(layout, profileName, requireExists: true, diagnostics);
        if (preflight is not null)
        {
            return preflight;
        }

        var state = _stateReader.Read(layout);
        if (string.Equals(state.ActiveProfile, profileName, StringComparison.Ordinal))
        {
            return new ProfileOperationResult
            {
                Success = true,
                ProfileName = profileName,
                Profile = _profileStore.Read(layout, profileName),
                Diagnostics = diagnostics,
            };
        }

        var newState = new StoreState
        {
            StoreVersion = state.StoreVersion,
            ActiveProfile = profileName,
            LastDeploy = state.LastDeploy,
            DefaultGameRoot = state.DefaultGameRoot,
            SubscribedCatalogs = state.SubscribedCatalogs,
            CatalogMaxDepth = state.CatalogMaxDepth,
            AllowInsecureSources = state.AllowInsecureSources,
            CatalogCacheStalenessHours = state.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = state.AllowInsecureCatalogSources,
            Installs = state.Installs,
        };
        _stateWriter.Write(layout, newState);

        return new ProfileOperationResult
        {
            Success = true,
            ProfileName = profileName,
            Profile = _profileStore.Read(layout, profileName),
            Diagnostics = diagnostics,
        };
    }

    public ProfileOperationResult Copy(StoreLayout layout, string source, string target, bool activate)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ProfileOperationResult { ProfileName = source, Diagnostics = diagnostics };
        }

        // Source must be a valid, existing profile.
        if (!ProfileNameValidator.IsValid(source, out var sourceReason))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileNameInvalid,
                $"Invalid source profile name '{source}': {sourceReason}"));
            return new ProfileOperationResult { ProfileName = source, Diagnostics = diagnostics };
        }

        if (!_profileStore.Exists(layout, source))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Source profile '{source}' has no file at '{layout.ProfileFile(source)}'."));
            return new ProfileOperationResult { ProfileName = source, Diagnostics = diagnostics };
        }

        // Target must be a valid name that isn't already taken (no implicit overwrite —
        // delete the target first, mirroring how `profile create` refuses to clobber).
        if (!ProfileNameValidator.IsValid(target, out var targetReason))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileNameInvalid,
                $"Invalid target profile name '{target}': {targetReason}"));
            return new ProfileOperationResult { ProfileName = target, Diagnostics = diagnostics };
        }

        if (_profileStore.Exists(layout, target))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileAlreadyExists,
                $"Profile '{target}' already exists at '{layout.ProfileFile(target)}'; delete it first to overwrite."));
            return new ProfileOperationResult { ProfileName = target, Diagnostics = diagnostics };
        }

        // Deep value-copy: each enabled mod (and its tweak overrides) is reconstructed so the
        // copy shares no mutable state with the source. The two profiles are separate files
        // anyway, but copying the collections keeps the in-memory result independent too.
        var sourceProfile = _profileStore.Read(layout, source);
        var copy = new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = target,
            Collection = sourceProfile.Collection,
            EnabledMods = sourceProfile.EnabledMods
                .Select(mod => new ProfileEnabledMod
                {
                    Id = mod.Id,
                    Version = mod.Version,
                    Tweaks = mod.Tweaks is null
                        ? null
                        : new Dictionary<string, string>(mod.Tweaks),
                })
                .ToList(),
            LoadOrder = [.. sourceProfile.LoadOrder],
        };
        _profileStore.Write(layout, copy);

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.ProfileCopied,
            $"Copied profile '{source}' to '{target}'."));

        if (activate)
        {
            // Reuse Use() so the active-profile switch preserves every state.yaml field
            // (last deploy, catalogs, default game root, …) through one tested path.
            var use = Use(layout, target);
            diagnostics.AddRange(use.Diagnostics);
            if (!use.Success)
            {
                return new ProfileOperationResult { ProfileName = target, Profile = copy, Diagnostics = diagnostics };
            }
        }

        return new ProfileOperationResult
        {
            Success = true,
            ProfileName = target,
            Profile = copy,
            Diagnostics = diagnostics,
        };
    }

    public ProfileOperationResult Delete(StoreLayout layout, string profileName)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        var preflight = PreflightWithName(layout, profileName, requireExists: true, diagnostics);
        if (preflight is not null)
        {
            return preflight;
        }

        if (string.Equals(profileName, StoreLayoutConstants.DefaultProfileName, StringComparison.Ordinal))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileDefaultDeletion,
                $"Profile '{profileName}' is the default profile and cannot be deleted."));
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        var state = _stateReader.Read(layout);
        if (string.Equals(state.ActiveProfile, profileName, StringComparison.Ordinal))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileActiveDeletion,
                $"Profile '{profileName}' is the active profile; switch to another profile before deleting it."));
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        File.Delete(layout.ProfileFile(profileName));

        return new ProfileOperationResult
        {
            Success = true,
            ProfileName = profileName,
            Diagnostics = diagnostics,
        };
    }

    public ProfileOperationResult Show(StoreLayout layout, string? profileName)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ProfileOperationResult { Diagnostics = diagnostics };
        }

        var state = _stateReader.Read(layout);
        var name = string.IsNullOrWhiteSpace(profileName)
            ? state.ActiveProfile ?? StoreLayoutConstants.DefaultProfileName
            : profileName!;

        if (!ProfileNameValidator.IsValid(name, out var reason))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileNameInvalid,
                $"Invalid profile name '{name}': {reason}"));
            return new ProfileOperationResult { ProfileName = name, Diagnostics = diagnostics };
        }

        if (!_profileStore.Exists(layout, name))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Profile '{name}' has no file at '{layout.ProfileFile(name)}'."));
            return new ProfileOperationResult { ProfileName = name, Diagnostics = diagnostics };
        }

        var profile = _profileStore.Read(layout, name);
        return new ProfileOperationResult
        {
            Success = true,
            ProfileName = name,
            Profile = profile,
            Diagnostics = diagnostics,
        };
    }

    private ProfileOperationResult? PreflightWithName(
        StoreLayout layout,
        string profileName,
        bool requireExists,
        List<ManagerDiagnostic> diagnostics)
    {
        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        if (!ProfileNameValidator.IsValid(profileName, out var reason))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileNameInvalid,
                $"Invalid profile name '{profileName}': {reason}"));
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        if (requireExists && !_profileStore.Exists(layout, profileName))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Profile '{profileName}' has no file at '{layout.ProfileFile(profileName)}'."));
            return new ProfileOperationResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        return null;
    }
}
