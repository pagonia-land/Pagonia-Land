using System.Diagnostics.CodeAnalysis;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public sealed class ProfileExportResult
{
    public bool Success { get; init; }
    public string? ProfileName { get; init; }
    public string? CollectionId { get; init; }
    public string? OutputPath { get; init; }
    public int ModCount { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>Optional collection metadata overrides for <see cref="ProfileExportService.Export"/>.</summary>
public sealed class ProfileExportOptions
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Version { get; init; }
}

/// <summary>
/// Exports an active or named profile into a shareable <c>*.collection.yaml</c>. The profile's
/// enabled mods + load order become the collection's <c>mods</c> + <c>loadOrder</c>, each mod's
/// per-profile tweak overrides fold into <c>mods[].tweaks</c>, and each mod's <c>source</c> is
/// recovered from its install provenance (sidecar, then the originating collection lockfile),
/// falling back to <c>"local"</c> with a warning. Note this is a re-curation, not a byte-faithful
/// round trip: the exported collection becomes the curator layer, so re-installing it seeds those
/// tweak values as <c>collection-default</c> origin (not <c>external</c> user overrides) — by design.
/// The local counterpart is
/// <see cref="ProfileLifecycleService.Copy"/>; for a bit-identical reproduction the user shares
/// the lockfile instead.
/// </summary>
public sealed class ProfileExportService
{
    private readonly StoreStateReader _stateReader = new();
    private readonly ProfileStore _profileStore = new();
    private readonly ManifestReader _manifestReader = new();

    // AOT: the manager serialises a Patcher.CollectionManifest (built here) and reads a
    // Patcher.CollectionLock for source recovery via YamlDotNet. Both live in
    // PagoniaLand.Patcher.Core; root their members explicitly at this use site.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(CollectionManifest))]
    [DynamicDependency(Shape, typeof(CollectionMod))]
    [DynamicDependency(Shape, typeof(CollectionLock))]
    [DynamicDependency(Shape, typeof(LockedMod))]
    public ProfileExportService()
    {
    }

    public ProfileExportResult Export(
        StoreLayout layout,
        string? profileName,
        string outputPath,
        ProfileExportOptions options)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ProfileExportResult { ProfileName = profileName, Diagnostics = diagnostics };
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
            return new ProfileExportResult { ProfileName = name, Diagnostics = diagnostics };
        }

        if (!_profileStore.Exists(layout, name))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Profile '{name}' has no file at '{layout.ProfileFile(name)}'."));
            return new ProfileExportResult { ProfileName = name, Diagnostics = diagnostics };
        }

        var profile = _profileStore.Read(layout, name);

        // A collection requires at least one mod (schema mods minItems 1), so an empty profile
        // can't produce a valid collection — refuse rather than write an invalid file.
        if (profile.EnabledMods.Count == 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileExportEmpty,
                $"Profile '{name}' has no enabled mods; a collection must reference at least one mod. Enable a mod before exporting."));
            return new ProfileExportResult { ProfileName = name, Diagnostics = diagnostics };
        }

        var ordered = OrderByLoadOrder(profile);

        // Locate each enabled mod's install directory; a missing one means the profile drifted
        // from the store (out-of-band uninstall) — surface it before building anything.
        var modDirectories = new List<string>();
        var directoryById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var enabled in ordered)
        {
            var directory = layout.ModVersionDirectory(enabled.Id, enabled.Version);
            if (!Directory.Exists(directory))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModInstallMissing,
                    $"Profile '{name}' references '{enabled.Id}@{enabled.Version}' but it is not installed at '{directory}'."));
                return new ProfileExportResult { ProfileName = name, Diagnostics = diagnostics };
            }

            modDirectories.Add(directory);
            directoryById[enabled.Id] = directory;
        }

        var collectionId = string.IsNullOrWhiteSpace(options.Id) ? DeriveCollectionId(name) : options.Id!;
        var displayName = string.IsNullOrWhiteSpace(options.Name) ? name : options.Name!;
        var version = string.IsNullOrWhiteSpace(options.Version) ? "0.1.0" : options.Version!;

        // Reuse the patcher's exporter to read each mod manifest, infer + agree on the
        // gameDatabaseVersion, and detect duplicate ids. We then rebuild the mod list with the
        // recovered source and folded tweaks (the exporter's per-mod source is the install path,
        // which we replace).
        var exportOptions = new CollectionExportOptions(
            Id: collectionId,
            Name: displayName,
            Version: version,
            Author: string.Empty,
            GameDatabaseVersion: null,
            Description: $"Exported from profile '{name}'.",
            ConflictPolicy: string.Empty);

        var export = new CollectionExporter().Export(modDirectories, exportOptions);
        foreach (var diagnostic in export.Diagnostics)
        {
            diagnostics.Add(ManagerDiagnostic.From(diagnostic));
        }

        if (export.Value is null)
        {
            return new ProfileExportResult { ProfileName = name, CollectionId = collectionId, Diagnostics = diagnostics };
        }

        var baseManifest = export.Value;
        var lockfile = TryReadCollectionLock(layout, profile.Collection);

        // Canonicalise stored tweak keys forward through any author rename (the declaration's
        // aliases:) before folding them into the collection, so an exported manifest never carries a
        // stale alias id. Mirrors the lazy migration TweakOverrideService runs on read, but read-only
        // here — the profile itself is not rewritten, only the values we hand to the export.
        var tweaksById = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var mod in ordered)
        {
            if (mod.Tweaks is not { Count: > 0 })
            {
                continue;
            }

            IReadOnlyList<TweakDeclaration> declarations = directoryById.TryGetValue(mod.Id, out var dir)
                ? _manifestReader.ReadMod(dir).Value?.Manifest.Tweaks ?? []
                : [];
            var (migrated, migrationDiagnostics, _) = TweakAliasMigrator.Migrate(mod.Id, mod.Tweaks, declarations);
            diagnostics.AddRange(migrationDiagnostics);
            if (migrated is { Count: > 0 })
            {
                tweaksById[mod.Id] = migrated;
            }
        }

        var mods = new List<CollectionMod>();
        foreach (var baseMod in baseManifest.Mods)
        {
            var source = RecoverSource(layout, baseMod.Id, lockfile);
            if (source is null)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ProfileExportLocalSource,
                    $"Mod '{baseMod.Id}' has no recoverable remote source; exported as source: \"local\". It won't fetch on another machine without a matching --mods-root."));
                source = "local";
            }

            mods.Add(new CollectionMod
            {
                Id = baseMod.Id,
                Version = baseMod.Version,
                Source = source,
                Required = true,
                Enabled = true,
                RequiresPackages = baseMod.RequiresPackages,
                Tweaks = tweaksById.TryGetValue(baseMod.Id, out var tweaks)
                    ? new Dictionary<string, string>(tweaks, StringComparer.Ordinal)
                    : null,
            });
        }

        var manifest = new CollectionManifest
        {
            CollectionFormatVersion = baseManifest.CollectionFormatVersion,
            Id = baseManifest.Id,
            Name = baseManifest.Name,
            Version = baseManifest.Version,
            Author = baseManifest.Author,
            GameDatabaseVersion = baseManifest.GameDatabaseVersion,
            Description = baseManifest.Description,
            ConflictPolicy = baseManifest.ConflictPolicy,
            Mods = mods,
            LoadOrder = ordered.Select(mod => mod.Id).ToList(),
            // Leave the optional list-valued metadata unset so the OmitNull serializer drops them
            // entirely. Emitting `previewImages: []` would violate the schema's minItems:1 on that
            // field; emitting `tags: []` is merely noise. A profile export carries neither.
            Tags = null!,
            PreviewImages = null!,
        };

        // Schema-validate the serialised manifest before committing it to the output path: write to
        // a temp file, validate, and only on success write it to <out>. A malformed export aborts
        // rather than emitting an invalid file.
        var yaml = ManagerYaml.CreateSerializer().Serialize(manifest);
        var tempPath = outputPath + ".export.tmp"; // .tmp suffix matches the store cleanup filter
        try
        {
            AtomicFile.WriteAllText(tempPath, yaml);
            var schemaDiagnostics = new SchemaValidator().ValidateCollection(tempPath)
                .Select(ManagerDiagnostic.From)
                .ToList();
            diagnostics.AddRange(schemaDiagnostics.Where(d => d.Severity == ManagerDiagnosticSeverity.Error));

            if (schemaDiagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
            {
                return new ProfileExportResult { ProfileName = name, CollectionId = collectionId, Diagnostics = diagnostics };
            }

            AtomicFile.WriteAllText(outputPath, yaml);
        }
        finally
        {
            TryDelete(tempPath);
        }

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.ProfileExported,
            $"Exported profile '{name}' to collection '{collectionId}' ({mods.Count} mod(s)) at '{outputPath}'."));

        return new ProfileExportResult
        {
            Success = true,
            ProfileName = name,
            CollectionId = collectionId,
            OutputPath = outputPath,
            ModCount = mods.Count,
            Diagnostics = diagnostics,
        };
    }

    private static List<ProfileEnabledMod> OrderByLoadOrder(ProfileFile profile)
    {
        var byId = profile.EnabledMods.ToDictionary(mod => mod.Id, StringComparer.Ordinal);
        var ordered = new List<ProfileEnabledMod>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in profile.LoadOrder)
        {
            if (byId.TryGetValue(id, out var mod) && seen.Add(id))
            {
                ordered.Add(mod);
            }
        }

        // Defensive: any enabled mod not named in loadOrder still gets exported, appended in
        // its stored order so nothing is silently dropped.
        foreach (var mod in profile.EnabledMods)
        {
            if (seen.Add(mod.Id))
            {
                ordered.Add(mod);
            }
        }

        return ordered;
    }

    /// <summary>
    /// Recover a collection <c>source:</c> for a mod from its install provenance. Order:
    /// the install sidecar's <c>source</c> (set for every remote install), then the originating
    /// collection lockfile's per-mod source when the profile is collection-pinned. Returns
    /// <c>null</c> when nothing remote is recoverable so the caller writes <c>"local"</c>.
    /// </summary>
    private static string? RecoverSource(StoreLayout layout, string modId, CollectionLock? lockfile)
    {
        var sidecarSource = TryReadSidecarSource(layout, modId);
        if (!string.IsNullOrWhiteSpace(sidecarSource))
        {
            return sidecarSource;
        }

        var locked = lockfile?.Mods.FirstOrDefault(mod =>
            string.Equals(mod.Id, modId, StringComparison.OrdinalIgnoreCase));
        if (locked is not null)
        {
            if (!string.IsNullOrWhiteSpace(locked.Source))
            {
                return locked.Source;
            }

            if (LooksRemote(locked.ResolvedSource))
            {
                return locked.ResolvedSource;
            }
        }

        return null;
    }

    private static string? TryReadSidecarSource(StoreLayout layout, string modId)
    {
        // Pick any installed version's sidecar — the enabled version's dir is the one we walked,
        // but the source provenance is the same across versions of one mod for our purposes.
        var modRoot = Path.GetDirectoryName(layout.ModVersionDirectory(modId, "x"));
        if (string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
        {
            return null;
        }

        foreach (var versionDir in Directory.EnumerateDirectories(modRoot))
        {
            var sidecarPath = Path.Combine(versionDir, ModInstaller.SidecarFileName);
            if (!File.Exists(sidecarPath))
            {
                continue;
            }

            try
            {
                var sidecar = ManagerYaml.CreateDeserializer()
                    .Deserialize<InstallSidecar>(File.ReadAllText(sidecarPath));
                if (!string.IsNullOrWhiteSpace(sidecar?.Source))
                {
                    return sidecar!.Source;
                }
            }
            catch
            {
                // A malformed sidecar is not fatal to an export — fall through to "local".
            }
        }

        return null;
    }

    private static CollectionLock? TryReadCollectionLock(StoreLayout layout, string? collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            return null;
        }

        var lockPath = layout.CollectionLockFile(collectionId);
        if (!File.Exists(lockPath))
        {
            return null;
        }

        var result = new ManifestReader().ReadCollectionLock(lockPath);
        return result.Value;
    }

    private static bool LooksRemote(string source)
        => !string.IsNullOrWhiteSpace(source)
            && (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("gh:", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("modio:", StringComparison.OrdinalIgnoreCase)
                || source.StartsWith("url:", StringComparison.OrdinalIgnoreCase));

    private static string DeriveCollectionId(string profileName)
    {
        // collection.schema.json id: ^[a-z0-9][a-z0-9._-]*[a-z0-9]$, length 3-140.
        var lowered = new string(profileName
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-')
            .ToArray());
        var trimmed = lowered.Trim('.', '_', '-');
        var slug = string.IsNullOrEmpty(trimmed) ? "profile" : trimmed;
        var id = $"pagonia-land.profiles.{slug}";
        return id.Length < 3 ? "pagonia-land.profiles.profile" : id;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of the validation temp file.
        }
    }
}
