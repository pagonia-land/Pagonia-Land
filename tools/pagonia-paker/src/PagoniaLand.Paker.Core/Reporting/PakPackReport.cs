using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>pack --json &lt;path&gt;</c>. Describes the pakinfo
/// source, the output pak, the filter that was applied, and how many entries
/// from pakinfo actually ended up in the output.
/// </summary>
public sealed record PakPackReport(
    string PakInfo,
    string Output,
    bool Success,
    int EntryCount,
    int PackedCount,
    PakReportFilter Filter,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(PakPackReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, PakPackJsonContext.Default.PakPackReport);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakPackReport))]
internal sealed partial class PakPackJsonContext : JsonSerializerContext;
