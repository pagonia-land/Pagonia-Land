namespace PagoniaLand.Patcher;

/// <summary>Where a resolved tweak value came from. Recorded on each <see cref="ResolvedTweak"/>.</summary>
public static class TweakOrigins
{
    public const string Default = "default";
    public const string Collection = "collection";
    public const string External = "external";
    public const string Lockfile = "lockfile";
}

/// <summary>One layer's contribution for a tweak: the raw value and the origin that supplied it.</summary>
public readonly record struct TweakValueSource(string Value, string Origin);

/// <summary>
/// The layered set of tweak values a plan resolves against, newest-wins by precedence:
/// <b>lockfile pin &gt; CLI <c>--tweak</c> override &gt; collection-supplied value &gt; mod default</b>.
/// A lockfile is a deterministic reproduction artifact, so its pin intentionally beats an ad-hoc CLI
/// override; the mod default is the implicit floor the planner falls back to when no layer provides a
/// value (handled by the planner, not here). Build one with the fluent <c>With…</c> methods.
/// </summary>
public sealed class TweakSelection
{
    private readonly Dictionary<string, Dictionary<string, string>> _collection = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _cli = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _lockfile = new(StringComparer.OrdinalIgnoreCase);

    public static TweakSelection Create() => new();

    public static TweakSelection ForCli(TweakOverrides? overrides) => new TweakSelection().WithCli(overrides);

    public TweakSelection WithCli(TweakOverrides? overrides)
    {
        if (overrides is not null)
        {
            foreach (var modId in overrides.ModIds)
            {
                if (overrides.ForMod(modId) is { } values)
                {
                    Merge(_cli, modId, values);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Populate values programmatically (origin <see cref="TweakOrigins.External"/>),
    /// for a library consumer that already holds a <c>tweakId → value</c> map rather than the raw
    /// <c>--tweak</c> CLI strings <see cref="WithCli"/> parses. The manager uses this to thread a
    /// profile's per-mod user overrides into a plan. Note this writes into the same
    /// underlying layer as <see cref="WithCli"/> (not a separate one), so external and
    /// CLI values share precedence and a later call overwrites an earlier one for the same key.
    /// </summary>
    public TweakSelection WithExternalValues(string modId, IReadOnlyDictionary<string, string>? values)
    {
        Merge(_cli, modId, values);
        return this;
    }

    public TweakSelection WithCollectionValues(string modId, IReadOnlyDictionary<string, string>? values)
    {
        Merge(_collection, modId, values);
        return this;
    }

    public TweakSelection WithLockfileValues(string modId, IReadOnlyDictionary<string, string>? values)
    {
        Merge(_lockfile, modId, values);
        return this;
    }

    /// <summary>
    /// The effective value for <paramref name="modId"/>/<paramref name="tweakId"/> from the
    /// highest-precedence layer that supplies it, or <c>null</c> when no layer does (the caller then
    /// uses the mod default).
    /// </summary>
    public TweakValueSource? Resolve(string modId, string tweakId)
    {
        if (TryGet(_lockfile, modId, tweakId, out var lockfileValue))
        {
            return new TweakValueSource(lockfileValue, TweakOrigins.Lockfile);
        }

        if (TryGet(_cli, modId, tweakId, out var cliValue))
        {
            return new TweakValueSource(cliValue, TweakOrigins.External);
        }

        if (TryGet(_collection, modId, tweakId, out var collectionValue))
        {
            return new TweakValueSource(collectionValue, TweakOrigins.Collection);
        }

        return null;
    }

    private static void Merge(Dictionary<string, Dictionary<string, string>> layer, string modId, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        if (!layer.TryGetValue(modId, out var map))
        {
            map = new Dictionary<string, string>(StringComparer.Ordinal);
            layer[modId] = map;
        }

        foreach (var (tweakId, value) in values)
        {
            map[tweakId] = value;
        }
    }

    private static bool TryGet(Dictionary<string, Dictionary<string, string>> layer, string modId, string tweakId, out string value)
    {
        if (layer.TryGetValue(modId, out var map) && map.TryGetValue(tweakId, out var found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
