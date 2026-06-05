namespace PagoniaLand.Manager;

/// <summary>
/// Computes, for each of the four canonical packages, the
/// <c>{ present, owned, effective }</c> triple the rest of the manager reasons
/// about, where <c>effective = present ∧ owned</c> describes the
/// <em>solo runtime effect</em> for messaging — <strong>not</strong>
/// deploy-ability (deployment keys off <c>present</c> alone; see the Phase-9
/// load-bearing rule). Pure function of its inputs: presence (an on-disk fact),
/// the declared ownership record, and an optional in-memory override map. The
/// override lets a CLI flag or a headless/CI run simulate entitlement without
/// touching the stored record.
/// </summary>
public static class ExpansionResolver
{
    /// <summary>
    /// Resolve every canonical package's state.
    /// </summary>
    /// <param name="presence">Which packages are physically present (from
    /// <see cref="PackagePresenceDetector"/>).</param>
    /// <param name="declared">The install's stored ownership, or <c>null</c> if
    /// the install has no record yet (⇒ every declarable expansion unknown).</param>
    /// <param name="overrides">Optional transient per-package ownership overrides,
    /// keyed by package name. Wins over <paramref name="declared"/> for declarable
    /// packages; ignored for always-owned <c>core</c> / <c>tools</c>. Never persisted.</param>
    public static IReadOnlyList<ExpansionState> Resolve(
        PackagePresence presence,
        OwnedExpansions? declared,
        IReadOnlyDictionary<string, OwnershipState>? overrides = null)
    {
        var result = new List<ExpansionState>(ExpansionPackages.All.Count);
        foreach (var package in ExpansionPackages.All)
        {
            result.Add(ResolveOne(package, presence, declared, overrides));
        }
        return result;
    }

    /// <summary>Resolve a single package's state. Convenience for the "is dlc1
    /// effective on this install?" call shape a later step (or GUI) wants.</summary>
    public static ExpansionState Resolve(
        string package,
        PackagePresence presence,
        OwnedExpansions? declared,
        IReadOnlyDictionary<string, OwnershipState>? overrides = null)
    {
        if (!ExpansionPackages.IsKnown(package))
        {
            throw new ArgumentOutOfRangeException(
                nameof(package), package, "Not one of the four canonical packages.");
        }
        return ResolveOne(package, presence, declared, overrides);
    }

    private static ExpansionState ResolveOne(
        string package,
        PackagePresence presence,
        OwnedExpansions? declared,
        IReadOnlyDictionary<string, OwnershipState>? overrides)
    {
        var present = presence.IsPresent(package);
        var ownership = ResolveOwnership(package, declared, overrides);
        // effective = present ∧ owned. Unknown and NotOwned both yield false,
        // but the ExpansionState carries the tri-state so a surface can tell
        // "you haven't said" apart from "you don't own it".
        var effective = present && ownership == OwnershipState.Owned;
        return new ExpansionState(package, present, ownership, effective);
    }

    private static OwnershipState ResolveOwnership(
        string package,
        OwnedExpansions? declared,
        IReadOnlyDictionary<string, OwnershipState>? overrides)
    {
        // core / tools are the base game + editor data — always owned, and an
        // override never flips that (you can't "not own" the base game).
        if (ExpansionPackages.IsAlwaysOwned(package))
        {
            return OwnershipState.Owned;
        }

        // A transient override (e.g. --assume-not-owned dlc1) wins over the
        // stored declaration for a declarable package. Matched case-insensitively
        // so callers don't have to construct the map with a specific comparer.
        if (overrides is not null)
        {
            foreach (var kvp in overrides)
            {
                if (string.Equals(kvp.Key, package, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        // Fall back to the declared value; absent record ⇒ unknown.
        return declared?.For(package) ?? OwnershipState.Unknown;
    }
}
