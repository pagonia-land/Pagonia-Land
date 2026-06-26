using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// JSON report emitted by <c>gdbin info --json &lt;path&gt;</c>. Mirrors the
/// shape of the other paker reports so a mod manager can consume them with the
/// same schema rhythm.
/// </summary>
public sealed record GdBinInfoReport(
    string Gdbin,
    bool Success,
    int EntryCount,
    IReadOnlyList<string> HeaderBytesHex,
    IReadOnlyList<string> Entries,
    bool HasTrailingTerminator,
    IReadOnlyList<PakReportDiagnostic> Diagnostics)
{
    public static string Serialize(GdBinInfoReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, GdBinInfoJsonContext.Default.GdBinInfoReport);
    }

    public static IReadOnlyList<string> HeaderBytesToHex(IReadOnlyList<byte> headerBytes)
    {
        var hex = new string[headerBytes.Count];
        for (var i = 0; i < headerBytes.Count; i++) hex[i] = $"0x{headerBytes[i]:X2}";
        return hex;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GdBinInfoReport))]
internal sealed partial class GdBinInfoJsonContext : JsonSerializerContext;
