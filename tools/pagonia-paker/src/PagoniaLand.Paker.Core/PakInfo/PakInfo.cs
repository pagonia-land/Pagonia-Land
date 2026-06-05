using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// The sidecar <c>pakinfo.json</c> document produced by <c>list</c> and consumed
/// by <c>pack</c>. The shape mirrors plpaker so the two tools stay
/// interchangeable on the JSON wire.
/// </summary>
public sealed record PakInfo(
    [property: JsonPropertyName("version")] uint Version,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("entries")] IReadOnlyList<PakInfoEntry> Entries);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakInfo))]
internal sealed partial class PakInfoJsonContext : JsonSerializerContext;
