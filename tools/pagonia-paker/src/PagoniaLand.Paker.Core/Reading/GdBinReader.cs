using System.Buffers.Binary;
using System.Text;

namespace PagoniaLand.Paker;

/// <summary>
/// Reads a <c>&lt;modulename&gt;.gd.bin</c> file into a <see cref="GdBinIndex"/>.
/// The file format is described on <see cref="GdBinFormatConstants"/>.
///
/// Reads are streaming with no bulk-buffering: the format has no count field,
/// so the reader simply consumes entries until end-of-stream.
/// </summary>
public sealed class GdBinReader
{
    // Strict UTF-16 LE: the shared Encoding.Unicode replaces invalid bytes with U+FFFD,
    // which would make the DecoderFallbackException catch below unreachable. This instance
    // throws so a corrupt path is reported instead of silently mangled.
    private static readonly Encoding StrictUtf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    public GdBinReadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var diagnostics = new List<PakDiagnostic>();

        Span<byte> header = stackalloc byte[GdBinFormatConstants.HeaderSize];
        var headerRead = ReadAtMost(stream, header);
        if (headerRead < GdBinFormatConstants.HeaderSize)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.GdBinHeaderInvalid,
                $"Stream is {headerRead} bytes; the gd.bin header alone needs {GdBinFormatConstants.HeaderSize}."));
            return new GdBinReadResult(null, diagnostics);
        }

        if (header[0] != GdBinFormatConstants.HeaderByte0
            || header[2] != GdBinFormatConstants.HeaderByte2
            || header[4] != 0x00 || header[5] != 0x00 || header[6] != 0x00)
        {
            diagnostics.Add(Error(
                DiagnosticCodes.GdBinHeaderInvalid,
                $"Header bytes {FormatHeader(header)} do not match the expected gd.bin shape "
                + "(byte[0]=0x03, byte[2]=0x02, bytes[4..6]=0x00 0x00 0x00)."));
            return new GdBinReadResult(null, diagnostics);
        }

        var entries = new List<string>();
        Span<byte> lengthBuffer = stackalloc byte[4];

        while (true)
        {
            var lengthRead = ReadAtMost(stream, lengthBuffer);
            if (lengthRead == 0) break;
            if (lengthRead < 4)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.GdBinEntryTruncated,
                    $"After {entries.Count} entries, only {lengthRead} of 4 length bytes remained — file is truncated."));
                return new GdBinReadResult(null, diagnostics);
            }

            var charCount = BinaryPrimitives.ReadUInt32LittleEndian(lengthBuffer);
            if (charCount == 0)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.GdBinEntryTruncated,
                    $"Entry #{entries.Count} declares zero UTF-16 code units; gd.bin entries must be non-empty."));
                return new GdBinReadResult(null, diagnostics);
            }
            if (charCount > int.MaxValue / 2)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.GdBinEntryTruncated,
                    $"Entry #{entries.Count} declares {charCount} UTF-16 code units, which would overflow a managed buffer."));
                return new GdBinReadResult(null, diagnostics);
            }

            var byteCount = checked((int)charCount * 2);
            var pathBuffer = new byte[byteCount];
            var pathRead = ReadAtMost(stream, pathBuffer);
            if (pathRead < byteCount)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.GdBinEntryTruncated,
                    $"Entry #{entries.Count} declares {charCount} UTF-16 code units ({byteCount} bytes) but only {pathRead} could be read."));
                return new GdBinReadResult(null, diagnostics);
            }

            string path;
            try
            {
                path = StrictUtf16.GetString(pathBuffer);
            }
            catch (DecoderFallbackException exception)
            {
                diagnostics.Add(Error(
                    DiagnosticCodes.GdBinPathDecodingFailed,
                    $"Entry #{entries.Count} bytes are not valid UTF-16 LE: {exception.Message}"));
                return new GdBinReadResult(null, diagnostics);
            }

            entries.Add(path);
        }

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.GdBinRead,
            $"Read gd.bin index with {entries.Count} entries."));

        var index = new GdBinIndex(header.ToArray(), entries);
        return new GdBinReadResult(index, diagnostics);
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

    private static string FormatHeader(ReadOnlySpan<byte> header)
    {
        var parts = new string[header.Length];
        for (var i = 0; i < header.Length; i++) parts[i] = $"0x{header[i]:X2}";
        return "[" + string.Join(' ', parts) + "]";
    }

    private static PakDiagnostic Error(string code, string message)
        => new(PakDiagnosticSeverity.Error, code, message);
}
