using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// One row of <c>pakinfo.json</c>. Field names use snake_case to match the
/// sidecar shape plpaker produces, so consumers and tutorials carry over.
/// </summary>
public sealed record PakInfoEntry(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("pos")] int Pos,
    [property: JsonPropertyName("compressed")] bool Compressed,
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("begin")] long Begin,
    [property: JsonPropertyName("end")] long End,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("size_compressed")] long SizeCompressed);
