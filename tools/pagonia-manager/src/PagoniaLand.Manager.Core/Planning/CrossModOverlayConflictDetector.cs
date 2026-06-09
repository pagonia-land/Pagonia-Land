using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Detects cross-mod GameDatabase-overlay conflicts across the enabled mods of a
/// profile: when two or more enabled mods destructively (<c>Replace</c> /
/// <c>Unload</c>) target the same inherited entity, the engine resolves them by
/// load order — the last-loaded mod wins and the earlier ones are silently
/// overridden (EE, 2026-06-06). This is the cross-mod companion to the patcher's
/// per-mod authoring advisor: that one lints a single mod in isolation; this one
/// catches the collision only an installed *set* in load order can have.
///
/// Advisory only (warnings) — it never blocks a plan or deploy. Additive modes
/// (<c>Incremental</c> / <c>Template</c>) stack and are intentionally not flagged.
/// </summary>
public sealed class CrossModOverlayConflictDetector
{
    /// <param name="orderedMods">Enabled mods in load order (first loaded → last loaded).</param>
    public IReadOnlyList<ManagerDiagnostic> Detect(IReadOnlyList<LoadedMod> orderedMods)
    {
        // InheritedGuid → the distinct mod ids (kept in load order) that
        // destructively claim it.
        var claims = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in orderedMods)
        {
            var modId = mod.Manifest.Id;
            var overlay = OverlayGdbReader.ReadFromMod(mod);

            var destructiveTargets = overlay.Entities
                .Where(entity => !string.IsNullOrWhiteSpace(entity.InheritedGuid)
                    && (string.Equals(entity.InheritanceMode, "Replace", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.InheritanceMode, "Unload", StringComparison.OrdinalIgnoreCase)))
                .Select(entity => entity.InheritedGuid!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var target in destructiveTargets)
            {
                if (!claims.TryGetValue(target, out var mods))
                {
                    mods = new List<string>();
                    claims[target] = mods;
                }

                if (!mods.Contains(modId, StringComparer.OrdinalIgnoreCase))
                {
                    mods.Add(modId);
                }
            }
        }

        var diagnostics = new List<ManagerDiagnostic>();

        // Ordered by target GUID for a stable, deterministic report.
        foreach (var (target, mods) in claims.OrderBy(claim => claim.Key, StringComparer.Ordinal))
        {
            if (mods.Count < 2)
            {
                continue;
            }

            var winner = mods[^1];
            var overridden = mods.Take(mods.Count - 1).ToList();

            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CrossModOverlayConflict,
                $"Entity {target} is Replaced/Unloaded by {mods.Count} enabled mods ({string.Join(", ", mods)}). "
                + $"Load order decides — '{winner}' (last loaded) wins; "
                + $"{string.Join(", ", overridden)} {(overridden.Count == 1 ? "is" : "are")} silently overridden. "
                + "Prefer Incremental/Template where the edit is additive, or reorder/disable to choose the winner deliberately."));
        }

        return diagnostics;
    }
}
