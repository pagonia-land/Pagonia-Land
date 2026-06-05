namespace PagoniaLand.Patcher;

/// <summary>outcome of <see cref="PatchApplier.ApplySparse"/>.
/// <para><see cref="ChangedFiles"/> maps every patched file's forward-slash
/// relative game-root path (e.g. <c>"core/gdb/buildings.gd.xml"</c>) to its
/// fully-patched XML bytes. Un-patched files do NOT appear — the dict's keys
/// are exactly the set of modified files. <see cref="Diagnostics"/> mirrors
/// the standard <c>Apply</c> per-write diagnostic stream; <see cref="Success"/>
/// is the convenience "no error diagnostics" check.</para></summary>
public sealed record SparseApplyResult(
    IReadOnlyDictionary<string, byte[]> ChangedFiles,
    IReadOnlyList<PatchDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error);
}
