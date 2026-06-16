using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;
using System.Text.Json;

namespace PagoniaLand.Paker;

/// <summary>
/// Reads the index of a `.pak` archive. The index contains every entry's
/// metadata; the actual data blobs are not touched here.
/// </summary>
public sealed class PakReader
{
    // The shared Encoding.UTF8 instance uses the replacement fallback (invalid bytes
    // silently become U+FFFD), which would make the DecoderFallbackException catch below
    // dead code. This strict instance throws on invalid bytes so a corrupt filename is
    // reported instead of mangled.
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public PakReadResult OpenIndex(Stream stream)
    {
        var diagnostics = new List<PakDiagnostic>();

        if (stream.Length < PakFormatConstants.FooterSize)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakFooterTruncated,
                $"Stream is {stream.Length} bytes; the pak footer alone needs {PakFormatConstants.FooterSize}."));
            return new PakReadResult(null, diagnostics);
        }

        var footerOffset = stream.Length - PakFormatConstants.FooterSize;
        stream.Seek(footerOffset, SeekOrigin.Begin);

        Span<byte> footerBytes = stackalloc byte[PakFormatConstants.FooterSize];
        stream.ReadExactly(footerBytes);
        var storedCrc = BinaryPrimitives.ReadUInt32LittleEndian(footerBytes[..4]);
        var indexBegin = BinaryPrimitives.ReadInt64LittleEndian(footerBytes[4..12]);

        if (indexBegin < 0 || indexBegin > footerOffset)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakIndexOffsetInvalid,
                $"Footer claims index begins at offset {indexBegin}, but the stream's footer starts at {footerOffset}."));
            return new PakReadResult(null, diagnostics);
        }

        // footerOffset and indexBegin are both int64; for a >2.1 GB index region the difference
        // would overflow a direct (int) cast and wrap negative. Validate against int.MaxValue first.
        var indexLengthLong = footerOffset - indexBegin;
        if (indexLengthLong > int.MaxValue)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakIndexOffsetInvalid,
                $"Index region is {indexLengthLong} bytes, larger than the {int.MaxValue}-byte maximum this reader supports."));
            return new PakReadResult(null, diagnostics);
        }
        var indexLength = (int)indexLengthLong;
        if (indexLength < PakFormatConstants.IndexHeaderSize)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakIndexTruncated,
                $"Index region is {indexLength} bytes; needs at least {PakFormatConstants.IndexHeaderSize} for the header."));
            return new PakReadResult(null, diagnostics);
        }

        // The CRC in the footer is rolled over all file bytes from offset 0 up to (but not
        // including) the footer itself, i.e. the data blobs followed by the entire index.
        // Stream the bytes through Crc32 instead of buffering the whole archive in memory.
        stream.Seek(0, SeekOrigin.Begin);
        var crc = new Crc32();
        var hashBuffer = new byte[81920];
        var remaining = footerOffset;
        while (remaining > 0)
        {
            var toRead = (int)Math.Min(hashBuffer.Length, remaining);
            var read = stream.Read(hashBuffer, 0, toRead);
            if (read == 0)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.PakIndexTruncated,
                    $"Unexpected end of stream while hashing bytes for CRC verification ({remaining} bytes still expected)."));
                return new PakReadResult(null, diagnostics);
            }
            crc.Append(hashBuffer.AsSpan(0, read));
            remaining -= read;
        }
        Span<byte> crcBytes = stackalloc byte[4];
        crc.GetCurrentHash(crcBytes);
        var computedCrc = BinaryPrimitives.ReadUInt32LittleEndian(crcBytes);
        if (computedCrc != storedCrc)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakIndexCrcMismatch,
                $"CRC32 over data + index 0x{computedCrc:X8} does not match footer CRC32 0x{storedCrc:X8}."));
            return new PakReadResult(null, diagnostics);
        }

        var indexBuffer = new byte[indexLength];
        stream.Seek(indexBegin, SeekOrigin.Begin);
        stream.ReadExactly(indexBuffer);

        var version = BinaryPrimitives.ReadUInt32LittleEndian(indexBuffer.AsSpan(0, 4));
        var count = BinaryPrimitives.ReadUInt32LittleEndian(indexBuffer.AsSpan(4, 8 - 4));

        // A corrupt count (e.g. 0xFFFFFFFF) must not drive a multi-GB List allocation or a
        // ~2-billion-iteration read loop before a single entry is read. The smallest possible
        // entry on disk is 18 bytes (1 compressed flag + 1-byte filename length + empty name +
        // 8-byte begin + 8-byte size), so a count whose entries can't fit the index data area is
        // corrupt — guard against count * minEntryBytes, not the looser count >= 1 byte bound.
        const int minEntryBytes = 18;
        var indexDataLength = indexLength - PakFormatConstants.IndexHeaderSize;
        if ((long)count * minEntryBytes > indexDataLength)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.PakIndexTruncated,
                $"Index header claims {count} entries (≥ {(long)count * minEntryBytes} bytes) but only {indexDataLength} byte(s) of index data follow it."));
            return new PakReadResult(null, diagnostics);
        }

        using var indexStream = new MemoryStream(indexBuffer, PakFormatConstants.IndexHeaderSize, indexDataLength, writable: false);
        // Cap the initial capacity; the list still grows to `count` as entries are read,
        // but a bogus count can't pre-allocate a giant backing array.
        var entries = new List<PakEntry>((int)Math.Min(count, 4096u));

        for (var i = 0; i < count; i++)
        {
            var entry = TryReadEntry(indexStream, diagnostics);
            if (entry is null)
            {
                return new PakReadResult(null, diagnostics);
            }
            entries.Add(entry);
        }

        // The on-disk Size field stores the uncompressed payload size, so for compressed
        // entries we can't trust it for stream extraction. Derive SizeInPak from each
        // entry's BeginOffset to the next blob's start (or to the index begin for the last).
        FillSizeInPak(entries, indexBegin);

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakIndexRead,
            $"Read pak index version {version} with {entries.Count} entries."));

        return new PakReadResult(new PakIndex(version, entries), diagnostics);
    }

    private static void FillSizeInPak(List<PakEntry> entries, long indexBegin)
    {
        if (entries.Count == 0) return;

        // Sort positions by BeginOffset so we can resolve "next blob start" without
        // assuming the on-disk index is in monotonic begin-order.
        var order = new int[entries.Count];
        for (var i = 0; i < entries.Count; i++) order[i] = i;
        Array.Sort(order, (a, b) => entries[a].BeginOffset.CompareTo(entries[b].BeginOffset));

        for (var k = 0; k < order.Length; k++)
        {
            var i = order[k];
            var nextStart = k + 1 < order.Length ? entries[order[k + 1]].BeginOffset : indexBegin;
            entries[i] = entries[i] with { SizeInPak = nextStart - entries[i].BeginOffset };
        }
    }

    private static PakEntry? TryReadEntry(Stream indexStream, List<PakDiagnostic> diagnostics)
    {
        var compressedByte = indexStream.ReadByte();
        if (compressedByte < 0)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, "Index ended before the compressed flag of the next entry."));
            return null;
        }
        var compressed = compressedByte != 0;

        uint filenameLength;
        try
        {
            filenameLength = FilenameLengthEncoding.Read(indexStream);
        }
        catch (EndOfStreamException)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, "Index ended before the filename length field."));
            return null;
        }

        if (filenameLength >= PakFormatConstants.LongFilenameMarkerThreshold)
        {
            var marker = indexStream.ReadByte();
            if (marker < 0)
            {
                diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, $"Index ended before the long-filename marker for a {filenameLength}-byte filename."));
                return null;
            }
            if (marker != PakFormatConstants.LongFilenameMarker)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.PakEntryLongFilenameMarkerMissing,
                    $"Expected long-filename marker 0x01 before a {filenameLength}-byte filename, found 0x{marker:X2}."));
                return null;
            }
        }

        var filenameBytes = new byte[filenameLength];
        try
        {
            indexStream.ReadExactly(filenameBytes);
        }
        catch (EndOfStreamException)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, $"Index ended before all {filenameLength} filename bytes were read."));
            return null;
        }

        string filename;
        try
        {
            filename = StrictUtf8.GetString(filenameBytes);
        }
        catch (DecoderFallbackException ex)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryFilenameInvalidUtf8, $"Filename bytes are not valid UTF-8: {ex.Message}"));
            return null;
        }

        Span<byte> longBuf = stackalloc byte[8];
        try
        {
            indexStream.ReadExactly(longBuf);
        }
        catch (EndOfStreamException)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, $"Index ended before the beginOffset of entry '{filename}'."));
            return null;
        }
        var beginOffset = BinaryPrimitives.ReadInt64LittleEndian(longBuf);

        try
        {
            indexStream.ReadExactly(longBuf);
        }
        catch (EndOfStreamException)
        {
            diagnostics.Add(Error(DiagnosticCodes.PakEntryTruncated, $"Index ended before the size of entry '{filename}'."));
            return null;
        }
        var size = BinaryPrimitives.ReadInt64LittleEndian(longBuf);

        return new PakEntry(compressed, filename, beginOffset, size);
    }

    /// <summary>
    /// Stream one entry's payload from the open pak stream to <paramref name="output"/>.
    /// Compressed entries are decompressed on the fly with <see cref="GZipStream"/>;
    /// uncompressed entries are copied byte for byte. The pak stream is seeked to the
    /// entry's begin offset first.
    /// </summary>
    public void ExtractEntry(Stream pakStream, PakEntry entry, Stream output)
    {
        ArgumentNullException.ThrowIfNull(pakStream);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(output);

        // A corrupt index can derive a negative SizeInPak (non-monotonic offsets) or carry
        // a negative Size. Without this, CopyExactly would silently write an empty/truncated
        // file. Surface it as a caught InvalidDataException instead.
        if (entry.BeginOffset < 0 || entry.SizeInPak < 0 || entry.Size < 0)
        {
            throw new InvalidDataException(
                $"Entry '{entry.Filename}' has an invalid data range (begin={entry.BeginOffset}, " +
                $"sizeInPak={entry.SizeInPak}, size={entry.Size}); the pak index is corrupt.");
        }

        pakStream.Seek(entry.BeginOffset, SeekOrigin.Begin);

        if (entry.Compressed)
        {
            // SizeInPak bounds the gzip stream to this entry's bytes only; without it,
            // GZipStream would happily continue into the next entry's gzip member.
            using var limited = new LimitedStream(pakStream, entry.SizeInPak);
            using var gzip = new GZipStream(limited, CompressionMode.Decompress, leaveOpen: true);
            // Bound the decompressed output to the index-declared uncompressed Size.
            // SizeInPak only caps the *compressed* input; without an output cap a
            // few MB of compressed zeros could inflate to gigabytes (decompression
            // bomb). The declared Size is the contract — anything more (or less) is corrupt.
            CopyDecompressedBounded(gzip, output, entry.Size, entry.Filename);
        }
        else
        {
            CopyExactly(pakStream, output, entry.Size);
        }
    }

    /// <summary>
    /// Reports whether a gd.bin entry lists at least one <c>*.gd.xml</c> resource path — i.e. whether
    /// the module contributes GameDatabase content. Only the 7-byte header is pulled (cheap even for a
    /// multi-MB gd.bin, since a compressed entry is decompressed lazily): it is validated for the
    /// gd.bin magic <c>[0x03][versionMinor][0x02]</c> followed by three zero bytes, and content is
    /// then decided by whether any entry record follows the header. An empty module-level gd.bin —
    /// the editor emits one even for a map-only mod — is exactly the 7-byte header and reports
    /// <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Byte 3 of the header is <c>entries.Count - 1</c> (see <see cref="GdBinFormatConstants"/>), NOT a
    /// usable count: it reads as 0 for both an empty index AND a single-entry one, and wraps modulo 256.
    /// So the presence of an entry record — not byte 3 — is the source of truth for "has content".
    /// </remarks>
    public bool GdBinHasEntries(Stream pakStream, PakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(pakStream);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.BeginOffset < 0 || entry.SizeInPak < 0 || entry.Size < 7) return false;

        Span<byte> head = stackalloc byte[7];
        try
        {
            pakStream.Seek(entry.BeginOffset, SeekOrigin.Begin);
            if (entry.Compressed)
            {
                using var limited = new LimitedStream(pakStream, entry.SizeInPak);
                using var gzip = new GZipStream(limited, CompressionMode.Decompress, leaveOpen: true);
                if (!TryReadExactly(gzip, head)) return false;
            }
            else
            {
                if (!TryReadExactly(pakStream, head)) return false;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return false;
        }

        // gd.bin header: [0x03][versionMinor][0x02][entries.Count-1][0x00][0x00][0x00].
        if (head[0] != 0x03 || head[2] != 0x02 || head[4] != 0x00 || head[5] != 0x00 || head[6] != 0x00)
            return false;

        // Any bytes beyond the 7-byte header are entry records (a uint32 length + a UTF-16 path that
        // the reader walks until EOF). A decompressed size past the header therefore means the index
        // lists at least one resource — the reliable signal byte 3 cannot give for a single entry.
        return entry.Size > 7;
    }

    private static bool TryReadExactly(Stream source, Span<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = source.Read(buffer[offset..]);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    /// <summary>
    /// Copy exactly <paramref name="expectedSize"/> decompressed bytes from
    /// <paramref name="source"/> (a <see cref="GZipStream"/>) to
    /// <paramref name="destination"/>. Throws <see cref="InvalidDataException"/> if the
    /// stream yields fewer bytes (truncated/corrupt) or even one more (the entry lies
    /// about its uncompressed size — a decompression bomb or corrupt index).
    /// </summary>
    private static void CopyDecompressedBounded(Stream source, Stream destination, long expectedSize, string filename)
    {
        var buffer = new byte[81920];
        var remaining = expectedSize;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = source.Read(buffer, 0, toRead);
            if (read == 0)
            {
                throw new InvalidDataException(
                    $"Entry '{filename}' decompressed to fewer bytes than its declared size ({expectedSize}); the pak index or entry data is corrupt.");
            }
            destination.Write(buffer, 0, read);
            remaining -= read;
        }

        if (source.ReadByte() != -1)
        {
            throw new InvalidDataException(
                $"Entry '{filename}' decompressed past its declared size ({expectedSize} bytes); refusing to continue (possible decompression bomb or corrupt index).");
        }
    }

    /// <summary>
    /// Build a <see cref="PakInfo"/> sidecar description from the given index. The
    /// returned object can be serialised to <c>pakinfo.json</c> with
    /// <see cref="PakInfoJsonContext"/>.
    /// </summary>
    public static PakInfo BuildPakInfo(PakIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var entries = new List<PakInfoEntry>(index.Entries.Count);

        for (var i = 0; i < index.Entries.Count; i++)
        {
            var entry = index.Entries[i];
            entries.Add(new PakInfoEntry(
                Index: i,
                Pos: i,
                Compressed: entry.Compressed,
                Filename: entry.Filename,
                Begin: entry.BeginOffset,
                End: entry.BeginOffset + entry.SizeInPak,
                Size: entry.Size,
                SizeCompressed: entry.SizeInPak));
        }

        return new PakInfo(index.Version, entries.Count, entries);
    }

    /// <summary>
    /// Serialise a <see cref="PakInfo"/> to the byte-compatible <c>pakinfo.json</c>
    /// shape (snake_case fields, indented). The serializer uses the source-generated
    /// <see cref="PakInfoJsonContext"/> so it stays AOT-clean.
    /// </summary>
    public static string SerializePakInfo(PakInfo pakInfo)
    {
        ArgumentNullException.ThrowIfNull(pakInfo);
        return JsonSerializer.Serialize(pakInfo, PakInfoJsonContext.Default.PakInfo);
    }

    private static void CopyExactly(Stream source, Stream destination, long byteCount)
    {
        var buffer = new byte[81920];
        var remaining = byteCount;

        while (remaining > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = source.Read(buffer, 0, toRead);
            if (read == 0)
            {
                throw new EndOfStreamException(
                    $"Unexpected end of pak stream while reading entry data; {remaining} bytes still expected.");
            }
            destination.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static PakDiagnostic Error(string code, string message)
        => new(PakDiagnosticSeverity.Error, code, message);
}
