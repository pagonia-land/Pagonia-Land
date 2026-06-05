using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>patch --json &lt;path&gt;</c>. Captures the input pak,
/// the output pak, one row per positional replacement (which can resolve to
/// either a Replace or an Add of an existing/new entry), and a flat list of
/// explicitly-deleted entry paths.
/// </summary>
public sealed record PakPatchReport(
    string Input,
    string Output,
    bool Success,
    int EntryCount,
    int ReplacedCount,
    int CopiedCount,
    int AddedCount,
    int DeletedCount,
    IReadOnlyList<PakPatchReplacementReport> Replacements,
    IReadOnlyList<string> Deletions,
    IReadOnlyList<PakPatchGdBinUpdateReport> GdbinUpdates,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(PakPatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, PakPatchJsonContext.Default.PakPatchReport);
    }
}

public sealed record PakPatchReplacementReport(
    string EntryName,
    string SourcePath);

/// <summary>
/// One row per module whose <c>&lt;m&gt;/&lt;m&gt;.gd.bin</c> index was
/// auto-updated to register newly-added <c>*.gd.xml</c> paths. Empty when no
/// add operation triggered the auto-register flow.
/// </summary>
public sealed record PakPatchGdBinUpdateReport(
    string EntryName,
    IReadOnlyList<string> Added);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakPatchReport))]
internal sealed partial class PakPatchJsonContext : JsonSerializerContext;
