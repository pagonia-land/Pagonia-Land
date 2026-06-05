namespace PagoniaLand.Paker;

/// <summary>
/// JSON-friendly projection of <see cref="PakDiagnostic"/> for the `--json`
/// report shapes. The field names mirror the patcher's diagnostic shape so a
/// mod manager that reads patcher reports can drop the same code in for paker
/// reports.
/// </summary>
public sealed record PakReportDiagnostic(
    string Severity,
    string Code,
    string Message,
    string? Path)
{
    public static PakReportDiagnostic From(PakDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new PakReportDiagnostic(
            diagnostic.Severity.ToString(),
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Path);
    }

    public static IReadOnlyList<PakReportDiagnostic> FromAll(IEnumerable<PakDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return diagnostics.Select(From).ToList();
    }
}

/// <summary>
/// Echo of the filter that was applied to a command, so the report self-describes
/// which entries were considered without having to re-parse the CLI args.
/// </summary>
public sealed record PakReportFilter(
    bool CompressedOnly,
    bool UncompressedOnly,
    int? Start,
    int? End,
    string? FilenameContains)
{
    public static PakReportFilter From(PakFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return new PakReportFilter(
            filter.CompressedOnly,
            filter.UncompressedOnly,
            filter.Start,
            filter.End,
            filter.FilenameContains);
    }
}
