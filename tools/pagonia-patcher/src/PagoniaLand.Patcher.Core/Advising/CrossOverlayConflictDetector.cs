namespace PagoniaLand.Patcher;

/// <summary>
/// A destructive cross-overlay collision: an inherited entity that two or more
/// loaded overlays <c>Replace</c>/<c>Unload</c>. The engine resolves it by load
/// order — the last-loaded overlay wins and the earlier ones are silently
/// overridden (EE, 2026-06-06). Additive modes (<c>Incremental</c>/<c>Template</c>)
/// stack and never collide, so they are not reported.
/// </summary>
/// <param name="Target">The inherited-entity GUID two or more overlays destructively claim.</param>
/// <param name="Claimants">The distinct overlay labels that claim it, in load order (first loaded → last).</param>
/// <param name="Winner">The last-loaded claimant — the one whose destructive edit the engine keeps.</param>
/// <param name="Overridden">The earlier claimants, silently overridden by the winner.</param>
public sealed record CrossOverlayConflict(
    string Target,
    IReadOnlyList<string> Claimants,
    string Winner,
    IReadOnlyList<string> Overridden);

/// <summary>
/// Detects <see cref="CrossOverlayConflict"/>s across a set of GameDatabase
/// overlays given in load order. The manager's cross-mod conflict warning (the
/// enabled mods of a profile) consumes this: it feeds <c>(label, OverlayGdbModel)</c>
/// pairs and formats the returned conflicts into manager diagnostics. Advisory
/// only — it never blocks anything.
/// </summary>
public static class CrossOverlayConflictDetector
{
    /// <param name="overlaysInLoadOrder">The overlays to compare, each with a display label, in load order (first loaded → last loaded).</param>
    public static IReadOnlyList<CrossOverlayConflict> Detect(
        IReadOnlyList<(string Label, OverlayGdbModel Model)> overlaysInLoadOrder)
    {
        ArgumentNullException.ThrowIfNull(overlaysInLoadOrder);

        // InheritedGuid -> the distinct labels (kept in load order) that
        // destructively claim it.
        var claims = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, model) in overlaysInLoadOrder)
        {
            var destructiveTargets = model.Entities
                .Where(entity => !string.IsNullOrWhiteSpace(entity.InheritedGuid)
                    && (string.Equals(entity.InheritanceMode, "Replace", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(entity.InheritanceMode, "Unload", StringComparison.OrdinalIgnoreCase)))
                .Select(entity => entity.InheritedGuid!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var target in destructiveTargets)
            {
                if (!claims.TryGetValue(target, out var labels))
                {
                    labels = new List<string>();
                    claims[target] = labels;
                }

                if (!labels.Contains(label, StringComparer.OrdinalIgnoreCase))
                {
                    labels.Add(label);
                }
            }
        }

        var conflicts = new List<CrossOverlayConflict>();

        // Ordered by target GUID for a stable, deterministic report.
        foreach (var (target, labels) in claims.OrderBy(claim => claim.Key, StringComparer.Ordinal))
        {
            if (labels.Count < 2)
            {
                continue;
            }

            var winner = labels[^1];
            var overridden = labels.Take(labels.Count - 1).ToList();
            conflicts.Add(new CrossOverlayConflict(target, labels, winner, overridden));
        }

        return conflicts;
    }
}
