using System.Buffers.Binary;

namespace PagoniaLand.Catalog.Assets;

/// <summary>
/// The game's binary texture format ("RTEX"), as shipped in the paks for <c>.image</c> /
/// <c>.texture</c> entries. Header (little-endian uints): magic <c>"RTEX"</c>, version,
/// texture format, width, height, depth, array count, mipmap-skip offset, mipmap count
/// (36 bytes); then per-mipmap three strides (width/height/depth) followed by the level's
/// pixel data. This parser reads the header and the level-0 (full-resolution) block data;
/// <see cref="TextureDecoder"/> turns that into RGBA. Format constants per the
/// <see href="https://pioneersofpagonia.wiki.gg/wiki/Texture_File_Format">wiki</see>.
/// </summary>
public sealed class RtexTexture
{
    /// <summary>Little-endian magic for the ASCII tag "RTEX".</summary>
    public const uint Magic = 0x58455452;

    // Texture-format enum values we care about (the 0x10000000 bit marks compressed formats).
    public const uint FormatR8G8B8A8 = 1;
    public const uint FormatSrgbR8G8B8A8 = 2;
    public const uint FormatDxt1 = 0x10000021;
    public const uint FormatDxt1Srgb = 0x10000022;
    public const uint FormatDxt5 = 0x10000023;
    public const uint FormatDxt5Srgb = 0x10000024;
    public const uint FormatBc7 = 0x10000026;
    public const uint FormatBc7Srgb = 0x10000027;

    private const int HeaderSize = 36; // 9 uints

    public required uint Format { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>The level-0 (full-resolution) pixel/block data — compressed for BCn formats.</summary>
    public required byte[] BaseMip { get; init; }

    /// <summary>Parse an RTEX blob, or return null if it isn't valid RTEX / unsupported.</summary>
    public static RtexTexture? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize || ReadU32(bytes, 0) != Magic)
        {
            return null;
        }

        var format = ReadU32(bytes, 8);
        var width = (int)ReadU32(bytes, 12);
        var height = (int)ReadU32(bytes, 16);
        var mipSkip = ReadU32(bytes, 28);

        // We only support streaming-complete textures (the full base mip is present). Icons
        // ship with mipSkip 0; a non-zero skip would mean the top mips were stripped.
        if (mipSkip != 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var baseSize = BaseMipSize(format, width, height);
        if (baseSize <= 0 || baseSize > bytes.Length - HeaderSize)
        {
            return null;
        }

        // The mip chain is stored **smallest-first** with the levels' data contiguous at the
        // end, so the full-resolution base mip is simply the last `baseSize` bytes — robust to
        // whether the per-level strides are interleaved or tabled.
        return new RtexTexture
        {
            Format = format,
            Width = width,
            Height = height,
            BaseMip = bytes[^baseSize..].ToArray(),
        };
    }

    /// <summary>Byte length of the level-0 data for a format at the given dimensions, or 0 if
    /// unsupported or too large to fit an <see cref="int"/> (a crafted header could otherwise wrap
    /// int32 arithmetic to a small positive size that passes the bounds check while Width/Height
    /// stay huge, making the decoder allocate Width*Height pixels for a tiny blob).</summary>
    private static int BaseMipSize(uint format, int width, int height)
    {
        // All arithmetic in long so it can't overflow; the caller bounds the result against the blob.
        long blocks = ((long)(width + 3) / 4) * ((height + 3) / 4);
        long size = format switch
        {
            FormatBc7 or FormatBc7Srgb or FormatDxt5 or FormatDxt5Srgb => blocks * 16,
            FormatDxt1 or FormatDxt1Srgb => blocks * 8,
            FormatR8G8B8A8 or FormatSrgbR8G8B8A8 => (long)width * height * 4,
            _ => 0,
        };

        return size > 0 && size <= int.MaxValue ? (int)size : 0;
    }

    private static uint ReadU32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
