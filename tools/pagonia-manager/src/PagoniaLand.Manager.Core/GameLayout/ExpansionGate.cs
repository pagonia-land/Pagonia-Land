using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Ownership-aware plan/deploy gate. Given the enabled mods and the resolved
/// expansion states for an install (presence + ownership + any transient
/// override already applied by <see cref="ExpansionResolver"/>), emits the
/// diagnostics that turn a would-be silent no-op into an actionable message.
///
/// <para>The load-bearing rule (Phase 9): <strong>presence blocks, ownership
/// only warns</strong>. A required expansion that isn't on disk is a hard error
/// (there is no pak to patch). Present-but-not-owned / unknown are warnings that
/// never block deployment — because Envision ships every pak, a non-owner must
/// still be able to write a present pak's bytes to match an owning co-op host.
/// Effective (<c>Present ∧ Owned</c>) describes solo runtime effect for the
/// message, not deploy-ability.</para>
/// </summary>
public static class ExpansionGate
{
    /// <summary>
    /// Warning codes that are advisory only and must NOT count toward the
    /// deploy's warning-block gate — ownership never gates deployment, only
    /// presence does. <see cref="DeployService"/> consults this so these warn
    /// (and print) but proceed without <c>--accept-warnings</c>.
    /// </summary>
    public static bool IsNonBlockingAdvisory(string code) =>
        code == ManagerDiagnosticCodes.ModExpansionNotOwned
        || code == ManagerDiagnosticCodes.ExpansionOwnershipUnknown;

    /// <summary>
    /// Evaluate every enabled mod against the resolved expansion states.
    /// <paramref name="resolvedExpansions"/> covers all four canonical packages
    /// (from <see cref="ExpansionResolver.Resolve"/>); each mod is checked only
    /// against the expansions it declares it needs.
    /// </summary>
    public static IReadOnlyList<ManagerDiagnostic> Evaluate(
        IReadOnlyList<LoadedMod> mods,
        IReadOnlyList<ExpansionState> resolvedExpansions)
    {
        var byPackage = resolvedExpansions.ToDictionary(e => e.Package, StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<ManagerDiagnostic>();

        foreach (var mod in mods)
        {
            var coopSafe = mod.Manifest.MultiplayerSafe == SafetyState.Yes;

            // Required = the manifest's requiredPackages plus every NON-optional
            // patchSet's requiresPackages. Optional = optionalPackages plus every
            // optional patchSet's requiresPackages, minus anything already required
            // (the stronger constraint wins).
            var required = new HashSet<string>(mod.Manifest.RequiredPackages, StringComparer.OrdinalIgnoreCase);
            foreach (var set in mod.Manifest.PatchSets.Where(s => !s.Optional))
            {
                required.UnionWith(set.RequiresPackages);
            }

            var optional = new HashSet<string>(mod.Manifest.OptionalPackages, StringComparer.OrdinalIgnoreCase);
            foreach (var set in mod.Manifest.PatchSets.Where(s => s.Optional))
            {
                optional.UnionWith(set.RequiresPackages);
            }
            optional.ExceptWith(required);

            foreach (var package in OrderedKnown(required))
            {
                EvaluateRequired(mod.Manifest.Id, byPackage[package], coopSafe, diagnostics);
            }

            foreach (var package in OrderedKnown(optional))
            {
                EvaluateOptional(mod.Manifest.Id, byPackage[package], diagnostics);
            }
        }

        return diagnostics;
    }

    // Only the four canonical packages have a resolved state. An unknown package
    // name in a manifest is the manifest validator's concern, not the gate's.
    private static IEnumerable<string> OrderedKnown(HashSet<string> packages) =>
        ExpansionPackages.All.Where(packages.Contains);

    private static void EvaluateRequired(
        string modId, ExpansionState state, bool coopSafe, List<ManagerDiagnostic> diagnostics)
    {
        if (!state.Present)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModExpansionNotPresent,
                $"Mod '{modId}' needs expansion '{state.Package}', but its pak is not present on this install — " +
                "there is nothing to patch. Envision ships every pak to every player, so a missing one is unusual; " +
                "check the game install."));
            return;
        }

        // Present + owned → effective → nothing to say. core/tools resolve to
        // Owned, so they only ever reach the not-present branch above.
        switch (state.Ownership)
        {
            case OwnershipState.NotOwned:
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ModExpansionNotOwned,
                    $"Mod '{modId}' targets '{state.Package}', which is present but not owned on this install. " +
                    "Solo, this content stays inactive (the engine gates it at runtime); the bytes still deploy." +
                    CoopNote(state.Package, coopSafe)));
                break;

            case OwnershipState.Unknown:
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ExpansionOwnershipUnknown,
                    $"Mod '{modId}' targets '{state.Package}', but you haven't declared whether you own it. " +
                    $"Solo, it only takes effect if you own it — declare with " +
                    $"'pagonia-manager expansions set {state.Package} <owned|not-owned>'." +
                    CoopNote(state.Package, coopSafe)));
                break;
        }
    }

    private static void EvaluateOptional(
        string modId, ExpansionState state, List<ManagerDiagnostic> diagnostics)
    {
        if (!state.Present)
        {
            // The "today behaviour" — optional content for an absent package is
            // skipped — now reported with its real reason (absent, not "not owned").
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.ModOptionalExpansionSkipped,
                $"Mod '{modId}' has optional content for '{state.Package}', which is not present on this install — " +
                "that content is skipped."));
            return;
        }

        if (!state.Effective)
        {
            // Present but not owned / unknown: the bytes still deploy (so a co-op
            // participant matches the host), but stay inert for this player solo.
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.ModOptionalExpansionInactive,
                $"Mod '{modId}' has optional content for '{state.Package}', which is present but not effective for you " +
                $"(ownership: {Describe(state.Ownership)}). It still deploys for co-op parity but stays inactive in solo play."));
        }
    }

    private static string CoopNote(string package, bool coopSafe) =>
        coopSafe
            ? $" In co-op, only the host needs to own '{package}', but every player needs the same deployed mod set — " +
              "share it via 'pagonia-manager profile export' → collection."
            : string.Empty;

    private static string Describe(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "owned",
        OwnershipState.NotOwned => "not owned",
        _ => "unknown",
    };
}
