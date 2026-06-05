namespace PagoniaLand.Paker;

/// <summary>
/// One entry in the pak index. Layout on disk:
///   byte    Compressed              (0 or 1)
///   uint32  FilenameLength          (BIG-endian; the only big-endian field)
///   byte    0x01                    (only when FilenameLength &gt;= 128)
///   bytes   Filename                (UTF-8, no NUL terminator)
///   int64   BeginOffset             (little-endian; absolute offset in the file)
///   int64   Size                    (little-endian; UNCOMPRESSED payload size)
///
/// <see cref="SizeInPak"/> is NOT part of the on-disk record — for uncompressed entries
/// it equals <see cref="Size"/>; for compressed entries it's the gzip-stream byte count,
/// which the reader derives from the next entry's <see cref="BeginOffset"/> (or from
/// the index start, for the last entry).
/// </summary>
public sealed record PakEntry(
    bool Compressed,
    string Filename,
    long BeginOffset,
    long Size)
{
    /// <summary>
    /// On-disk byte count for this entry's blob in the .pak file. Derived, not stored.
    /// </summary>
    public long SizeInPak { get; init; }
}
