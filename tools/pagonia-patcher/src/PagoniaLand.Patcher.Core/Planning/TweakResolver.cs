using System.Text.RegularExpressions;

namespace PagoniaLand.Patcher;

/// <summary>
/// Substitutes <c>{{ tweaks.&lt;id&gt; }}</c> placeholders in a patch operation's value-carrying
/// fields with the user-chosen (or default) tweak value, before the operation is resolved against
/// the XML. Two forms only — a literal substitution and a single boolean ternary:
/// <code>
/// {{ tweaks.softwood-cost }}
/// {{ tweaks.free-upkeep ? 'NoUpkeep' : 'Normal' }}
/// </code>
/// No arithmetic, no nested expressions, no eval surface — keeps the patcher AOT-clean and the
/// grammar tiny. Resolution runs at plan time so the plan report shows the substituted values.
/// </summary>
public sealed partial class TweakResolver
{
    // A {{ ... }} block (non-greedy, may span lines inside an `xml` field). The inner text is then
    // matched against the strict grammar so incidental braces in XML content stay untouched.
    [GeneratedRegex(@"\{\{(.*?)\}\}", RegexOptions.Singleline)]
    private static partial Regex PlaceholderBlock();

    [GeneratedRegex(@"^tweaks\.([a-z0-9][a-z0-9-]*)$")]
    private static partial Regex LiteralForm();

    [GeneratedRegex(@"^tweaks\.([a-z0-9][a-z0-9-]*)\s*\?\s*'([^']*)'\s*:\s*'([^']*)'$")]
    private static partial Regex TernaryForm();

    /// <summary>
    /// Resolve every tweak placeholder in <paramref name="input"/>. <paramref name="values"/> maps a
    /// declared tweak id to its effective string value. Each substituted placeholder is appended to
    /// <paramref name="resolutions"/>. Throws <see cref="TweakResolutionError"/> when a placeholder
    /// references an id absent from <paramref name="values"/> (undeclared) or is malformed.
    /// </summary>
    public string Resolve(string input, IReadOnlyDictionary<string, string> values, List<TweakPlaceholderResolution> resolutions)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains("{{", StringComparison.Ordinal))
        {
            return input;
        }

        return PlaceholderBlock().Replace(input, match =>
        {
            var inner = match.Groups[1].Value.Trim();

            // Only blocks that name a tweak are placeholders; leave any other `{{ ... }}` text as-is.
            if (!inner.StartsWith("tweaks.", StringComparison.Ordinal))
            {
                return match.Value;
            }

            var literal = LiteralForm().Match(inner);
            if (literal.Success)
            {
                var id = literal.Groups[1].Value;
                var value = RequireValue(id, values, match.Value);
                resolutions.Add(new TweakPlaceholderResolution(id, value));
                return value;
            }

            var ternary = TernaryForm().Match(inner);
            if (ternary.Success)
            {
                var id = ternary.Groups[1].Value;
                var value = RequireValue(id, values, match.Value);
                var resolved = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                    ? ternary.Groups[2].Value
                    : ternary.Groups[3].Value;
                resolutions.Add(new TweakPlaceholderResolution(id, resolved));
                return resolved;
            }

            throw new TweakResolutionError(TweakResolutionErrorKind.MalformedSyntax, match.Value, null);
        });
    }

    private static string RequireValue(string id, IReadOnlyDictionary<string, string> values, string placeholder)
        => values.TryGetValue(id, out var value)
            ? value
            : throw new TweakResolutionError(TweakResolutionErrorKind.UndeclaredTweak, placeholder, id);

    /// <summary>
    /// Enumerate the well-formed tweak placeholders in <paramref name="input"/> without substituting
    /// or throwing — used by <c>validate-mod</c>'s lint pass. Malformed or non-tweak <c>{{ … }}</c>
    /// blocks are skipped here; plan-time resolution is what reports those.
    /// </summary>
    public IReadOnlyList<TweakReference> ExtractReferences(string? input)
    {
        var references = new List<TweakReference>();
        if (string.IsNullOrEmpty(input) || !input.Contains("{{", StringComparison.Ordinal))
        {
            return references;
        }

        foreach (Match match in PlaceholderBlock().Matches(input))
        {
            var inner = match.Groups[1].Value.Trim();
            if (!inner.StartsWith("tweaks.", StringComparison.Ordinal))
            {
                continue;
            }

            var literal = LiteralForm().Match(inner);
            if (literal.Success)
            {
                references.Add(new TweakReference(literal.Groups[1].Value, IsTernary: false));
                continue;
            }

            var ternary = TernaryForm().Match(inner);
            if (ternary.Success)
            {
                references.Add(new TweakReference(ternary.Groups[1].Value, IsTernary: true));
            }
        }

        return references;
    }
}

/// <summary>A tweak placeholder found in a patch op: the referenced id and whether it used the ternary form.</summary>
public readonly record struct TweakReference(string TweakId, bool IsTernary);

/// <summary>One placeholder substitution: the tweak id and the text it resolved to.</summary>
public readonly record struct TweakPlaceholderResolution(string TweakId, string ResolvedText);

public enum TweakResolutionErrorKind
{
    MalformedSyntax,
    UndeclaredTweak,
}

/// <summary>Raised by <see cref="TweakResolver"/> for an undeclared tweak reference or malformed placeholder.</summary>
public sealed class TweakResolutionError : Exception
{
    public TweakResolutionError(TweakResolutionErrorKind kind, string placeholder, string? tweakId)
        : base(BuildMessage(kind, placeholder, tweakId))
    {
        Kind = kind;
        Placeholder = placeholder;
        TweakId = tweakId;
    }

    public TweakResolutionErrorKind Kind { get; }

    public string Placeholder { get; }

    public string? TweakId { get; }

    private static string BuildMessage(TweakResolutionErrorKind kind, string placeholder, string? tweakId) => kind switch
    {
        TweakResolutionErrorKind.UndeclaredTweak =>
            $"Placeholder '{placeholder}' references tweak '{tweakId}', which the mod does not declare.",
        _ =>
            $"Malformed tweak placeholder '{placeholder}'. Use {{{{ tweaks.<id> }}}} or {{{{ tweaks.<id> ? 'a' : 'b' }}}}.",
    };
}
