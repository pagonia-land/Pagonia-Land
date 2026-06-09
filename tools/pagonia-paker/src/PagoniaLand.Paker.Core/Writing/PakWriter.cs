using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace PagoniaLand.Paker;

/// <summary>
/// Writes the index of a `.pak` archive plus its 12-byte footer. The caller
/// owns the data blobs that come before and is responsible for feeding their
/// bytes into the running <see cref="Crc32"/> instance that this writer then
/// extends over the index bytes — the footer CRC covers everything from file
/// offset 0 up to (but not including) the footer itself.
/// </summary>
public sealed class PakWriter
{
    /// <summary>
    /// Write the index and footer for a pak whose data blobs have already been
    /// written to <paramref name="output"/>. <paramref name="rollingCrc"/> is the
    /// running CRC the caller built up over the data blobs; pass <c>null</c> for
    /// an empty archive (no data blobs). This method appends the index bytes to
    /// the same CRC, then writes the 12-byte footer.
    /// </summary>
    public IReadOnlyList<PakDiagnostic> WriteIndex(Stream output, IReadOnlyList<PakEntry> entries, uint version, Crc32? rollingCrc = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(entries);

        var diagnostics = new List<PakDiagnostic>();
        var indexBegin = output.Position;

        // Serialise the index to a memory buffer first so we can extend the rolling CRC
        // over the exact bytes that will end up on disk. The index is metadata-sized in
        // practice (a few KB at most), not a multi-MB blob, so the buffer is cheap.
        using var indexBuffer = new MemoryStream();

        Span<byte> headerBuf = stackalloc byte[PakFormatConstants.IndexHeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(headerBuf[..4], version);
        // entries is an in-memory List, so Count is bounded well below uint.MaxValue
        // (a List holds < 2^31 items); the (uint) cast is safe for any real archive.
        BinaryPrimitives.WriteUInt32LittleEndian(headerBuf[4..8], (uint)entries.Count);
        indexBuffer.Write(headerBuf);

        foreach (var entry in entries)
        {
            WriteEntry(indexBuffer, entry);
        }

        var indexBytes = indexBuffer.GetBuffer().AsSpan(0, (int)indexBuffer.Length);

        var crc = rollingCrc ?? new Crc32();
        crc.Append(indexBytes);
        Span<byte> crcBytes = stackalloc byte[4];
        crc.GetCurrentHash(crcBytes);
        var crcValue = BinaryPrimitives.ReadUInt32LittleEndian(crcBytes);

        output.Write(indexBytes);

        Span<byte> footerBuf = stackalloc byte[PakFormatConstants.FooterSize];
        BinaryPrimitives.WriteUInt32LittleEndian(footerBuf[..4], crcValue);
        BinaryPrimitives.WriteInt64LittleEndian(footerBuf[4..12], indexBegin);
        output.Write(footerBuf);

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakIndexWrite,
            $"Wrote pak index version {version} with {entries.Count} entries (CRC32 0x{crcValue:X8})."));

        return diagnostics;
    }

    private static void WriteEntry(Stream stream, PakEntry entry)
    {
        stream.WriteByte(entry.Compressed ? (byte)1 : (byte)0);

        var filenameBytes = Encoding.UTF8.GetBytes(entry.Filename);
        FilenameLengthEncoding.Write(stream, (uint)filenameBytes.Length);

        if (filenameBytes.Length >= PakFormatConstants.LongFilenameMarkerThreshold)
        {
            stream.WriteByte(PakFormatConstants.LongFilenameMarker);
        }

        stream.Write(filenameBytes);

        Span<byte> longBuf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(longBuf, entry.BeginOffset);
        stream.Write(longBuf);
        BinaryPrimitives.WriteInt64LittleEndian(longBuf, entry.Size);
        stream.Write(longBuf);
    }
}
