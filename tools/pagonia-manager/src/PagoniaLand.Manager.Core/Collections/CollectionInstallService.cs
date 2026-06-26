using System.Diagnostics.CodeAnalysis;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public enum CollectionInstallOutcome
{
    Failed,
    Installed,
    AlreadyInstalled,
}

public sealed class CollectionInstallResult
{
    public CollectionInstallOutcome Outcome { get; init; } = CollectionInstallOutcome.Failed;
    public string? CollectionId { get; init; }
    public string? CollectionVersion { get; init; }
    public string? CollectionName { get; init; }
    public string? ProfileName { get; init; }
    public bool ProfileActivated { get; init; }
    public string? ManifestPath { get; init; }
    public string? LockfilePath { get; init; }
    public IReadOnlyList<(string Id, string Version)> InstalledMods { get; init; } = [];
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// Extra knobs for <see cref="CollectionInstallService.Install"/>. The
/// zero-options overload uses defaults that match the original local-only
/// behaviour; remote callers populate the new fields to thread remote
/// provenance into the lockfile and flip the active profile in one shot.
/// </summary>
public sealed class CollectionInstallOptions
{
    /// <summary>Override the auto-derived profile name (defaults to the collection id).</summary>
    public string? ProfileNameOverride { get; init; }

    /// <summary>When true, an existing profile with the chosen name is overwritten in-place instead of blocking the install. Lockfile + manifest semantics are unchanged — only the profile slot's replacement rule differs.</summary>
    public bool Overwrite { get; init; }

    /// <summary>When true, set <see cref="StoreState.ActiveProfile"/> to the new profile after a successful install. The next plan / deploy targets it. Only valid when an install actually creates a profile.</summary>
    public bool Activate { get; init; }

    /// <summary>For remote-fetch installs: mod id -> resolved "gh:owner/repo#&lt;sha&gt;/&lt;id&gt;" origin. Used to populate the lockfile's per-mod <c>source</c> + <c>resolvedAt</c> fields so a re-install months later reproduces byte-identical.</summary>
    public IReadOnlyDictionary<string, string>? RemoteModSources { get; init; }

    /// <summary>For remote-fetch installs: the resolved "gh:owner/repo#&lt;sha&gt;/&lt;collection-id&gt;" origin of the collection itself. Written to a provenance sidecar beside the manifest so a later read-only update check knows which repo's <c>index.yaml</c> advertises this collection's version. Null for local-file installs.</summary>
    public string? RemoteCollectionSource { get; init; }
}

public sealed class CollectionInstallService
{
    /// <summary>Provenance sidecar written beside a remotely-installed collection's
    /// manifest (mirrors <see cref="ModInstaller.SidecarFileName"/> for mods).
    /// <see cref="CollectionLister"/> reads it back into
    /// <see cref="InstalledCollection.Source"/>.</summary>
    public const string SidecarFileName = ".manager-collection-install.yaml";

    private readonly ProfileStore _profileStore = new();
    private readonly ProfileMutator _mutator = new();

