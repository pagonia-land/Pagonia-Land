using BCnEncoder.Decoder;
using BCnEncoder.Shared;

namespace PagoniaLand.Catalog.Assets;

/// <summary>
/// Decodes an <see cref="RtexTexture"/>'s level-0 data into straight 8-bit RGBA. BCn formats
/// (BC7 — what icons use — and DXT5) go through the managed <c>BCnEncoder.Net</c> decoder;
/// raw R8G8B8A8 is passed through. Unsupported formats return null.
/// </summary>
public static class TextureDecoder
{
    public static RgbaImage? Decode(RtexTexture texture)
    {
        var rgba = texture.Format switch
        {
            RtexTexture.FormatBc7 or RtexTexture.FormatBc7Srgb => DecodeBc(texture, CompressionFormat.Bc7),
            RtexTexture.FormatDxt5 or RtexTexture.FormatDxt5Srgb => DecodeBc(texture, CompressionFormat.Bc3),
            RtexTexture.FormatDxt1 or RtexTexture.FormatDxt1Srgb => DecodeBc(texture, CompressionFormat.Bc1),
            RtexTexture.FormatR8G8B8A8 or RtexTexture.FormatSrgbR8G8B8A8 => Raw(texture),
            _ => null,
        };

        return rgba is null ? null : new RgbaImage(texture.Width, texture.Height, rgba);
    }

    private static byte[] DecodeBc(RtexTexture texture, CompressionFormat format)
    {
        // A fresh decoder per call keeps Decode thread-safe (the parallel PNG dump fans this out).
        var pixels = new BcDecoder().DecodeRaw(texture.BaseMip, texture.Width, texture.Height, format);
        var rgba = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            rgba[(i * 4) + 0] = p.r;
            rgba[(i * 4) + 1] = p.g;
            rgba[(i * 4) + 2] = p.b;
            rgba[(i * 4) + 3] = p.a;
        }

        return rgba;
    }

    private static byte[]? Raw(RtexTexture texture)
    {
        var expected = texture.Width * texture.Height * 4;
        return texture.BaseMip.Length >= expected ? texture.BaseMip[..expected] : null;
    }
}
