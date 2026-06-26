using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>loca info --json &lt;path&gt;</c>. Mirrors the
/// shape of the other paker reports so a mod manager can consume them with the
/// same schema rhythm. <see cref="Strings"/> is the flat, positionally ordered
/// list of localized strings decoded from a <c>loca_&lt;lang&gt;.bin</c> blob.
/// </summary>
public sealed record LocaInfoReport(
    string Loca,
    bool Success,
    int StringCount,
    IReadOnlyList<string> Strings,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(LocaInfoReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, LocaInfoJsonContext.Default.LocaInfoReport);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LocaInfoReport))]
internal sealed partial class LocaInfoJsonContext : JsonSerializerContext;
