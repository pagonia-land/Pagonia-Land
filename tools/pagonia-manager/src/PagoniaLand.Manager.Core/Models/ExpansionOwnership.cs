using YamlDotNet.Serialization;

namespace PagoniaLand.Manager;

/// <summary>
/// The four canonical Pioneers of Pagonia packages the manager reasons about,
/// plus which of them are user-declarable as "owned". Envision Entertainment
/// ships every pak to every player; <c>core</c> and <c>tools</c> are the base
/// game + editor data and are always treated as owned, so only
/// <c>decorations1</c> and <c>dlc1</c> carry a declarable ownership state.
/// </summary>
public static class ExpansionPackages
{
    public const string Core = "core";
    public const string Decorations1 = "decorations1";
    public const string Dlc1 = "dlc1";
    public const string Tools = "tools";

    /// <summary>All four canonical packages, in the order surfaces should list them.</summary>
    public static readonly IReadOnlyList<string> All = [Core, Decorations1, Dlc1, Tools];

    /// <summary>Packages whose ownership the user can declare. <c>core</c> / <c>tools</c>
    /// are excluded — they are always owned and never stored.</summary>
    public static readonly IReadOnlyList<string> Declarable = [Decorations1, Dlc1];

    /// <summary>Packages that are always owned (base game + editor data).</summary>
    public static readonly IReadOnlyList<string> AlwaysOwned = [Core, Tools];

    /// <summary>True if <paramref name="package"/> is one of the four canonical packages.</summary>
    public static bool IsKnown(string package) =>
        All.Any(p => string.Equals(p, package, StringComparison.OrdinalIgnoreCase));

    /// <summary>True if the user can declare ownership for <paramref name="package"/>
    /// (i.e. it is <c>decorations1</c> or <c>dlc1</c>).</summary>
    public static bool IsDeclarable(string package) =>
        Declarable.Any(p => string.Equals(p, package, StringComparison.OrdinalIgnoreCase));

    /// <summary>True if <paramref name="package"/> is always owned (<c>core</c> / <c>tools</c>).</summary>
    public static bool IsAlwaysOwned(string package) =>
        AlwaysOwned.Any(p => string.Equals(p, package, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Tri-state ownership of a declarable expansion. <see cref="Unknown"/> is the
/// default for an install the manager has never been told about — it resolves
/// to "not effective" (we never silently assume ownership) but is rendered
/// distinctly from a hard <see cref="NotOwned"/> so a surface can prompt the
/// user to declare rather than mislead them into thinking they don't own it.
/// </summary>
public enum OwnershipState
{
    Unknown,
    Owned,
    NotOwned,
}

/// <summary>
/// Mapping between the stored nullable-bool shape (<c>true</c> = owned,
/// <c>false</c> = not owned, absent/<c>null</c> = unknown) and
/// <see cref="OwnershipState"/>. Keeping the on-disk form a nullable bool lets
/// an unknown expansion simply be omitted from <c>ownedExpansions</c> (via the
/// serializer's OmitNull), so a pre-Phase-9 store with no <c>installs:</c> map
/// reads back as every expansion unknown with no migration step.
/// </summary>
public static class OwnershipStateExtensions
{
    public static OwnershipState ToOwnershipState(this bool? stored) => stored switch
    {
        true => OwnershipState.Owned,
        false => OwnershipState.NotOwned,
        null => OwnershipState.Unknown,
    };

    public static bool? ToStoredValue(this OwnershipState state) => state switch
    {
        OwnershipState.Owned => true,
        OwnershipState.NotOwned => false,
        _ => null,
    };
}

/// <summary>
/// Declared ownership for the two declarable expansions of a single game
/// install. Persisted under each <see cref="InstallRecord.OwnedExpansions"/>.
/// A <c>null</c> member (the default) means "unknown" — the serializer omits
/// it, so an all-unknown install carries no ownership keys at all.
/// </summary>
public sealed class OwnedExpansions
{
    [YamlMember(Alias = "decorations1")]
    public bool? Decorations1 { get; init; }

    [YamlMember(Alias = "dlc1")]
    public bool? Dlc1 { get; init; }

    /// <summary>Tri-state ownership for the named declarable package; throws for a
    /// non-declarable or unknown package name (callers gate on
    /// <see cref="ExpansionPackages.IsDeclarable"/> first).</summary>
    public OwnershipState For(string package)
    {
        if (string.Equals(package, ExpansionPackages.Decorations1, StringComparison.OrdinalIgnoreCase))
        {
            return Decorations1.ToOwnershipState();
        }
        if (string.Equals(package, ExpansionPackages.Dlc1, StringComparison.OrdinalIgnoreCase))
        {
            return Dlc1.ToOwnershipState();
        }
        throw new ArgumentOutOfRangeException(
            nameof(package), package, "Only declarable expansions carry a stored ownership value.");
    }

    /// <summary>Return a copy with <paramref name="package"/>'s ownership set to
    /// <paramref name="state"/>. Used by the <c>expansions set</c> writer.</summary>
    public OwnedExpansions With(string package, OwnershipState state)
    {
        if (string.Equals(package, ExpansionPackages.Decorations1, StringComparison.OrdinalIgnoreCase))
        {
            return new OwnedExpansions { Decorations1 = state.ToStoredValue(), Dlc1 = Dlc1 };
        }
        if (string.Equals(package, ExpansionPackages.Dlc1, StringComparison.OrdinalIgnoreCase))
        {
            return new OwnedExpansions { Decorations1 = Decorations1, Dlc1 = state.ToStoredValue() };
        }
        throw new ArgumentOutOfRangeException(
            nameof(package), package, "Only declarable expansions carry a stored ownership value.");
    }
}

/// <summary>
/// Per-game-install ownership record stored at store scope under
/// <c>state.yaml</c> → <c>installs:</c>, keyed by the same gameRoot fingerprint
/// the deploy history uses (<see cref="GameFingerprint"/>). Ownership is a fact
/// about the installation/account, stable across every profile — so it lives
/// here, never in a portable profile.
/// </summary>
public sealed class InstallRecord
{
    /// <summary>The absolute game-root path this record was last written for —
    /// stored for human readability / debugging; the map key (the fingerprint)
    /// is the identity.</summary>
    [YamlMember(Alias = "gameRoot")]
    public string GameRoot { get; init; } = string.Empty;

    [YamlMember(Alias = "ownedExpansions")]
    public OwnedExpansions OwnedExpansions { get; init; } = new();

    /// <summary>
    /// True once the interactive onboarding nudge ("do you own Meadowsong?") has
    /// been offered for this install — so a user who picked "ask me later" (which
    /// leaves ownership <c>unknown</c>) is not re-prompted on every later deploy.
    /// Nullable so the serializer omits it (absent ⇒ never offered); only ever
    /// written as <c>true</c>.
    /// </summary>
    [YamlMember(Alias = "nudgeOffered")]
    public bool? NudgeOffered { get; init; }
}

/// <summary>
/// Resolved state of one expansion for a given install, produced by
/// <see cref="ExpansionResolver"/>. <see cref="Effective"/> describes the
/// <em>solo runtime effect</em> (<c>Present ∧ Owned</c>) for messaging — it is
/// <strong>not</strong> deploy-ability: deployment keys off
/// <see cref="Present"/> alone (EE ships every pak, so a non-owner can still
/// write a present pak's bytes — needed for co-op parity with an owning host).
/// </summary>
public sealed record ExpansionState(
    string Package,
    bool Present,
    OwnershipState Ownership,
    bool Effective);
