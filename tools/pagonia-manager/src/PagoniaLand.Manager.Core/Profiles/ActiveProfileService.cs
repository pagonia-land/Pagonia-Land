namespace PagoniaLand.Manager;

public sealed class ActiveProfileResult
{
    /// <summary>True if the service handled the request without a system error.</summary>
    public bool Success { get; init; }

    /// <summary>
    /// True only when the underlying ProfileMutator actually changed the profile.
    /// False on no-op outcomes — e.g. disable on a non-enabled mod, enable on an
    /// already-enabled id+version — where the mutator emits a warning and writes
    /// nothing to disk. CLI/UI callers must check Mutated before printing a
    /// "Disabled X" / "Enabled X" confirmation; the warning diagnostic alone
    /// would otherwise sit next to a contradicting success line.
    /// </summary>
    public bool Mutated { get; init; }

    public string? ProfileName { get; init; }
    public ProfileFile? Profile { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ActiveProfileService
{
    private readonly StoreStateReader _stateReader = new();
    private readonly ProfileStore _profileStore = new();
    private readonly ProfileMutator _mutator = new();

    public ActiveProfileResult Show(StoreLayout layout)
        => LoadActive(layout);

    public ActiveProfileResult Enable(StoreLayout layout, string modId, string? requestedVersion)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        var modDirectory = Path.Combine(layout.ModsDirectory, modId);
        if (!Directory.Exists(modDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModNotInstalled,
                $"Mod '{modId}' is not installed in the store."));
            return new ActiveProfileResult { Diagnostics = diagnostics };
        }

        string? version = requestedVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            version = ResolveLatestInstalledVersion(modDirectory);
            if (version is null)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModNotInstalled,
                    $"Mod '{modId}' has no installed versions under '{modDirectory}'."));
                return new ActiveProfileResult { Diagnostics = diagnostics };
            }
        }
        else if (!Directory.Exists(layout.ModVersionDirectory(modId, version)))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModVersionNotInstalled,
                $"Mod '{modId}' version '{version}' is not installed."));
            return new ActiveProfileResult { Diagnostics = diagnostics };
        }

        var loaded = LoadActive(layout);
        if (!loaded.Success || loaded.Profile is null)
        {
            return loaded;
        }

        var mutation = _mutator.Enable(loaded.Profile, modId, version);
        diagnostics.AddRange(mutation.Diagnostics);
        if (mutation.Mutated)
        {
            _profileStore.Write(layout, mutation.Profile);
        }

        return new ActiveProfileResult
        {
            Success = true,
            Mutated = mutation.Mutated,
            ProfileName = loaded.ProfileName,
            Profile = mutation.Profile,
            Diagnostics = diagnostics,
        };
    }

    public ActiveProfileResult Disable(StoreLayout layout, string modId)
        => Mutate(layout, profile => _mutator.Disable(profile, modId));

    public ActiveProfileResult MoveToPosition(StoreLayout layout, string modId, int position1Based)
        => Mutate(layout, profile => _mutator.MoveToPosition(profile, modId, position1Based));

    public ActiveProfileResult MoveBefore(StoreLayout layout, string modId, string anchorId)
        => Mutate(layout, profile => _mutator.MoveBefore(profile, modId, anchorId));

    public ActiveProfileResult MoveAfter(StoreLayout layout, string modId, string anchorId)
        => Mutate(layout, profile => _mutator.MoveAfter(profile, modId, anchorId));

    private ActiveProfileResult Mutate(StoreLayout layout, Func<ProfileFile, ProfileMutationResult> mutate)
    {
        var loaded = LoadActive(layout);
        if (!loaded.Success || loaded.Profile is null)
        {
            return loaded;
        }

        var mutation = mutate(loaded.Profile);
        if (mutation.Mutated)
        {
            _profileStore.Write(layout, mutation.Profile);
        }

        return new ActiveProfileResult
        {
            Success = true,
            Mutated = mutation.Mutated,
            ProfileName = loaded.ProfileName,
            Profile = mutation.Profile,
            Diagnostics = mutation.Diagnostics,
        };
    }

    private ActiveProfileResult LoadActive(StoreLayout layout)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ActiveProfileResult { Diagnostics = diagnostics };
        }

        var state = _stateReader.Read(layout);
        var profileName = string.IsNullOrWhiteSpace(state.ActiveProfile)
            ? StoreLayoutConstants.DefaultProfileName
            : state.ActiveProfile!;

        if (!_profileStore.Exists(layout, profileName))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Active profile '{profileName}' has no file at '{layout.ProfileFile(profileName)}'."));
            return new ActiveProfileResult { ProfileName = profileName, Diagnostics = diagnostics };
        }

        var profile = _profileStore.Read(layout, profileName);
        return new ActiveProfileResult
        {
            Success = true,
            ProfileName = profileName,
            Profile = profile,
            Diagnostics = diagnostics,
        };
    }

    private static string? ResolveLatestInstalledVersion(string modDirectory)
    {
        var candidates = new List<(string Version, DateTimeOffset Stamp)>();

        foreach (var versionDirectory in Directory.EnumerateDirectories(modDirectory))
        {
            var versionName = Path.GetFileName(versionDirectory);
            if (string.IsNullOrEmpty(versionName))
            {
                continue;
            }

            var stamp = ReadSidecarTimestamp(Path.Combine(versionDirectory, ModInstaller.SidecarFileName))
                        ?? new DateTimeOffset(Directory.GetCreationTimeUtc(versionDirectory), TimeSpan.Zero);

            candidates.Add((versionName, stamp));
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort((a, b) => b.Stamp.CompareTo(a.Stamp));
        return candidates[0].Version;
    }

    private static DateTimeOffset? ReadSidecarTimestamp(string sidecarPath)
    {
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(sidecarPath);
            var sidecar = ManagerYaml.CreateDeserializer().Deserialize<InstallSidecar>(yaml);
            if (sidecar is null || string.IsNullOrWhiteSpace(sidecar.InstalledAt))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(
                    sidecar.InstalledAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // Malformed sidecar -> fall through to directory mtime fallback in caller.
        }

        return null;
    }
}
