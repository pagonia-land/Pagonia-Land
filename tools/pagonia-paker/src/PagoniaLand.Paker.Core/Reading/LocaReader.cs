using System.Text;

namespace PagoniaLand.Paker;

/// <summary>
/// Reads a compiled <c>loca_&lt;lang&gt;.bin</c> file into the flat list of
/// localized strings it carries. The file format is described on
/// <see cref="LocaFormatConstants"/>.
///
/// Reads are streaming with no bulk-buffering: the format has no count field,
/// so the reader consumes 7-bit-length-prefixed UTF-8 strings until
/// end-of-stream. A 0-byte file is a valid empty loca (no strings).
/// </summary>
public sealed class LocaReader
{
    // Strict UTF-8: the shared Encoding.UTF8 replaces invalid bytes with U+FFFD,
    // which would silently mangle a corrupt payload. This instance throws so the
    // DecoderFallbackException catch below reports the bad string instead.
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public LocaReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var diagnostics = new List<PakDiagnostic>();
        var strings = new List<string>();

        while (true)
        {
            // Peek the first prefix byte: a clean end-of-stream here ends the file.
            var first = stream.ReadByte();
            if (first < 0) break;

            if (!TryReadLength(stream, (byte)first, strings.Count, out var byteCount, out var lengthError))
            {
                diagnostics.Add(lengthError!);
                return new LocaReadResult(null, diagnostics);
            }

            // A corrupt prefix (e.g. `FF FF FF FF 07`) decodes to ~2 GB; never allocate that on a
            // declared length the stream can't possibly hold. On a seekable stream the remaining
            // bytes are the exact bound — reject before allocating rather than after a failed read.
            if (stream.CanSeek && byteCount > stream.Length - stream.Position)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.LocaEntryTruncated,
                    $"String #{strings.Count} declares {byteCount} UTF-8 bytes but only {stream.Length - stream.Position} remain in the stream — file is truncated or not a loca blob."));
                return new LocaReadResult(null, diagnostics);
            }

            var payload = new byte[byteCount];
            var read = ReadAtMost(stream, payload);
            if (read < byteCount)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.LocaEntryTruncated,
                    $"String #{strings.Count} declares {byteCount} UTF-8 bytes but only {read} could be read — file is truncated."));
                return new LocaReadResult(null, diagnostics);
            }

            string value;
            try
            {
                value = StrictUtf8.GetString(payload);
            }
            catch (DecoderFallbackException exception)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.LocaStringDecodingFailed,
                    $"String #{strings.Count} bytes are not valid UTF-8: {exception.Message}"));
                return new LocaReadResult(null, diagnostics);
            }

            strings.Add(value);
        }

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.LocaRead,
            $"Read loca blob with {strings.Count} strings."));

        return new LocaReadResult(strings, diagnostics);
    }

    /// <summary>
    /// Decode a .NET 7-bit-encoded-int length prefix whose first byte has
    /// already been consumed into <paramref name="firstByte"/>. Mirrors
    /// <c>BinaryReader.Read7BitEncodedInt</c>: little-endian groups of seven
    /// bits, high bit set means "another byte follows", capped at five bytes.
    /// </summary>
    private static bool TryReadLength(Stream stream, byte firstByte, int stringIndex, out int length, out PakDiagnostic? error)
    {
        length = 0;
        error = null;

        var current = firstByte;
        var shift = 0;
        var consumed = 1;
        while (true)
        {
            length |= (current & 0x7F) << shift;
            if ((current & 0x80) == 0) break;

            if (consumed >= LocaFormatConstants.MaxLengthPrefixBytes)
            {
                error = Error(
                    DiagnosticCodes.LocaEntryTruncated,
                    $"String #{stringIndex} length prefix exceeds {LocaFormatConstants.MaxLengthPrefixBytes} bytes — not a valid loca blob.");
                return false;
            }

            var next = stream.ReadByte();
            if (next < 0)
            {
                error = Error(
                    DiagnosticCodes.LocaEntryTruncated,
                    $"String #{stringIndex} length prefix is truncated at end-of-stream.");
                return false;
            }

            current = (byte)next;
            shift += 7;
            consumed++;
        }

        if (length < 0)
        {
            error = Error(
                DiagnosticCodes.LocaEntryTruncated,
                $"String #{stringIndex} declares a negative length — not a valid loca blob.");
            return false;
        }

        return true;
    }

    private static int ReadAtMost(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static PakDiagnostic Error(string code, string message)
        => new(PakDiagnosticSeverity.Error, code, message);
}
