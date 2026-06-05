namespace PagoniaLand.Paker;

/// <summary>
/// Fixed offsets and marker bytes from the Pioneers of Pagonia `.pak` layout
/// inferred from plpaker. Kept in one place so reader and writer never drift.
/// </summary>
public static class PakFormatConstants
{
    /// <summary>
    /// Footer is always 12 bytes at the very end of the archive:
    /// <c>uint32 Crc</c> followed by <c>int64 IndexBegin</c>, both little-endian.
    /// (plpaker's README hints at a 16-byte footer with padding, but the source
    /// code and shipped paks both use 12 bytes; the apparent leading zeros are
    /// the tail of the last index entry's <c>int64 size</c> field.)
    /// </summary>
    public const int FooterSize = 12;

    /// <summary>Index header is `uint32 version` + `uint32 count`.</summary>
    public const int IndexHeaderSize = 8;

    /// <summary>
    /// When a filename's UTF-8 byte length is greater than or equal to this
    /// threshold, plpaker writes a one-byte marker (<see cref="LongFilenameMarker"/>)
    /// between the length field and the filename bytes.
    /// </summary>
    public const int LongFilenameMarkerThreshold = 128;

    /// <summary>The single marker byte used for long filenames.</summary>
    public const byte LongFilenameMarker = 0x01;
}
