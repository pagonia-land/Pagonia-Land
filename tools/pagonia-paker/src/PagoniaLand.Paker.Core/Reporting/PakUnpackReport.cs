using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>unpack --json &lt;path&gt;</c>. Captures the input pak,
/// the output directory, the filter that was applied, and one row per entry
/// describing whether it was extracted, skipped by the filter, or failed.
/// </summary>
public sealed record PakUnpackReport(
    string Pak,
    bool Success,
    string OutputDir,
    int EntryCount,
    int ExtractedCount,
    int SkippedCount,
    int FailedCount,
    PakReportFilter Filter,
    IReadOnlyList<PakUnpackEntryReport> Entries,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(PakUnpackReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, PakUnpackJsonContext.Default.PakUnpackReport);
    }
}

/// <summary>
/// Per-entry outcome row in the unpack report.
/// <c>Status</c> is one of <c>extracted</c>, <c>skipped</c>, <c>failed</c>.
/// <c>OutputPath</c> is set only for <c>extracted</c> rows.
/// </summary>
public sealed record PakUnpackEntryReport(
    int Index,
    string Filename,
    bool Compressed,
    string Status,
    string? OutputPath);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakUnpackReport))]
internal sealed partial class PakUnpackJsonContext : JsonSerializerContext;
