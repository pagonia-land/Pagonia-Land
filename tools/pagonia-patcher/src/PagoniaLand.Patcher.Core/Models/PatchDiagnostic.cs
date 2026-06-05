namespace PagoniaLand.Patcher;

public enum PatchDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record PatchDiagnostic(
    PatchDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path = null);
