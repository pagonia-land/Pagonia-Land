using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Resolves stored tweak-override keys against a mod's current tweak declarations, mapping a value
/// stored under a renamed tweak's old id (listed in the declaration's <c>aliases:</c>) forward to the
/// current id. Shared by <see cref="TweakOverrideService"/> (which rewrites the profile on the spot)
/// and <see cref="ProfileExportService"/> (which canonicalises the values it folds into the exported
/// collection), so both surfaces apply the identical rename logic and never emit a stale alias key.
/// </summary>
public static class TweakAliasMigrator
{
    /// <summary>
    /// Map every stored override key to its current id. A current id is kept as-is; a known alias is
    /// migrated forward; an unknown key is kept untouched (orphan) so nothing is silently lost.
    /// Returns the (possibly) rewritten map, the diagnostics to surface, and whether anything moved
    /// (only then is a profile rewrite warranted).
    /// </summary>
    public static (Dictionary<string, string>? Migrated, List<ManagerDiagnostic> Diagnostics, bool Changed) Migrate(
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
        // Tracks which legacy alias each currentId was migrated from, so a second alias mapping to the
        // same currentId is resolved deterministically (not by dictionary enumeration order).
        var migratedFrom = new Dictionary<string, string>(StringComparer.Ordinal);
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
                else if (migratedFrom.TryGetValue(currentId, out var existingAlias))
                {
                    // A SECOND legacy alias for the same renamed tweak is stored (the tweak was renamed
                    // more than once and a hand-edited / raced profile kept both old ids). Keep one
                    // deterministically — the ordinally-smaller alias key — and warn, rather than let
                    // dictionary enumeration order silently decide which value survives.
                    var keepKey = string.CompareOrdinal(key, existingAlias) < 0 ? key : existingAlias;
                    var dropKey = string.Equals(keepKey, key, StringComparison.Ordinal) ? existingAlias : key;
                    if (string.Equals(keepKey, key, StringComparison.Ordinal))
                    {
                        result[currentId] = value;
                        migratedFrom[currentId] = key;
                    }
                    diagnostics.Add(new ManagerDiagnostic(
                        ManagerDiagnosticSeverity.Warning,
                        ManagerDiagnosticCodes.TweakAliasConflict,
                        $"Mod '{modId}' has multiple legacy aliases ('{keepKey}', '{dropKey}') mapping to '{currentId}'; keeping '{keepKey}' and dropping '{dropKey}'."));
                    changed = true;
                }
                else
                {
                    // Migrate: move the alias's value forward to the current id.
                    result[currentId] = value;
                    migratedFrom[currentId] = key;
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
}
