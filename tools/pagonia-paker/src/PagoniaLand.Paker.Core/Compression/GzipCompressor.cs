using System.IO.Compression;

namespace PagoniaLand.Paker;

/// <summary>
/// Stream-based gzip helpers used by the standalone <c>compress</c>/<c>decompress</c>
/// commands and by <see cref="PakPacker"/> when writing compressed entries. The
/// gzip wrapper (zlib DEFLATE, <c>windowBits = 31</c>) matches the format
/// plpaker uses for `.pak` entries.
/// </summary>
public static class GzipCompressor
{
    /// <summary>Default ceiling on decompressed output for the standalone <c>decompress</c>
    /// command — bounds a decompression bomb while staying well above any legitimate single
    /// game asset. Callers can raise it for a genuinely larger input.</summary>
    public const long DefaultMaxDecompressedBytes = 2L * 1024 * 1024 * 1024; // 2 GiB

    /// <summary>
    /// Stream <paramref name="input"/> through a gzip compressor into <paramref name="output"/>.
    /// Neither stream is disposed; the gzip wrapper is finalised before returning.
    /// </summary>
    public static void Compress(Stream input, Stream output, CompressionLevel level = CompressionLevel.Optimal)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var gzip = new GZipStream(output, level, leaveOpen: true);
        input.CopyTo(gzip);
    }

    /// <summary>
    /// Stream <paramref name="input"/> through a gzip decompressor into <paramref name="output"/>,
    /// refusing to write more than <paramref name="maxOutputBytes"/> decompressed bytes (a
    /// decompression-bomb guard — a few KB of compressed zeros can inflate to gigabytes).
    /// Neither stream is disposed. Throws <see cref="InvalidDataException"/> when the cap is exceeded.
    /// </summary>
    public static void Decompress(Stream input, Stream output, long maxOutputBytes = DefaultMaxDecompressedBytes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        var buffer = new byte[81920];
        long written = 0;
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            written += read;
            if (written > maxOutputBytes)
            {
                throw new InvalidDataException(
                    $"Decompressed output exceeded the {maxOutputBytes}-byte limit; refusing to continue (possible decompression bomb). Pass a higher limit if this input is legitimately larger.");
            }
            output.Write(buffer, 0, read);
        }
    }
}
