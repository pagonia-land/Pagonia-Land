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
    /// Stream <paramref name="input"/> through a gzip decompressor into <paramref name="output"/>.
    /// Neither stream is disposed.
    /// </summary>
    public static void Decompress(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        gzip.CopyTo(output);
    }
}
