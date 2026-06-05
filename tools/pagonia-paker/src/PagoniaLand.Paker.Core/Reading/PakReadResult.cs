namespace PagoniaLand.Paker;

/// <summary>
/// The result of <see cref="PakReader.OpenIndex"/>. <see cref="Index"/> is
/// <c>null</c> when the read failed; the failing condition is captured in
/// <see cref="Diagnostics"/>.
/// </summary>
public sealed record PakReadResult(
    PakIndex? Index,
    IReadOnlyList<PakDiagnostic> Diagnostics)
{
    public bool Success => Index is not null
        && Diagnostics.All(d => d.Severity != PakDiagnosticSeverity.Error);
}
