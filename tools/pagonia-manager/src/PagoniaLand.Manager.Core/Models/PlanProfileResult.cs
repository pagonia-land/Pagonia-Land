using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public sealed class PlanProfileResult
{
    public bool Success { get; init; }
    public string? ProfileName { get; init; }
    public string GameRoot { get; init; } = string.Empty;
    public CombinedPatchPlan? PatcherPlan { get; init; }
    public IReadOnlyList<ManagerDiagnostic> ManagerDiagnostics { get; init; } = [];
}
