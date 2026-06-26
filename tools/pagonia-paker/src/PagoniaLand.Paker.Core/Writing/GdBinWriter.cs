using System.Buffers.Binary;
using System.Text;

namespace PagoniaLand.Paker;

/// <summary>
/// Writes a <see cref="GdBinIndex"/> to a stream in the
/// <c>&lt;modulename&gt;.gd.bin</c> format described on
/// <see cref="GdBinFormatConstants"/>.
///
/// Round-trips byte-identically against the four shipped indexes provided the
/// caller obtained the <see cref="GdBinIndex"/> via <see cref="GdBinReader.Read"/>
/// — the header is preserved verbatim and entries are written in list order.
/// </summary>
public sealed class GdBinWriter
{
    public void Write(Stream output, GdBinIndex index)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(index);

        if (index.HeaderBytes.Count != GdBinFormatConstants.HeaderSize)
        {
            throw new ArgumentException(
                $"GdBinIndex.HeaderBytes must be exactly {GdBinFormatConstants.HeaderSize} bytes; got {index.HeaderBytes.Count}.",
                nameof(index));
        }

        Span<byte> headerBuffer = stackalloc byte[GdBinFormatConstants.HeaderSize];
        for (var i = 0; i < headerBuffer.Length; i++) headerBuffer[i] = index.HeaderBytes[i];
        output.Write(headerBuffer);

        Span<byte> lengthBuffer = stackalloc byte[4];
        for (var i = 0; i < index.Entries.Count; i++)
        {
            var path = index.Entries[i];
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(
                    $"GdBinIndex.Entries[{i}] is null or empty; gd.bin entries must be non-empty.",
                    nameof(index));
            }

            var byteCount = Encoding.Unicode.GetByteCount(path);
            var charCount = byteCount / 2; // UTF-16 LE: 2 bytes per code unit
            BinaryPrimitives.WriteUInt32LittleEndian(lengthBuffer, (uint)charCount);
            output.Write(lengthBuffer);

            var pathBytes = Encoding.Unicode.GetBytes(path);
            output.Write(pathBytes);
        }

        // The 1.4.0 editor closes every index with a zero-length terminator record;
        // re-emit it so an editor-read index round-trips byte-identically. Shipped /
        // scaffolded indexes leave this false and end cleanly at the last entry.
        if (index.HasTrailingTerminator)
        {
            lengthBuffer.Clear();
            output.Write(lengthBuffer);
        }
    }
}
