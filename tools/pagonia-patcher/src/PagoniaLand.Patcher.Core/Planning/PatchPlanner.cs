using System.Globalization;
using System.IO;

namespace PagoniaLand.Patcher;

public sealed class PatchPlanner
{
    private readonly XmlTargetResolver _resolver = new();
    private readonly TweakResolver _tweakResolver = new();

    public CombinedPatchPlan Plan(string gameRoot, IReadOnlyList<LoadedMod> mods, TweakSelection? tweakSelection = null)
    {
        var modPlans = mods.Select(mod => Plan(gameRoot, mod, tweakSelection)).ToList();
        var diagnostics = new List<PatchDiagnostic>();
        var conflicts = DetectConflicts(modPlans);
        var entryConflicts = DetectEntryConflicts(modPlans);

        foreach (var conflict in conflicts)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.DuplicateWriteTarget,
                $"Multiple mods write target '{conflict.TargetKey}'."));
        }

        foreach (var conflict in entryConflicts)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.EntryConflict,
                $"Multiple mods touch pak entry '{conflict.Path}' (mods: {string.Join(", ", conflict.Writes.Select(w => w.ModId).Distinct())})."));
        }

        if (diagnostics.Count == 0 && modPlans.All(plan => plan.Success))
        {
            var entryCount = modPlans.Sum(plan => plan.EntryWrites.Count);
            var summary = entryCount > 0
                ? $"Combined patch plan contains {modPlans.Sum(plan => plan.Writes.Count)} write(s) and {entryCount} entry operation(s) from {modPlans.Count} mod(s)."
                : $"Combined patch plan contains {modPlans.Sum(plan => plan.Writes.Count)} write(s) from {modPlans.Count} mod(s).";
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.CombinedPatchPlanReady,
                summary));
        }

        return new CombinedPatchPlan(modPlans, conflicts, diagnostics, entryConflicts);
    }

    public PatchPlan Plan(string gameRoot, LoadedMod mod, TweakSelection? tweakSelection = null)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var writes = new List<PatchWrite>();

        var (tweakValues, tweakOrigins, resolvedTweaks) = BuildTweakValues(mod, tweakSelection, diagnostics);

        foreach (var loadedPatchFile in mod.PatchFiles)
        {
            // Patch-set gating: a file that came from a `patchSets:` entry only
            // applies when every package it requires is present under the game
            // root. An optional set is skipped silently; a required one is an error.
            if (loadedPatchFile.RequiresPackages is { Count: > 0 } requiredPackages)
            {
                var missing = requiredPackages
                    .Where(package => !Directory.Exists(Path.Combine(gameRoot, package)))
                    .ToList();
                if (missing.Count > 0)
                {
                    diagnostics.Add(new PatchDiagnostic(
                        loadedPatchFile.Optional ? PatchDiagnosticSeverity.Info : PatchDiagnosticSeverity.Error,
                        loadedPatchFile.Optional ? DiagnosticCodes.PatchSetSkipped : DiagnosticCodes.PatchSetMissingPackage,
                        loadedPatchFile.Optional
                            ? $"Optional patch set skipped — package(s) not present: {string.Join(", ", missing)}."
                            : $"Patch set requires package(s) not present: {string.Join(", ", missing)}.",
                        loadedPatchFile.Path));
                    continue;
                }
            }

            foreach (var rawOperation in loadedPatchFile.PatchFile.Operations)
            {
                PatchOperation operation;
                try
                {
                    operation = ResolveTweakPlaceholders(rawOperation, tweakValues, tweakOrigins, diagnostics);
                }
                catch (TweakResolutionError error)
                {
                    var code = error.Kind == TweakResolutionErrorKind.UndeclaredTweak
                        ? DiagnosticCodes.TweakUndeclared
                        : DiagnosticCodes.TweakSyntaxError;
                    diagnostics.Add(Error(code, $"{error.Message} (operation '{rawOperation.Id}')", loadedPatchFile.Path));
                    continue;
                }

                TargetResolveResult? result = operation.Operation switch
                {
                    PatchOperationTypes.ReplaceValue => _resolver.ResolveReplaceValue(gameRoot, operation),
                    PatchOperationTypes.ReplaceAttribute => _resolver.ResolveReplaceAttribute(gameRoot, operation),
                    PatchOperationTypes.ReplaceNode => _resolver.ResolveReplaceNode(gameRoot, operation),
                    PatchOperationTypes.AddListItem => _resolver.ResolveAddListItem(gameRoot, operation),
                    PatchOperationTypes.RemoveListItem => _resolver.ResolveRemoveListItem(gameRoot, operation),
                    PatchOperationTypes.AddEntity => _resolver.ResolveAddEntity(gameRoot, operation),
                    PatchOperationTypes.RemoveEntity => _resolver.ResolveRemoveEntity(gameRoot, operation),
                    PatchOperationTypes.MergeComponent => _resolver.ResolveMergeComponent(gameRoot, operation),
                    _ => null,
                };

                if (result is null)
                {
                    diagnostics.Add(Error(
                        DiagnosticCodes.UnsupportedOperation,
                        $"Operation '{operation.Operation}' is not supported by the planner yet.",
                        loadedPatchFile.Path));
                    continue;
                }

                diagnostics.AddRange(result.Diagnostics);

                if (result.Write is not null)
                {
                    writes.Add(result.Write);
                }
            }
        }

        var entryWrites = PlanEntryOperations(mod, diagnostics);

        if (diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.PatchPlanReady,
                entryWrites.Count > 0
                    ? $"Patch plan contains {writes.Count} write(s) and {entryWrites.Count} entry operation(s)."
                    : $"Patch plan contains {writes.Count} write(s)."));
        }

        return new PatchPlan(mod, writes, diagnostics, entryWrites, resolvedTweaks);
    }

    // Build the effective value of every declared tweak by precedence (lockfile > CLI > collection >
    // default — see TweakSelection). A renamed tweak's stored value is followed forward by also
    // looking up the selection under each declared alias. A non-default numeric value outside the
    // declared range warns but is still used; a lockfile pin that differs from the current default
    // surfaces as info so the user sees why an old value persisted.
    private static (Dictionary<string, string> Values, Dictionary<string, string> Origins, List<ResolvedTweak> Resolved) BuildTweakValues(
        LoadedMod mod,
        TweakSelection? selection,
        List<PatchDiagnostic> diagnostics)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var origins = new Dictionary<string, string>(StringComparer.Ordinal);
        var resolved = new List<ResolvedTweak>();

        foreach (var tweak in mod.Manifest.Tweaks)
        {
            var source = ResolveFromSelection(selection, mod.Manifest.Id, tweak);

            string value;
            string origin;

            if (source is { } supplied)
            {
                value = supplied.Value;
                origin = supplied.Origin;

                if (tweak.Type is "number" or "integer"
                    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric)
                    && ((tweak.Min is { } min && numeric < min) || (tweak.Max is { } max && numeric > max)))
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Warning,
                        DiagnosticCodes.TweakValueOutOfRange,
                        $"Tweak '{mod.Manifest.Id}:{tweak.Id}' value '{value}' is outside the declared range; using it anyway."));
                }

                if (origin == TweakOrigins.Lockfile && !string.Equals(value, tweak.Default, StringComparison.Ordinal))
                {
                    diagnostics.Add(new PatchDiagnostic(
                        PatchDiagnosticSeverity.Info,
                        DiagnosticCodes.TweakValuePinnedByLockfile,
                        $"Tweak '{mod.Manifest.Id}:{tweak.Id}' is pinned to '{value}' by the lockfile (the mod's current default is '{tweak.Default}')."));
                }
            }
            else
            {
                value = tweak.Default;
                origin = TweakOrigins.Default;
            }

            values[tweak.Id] = value;
            origins[tweak.Id] = origin;
            resolved.Add(new ResolvedTweak(tweak.Id, value, value, origin));
        }

        return (values, origins, resolved);
    }

    // Look the tweak up by its current id first, then by each legacy alias so a stored value written
    // under an old id survives a rename (the manager keeps the same contract on the storage side).
    private static TweakValueSource? ResolveFromSelection(TweakSelection? selection, string modId, TweakDeclaration tweak)
    {
        if (selection is null)
        {
            return null;
        }

        if (selection.Resolve(modId, tweak.Id) is { } direct)
        {
            return direct;
        }

        foreach (var alias in tweak.Aliases)
        {
            if (selection.Resolve(modId, alias) is { } viaAlias)
            {
                return viaAlias;
            }
        }

        return null;
    }

    // Return a copy of the operation with every value-carrying field's placeholders substituted, and
    // emit one tweakValueResolved info per placeholder. Operations without `{{` are returned as-is.
    private PatchOperation ResolveTweakPlaceholders(
        PatchOperation operation,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> origins,
        List<PatchDiagnostic> diagnostics)
    {
        if (!HasPlaceholder(operation))
        {
            return operation;
        }

        var resolutions = new List<TweakPlaceholderResolution>();
        string? Resolve(string? field) => field is null ? null : _tweakResolver.Resolve(field, values, resolutions);

        var resolved = new PatchOperation
        {
            Id = operation.Id,
            Operation = operation.Operation,
            Risk = operation.Risk,
            Reason = operation.Reason,
            Target = operation.Target,
            Attribute = operation.Attribute,
            Value = Resolve(operation.Value),
            ExpectedOldValue = Resolve(operation.ExpectedOldValue),
            Xml = Resolve(operation.Xml),
            ExpectedOldXml = Resolve(operation.ExpectedOldXml),
        };

        foreach (var resolution in resolutions)
        {
            var origin = origins.TryGetValue(resolution.TweakId, out var value) ? value : "unknown";
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.TweakValueResolved,
                $"Resolved {{{{ tweaks.{resolution.TweakId} }}}} to '{resolution.ResolvedText}' (source: {origin}) in operation '{operation.Id}'."));
        }

        return resolved;
    }

    private static bool HasPlaceholder(PatchOperation operation)
        => ContainsPlaceholder(operation.Value)
            || ContainsPlaceholder(operation.ExpectedOldValue)
            || ContainsPlaceholder(operation.Xml)
            || ContainsPlaceholder(operation.ExpectedOldXml);

    private static bool ContainsPlaceholder(string? value)
        => value is not null && value.Contains("{{", StringComparison.Ordinal);

    private static List<PatchEntryWrite> PlanEntryOperations(LoadedMod mod, List<PatchDiagnostic> diagnostics)
    {
        var entryWrites = new List<PatchEntryWrite>();
        var entries = mod.Manifest.Entries;
        if (entries is null) return entryWrites;

        foreach (var mapping in entries.Replace)
        {
            if (ResolveSource(mod, mapping, diagnostics) is { } source)
            {
                entryWrites.Add(new PatchEntryWrite(mod.Manifest.Id, EntryOperationType.Replace, mapping.Path, source));
            }
        }

        foreach (var mapping in entries.Add)
        {
            if (ResolveSource(mod, mapping, diagnostics) is { } source)
            {
                entryWrites.Add(new PatchEntryWrite(mod.Manifest.Id, EntryOperationType.Add, mapping.Path, source));
            }
        }

        foreach (var path in entries.Delete)
        {
            entryWrites.Add(new PatchEntryWrite(mod.Manifest.Id, EntryOperationType.Delete, path, SourceFile: null));
        }

        return entryWrites;
    }

    private static string? ResolveSource(LoadedMod mod, EntryFileMapping mapping, List<PatchDiagnostic> diagnostics)
    {
        var fullSource = System.IO.Path.Combine(mod.Directory, mapping.Source);
        if (!File.Exists(fullSource))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.EntrySourceMissing,
                $"Entry source '{mapping.Source}' for pak entry '{mapping.Path}' not found at '{fullSource}'.",
                fullSource));
            return null;
        }
        return fullSource;
    }

    private static PatchDiagnostic Error(string code, string message, string? path = null)
        => new(PatchDiagnosticSeverity.Error, code, message, path);

    private static List<PatchWriteConflict> DetectConflicts(IEnumerable<PatchPlan> modPlans)
    {
        return modPlans
            .SelectMany(plan => plan.Writes)
            .GroupBy(GetTargetKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new PatchWriteConflict(DiagnosticCodes.DuplicateWriteTarget, group.Key, group.ToList()))
            .ToList();
    }

    private static List<PatchEntryConflict> DetectEntryConflicts(IEnumerable<PatchPlan> modPlans)
    {
        // Group entry operations by their pak path. Two or more touches on the
        // same path conflict UNLESS every touching mod is the same one (a mod
        // listing the same path twice is its own pre-flight issue, not a
        // cross-mod conflict) OR every operation is a Delete (deletes are
        // idempotent — two mods deleting the same entry agree).
        return modPlans
            .SelectMany(plan => plan.EntryWrites)
            .GroupBy(write => write.Path, StringComparer.OrdinalIgnoreCase)
            .Where(group =>
            {
                var distinctMods = group.Select(w => w.ModId).Distinct(StringComparer.Ordinal).Count();
                if (distinctMods < 2) return false;
                return !group.All(w => w.Operation == EntryOperationType.Delete);
            })
            .Select(group => new PatchEntryConflict(DiagnosticCodes.EntryConflict, group.Key, group.ToList()))
            .ToList();
    }

    private static string GetTargetKey(PatchWrite write)
    {
        // Single-target operations (replaceValue/Attribute/Node) collide on file+entity+component+path+attribute.
        // List operations also include the item content so two adds with different items do not collide, but two
        // adds or an add and a remove targeting the same item content do.
        // Entity operations collide on file+entity-guid so add+add, add+remove, and remove+remove for the same
        // entity all surface as a single conflict regardless of payload differences.
        // mergeComponent collides on file+entity+component so two merges into the same component conflict.
        var (kindMarker, itemKey) = write.OperationType switch
        {
            PatchOperationTypes.AddListItem => ("list", write.NewValue),
            PatchOperationTypes.RemoveListItem => ("list", write.OldValue),
            PatchOperationTypes.AddEntity => ("entity", string.Empty),
            PatchOperationTypes.RemoveEntity => ("entity", string.Empty),
            PatchOperationTypes.MergeComponent => ("merge", string.Empty),
            _ => ("single", string.Empty),
        };

        // For entity-level operations the path and attribute slots are unused; for merge the path slot is unused.
        // Including them in the key keeps the format uniform without changing the semantics.
        return string.Join(
            '|',
            write.File,
            write.EntityGuid,
            write.Component,
            write.Path,
            write.Attribute ?? string.Empty,
            kindMarker,
            itemKey);
    }
}
