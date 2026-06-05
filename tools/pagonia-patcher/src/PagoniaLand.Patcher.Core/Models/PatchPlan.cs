namespace PagoniaLand.Patcher;

public sealed record PatchPlan(
    LoadedMod Mod,
    IReadOnlyList<PatchWrite> Writes,
    IReadOnlyList<PatchDiagnostic> Diagnostics,
    IReadOnlyList<PatchEntryWrite> EntryWrites,
    IReadOnlyList<ResolvedTweak> ResolvedTweaks)
{
    public PatchPlan(LoadedMod mod, IReadOnlyList<PatchWrite> writes, IReadOnlyList<PatchDiagnostic> diagnostics, IReadOnlyList<PatchEntryWrite> entryWrites)
        : this(mod, writes, diagnostics, entryWrites, Array.Empty<ResolvedTweak>())
    {
    }

    public PatchPlan(LoadedMod mod, IReadOnlyList<PatchWrite> writes, IReadOnlyList<PatchDiagnostic> diagnostics)
        : this(mod, writes, diagnostics, Array.Empty<PatchEntryWrite>(), Array.Empty<ResolvedTweak>())
    {
    }

    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}

/// <summary>
/// The effective value a declared tweak resolved to for this plan, and where it came from.
/// <see cref="Origin"/> is one of the values in <c>TweakOrigins</c> —
/// <c>default</c>, <c>collection</c>, <c>external</c>, or <c>lockfile</c> — all of
/// which are produced today (the manager threads collection/lockfile/profile layers in).
/// <para><see cref="SourceValue"/> and <see cref="ResolvedValue"/> are equal at the
/// tweak level — a tweak resolves to a single value. The only source-vs-resolved
/// distinction is the per-operation boolean ternary (<c>{{ x ? 'a' : 'b' }}</c>),
/// which is resolved when each operation is planned and surfaced in that operation's
/// diagnostics, not here.</para>
/// </summary>
public sealed record ResolvedTweak(
    string TweakId,
    string SourceValue,
    string ResolvedValue,
    string Origin);

public sealed record PatchWrite(
    string OperationId,
    string OperationType,
    string File,
    string EntityGuid,
    string EntityName,
    string Component,
    string Path,
    string? Attribute,
    string OldValue,
    string NewValue);