    // AOT: CollectionInstallService writes a Patcher.CollectionLock instance to
    // <store>/collections/locks/<id>.lock.yaml via YamlDotNet. CollectionLock
    // lives in PagoniaLand.Patcher.Core, not in this assembly — Patcher.Core's
    // own ILLink.Descriptors pins the type today by accident. The annotation
    // here makes the dependency explicit at the use site so it survives even
    // if the cross-assembly descriptor coverage ever changes. The second pin
    // covers the collection provenance sidecar this class also serialises.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields,
        typeof(CollectionLock))]
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields,
        typeof(CollectionInstallSidecar))]
    public CollectionInstallService()
    {
    }

    public CollectionInstallResult Install(
        StoreLayout layout,
        string collectionPath,
        string modsRoot,
        string? profileNameOverride)
        => InstallWithOptions(layout, collectionPath, modsRoot, new CollectionInstallOptions { ProfileNameOverride = profileNameOverride });

    /// <summary>
    /// Same as <see cref="Install(StoreLayout, string, string, string?)"/> but
    /// takes the full option set (overwrite / activate / remote
    /// source map). Named differently to avoid an overload-ambiguity when
    /// callers pass a literal null for the profile override.
    /// </summary>
    public CollectionInstallResult InstallWithOptions(
        StoreLayout layout,
        string collectionPath,
        string modsRoot,
        CollectionInstallOptions options)
    {
        var profileNameOverride = options.ProfileNameOverride;
        var diagnostics = new List<ManagerDiagnostic>();

        if (!File.Exists(collectionPath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModSourceNotFound,
                $"Collection manifest '{collectionPath}' does not exist."));
            return new CollectionInstallResult { Diagnostics = diagnostics };
        }

        diagnostics.AddRange(
            new SchemaValidator().ValidateCollection(collectionPath).Select(ManagerDiagnostic.From));
        if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
        {
            return new CollectionInstallResult { Diagnostics = diagnostics };
        }

        var resolveResult = new CollectionResolver().Resolve(collectionPath, modsRoot);
        diagnostics.AddRange(resolveResult.Diagnostics.Select(ManagerDiagnostic.From));
        if (!resolveResult.Success || resolveResult.Value is null)
        {
            return new CollectionInstallResult { Diagnostics = diagnostics };
        }

        var resolution = resolveResult.Value;
        var collection = resolution.Collection;

        // Only warn about an unfetched http(s) mod source on the genuinely local-only path. When the
        // collection itself was remote-fetched (RemoteModSources / RemoteCollectionSource set), those
        // mods were just downloaded — the "not yet implemented" wording would be stale and wrong.
        var remoteFetched = options.RemoteModSources is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(options.RemoteCollectionSource);
        if (!remoteFetched)
        {
            foreach (var resolved in resolution.Mods)
            {
                var url = resolved.CollectionMod.Source;
                if (!string.IsNullOrWhiteSpace(url) &&
                    (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.CollectionRemoteSourceUnsupported,
                        $"Mod '{resolved.CollectionMod.Id}' declares remote source '{url}'; using local match at '{resolved.LocalPath}' instead. A local-file collection install can't fetch it — install the collection from its `gh:` repo to pull remote mods."));
                }
            }
        }

        var manifestPath = layout.CollectionManifestFile(collection.Id, collection.Version);
        var lockfilePath = layout.CollectionLockFile(collection.Id);

        // Profile preflight runs BEFORE any disk write so name collisions / invalid names
        // abort the install cleanly — manifest, lockfile, and mod copies stay untouched
        // until we're sure the profile slot is available. Derive the name up front because
        // we also need it for the AlreadyInstalled / recreate-profile branch below.
        var profileName = !string.IsNullOrWhiteSpace(profileNameOverride)
            ? profileNameOverride!
            : collection.Id;

        if (!ProfileNameValidator.IsValid(profileName, out var reason))
        {
            // Branch on whether the bad name came from an override or from the
            // collection id, so the error doesn't tell the user to "pass --profile"
            // when they already did exactly that.
            var message = !string.IsNullOrWhiteSpace(profileNameOverride)
                ? $"Profile name override '{profileName}' is invalid: {reason}"
                : $"Cannot derive a profile name from collection id '{profileName}': {reason} Pass --profile <name> to override.";

            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileNameInvalid,
                message));
            return new CollectionInstallResult
            {
                CollectionId = collection.Id,
                CollectionVersion = collection.Version,
                CollectionName = collection.Name,
                Diagnostics = diagnostics,
            };
        }

        if (File.Exists(manifestPath) && File.Exists(lockfilePath))
        {
            // Truly-already-installed only counts when the profile is also there
            // AND the caller didn't ask to overwrite. With --overwrite the user
            // explicitly wants to reseed (e.g. to pick up new collection tweak
            // values), so we fall through and rebuild the profile.
            // If the user did `profile delete <name>` (legal for non-default,
            // non-active profiles), the manifest+lockfile linger but the profile
            // is gone — the previous short-circuit refused to recreate it and
            // the user was stuck. Fall through into the normal install path:
            // ModInstaller emits AlreadyInstalled warnings per mod (no harm),
            // manifest/lockfile get overwritten with identical content (atomic),
            // and the profile is rebuilt from the resolved mod set.
            var profileExists = _profileStore.Exists(layout, profileName);
            if (profileExists && !options.Overwrite)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.CollectionAlreadyInstalled,
                    $"Collection '{collection.Id}' version '{collection.Version}' is already installed."));
                return new CollectionInstallResult
                {
                    Outcome = CollectionInstallOutcome.AlreadyInstalled,
                    CollectionId = collection.Id,
                    CollectionVersion = collection.Version,
                    CollectionName = collection.Name,
                    ProfileName = profileName,
                    ManifestPath = manifestPath,
                    LockfilePath = lockfilePath,
                    Diagnostics = diagnostics,
                };
            }

            // Fall-through cases: either the profile is gone (recreate from the
            // lockfile) or --overwrite asked us to reseed an existing one. Only
            // the recreate case gets the "profile missing" note; the overwrite
            // reseed surfaces its own tweakOverridesResetByReinstall below.
            if (!profileExists)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.CollectionAlreadyInstalled,
                    $"Collection '{collection.Id}' artifacts exist but profile '{profileName}' is missing; recreating the profile from the lockfile."));
            }
        }
        else if (_profileStore.Exists(layout, profileName) && !options.Overwrite)
        {
            // Fresh collection install but the chosen profile name is already taken
            // by something else — refuse rather than silently clobber. Users who
            // explicitly want the replacement pass --overwrite.
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileAlreadyExists,
                $"Profile '{profileName}' already exists; pass --overwrite to replace it or --as-profile <name> to choose a different name."));
            return new CollectionInstallResult
            {
                CollectionId = collection.Id,
                CollectionVersion = collection.Version,
                CollectionName = collection.Name,
                Diagnostics = diagnostics,
            };
        }

        var installer = new ModInstaller();
        var installedMods = new List<(string Id, string Version)>();

        foreach (var resolved in resolution.Mods)
        {
            var modInstallResult = installer.Install(resolved.LocalPath, layout);
            diagnostics.AddRange(modInstallResult.Diagnostics);

            if (modInstallResult.Outcome == InstallOutcome.Failed)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.CollectionInstallAborted,
                    $"Aborting collection install: mod '{resolved.LoadedMod.Manifest.Id}' could not be installed."));
                return new CollectionInstallResult
                {
                    CollectionId = collection.Id,
                    CollectionVersion = collection.Version,
                    CollectionName = collection.Name,
                    Diagnostics = diagnostics,
                };
            }

            installedMods.Add((resolved.LoadedMod.Manifest.Id, resolved.LoadedMod.Manifest.Version));
        }

        Directory.CreateDirectory(layout.CollectionVersionDirectory(collection.Id, collection.Version));
        AtomicFile.WriteAllText(manifestPath, File.ReadAllText(collectionPath));

        // Collection provenance sidecar: for a `--from gh:` install, record where
        // the collection itself came from (the per-mod origins already live in the
        // lockfile). A later read-only update check reads this to find the repo
        // whose index.yaml advertises this collection's version. Local-file
        // installs leave no sidecar — there's nothing to update-check against.
        if (!string.IsNullOrWhiteSpace(options.RemoteCollectionSource))
        {
            var sidecar = new CollectionInstallSidecar
            {
                InstalledAt = DateTimeOffset.UtcNow.ToString("o"),
                Source = options.RemoteCollectionSource!,
            };
            AtomicFile.WriteAllText(
                Path.Combine(layout.CollectionVersionDirectory(collection.Id, collection.Version), SidecarFileName),
                ManagerYaml.CreateSerializer().Serialize(sidecar));
        }

        // If the caller provided remote sources (i.e. this is a `collection
        // install --from gh:...` run), augment the lockfile with the
        // per-mod source + resolvedAt fields so a future re-install can
        // re-fetch byte-identical content from the exact same commit.
        var lockToWrite = options.RemoteModSources is { Count: > 0 }
            ? AugmentLockWithRemoteSources(resolution.Lock, options.RemoteModSources)
            : resolution.Lock;

        Directory.CreateDirectory(layout.CollectionLocksDirectory);
        AtomicFile.WriteAllText(
            lockfilePath,
            ManagerYaml.CreateSerializer().Serialize(lockToWrite));

        // Curator-supplied tweak overrides per mod (the collection manifest's
        // mods[].tweaks). Only the curator's explicit values are seeded — tweaks
        // the curator left alone fall back to the mod default at plan time, so
        // they stay origin=default in `tweak list`.
        var collectionTweaksByMod = resolution.Mods.ToDictionary(
            m => m.LoadedMod.Manifest.Id,
            m => m.CollectionMod.Tweaks,
            StringComparer.Ordinal);

        // When --overwrite replaces a profile that already carried tweak overrides,
        // those user values are about to be discarded by the reseed. Surface that
        // before the write so the user isn't silently reset.
        if (_profileStore.Exists(layout, profileName))
        {
            var existing = _profileStore.Read(layout, profileName);
            if (existing.EnabledMods.Any(m => m.Tweaks is { Count: > 0 }))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.TweakOverridesResetByReinstall,
                    $"Profile '{profileName}' had tweak overrides; reinstalling collection '{collection.Id}' reseeds them from the collection and discards the previous values."));
            }
        }

        var profile = new ProfileFile
        {
            ProfileVersion = StoreLayoutConstants.CurrentProfileVersion,
            Name = profileName,
            Collection = collection.Id,
        };

        foreach (var (id, version) in installedMods)
        {
            profile = _mutator.Enable(profile, id, version).Profile;
            if (collectionTweaksByMod.TryGetValue(id, out var curatorTweaks) && curatorTweaks is { Count: > 0 })
            {
                // Normalise curator values against the mod's declarations before seeding (trim,
                // lowercase booleans, alias->current id) so the patcher's resolver handles them — a
                // raw " True " / " 3 " would otherwise be stored verbatim and mishandled.
                var declarations = new ManifestReader().ReadMod(layout.ModVersionDirectory(id, version)).Value?.Manifest.Tweaks
                    ?? (IReadOnlyList<TweakDeclaration>)Array.Empty<TweakDeclaration>();
                profile = WithModTweaks(profile, id, TweakOverrideService.NormalizeCuratorTweaks(declarations, curatorTweaks));
            }
        }

        _profileStore.Write(layout, profile);

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.ProfileCreatedFromCollection,
            $"Profile '{profileName}' created from collection '{collection.Id}'@'{collection.Version}' ({installedMods.Count} mod(s) enabled)."));

        // Activate the new profile if the caller asked for it. Failure here
        // doesn't unwind the install (manifest + lockfile + profile + mods
        // are all on disk and consistent), but emits a clear diagnostic so
        // the user knows to run `profile use <name>` manually.
        var profileActivated = false;
        if (options.Activate)
        {
            var activated = new ProfileLifecycleService().Use(layout, profileName);
            diagnostics.AddRange(activated.Diagnostics);
            if (activated.Success)
            {
                profileActivated = true;
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.ProfileActivatedFromCollection,
                    $"Activated profile '{profileName}' — next plan / deploy targets it."));
            }
        }

        return new CollectionInstallResult
        {
            Outcome = CollectionInstallOutcome.Installed,
            ProfileActivated = profileActivated,
            CollectionId = collection.Id,
            CollectionVersion = collection.Version,
            CollectionName = collection.Name,
            ProfileName = profileName,
            ManifestPath = manifestPath,
            LockfilePath = lockfilePath,
            InstalledMods = installedMods,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Return a copy of <paramref name="profile"/> with <paramref name="tweaks"/>
    /// set on the named enabled mod (other entries untouched).</summary>
    private static ProfileFile WithModTweaks(ProfileFile profile, string modId, Dictionary<string, string>? tweaks)
    {
        var enabled = profile.EnabledMods
            .Select(m => string.Equals(m.Id, modId, StringComparison.Ordinal)
                // Curator-seeded values are not user overrides — record an explicit empty
                // userTweaks so the origin is unambiguous (and a later read doesn't re-infer it).
                ? new ProfileEnabledMod { Id = m.Id, Version = m.Version, Tweaks = tweaks, UserTweaks = new List<string>() }
                : m)
            .ToList();

        return new ProfileFile
        {
            ProfileVersion = profile.ProfileVersion,
            Name = profile.Name,
            Collection = profile.Collection,
            EnabledMods = enabled,
            LoadOrder = profile.LoadOrder,
        };
    }

    /// <summary>
    /// Copy a resolved <see cref="CollectionLock"/> with each mod's optional
    /// <c>source</c> + <c>resolvedAt</c> fields populated from the
    /// <paramref name="remoteModSources"/> map. Mods absent from the map
    /// (e.g. a hypothetical mix of local + remote in one collection) keep
    /// their fields empty, which is still valid.
    /// </summary>
    private static CollectionLock AugmentLockWithRemoteSources(
        CollectionLock baseLock,
        IReadOnlyDictionary<string, string> remoteModSources)
    {
        var resolvedAt = DateTimeOffset.UtcNow.ToString("O");
        var augmentedMods = baseLock.Mods
            .Select(m =>
            {
                if (!remoteModSources.TryGetValue(m.Id, out var sourceString))
                {
                    return m;
                }
                return new LockedMod
                {
                    Id = m.Id,
                    Version = m.Version,
                    ResolvedSource = m.ResolvedSource,
                    ArchiveSha256 = m.ArchiveSha256,
                    Enabled = m.Enabled,
                    Source = sourceString,
                    ResolvedAt = resolvedAt,
                    Tweaks = m.Tweaks, // preserve resolved tweaks across the remote-source augment
                };
            })
            .ToList();

        return new CollectionLock
        {
            CollectionLockVersion = baseLock.CollectionLockVersion,
            CollectionId = baseLock.CollectionId,
            CollectionVersion = baseLock.CollectionVersion,
            GameDatabaseVersion = baseLock.GameDatabaseVersion,
            GeneratedAt = baseLock.GeneratedAt,
            Mods = augmentedMods,
        };
    }
}
