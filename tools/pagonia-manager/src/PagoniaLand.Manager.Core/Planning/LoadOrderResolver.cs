namespace PagoniaLand.Manager;

/// <summary>One mod's load-order constraints, as declared in its manifest.</summary>
public sealed record LoadOrderInput(string Id, IReadOnlyList<string> LoadAfter, IReadOnlyList<string> LoadBefore);

/// <summary>Outcome of resolving a profile's load order against its mods' constraints.</summary>
public sealed class LoadOrderResult
{
    /// <summary>The constraint-respecting order (a cyclic remainder, if any, is appended in manual order).</summary>
    public IReadOnlyList<string> Order { get; init; } = Array.Empty<string>();

    /// <summary>Ids that participate in at least one honoured constraint — "dependency-pinned"
    /// positions, vs the freely-orderable rest. Surfaced by the interactive reorder screen.</summary>
    public IReadOnlySet<string> Constrained { get; init; } = new HashSet<string>();

    /// <summary>Info when the order was adjusted away from the manual order; warning on a cycle.</summary>
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>
/// Orders a profile's enabled mods to honour their <c>loadAfter</c> / <c>loadBefore</c> constraints,
/// with the user's manual profile order as the <b>stable tiebreaker</b> — a constraint reorders only
/// where it must, and never silently: an adjustment emits <c>manager.loadOrderAdjusted</c>. A stable
/// topological sort (Kahn's, always taking the available node with the smallest manual index) keeps
/// unconstrained mods exactly where the user put them. A constraint cycle can't be ordered: it's
/// reported (<c>manager.loadOrderCycle</c>) and the cyclic mods fall back to manual order.
///
/// <para>Constraints that name a mod not in the enabled set are inert (no edge) — you can declare
/// <c>loadAfter</c> a mod you don't run without it forcing anything.</para>
/// </summary>
public sealed class LoadOrderResolver
{
    /// <param name="manualOrder">The enabled mods in the user's manual profile order.</param>
    public LoadOrderResult Resolve(IReadOnlyList<LoadOrderInput> manualOrder)
    {
        // Dedupe ids defensively: a hand-edited or externally-corrupted profile can repeat an id in
        // loadOrder, and an un-deduped list would throw ArgumentException from the ToDictionary calls
        // below, crashing plan/deploy/reorder instead of ordering. First occurrence wins (manual order).
        var ids = manualOrder.Select(m => m.Id).Distinct(StringComparer.Ordinal).ToList();
        var present = new HashSet<string>(ids, StringComparer.Ordinal);
        var manualIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < ids.Count; i++)
        {
            manualIndex[ids[i]] = i;
        }

        // Build "before" edges (from -> to means from loads before to), deduped. Ignore constraints
        // naming a mod that isn't enabled.
        var edges = new HashSet<(string From, string To)>();
        foreach (var mod in manualOrder)
        {
            foreach (var after in mod.LoadAfter)
            {
                if (present.Contains(after) && !string.Equals(after, mod.Id, StringComparison.Ordinal))
                {
                    edges.Add((after, mod.Id)); // `mod` loads after `after`
                }
            }
            foreach (var before in mod.LoadBefore)
            {
                if (present.Contains(before) && !string.Equals(before, mod.Id, StringComparison.Ordinal))
                {
                    edges.Add((mod.Id, before)); // `mod` loads before `before`
                }
            }
        }

        var constrained = new HashSet<string>(StringComparer.Ordinal);
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var inDegree = ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var (from, to) in edges)
        {
            (adjacency.TryGetValue(from, out var list) ? list : adjacency[from] = new List<string>()).Add(to);
            inDegree[to]++;
            constrained.Add(from);
            constrained.Add(to);
        }

        // Kahn's, stable: among nodes with no remaining predecessor, always take the one that came
        // first in the manual order.
        var resolved = new List<string>(ids.Count);
        var available = ids.Where(id => inDegree[id] == 0).ToList();
        while (available.Count > 0)
        {
            available.Sort((a, b) => manualIndex[a].CompareTo(manualIndex[b]));
            var next = available[0];
            available.RemoveAt(0);
            resolved.Add(next);

            if (adjacency.TryGetValue(next, out var successors))
            {
                foreach (var successor in successors)
                {
                    if (--inDegree[successor] == 0)
                    {
                        available.Add(successor);
                    }
                }
            }
        }

        var diagnostics = new List<ManagerDiagnostic>();

        // Anything left has a remaining predecessor → it's in (or behind) a cycle. Report and fall
        // back to manual order for the remainder so the plan still proceeds.
        if (resolved.Count < ids.Count)
        {
            var cyclic = ids.Where(id => !resolved.Contains(id)).ToList();
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.LoadOrderCycle,
                $"loadAfter/loadBefore constraints form a cycle among: {string.Join(", ", cyclic)}. "
                + "Can't order them automatically — left in your manual order. Remove a conflicting loadAfter/loadBefore."));
            resolved.AddRange(cyclic); // already in manual order (ids is manual order)
        }

        if (!resolved.SequenceEqual(ids, StringComparer.Ordinal))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.LoadOrderAdjusted,
                $"Load order adjusted to honour loadAfter/loadBefore: {string.Join(" -> ", resolved)}."));
        }

        return new LoadOrderResult { Order = resolved, Constrained = constrained, Diagnostics = diagnostics };
    }
}
