using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>list --json &lt;path&gt;</c>. Describes the archive's
/// header plus one row per entry, so downstream tools see exactly what
/// <c>pakinfo.json</c> sees without re-parsing the sidecar.
/// </summary>
public sealed record PakListReport(
    string Pak,
    bool Success,
    uint Version,
    int EntryCount,
    string? PakInfoPath,
    IReadOnlyList<PakListEntryReport> Entries,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(PakListReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, PakListJsonContext.Default.PakListReport);
    }
}

public sealed record PakListEntryReport(
    int Index,
    bool Compressed,
    string Filename,
    long BeginOffset,
    long Size,
    long SizeInPak);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakListReport))]
internal sealed partial class PakListJsonContext : JsonSerializerContext;
