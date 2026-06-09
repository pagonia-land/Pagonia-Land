using PagoniaLand.Patcher;

namespace PagoniaLand.Manager.Cli.Interactive;

// Builds the op-aware hints the Configure-mod wizard shows for a tweak: a compact cell for the
// table's Effect column and a one-line explanation for the edit screen, both with a before -> after
// preview for arithmetic ops. The preview math goes through ArithmeticPatchOps so it matches what a
// deploy would actually write (rounding + clamping included). Pure display — no I/O, no mutation.
internal static class TweakEffectHint
{
    /// <summary>Compact Effect-column cell for <paramref name="value"/>; empty when there is nothing
    /// useful to add (e.g. an enum/preset whose value column already says it all).</summary>
    public static string Summary(TweakValueView tweak, string value)
    {
        var arithmetic = ArithmeticUsages(tweak);
        if (arithmetic.Count > 0)
        {
            var first = arithmetic[0];
            var symbol = first.OperationType == PatchOperationTypes.MultiplyValue ? "x" : "+";
            var preview = Preview(first, value);
            var head = preview is null ? symbol : $"{symbol} {preview}";
            return arithmetic.Count > 1 ? $"{head} (+{arithmetic.Count - 1} more)" : head;
        }

        if (string.Equals(tweak.Declaration.Type, "boolean", StringComparison.Ordinal))
        {
            return "on/off toggle";
        }

        return tweak.Usages.Any(u => u.OperationType == PatchOperationTypes.ReplaceValue)
            ? "sets value directly"
            : string.Empty;
    }

    /// <summary>One-line explanation for the edit screen, previewing <paramref name="value"/>.
    /// Null when the tweak feeds no arithmetic op (the value prompt already speaks for itself).</summary>
    public static string? Detail(TweakValueView tweak, string value)
    {
        var arithmetic = ArithmeticUsages(tweak);
        if (arithmetic.Count == 0)
        {
            return null;
        }

        var first = arithmetic[0];
        var verb = first.OperationType == PatchOperationTypes.MultiplyValue ? "multiplies" : "offsets";
        var scope = arithmetic.Count == 1 ? "1 value" : $"{arithmetic.Count} values";
        var reason = first.Reason is { } r ? $" — \"{r}\"" : string.Empty;
        var preview = Preview(first, value);

        return preview is null
            ? $"{verb} {scope}{reason}"
            : $"{verb} {scope}{reason}: {preview}";
    }

    private static IReadOnlyList<TweakUsage> ArithmeticUsages(TweakValueView tweak)
        => tweak.Usages.Where(u => ArithmeticPatchOps.IsArithmetic(u.OperationType)).ToList();

    // "4 -> 10" for the given operand value, or null when the base/operand isn't numeric.
    private static string? Preview(TweakUsage usage, string value)
    {
        if (usage.ExpectedOldValue is not { } baseText
            || !ArithmeticPatchOps.TryParse(baseText, out var baseValue)
            || !ArithmeticPatchOps.TryParse(value, out var operand))
        {
            return null;
        }

        double? min = ArithmeticPatchOps.TryParse(usage.ClampMin, out var parsedMin) ? parsedMin : null;
        double? max = ArithmeticPatchOps.TryParse(usage.ClampMax, out var parsedMax) ? parsedMax : null;

        var result = ArithmeticPatchOps.Compute(usage.OperationType, baseValue, operand, usage.Rounding, min, max, out _);
        return $"{baseText} -> {result}";
    }
}
