using System.Globalization;

namespace PagoniaLand.Patcher;

/// <summary>
/// The pure arithmetic behind <c>multiplyValue</c> / <c>addValue</c>: combine an existing numeric
/// value with an operand, round (game-database values are integers), then optionally clamp. Kept in
/// one place so the planner's write path and any preview (e.g. the manager's tweak wizard) compute
/// byte-identical results — no duplicated rounding/clamp logic to drift apart.
/// </summary>
public static class ArithmeticPatchOps
{
    public static bool IsArithmetic(string operationType)
        => operationType is PatchOperationTypes.MultiplyValue or PatchOperationTypes.AddValue;

    /// <summary>True when combining the two finite inputs stays finite. Guards against an overflow
    /// to ±Infinity (e.g. two near-<c>double.MaxValue</c> operands) that <see cref="Compute"/> would
    /// otherwise serialise as the literal text "Infinity" into a game field. The combine logic lives
    /// here so the caller's check can't drift from what <see cref="Compute"/> actually does.</summary>
    public static bool ResultIsFinite(string operationType, double oldValue, double operand)
    {
        var combined = operationType == PatchOperationTypes.AddValue
            ? oldValue + operand
            : oldValue * operand;
        return double.IsFinite(combined);
    }

    /// <summary>Invariant-culture float parse, shared so every caller accepts the same number shapes.
    /// Rejects NaN / ±Infinity so they surface as a clean "not numeric" diagnostic instead of
    /// silently computing nonsensical results from a target leaf or operand that literally holds
    /// one of those words.</summary>
    public static bool TryParse(string? text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Combine <paramref name="oldValue"/> with <paramref name="operand"/> (multiply for
    /// <c>multiplyValue</c>, add for <c>addValue</c>), round per <paramref name="rounding"/>
    /// (<c>round</c> / <c>floor</c> / <c>ceil</c>; null → round), then clamp to the optional bounds.
    /// <paramref name="clamped"/> reports whether a bound actually moved the result.
    /// </summary>
    public static string Compute(
        string operationType,
        double oldValue,
        double operand,
        string? rounding,
        double? clampMin,
        double? clampMax,
        out bool clamped)
    {
        var combined = operationType == PatchOperationTypes.AddValue
            ? oldValue + operand
            : oldValue * operand;

        var rounded = ApplyRounding(combined, rounding);
        var result = rounded;
        // Round the clamp bounds with the same policy before applying them: game values are
        // integers, so a fractional bound (e.g. clampMin "1.5") must not leave a non-integer
        // result in an integer field. With both the value and the bounds rounded, the clamped
        // result stays integral.
        if (clampMin is { } rawMin)
        {
            var min = ApplyRounding(rawMin, rounding);
            if (result < min) { result = min; }
        }
        if (clampMax is { } rawMax)
        {
            var max = ApplyRounding(rawMax, rounding);
            if (result > max) { result = max; }
        }

        clamped = result != rounded;
        return Format(result);
    }

    private static double ApplyRounding(double value, string? rounding)
        => rounding?.Trim().ToLowerInvariant() switch
        {
            "floor" => Math.Floor(value),
            "ceil" => Math.Ceiling(value),
            // null/empty/"round"/anything else → nearest, ties away from zero. The schema enum keeps
            // authors to the three documented values; an unknown literal falls back to round defensively.
            _ => Math.Round(value, MidpointRounding.AwayFromZero),
        };

    /// <summary>Whole numbers serialise without a trailing ".0" (a rounded result reads as a plain
    /// game value "6", not "6.0"); a fractional value still round-trips. The <c>(long)</c> cast is
    /// guarded: a whole value beyond <see cref="long"/> range would overflow, so fall back to
    /// <c>"F0"</c> (a plain integer string, no exponent).</summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
        if (value == Math.Floor(value))
        {
            return Math.Abs(value) < 9.2e18
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("F0", CultureInfo.InvariantCulture);
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
