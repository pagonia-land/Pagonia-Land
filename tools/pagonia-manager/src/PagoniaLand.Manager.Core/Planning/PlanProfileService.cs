using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public sealed class PlanProfileService
{
    private readonly StoreStateReader _stateReader = new();
    private readonly ProfileStore _profileStore = new();
    private readonly ManifestReader _manifestReader = new();
    private readonly PatchPlanner _planner = new();

    /// <param name="installGameVersion">The install's real GameDatabase version,
    /// read from the game executable's ProductVersion by the caller (null when
    /// unknown — extracted layout, fixtures, exe stripped). Decoupled from
    /// <paramref name="gameRoot"/> because for a live install the latter is the
    /// extract-cache root, which has no exe. When non-null, each enabled mod's
    /// declared <c>gameDatabaseVersion</c> is compared against it (advisory).</param>
    /// <param name="expansions">Resolved present/owned/effective state for the
    /// install's expansions, computed by the caller from the <em>real</em> game
    /// install (decoupled from <paramref name="gameRoot"/> for the same reason as
    /// <paramref name="installGameVersion"/> — a live-install deploy passes the
    /// extract-cache root here). When non-null the ownership gate runs:
    /// a required expansion that isn't present is an error; present-but-not-owned
    /// / unknown are warnings. Null ⇒ gate skipped (no behaviour change).</param>
    public PlanProfileResult Plan(StoreLayout layout, string gameRoot, string? profileName, string? installGameVersion = null, IReadOnlyList<ExpansionState>? expansions = null)
        => PlanAsync(layout, gameRoot, profileName, installGameVersion, CancellationToken.None, expansions).GetAwaiter().GetResult();

    /// <summary>
    /// Async overload of <see cref="Plan"/> for callers (e.g. a GUI) that must not
    /// block their thread — reads over a large game tree can be slow. The
    /// synchronous <c>Plan</c> is a thin wrapper over this. The token is honoured
    /// at the orchestration boundary; planning writes nothing, so a cancel simply
    /// abandons the analysis.
    /// </summary>
    public Task<PlanProfileResult> PlanAsync(StoreLayout layout, string gameRoot, string? profileName, string? installGameVersion = null, CancellationToken cancellationToken = default, IReadOnlyList<ExpansionState>? expansions = null)
        => Task.Run(() => PlanCore(layout, gameRoot, profileName, installGameVersion, expansions, cancellationToken), cancellationToken);

    private PlanProfileResult PlanCore(StoreLayout layout, string gameRoot, string? profileName, string? installGameVersion, IReadOnlyList<ExpansionState>? expansions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireGameRoot(gameRoot, diagnostics))
        {
            return new PlanProfileResult { GameRoot = gameRoot, ManagerDiagnostics = diagnostics };
        }

        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new PlanProfileResult { GameRoot = gameRoot, ManagerDiagnostics = diagnostics };
        }

        var state = _stateReader.Read(layout);
        var resolvedProfileName = string.IsNullOrWhiteSpace(profileName)
            ? state.ActiveProfile ?? StoreLayoutConstants.DefaultProfileName
            : profileName!;

        if (!_profileStore.Exists(layout, resolvedProfileName))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ProfileMissing,
                $"Profile '{resolvedProfileName}' has no file at '{layout.ProfileFile(resolvedProfileName)}'."));
            return new PlanProfileResult { GameRoot = gameRoot, ProfileName = resolvedProfileName, ManagerDiagnostics = diagnostics };
        }

        var profile = _profileStore.Read(layout, resolvedProfileName);

        if (profile.LoadOrder.Count == 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.ProfileEmpty,
                $"Profile '{resolvedProfileName}' has no enabled mods. The patcher plan will be empty."));
        }

        var loadedMods = new List<LoadedMod>();
        var orderedEnabled = OrderByLoadOrder(profile);

        foreach (var enabledMod in orderedEnabled)
        {
            var modDirectory = layout.ModVersionDirectory(enabledMod.Id, enabledMod.Version);
            if (!Directory.Exists(modDirectory))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModInstallMissing,
                    $"Mod '{enabledMod.Id}' version '{enabledMod.Version}' is enabled in profile '{resolvedProfileName}' but not installed at '{modDirectory}'."));
                continue;
            }

            var readResult = _manifestReader.ReadMod(modDirectory);
            diagnostics.AddRange(readResult.Diagnostics
                .Where(d => d.Severity == PatchDiagnosticSeverity.Error)
                .Select(ManagerDiagnostic.From));

            if (readResult.Value is null)
            {
                continue;
            }

            loadedMods.Add(readResult.Value);
        }

        // Honour loadAfter/loadBefore: topologically order the enabled set, with the manual profile
        // order as the stable tiebreaker. Reorders the applied set; emits an info when it adjusts the
        // order away from manual, a warning on a constraint cycle. Cross-mod conflict resolution
        // (last-loaded wins) then sees the corrected order.
        var loadOrder = new LoadOrderResolver().Resolve(loadedMods
            .Select(m => new LoadOrderInput(m.Manifest.Id, m.Manifest.LoadAfter, m.Manifest.LoadBefore))
            .ToList());
        diagnostics.AddRange(loadOrder.Diagnostics);
        if (loadedMods.Count > 0)
        {
            var byId = loadedMods.ToDictionary(m => m.Manifest.Id, StringComparer.Ordinal);
            loadedMods = loadOrder.Order.Select(id => byId[id]).ToList();
        }

        // Cross-mod gameDatabaseVersion check: every enabled mod must declare the same version,
        // otherwise the apply step would produce inconsistent results. We don't read the actual
        // game's version here (no portable way to determine it from the gameRoot alone) — the
        // check is internal to the profile.
        var distinctVersions = loadedMods
            .Select(mod => mod.Manifest.GameDatabaseVersion)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctVersions.Count > 1)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.ProfileGameVersionMismatch,
                $"Profile '{resolvedProfileName}' has mods targeting different GameDatabase versions: {string.Join(", ", distinctVersions)}."));
        }

        // Game-vs-mod axis: when we know the install's real version (exe ProductVersion,
        // threaded in by the caller), compare each enabled mod's declared version
        // against it. Orthogonal to the intra-profile check above — that one catches
        // mods disagreeing with each other; this one catches a mod disagreeing with
        // the actual game. Advisory only: a same-line build drift is info, a real
        // version gap is a warning gated by the normal --accept-warnings path, never
        // a hard block (the patcher fails loudly at apply time if a value truly moved).
        // Degrades silently to the intra-profile-only behaviour when the version is
        // unknown.
        AddGameVersionDiagnostics(installGameVersion, loadedMods, diagnostics);

        // Expansion-ownership gate: when the caller resolved the install's
        // present/owned/effective state, turn a mod that targets a missing or
        // not-owned expansion into an actionable diagnostic instead of a silent
        // no-op. Presence blocks (error → planning fails below); ownership only
        // warns. Skipped entirely when expansions is null (extracted-layout
        // deploys / callers that don't resolve ownership).
        if (expansions is not null)
        {
            diagnostics.AddRange(ExpansionGate.Evaluate(loadedMods, expansions));
        }

        // Cross-mod overlay conflict check: two enabled mods that destructively
        // (Replace/Unload) target the same inherited entity collide — load order
        // picks the winner and silently overrides the rest. Advisory (warnings),
        // so it never blocks the plan. The per-mod authoring advisor can't see
        // this; only the ordered enabled set can.
        diagnostics.AddRange(new CrossModOverlayConflictDetector().Detect(loadedMods));

        // Dependency / incompatibility check across the enabled set — advisory, never blocks.
        // A required mod not enabled, or two enabled mods that declare each other incompatible.
        var installedIds = new HashSet<string>(
            new ModLister().List(layout).Select(m => m.Id), StringComparer.Ordinal);
        diagnostics.AddRange(new ModDependencyDetector().Detect(loadedMods, installedIds));

        if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
        {
            return new PlanProfileResult
            {
                ProfileName = resolvedProfileName,
                GameRoot = gameRoot,
                ManagerDiagnostics = diagnostics,
            };
        }

        // Thread each enabled mod's per-profile tweak overrides into the plan as the
        // external tweak layer. The patcher resolves them against each mod's declared
        // tweaks (substituting `{{ tweaks.<id> }}` placeholders) and surfaces a
        // tweakValueResolved info per placeholder in the wrapped plan report. Mods with
        // no stored overrides resolve every tweak to its declared default.
        var tweakSelection = BuildTweakSelection(orderedEnabled);
        cancellationToken.ThrowIfCancellationRequested();
        var patcherPlan = _planner.Plan(gameRoot, loadedMods, tweakSelection);
        var managerSuccess = !diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error);

        return new PlanProfileResult
        {
            Success = managerSuccess && patcherPlan.Success,
            ProfileName = resolvedProfileName,
            GameRoot = gameRoot,
            PatcherPlan = patcherPlan,
            ManagerDiagnostics = diagnostics,
        };
    }

    /// <summary>Compare each enabled mod's declared <c>gameDatabaseVersion</c> to the
    /// install's actual version. No-op when the install version is unknown/unparseable.</summary>
    private static void AddGameVersionDiagnostics(
        string? installGameVersion,
        IReadOnlyList<LoadedMod> loadedMods,
        List<ManagerDiagnostic> diagnostics)
    {
        if (!GameDatabaseVersion.TryParse(installGameVersion, out var gameVersion) || gameVersion is null)
        {
            return; // unknown / unparseable install baseline — degrade to intra-profile only
        }

        foreach (var mod in loadedMods)
        {
            if (!GameDatabaseVersion.TryParse(mod.Manifest.GameDatabaseVersion, out var modVersion) || modVersion is null)
            {
                continue; // mod declares no/invalid version — the manifest validator owns that
            }

            switch (modVersion.RelateTo(gameVersion))
            {
                case GameVersionRelation.Exact:
                    break; // silent

                case GameVersionRelation.SameLineDrift:
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Info,
                        ManagerDiagnosticCodes.ModGameVersionDrift,
                        $"Mod '{mod.Manifest.Id}' targets GameDatabase {modVersion} but the game is {gameVersion} — " +
                        $"same {gameVersion.Line} line, different build; almost always still applies."));
                    break;

                case GameVersionRelation.LineGap:
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.ModGameVersionMismatch,
                        $"Mod '{mod.Manifest.Id}' targets GameDatabase {modVersion} but the game is {gameVersion} — " +
                        "a different version line; it may not apply cleanly. Pass --accept-warnings to deploy anyway."));
                    break;
            }
        }
    }

    /// <summary>Build the patcher's tweak selection from the profile's per-mod override maps.
    /// A mod with no overrides (Tweaks null/empty) contributes nothing — it resolves to defaults.</summary>
    private static TweakSelection BuildTweakSelection(IReadOnlyList<ProfileEnabledMod> enabled)
    {
        var selection = TweakSelection.Create();
        foreach (var mod in enabled)
        {
            if (mod.Tweaks is { Count: > 0 })
            {
                selection.WithExternalValues(mod.Id, mod.Tweaks);
            }
        }
        return selection;
    }

    private static IReadOnlyList<ProfileEnabledMod> OrderByLoadOrder(ProfileFile profile)
    {
        if (profile.LoadOrder.Count == 0)
        {
            return profile.EnabledMods;
        }

        var byId = profile.EnabledMods.ToDictionary(mod => mod.Id, StringComparer.Ordinal);
        var ordered = new List<ProfileEnabledMod>();

        foreach (var modId in profile.LoadOrder)
        {
            if (byId.Remove(modId, out var mod))
            {
                ordered.Add(mod);
            }
        }

        ordered.AddRange(byId.Values);
        return ordered;
    }
}
