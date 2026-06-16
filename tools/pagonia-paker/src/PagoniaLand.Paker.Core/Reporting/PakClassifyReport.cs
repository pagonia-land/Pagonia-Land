using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>classify --json &lt;path&gt;</c>. Mirrors
/// <see cref="PakClassifyResult"/> but flattens it into a stable shape that
/// the schema under <c>schemas/paker/pak-classify-report.schema.json</c>
/// pins. Field names match the existing paker report shapes (PascalCase).
/// </summary>
public sealed record PakClassifyReport(
    string Pak,
    bool Success,
    string? Name,
    string? ModuleFolder,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> GdbScopes,
    int PopmapCount,
    IReadOnlyList<string> OverridesAtRoot,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(PakClassifyReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, PakClassifyJsonContext.Default.PakClassifyReport);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakClassifyReport))]
internal sealed partial class PakClassifyJsonContext : JsonSerializerContext;
