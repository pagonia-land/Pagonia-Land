namespace PagoniaLand.Paker;

/// <summary>
/// The result of <see cref="LocaReader.Read"/>. <see cref="Strings"/> is
/// <c>null</c> when the read failed; the failing condition is captured in
/// <see cref="Diagnostics"/>.
/// </summary>
public sealed record LocaReadResult(
    IReadOnlyList<string>? Strings,
    IReadOnlyList<PakDiagnostic> Diagnostics)
{
    public bool Success => Strings is not null
        && Diagnostics.All(d => d.Severity != PakDiagnosticSeverity.Error);
}
