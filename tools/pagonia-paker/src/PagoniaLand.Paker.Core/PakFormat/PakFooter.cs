namespace PagoniaLand.Paker;

/// <summary>
/// The 12-byte trailer at the end of every `.pak` archive.
/// Layout (little-endian; verified against shipping Pioneers of Pagonia paks):
///   bytes 0..3   : uint32 Crc       (zlib CRC32 over bytes [0, length - 12) — i.e. data blobs + index)
///   bytes 4..11  : int64 IndexBegin (absolute file offset of the index)
/// </summary>
public sealed record PakFooter(long IndexBegin, uint Crc);
