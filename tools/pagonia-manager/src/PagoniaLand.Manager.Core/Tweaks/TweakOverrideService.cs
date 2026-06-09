using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>Where a resolved tweak value came from, as the manager sees it.</summary>
public static class TweakValueOrigins
{
    /// <summary>The mod author's declared default — no override stored.</summary>
    public const string Default = "default";

    /// <summary>A value stored in this profile's <c>enabledMods[].tweaks</c> map.</summary>
    public const string ProfileOverride = "profile-override";

    /// <summary>A value seeded into the profile by a collection install (reserved; populated later).</summary>
    public const string CollectionDefault = "collection-default";
}

/// <summary>One tweak as the manager presents it: the mod's declaration, the current
/// effective value, and where that value came from. <see cref="Usages"/> lists the patch
/// operations the tweak feeds (empty unless the caller asked the reader to scan them), so a UI
/// can explain what setting the value actually does.</summary>
public sealed record TweakValueView(
    TweakDeclaration Declaration,
    string Value,
    string Origin)
{
    public IReadOnlyList<TweakUsage> Usages { get; init; } = [];
}

public sealed record TweakReadResult
{
    public bool Success { get; init; }
    public string? ProfileName { get; init; }
    public string ModId { get; init; } = string.Empty;
    public string? ModVersion { get; init; }
    public IReadOnlyList<TweakValueView> Tweaks { get; init; } = [];
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed record TweakMutationResult
{
    public bool Success { get; init; }
    public bool Mutated { get; init; }
    public string? ProfileName { get; init; }
    public string ModId { get; init; } = string.Empty;
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>
/// Reads, sets, and resets per-mod-per-profile tweak overrides. The override values live in the
/// profile YAML (<c>enabledMods[].tweaks</c>, profileVersion 0.1); the declarations they're
/// validated against come from the installed mod's <c>mod.yaml</c>. <see cref="PlanProfileService"/>
/// threads the stored overrides into the patcher's plan as the external tweak layer.
/// </summary>
public sealed class TweakOverrideService
{
    private readonly StoreStateReader _stateReader = new();
    private readonly ProfileStore _profileStore = new();
    private readonly ManifestReader _manifestReader = new();

    /// <summary>Declarations + current effective values + origins for every tweak a mod declares.</summary>
    public TweakReadResult Read(StoreLayout layout, string? profileName, string modId)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (!TryLoadContext(layout, profileName, modId, diagnostics, out var ctx))
        {
            return new TweakReadResult { ProfileName = ctx?.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        // The collection (if the profile is pinned to one) supplied some of the
        // stored values at install time. Re-read its curator tweaks so a value
        // still matching the collection's reads as collection-default, while a
        // value the user changed reads as profile-override.
        var collectionTweaks = LoadCollectionCuratorTweaks(layout, ctx.Profile, modId);

        var usagesByTweak = ctx.Usages
            .GroupBy(u => u.TweakId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TweakUsage>)g.ToList(), StringComparer.Ordinal);

        var views = ctx.Declarations
            .Select(decl => new TweakValueView(decl, ResolveValue(ctx, decl), ResolveOrigin(ctx, decl, collectionTweaks))
            {
                Usages = usagesByTweak.TryGetValue(decl.Id, out var u) ? u : [],
            })
            .ToList();

        return new TweakReadResult
        {
            Success = true,
            ProfileName = ctx.ProfileName,
            ModId = modId,
            ModVersion = ctx.EnabledMod.Version,
            Tweaks = views,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Validate a value against the mod's declaration and store it as an override.</summary>
    public TweakMutationResult Set(StoreLayout layout, string? profileName, string modId, string tweakId, string value)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (!TryLoadContext(layout, profileName, modId, diagnostics, out var ctx))
        {
            return new TweakMutationResult { ProfileName = ctx?.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        var declaration = FindDeclaration(ctx, tweakId);
        if (declaration is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.TweakUnknownId,
                $"Mod '{modId}' declares no tweak '{tweakId}'."));
            return new TweakMutationResult { ProfileName = ctx.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        if (!TryValidateValue(modId, declaration, value, diagnostics))
        {
            return new TweakMutationResult { ProfileName = ctx.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        var updated = new Dictionary<string, string>(
            ctx.EnabledMod.Tweaks ?? new Dictionary<string, string>(),
            StringComparer.Ordinal)
        {
            [declaration.Id] = NormalizeValue(declaration, value),
        };

        WriteTweaks(layout, ctx, updated);

        // No success info diagnostic: Success + Mutated already convey the outcome,
        // and each caller (CLI / wizard) prints its own confirmation line.
        return new TweakMutationResult
        {
            Success = true,
            Mutated = true,
            ProfileName = ctx.ProfileName,
            ModId = modId,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Drop a single override (<paramref name="tweakId"/> set) or every override for the mod
    /// (<paramref name="tweakId"/> null).</summary>
    public TweakMutationResult Reset(StoreLayout layout, string? profileName, string modId, string? tweakId)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (!TryLoadContext(layout, profileName, modId, diagnostics, out var ctx))
        {
            return new TweakMutationResult { ProfileName = ctx?.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        // Whole-mod reset: drop the entire overrides map.
        if (string.IsNullOrEmpty(tweakId))
        {
            var hadAny = ctx.EnabledMod.Tweaks is { Count: > 0 };
            if (hadAny)
            {
                WriteTweaks(layout, ctx, null);
            }
            return new TweakMutationResult
            {
                Success = true,
                Mutated = hadAny,
                ProfileName = ctx.ProfileName,
                ModId = modId,
                Diagnostics = diagnostics,
            };
        }

        var declaration = FindDeclaration(ctx, tweakId);
        if (declaration is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.TweakUnknownId,
                $"Mod '{modId}' declares no tweak '{tweakId}'."));
            return new TweakMutationResult { ProfileName = ctx.ProfileName, ModId = modId, Diagnostics = diagnostics };
        }

        // Single-tweak reset: a stored override is removed; nothing stored is a no-op.
        if (ctx.EnabledMod.Tweaks is null || !ctx.EnabledMod.Tweaks.ContainsKey(declaration.Id))
        {
            return new TweakMutationResult
            {
                Success = true,
                Mutated = false,
                ProfileName = ctx.ProfileName,
                ModId = modId,
                Diagnostics = diagnostics,
            };
        }

        var remaining = new Dictionary<string, string>(ctx.EnabledMod.Tweaks, StringComparer.Ordinal);
        remaining.Remove(declaration.Id);
        WriteTweaks(layout, ctx, remaining.Count == 0 ? null : remaining);

        return new TweakMutationResult
        {
            Success = true,
            Mutated = true,
            ProfileName = ctx.ProfileName,
            ModId = modId,
            Diagnostics = diagnostics,
        };
    }

    private sealed record Context(
        string ProfileName,
        ProfileFile Profile,
        ProfileEnabledMod EnabledMod,
        IReadOnlyList<TweakDeclaration> Declarations,
        IReadOnlyList<TweakUsage> Usages);

    private bool TryLoadContext(
        StoreLayout layout,
        string? profileName,
        string modId,
        List<ManagerDiagnostic> diagnostics,
        [NotNullWhen(true)] out Context? context)
    {
        context = null;

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return false;
        }

        var resolvedName = string.IsNullOrWhiteSpace(profileName)
            ? _stateReader.Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName
            : profileName!;

        if (!_profileStore.Exists(layout, resolvedName))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Profile '{resolvedName}' has no file at '{layout.ProfileFile(resolvedName)}'."));
            return false;
        }

        var profile = _profileStore.Read(layout, resolvedName);
        var enabledMod = profile.EnabledMods.FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.Ordinal));
        if (enabledMod is null)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.TweakUnknownMod,
                $"Mod '{modId}' is not enabled in profile '{resolvedName}'."));
            return false;
        }

        var modDirectory = layout.ModVersionDirectory(enabledMod.Id, enabledMod.Version);
        if (!Directory.Exists(modDirectory))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModInstallMissing,
                $"Mod '{modId}' version '{enabledMod.Version}' is enabled in profile '{resolvedName}' but not installed at '{modDirectory}'."));
            context = new Context(resolvedName, profile, enabledMod, [], []);
            return false;
        }

        var readResult = _manifestReader.ReadMod(modDirectory);
        diagnostics.AddRange(readResult.Diagnostics
            .Where(d => d.Severity == PatchDiagnosticSeverity.Error)
            .Select(ManagerDiagnostic.From));
        if (readResult.Value is null)
        {
            context = new Context(resolvedName, profile, enabledMod, [], []);
            return false;
        }

        var declarations = readResult.Value.Manifest.Tweaks;

        // The whole mod (manifest + patch files) is already loaded here, so scanning which ops each
        // tweak feeds is free — it lets the reader hand the wizard an op-aware hint with no extra I/O.
        var usages = TweakUsageScanner.Scan(readResult.Value);

        // Lazily migrate stored overrides keyed by a renamed tweak's old id to the
        // current id (the author lists the old id under `aliases:`). When anything
        // moves, the profile YAML is rewritten on the spot so the stored shape
        // catches up — the migration diagnostic then fires only once.
        var (migrated, migrationDiagnostics, changed) =
            MigrateAliases(modId, enabledMod.Tweaks, declarations);
        diagnostics.AddRange(migrationDiagnostics);

        if (changed)
        {
            enabledMod = new ProfileEnabledMod { Id = enabledMod.Id, Version = enabledMod.Version, Tweaks = migrated };
            profile = ReplaceEnabledMod(profile, enabledMod);
            _profileStore.Write(layout, profile);
        }

        context = new Context(resolvedName, profile, enabledMod, declarations, usages);
        return true;
    }

    // Resolve stored override keys against the current declarations + their aliases.
    // Returns the (possibly) rewritten map, the diagnostics to surface, and whether
    // anything actually moved (only then is a profile rewrite warranted).
    private static (Dictionary<string, string>? Migrated, List<ManagerDiagnostic> Diagnostics, bool Changed) MigrateAliases(
        string modId,
        IReadOnlyDictionary<string, string>? stored,
        IReadOnlyList<TweakDeclaration> declarations)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (stored is null || stored.Count == 0)
        {
            return (stored is null ? null : new Dictionary<string, string>(StringComparer.Ordinal), diagnostics, false);
        }

        var declaredIds = new HashSet<string>(declarations.Select(d => d.Id), StringComparer.Ordinal);
        var aliasToCurrent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var decl in declarations)
        {
            foreach (var alias in decl.Aliases)
            {
                aliasToCurrent[alias] = decl.Id;
            }
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var changed = false;

        foreach (var (key, value) in stored)
        {
            if (declaredIds.Contains(key))
            {
                result[key] = value; // a current id — keep as-is
            }
            else if (aliasToCurrent.TryGetValue(key, out var currentId))
            {
                if (stored.ContainsKey(currentId))
                {
                    // Both the old (alias) and the new id are stored — the new id wins;
                    // drop the stale alias entry. (Hand-edited / raced profile.)
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.TweakAliasConflict,
                        $"Mod '{modId}' has both '{key}' (a legacy alias) and '{currentId}' stored; keeping '{currentId}' and dropping '{key}'."));
                    changed = true;
                }
                else
                {
                    // Migrate: move the alias's value forward to the current id.
                    result[currentId] = value;
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Info,
                        ManagerDiagnosticCodes.TweakMigratedFromAlias,
                        $"Migrated tweak override '{key}' -> '{currentId}' for mod '{modId}' (renamed by the author)."));
                    changed = true;
                }
            }
            else
            {
                // Neither a current id nor a known alias — an orphan (the tweak was
                // removed, or the alias dropped). Keep it so nothing is silently lost;
                // surface it so the user can clean it up.
                result[key] = value;
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.TweakOrphanedOverride,
                    $"Mod '{modId}' has a stored override for '{key}', which it no longer declares (nor as an alias); leaving it untouched."));
            }
        }

        return (result, diagnostics, changed);
    }

    private static ProfileFile ReplaceEnabledMod(ProfileFile profile, ProfileEnabledMod replacement)
    {
        var enabled = profile.EnabledMods
            .Select(m => string.Equals(m.Id, replacement.Id, StringComparison.Ordinal) ? replacement : m)
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

    private static TweakDeclaration? FindDeclaration(Context ctx, string tweakId)
        => ctx.Declarations.FirstOrDefault(d => string.Equals(d.Id, tweakId, StringComparison.Ordinal));

    private static string ResolveValue(Context ctx, TweakDeclaration decl)
        => ctx.EnabledMod.Tweaks is { } tweaks && tweaks.TryGetValue(decl.Id, out var stored)
            ? stored
            : decl.Default;

    private static string ResolveOrigin(Context ctx, TweakDeclaration decl, IReadOnlyDictionary<string, string>? collectionTweaks)
    {
        if (ctx.EnabledMod.Tweaks is not { } tweaks || !tweaks.TryGetValue(decl.Id, out var stored))
        {
            return TweakValueOrigins.Default;
        }

        // A stored value still equal to the collection's curator-supplied value is
        // attributed to the collection; anything else is the user's own override.
        if (collectionTweaks is not null
            && collectionTweaks.TryGetValue(decl.Id, out var curatorValue)
            && string.Equals(stored, curatorValue, StringComparison.Ordinal))
        {
            return TweakValueOrigins.CollectionDefault;
        }

        return TweakValueOrigins.ProfileOverride;
    }

    /// <summary>The curator-supplied tweak overrides for <paramref name="modId"/> from the
    /// collection the profile is pinned to, or null when there's no collection / it can't be read.
    /// Resolves the pinned version via the lockfile, then reads that version's manifest.</summary>
    private IReadOnlyDictionary<string, string>? LoadCollectionCuratorTweaks(StoreLayout layout, ProfileFile profile, string modId)
    {
        if (string.IsNullOrWhiteSpace(profile.Collection))
        {
            return null;
        }

        var lockPath = layout.CollectionLockFile(profile.Collection);
        if (!File.Exists(lockPath))
        {
            return null;
        }

        var version = _manifestReader.ReadCollectionLock(lockPath).Value?.CollectionVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var manifestPath = layout.CollectionManifestFile(profile.Collection, version);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var manifest = _manifestReader.ReadCollectionManifest(manifestPath).Value;
        return manifest?.Mods
            .FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.Ordinal))?
            .Tweaks;
    }

    private void WriteTweaks(StoreLayout layout, Context ctx, Dictionary<string, string>? tweaks)
    {
        var rebuiltEnabled = ctx.Profile.EnabledMods
            .Select(m => string.Equals(m.Id, ctx.EnabledMod.Id, StringComparison.Ordinal)
                ? new ProfileEnabledMod { Id = m.Id, Version = m.Version, Tweaks = tweaks }
                : m)
            .ToList();

        var updatedProfile = new ProfileFile
        {
            ProfileVersion = ctx.Profile.ProfileVersion,
            Name = ctx.Profile.Name,
            Collection = ctx.Profile.Collection,
            EnabledMods = rebuiltEnabled,
            LoadOrder = ctx.Profile.LoadOrder,
        };

        _profileStore.Write(layout, updatedProfile);
    }

    // Validate a user-supplied value against the tweak's declared type (and numeric range).
    // Type/enum-membership failures → tweakValueInvalid; a numeric value outside min..max →
    // tweakValueOutOfRange. Both are errors: at `tweak set` time the user can fix the value.
    // Canonicalise a validated override before it is stored, so the patcher sees the exact
    // string it expects: a boolean becomes lowercase "true"/"false" (the ternary does an exact
    // match, so a stored " true " would otherwise resolve to the false branch), and every value
    // is trimmed (e.g. a literal " 3 " must not land verbatim in an integer field).
    private static string NormalizeValue(TweakDeclaration decl, string value)
        => decl.Type == "boolean" && bool.TryParse(value, out var b)
            ? b.ToString().ToLowerInvariant()
            : value.Trim();

    private static bool TryValidateValue(string modId, TweakDeclaration decl, string value, List<ManagerDiagnostic> diagnostics)
    {
        switch (decl.Type)
        {
            case "boolean":
                if (!bool.TryParse(value, out _))
                {
                    return Invalid(diagnostics, $"Tweak '{modId}:{decl.Id}' is a boolean; '{value}' is not 'true' or 'false'.");
                }
                return true;

            case "integer":
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    return Invalid(diagnostics, $"Tweak '{modId}:{decl.Id}' is an integer; '{value}' is not a whole number.");
                }
                return CheckRange(modId, decl, value, integer, diagnostics);

            case "number":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return Invalid(diagnostics, $"Tweak '{modId}:{decl.Id}' is a number; '{value}' is not numeric.");
                }
                return CheckRange(modId, decl, value, number, diagnostics);

            case "enum":
                if (!decl.Values.Any(v => string.Equals(v.Value, value, StringComparison.Ordinal)))
                {
                    var allowed = string.Join(", ", decl.Values.Select(v => v.Value));
                    return Invalid(diagnostics, $"Tweak '{modId}:{decl.Id}' value '{value}' is not one of: {allowed}.");
                }
                return true;

            default:
                // Unknown type — the manifest validator owns rejecting it; accept here.
                return true;
        }
    }

    private static bool CheckRange(string modId, TweakDeclaration decl, string value, double numeric, List<ManagerDiagnostic> diagnostics)
    {
        if ((decl.Min is { } min && numeric < min) || (decl.Max is { } max && numeric > max))
        {
            var range = $"{(decl.Min?.ToString(CultureInfo.InvariantCulture) ?? "-∞")}..{(decl.Max?.ToString(CultureInfo.InvariantCulture) ?? "+∞")}";
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.TweakValueOutOfRange,
                $"Tweak '{modId}:{decl.Id}' value '{value}' is outside the declared range {range}."));
            return false;
        }
        return true;
    }

    private static bool Invalid(List<ManagerDiagnostic> diagnostics, string message)
    {
        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Error,
            ManagerDiagnosticCodes.TweakValueInvalid,
            message));
        return false;
    }
}
