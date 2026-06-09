namespace PagoniaLand.Patcher;

/// <summary>
/// How a declared tweak is consumed by a patch operation — the referencing op's id, type, the field
/// the placeholder sits in, the author's <c>reason</c>, and the op's <c>expectedOldValue</c> (the
/// vanilla base for an arithmetic preview). Enough for a UI to explain what setting the tweak does
/// without re-parsing the patches itself.
/// </summary>
public sealed record TweakUsage(
    string TweakId,
    string OperationId,
    string OperationType,
    string OperandField,
    string? Reason,
    string? ExpectedOldValue,
    string? Rounding,
    string? ClampMin,
    string? ClampMax);

/// <summary>
/// Scans a loaded mod's patch operations for <c>{{ tweaks.&lt;id&gt; }}</c> references in their
/// value-producing fields (<c>value</c> / <c>factor</c> / <c>delta</c>), so the manager can show an
/// op-aware hint when a player edits a tweak. Read-only and op-agnostic on the tweak side — it does
/// not change resolution, it only reports the wiring that already exists.
/// </summary>
public static class TweakUsageScanner
{
    private static readonly TweakResolver Resolver = new();

    public static IReadOnlyList<TweakUsage> Scan(LoadedMod mod)
    {
        var usages = new List<TweakUsage>();

        foreach (var operation in mod.PatchFiles.SelectMany(file => file.PatchFile.Operations))
        {
            foreach (var (field, name) in OperandFields(operation))
            {
                foreach (var reference in Resolver.ExtractReferences(field))
                {
                    usages.Add(new TweakUsage(
                        reference.TweakId,
                        operation.Id,
                        operation.Operation,
                        name,
                        string.IsNullOrWhiteSpace(operation.Reason) ? null : operation.Reason,
                        operation.ExpectedOldValue,
                        operation.Rounding,
                        operation.ClampMin,
                        operation.ClampMax));
                }
            }
        }

        return usages;
    }

    // The fields that carry the value a tweak produces. expectedOldValue/xml can also reference a
    // tweak, but those are not the "effect" a player is dialing in, so they are left out of the hint.
    private static IEnumerable<(string? Field, string Name)> OperandFields(PatchOperation operation)
    {
        yield return (operation.Value, "value");
        yield return (operation.Factor, "factor");
        yield return (operation.Delta, "delta");
    }
}
