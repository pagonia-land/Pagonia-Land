namespace PagoniaLand.Paker;

/// <summary>
/// The index sits between the data blobs and the footer. Header is
/// `uint32 Version` + `uint32 entry count`, both little-endian, followed by
/// `Entries.Count` <see cref="PakEntry"/> records.
/// </summary>
public sealed record PakIndex(uint Version, IReadOnlyList<PakEntry> Entries);
