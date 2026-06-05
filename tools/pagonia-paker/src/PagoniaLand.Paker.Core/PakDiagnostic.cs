namespace PagoniaLand.Paker;

public enum PakDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// One observation produced while reading, writing, or patching a `.pak`
/// archive. Mirrors the patcher's <c>PatchDiagnostic</c> on purpose so a mod
/// manager can consume both with the same shape.
/// </summary>
public sealed record PakDiagnostic(
    PakDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path = null);
