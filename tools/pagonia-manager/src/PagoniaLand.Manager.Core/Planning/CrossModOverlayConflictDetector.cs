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
        // The destructive-collision rule lives in the patcher's shared
        // CrossOverlayConflictDetector. Here we just supply (mod id, overlay)
        // pairs in load order and format the conflicts as manager diagnostics.
        var overlays = orderedMods
            .Select(mod => (Label: mod.Manifest.Id, Model: OverlayGdbReader.ReadFromMod(mod)))
            .ToList();

        var diagnostics = new List<ManagerDiagnostic>();
        foreach (var conflict in CrossOverlayConflictDetector.Detect(overlays))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CrossModOverlayConflict,
                $"Entity {conflict.Target} is Replaced/Unloaded by {conflict.Claimants.Count} enabled mods ({string.Join(", ", conflict.Claimants)}). "
                + $"Load order decides — '{conflict.Winner}' (last loaded) wins; "
                + $"{string.Join(", ", conflict.Overridden)} {(conflict.Overridden.Count == 1 ? "is" : "are")} silently overridden. "
                + "Prefer Incremental/Template where the edit is additive, or reorder/disable to choose the winner deliberately."));
        }

        return diagnostics;
    }
}
