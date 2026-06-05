using PagoniaLand.Paker;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public enum ManagerDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record ManagerDiagnostic(
    ManagerDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path = null)
{
    public static ManagerDiagnostic From(PatchDiagnostic source) =>
        new(MapSeverity(source.Severity), source.Code, source.Message, source.Path);

    public static ManagerDiagnostic From(PakDiagnostic source) =>
        new(MapSeverity(source.Severity), source.Code, source.Message);

    private static ManagerDiagnosticSeverity MapSeverity(PatchDiagnosticSeverity severity) => severity switch
    {
        PatchDiagnosticSeverity.Info => ManagerDiagnosticSeverity.Info,
        PatchDiagnosticSeverity.Warning => ManagerDiagnosticSeverity.Warning,
        PatchDiagnosticSeverity.Error => ManagerDiagnosticSeverity.Error,
        _ => ManagerDiagnosticSeverity.Error,
    };

    private static ManagerDiagnosticSeverity MapSeverity(PakDiagnosticSeverity severity) => severity switch
    {
        PakDiagnosticSeverity.Info => ManagerDiagnosticSeverity.Info,
        PakDiagnosticSeverity.Warning => ManagerDiagnosticSeverity.Warning,
        PakDiagnosticSeverity.Error => ManagerDiagnosticSeverity.Error,
        _ => ManagerDiagnosticSeverity.Error,
    };
}
