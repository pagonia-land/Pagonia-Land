namespace PagoniaLand.Patcher;

/// <summary>
/// External tweak values a CLI consumer supplies as repeatable
/// <c>--tweak &lt;mod-id&gt;:&lt;tweak-id&gt;=&lt;value&gt;</c> flags. Keyed by mod id, then tweak id.
/// A tweak with no override resolves to the mod-declared default. Later phases layer
/// collection-supplied and lockfile-pinned values on top of this.
/// </summary>
public sealed class TweakOverrides
{
    private readonly Dictionary<string, Dictionary<string, string>> _byMod;

    private TweakOverrides(Dictionary<string, Dictionary<string, string>> byMod) => _byMod = byMod;

    public static TweakOverrides Empty { get; } = new(new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal));

    public bool IsEmpty => _byMod.Count == 0;

    /// <summary>The mod ids that carry at least one override.</summary>
    public IReadOnlyCollection<string> ModIds => _byMod.Keys;

    /// <summary>The tweak-id → value overrides for one mod, or <c>null</c> when the mod has none.</summary>
    public IReadOnlyDictionary<string, string>? ForMod(string modId)
        => _byMod.TryGetValue(modId, out var map) ? map : null;

    /// <summary>
    /// Parse <c>--tweak</c> flag values of the form <c>&lt;mod-id&gt;:&lt;tweak-id&gt;=&lt;value&gt;</c>.
    /// A malformed entry yields a <see cref="DiagnosticCodes.TweakOverrideMalformed"/> error and is
    /// skipped; well-formed entries still parse. A repeated mod+tweak keeps the last value.
    /// </summary>
    public static (TweakOverrides Overrides, IReadOnlyList<PatchDiagnostic> Diagnostics) Parse(IEnumerable<string> flags)
    {
        var byMod = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var diagnostics = new List<PatchDiagnostic>();

        foreach (var flag in flags)
        {
            var colon = flag.IndexOf(':', StringComparison.Ordinal);
            var equals = flag.IndexOf('=', StringComparison.Ordinal);

            // Need a non-empty mod id before the colon and a non-empty tweak id between colon and '='.
            if (colon <= 0 || equals <= colon + 1)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.TweakOverrideMalformed,
                    $"Malformed --tweak '{flag}'. Expected <mod-id>:<tweak-id>=<value>."));
                continue;
            }

            var modId = flag[..colon];
            var tweakId = flag[(colon + 1)..equals];
            var value = flag[(equals + 1)..];

            if (!byMod.TryGetValue(modId, out var map))
            {
                map = new Dictionary<string, string>(StringComparer.Ordinal);
                byMod[modId] = map;
            }

            map[tweakId] = value;
        }

        return (new TweakOverrides(byMod), diagnostics);
    }
}
