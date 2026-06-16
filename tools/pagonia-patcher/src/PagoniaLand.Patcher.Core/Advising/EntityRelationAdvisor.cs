using System.Xml.Linq;

namespace PagoniaLand.Patcher;

/// <summary>
/// Conflict-minimising authoring advisor. Lints a mod's own overlay <c>*.gd.xml</c> for the engine's
/// entity-relation primitives and encodes EE's 2026-06-06 guidance: prefer the
/// additive, stackable modes (<c>Incremental</c> / <c>Template</c>) over the
/// destructive, last-loaded-wins modes (<c>Replace</c> / <c>Unload</c>), so a
/// mod co-exists cleanly with others in a player's set.
///
/// Every finding here is advisory: the destructive-mode notice and the risk
/// score are <see cref="PatchDiagnosticSeverity.Info"/>; only an unload that
/// would dangle a still-referenced GUID is a <see cref="PatchDiagnosticSeverity.Warning"/>.
/// None are errors, so <c>validate-mod</c> still exits 0 — the engine accepts
/// these mods; the advisor only flags conflict risk.
/// </summary>
public sealed class EntityRelationAdvisor
{
    private const string Replace = "Replace";
    private const string Unload = "Unload";

    /// <param name="reference">
    /// Optional reference GameDatabase (core/dlc). When supplied, the advisor
    /// also checks unload targets against the whole shipped set and judges
    /// whether a wholesale Replace could be an additive Incremental. Without it,
    /// only the base-free rules run.
    /// </param>
    public IReadOnlyList<PatchDiagnostic> Advise(OverlayGdbModel model, ReferenceGdbIndex? reference = null)
    {
        var diagnostics = new List<PatchDiagnostic>();

        var destructive = model.Entities
            .Where(entity => IsMode(entity, Replace) || IsMode(entity, Unload))
            .ToList();

        var replaceCount = 0;
        var unloadCount = 0;

        foreach (var entity in destructive)
        {
            var mode = IsMode(entity, Replace) ? Replace : Unload;
            if (mode == Replace)
            {
                replaceCount++;
            }
            else
            {
                unloadCount++;
            }

            // The additive-mode advice only makes sense for Replace; Unload removes the
            // entity, so it gets its own guidance instead of a nonsensical "prefer Incremental".
            var advice = mode == Replace
                ? "If the change only adds to the inherited entity, prefer Incremental/Template so it stacks instead of clobbering."
                : "Unload removes the inherited entity for everyone loaded after it — make sure nothing else still needs it (see the dangling-reference check), or leave it in place.";
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.UsesDestructiveInheritanceMode,
                $"{Describe(entity)} uses InheritanceMode=\"{mode}\" — destructive (across competing mods the last-loaded one wins). {advice}",
                entity.SourceFile));
        }

        // An Unload only resolves cleanly when nothing else still references the
        // target (EE, 2026-06-06). Base-free: does the target GUID appear in this
        // mod's own overlay beyond the unload pointer(s)? Base-aware: is it also
        // referenced anywhere in the shipped database?
        foreach (var group in destructive
            .Where(entity => IsMode(entity, Unload) && !string.IsNullOrWhiteSpace(entity.InheritedGuid))
            .GroupBy(entity => entity.InheritedGuid!, StringComparer.OrdinalIgnoreCase))
        {
            var target = group.Key;
            // Subtract every entity that POINTS at this target via InheritanceMode (Unload, but also a
            // sibling Replace/Template/Incremental whose InheritedGuid is the same target) — those
            // InheritedGuid values are themselves in ReferenceValues and are not genuine value-position
            // dependents. Only references beyond those count as "still needs the target".
            var inheritancePointers = model.Entities.Count(entity =>
                !string.IsNullOrWhiteSpace(entity.InheritedGuid)
                && string.Equals(entity.InheritedGuid!.Trim(), target, StringComparison.OrdinalIgnoreCase));
            // Match the base-aware path's exact-GUID semantics (ReferenceGdbIndex treats a
            // value as a reference only when the whole trimmed value is a GUID), rather than a
            // loose substring scan that could over-count an embedded GUID.
            var inOwnOverlay = model.ReferenceValues
                .Count(value => string.Equals(value.Trim(), target, StringComparison.OrdinalIgnoreCase)) > inheritancePointers;
            var inBaseGame = reference?.IsReferenced(target) == true;

            if (inOwnOverlay || inBaseGame)
            {
                var where = (inOwnOverlay, inBaseGame) switch
                {
                    (true, true) => "this mod's overlay and the base game database",
                    (true, false) => "this mod's overlay",
                    _ => "the base game database",
                };

                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Warning,
                    DiagnosticCodes.UnloadsReferencedEntity,
                    $"Unload targets entity {target}, but that GUID is still referenced in {where} — the reference will dangle after the unload. Unload its dependents too, or avoid Unload.",
                    group.First().SourceFile));
            }
        }

        // Base-aware: a wholesale Replace whose content is the inherited entity
        // verbatim plus additions could instead be an Incremental, which stacks
        // with other mods. Only fired with a reference DB to diff against.
        if (reference is not null)
        {
            foreach (var entity in destructive.Where(e => IsMode(e, Replace) && e.Element is not null && !string.IsNullOrWhiteSpace(e.InheritedGuid)))
            {
                var baseElement = reference.GetEntity(entity.InheritedGuid);
                if (baseElement is not null && IsAdditiveSuperset(baseElement, entity.Element!, isEntityRoot: true))
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Warning,
                        DiagnosticCodes.ReplaceCouldBeIncremental,
                        $"{Describe(entity)} uses InheritanceMode=\"Replace\" but only *adds* to the inherited entity {entity.InheritedGuid} (every existing part is still present, reordering aside). Prefer InheritanceMode=\"Incremental\" so the additions stack with other mods instead of one Replace winning.",
                        entity.SourceFile));
                }
            }
        }

        var destructiveCount = replaceCount + unloadCount;
        if (destructiveCount > 0)
        {
            var risk = destructiveCount <= 2 ? "low" : destructiveCount <= 5 ? "medium" : "high";
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.InheritanceConflictRisk,
                $"{replaceCount} Replace + {unloadCount} Unload on inherited entities → {risk} inter-mod collision risk. Additive modes (Incremental/Template) stack across mods; destructive modes are last-loaded-wins.",
                null));
        }

        return diagnostics;
    }

    private static bool IsMode(OverlayEntity entity, string mode)
        => string.Equals(entity.InheritanceMode, mode, StringComparison.OrdinalIgnoreCase);

    // True when `candidate` contains everything in `baseline` verbatim and only
    // adds to it — i.e. the change is purely additive. Conservative by design:
    // any modified or removed value makes a baseline child unmatchable, so the
    // result is false (no false "could be incremental" on a genuine rewrite).
    // The entity root skips the attributes that differ by construction
    // (a Replace has its own Guid/Name and the InheritanceMode/InheritedGuid).
    private static bool IsAdditiveSuperset(XElement baseline, XElement candidate, bool isEntityRoot)
    {
        foreach (var attribute in baseline.Attributes())
        {
            var name = attribute.Name.LocalName;
            if (isEntityRoot && name is "Guid" or "Name" or "InheritanceMode" or "InheritedGuid")
            {
                continue;
            }

            var candidateAttribute = candidate.Attribute(attribute.Name);
            if (candidateAttribute is null || !string.Equals(candidateAttribute.Value, attribute.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!baseline.HasElements)
        {
            return string.Equals(
                (baseline.Value ?? string.Empty).Trim(),
                (candidate.Value ?? string.Empty).Trim(),
                StringComparison.Ordinal);
        }

        // Every baseline child must be matched (additively) by a distinct
        // candidate child of the same name; leftover candidate children are the
        // permitted additions.
        var available = candidate.Elements().ToList();
        foreach (var baselineChild in baseline.Elements())
        {
            var matchIndex = -1;
            for (var index = 0; index < available.Count; index++)
            {
                if (available[index].Name == baselineChild.Name
                    && IsAdditiveSuperset(baselineChild, available[index], isEntityRoot: false))
                {
                    matchIndex = index;
                    break;
                }
            }

            if (matchIndex < 0)
            {
                return false;
            }

            available.RemoveAt(matchIndex);
        }

        return true;
    }

    private static string Describe(OverlayEntity entity)
    {
        var hasName = !string.IsNullOrWhiteSpace(entity.Name);
        var hasGuid = !string.IsNullOrWhiteSpace(entity.Guid);
        return (hasName, hasGuid) switch
        {
            (true, true) => $"Entity '{entity.Name}' ({entity.Guid})",
            (true, false) => $"Entity '{entity.Name}'",
            (false, true) => $"Entity {entity.Guid}",
            _ => "An entity",
        };
    }
}
