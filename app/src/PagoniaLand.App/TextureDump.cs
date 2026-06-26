using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PagoniaLand.Catalog.Assets;

namespace PagoniaLand.App;

/// <summary>
/// Texture → PNG export tool: walk a folder and write a lossless, full-resolution PNG next to
/// every supported texture file (the game's RTEX <c>.image</c> / <c>.texture</c>), with the same
/// name and a <c>.png</c> extension. For pulling icons and textures out of an extracted game-gdb
/// or pak folder.
///
/// Files are processed in parallel across all cores, and PNGs are encoded directly from the
/// decoded RGBA (a tiny dependency-free writer) rather than via Avalonia — so the whole dump
/// runs off-thread and scales with the CPU.
/// </summary>
public static class TextureDump
{
    private static readonly string[] SupportedExtensions = { ".image", ".texture" };
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Convert every supported texture under <paramref name="folder"/> (recursively) to a PNG
    /// next to it, in parallel. Returns how many were written vs skipped (not RTEX / undecodable
    /// / errored).
    /// </summary>
    public static (int Written, int Skipped) DumpFolder(string folder, Action<string>? log = null)
    {
        var files = Directory
            .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f => Array.IndexOf(SupportedExtensions, Path.GetExtension(f).ToLowerInvariant()) >= 0)
            .ToArray();

        var written = 0;
        var skipped = 0;

        Parallel.ForEach(files, file =>
        {
            try
            {
                var texture = RtexTexture.Parse(File.ReadAllBytes(file));
                var image = texture is null ? null : TextureDecoder.Decode(texture);
                if (image is null)
                {
                    Interlocked.Increment(ref skipped);
                    log?.Invoke($"skip (not decodable): {file}");
                    return;
                }

                WritePng(image, Path.ChangeExtension(file, ".png"));
                Interlocked.Increment(ref written);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref skipped);
                log?.Invoke($"skip ({ex.GetType().Name}): {file} — {ex.Message}");
            }
        });

        return (written, skipped);
    }

    /// <summary>
    /// Minimal, thread-safe, dependency-free 8-bit RGBA PNG writer (one IDAT, no row filtering).
    /// PNG is lossless at any compression level, so we use the fastest deflate — same pixels,
    /// less CPU. Full resolution, best available quality.
    /// </summary>
    private static void WritePng(RgbaImage image, string path)
    {
        using var output = File.Create(path);
        output.Write(PngSignature, 0, PngSignature.Length);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: truecolour with alpha (RGBA)
        WriteChunk(output, "IHDR", header);

        using var idat = new MemoryStream();
        using (var zlib = new ZLibStream(idat, CompressionLevel.Fastest, leaveOpen: true))
        {
            var stride = image.Width * 4;
            var filter = new byte[1]; // filter type 0 (none), one byte per scanline
            for (var y = 0; y < image.Height; y++)
            {
                zlib.Write(filter, 0, 1);
                zlib.Write(image.Rgba, y * stride, stride);
            }
        }

        WriteChunk(output, "IDAT", idat.ToArray());
        WriteChunk(output, "IEND", Array.Empty<byte>());
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes, 0, typeBytes.Length);
        stream.Write(data, 0, data.Length);

        var crc = new Crc32();
        crc.Append(typeBytes);
        crc.Append(data);
        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, crc.GetCurrentHashAsUInt32());
        stream.Write(checksum);
    }
}
