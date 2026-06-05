namespace PagoniaLand.Patcher;

public sealed record CombinedPatchPlan(
    IReadOnlyList<PatchPlan> ModPlans,
    IReadOnlyList<PatchWriteConflict> Conflicts,
    IReadOnlyList<PatchDiagnostic> Diagnostics,
    IReadOnlyList<PatchEntryConflict> EntryConflicts)
{
    public CombinedPatchPlan(
        IReadOnlyList<PatchPlan> modPlans,
        IReadOnlyList<PatchWriteConflict> conflicts,
        IReadOnlyList<PatchDiagnostic> diagnostics)
        : this(modPlans, conflicts, diagnostics, Array.Empty<PatchEntryConflict>())
    {
    }

    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
        && ModPlans.All(plan => plan.Success)
        && Conflicts.Count == 0
        && EntryConflicts.Count == 0;

    public IReadOnlyList<PatchWrite> Writes => ModPlans.SelectMany(plan => plan.Writes).ToList();

    public IReadOnlyList<PatchEntryWrite> EntryWrites =>
        ModPlans.SelectMany(plan => plan.EntryWrites).ToList();
}

public sealed record PatchWriteConflict(
    string Type,
    string TargetKey,
    IReadOnlyList<PatchWrite> Writes);
