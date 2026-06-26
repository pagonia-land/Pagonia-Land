namespace PagoniaLand.Catalog.Assets;

/// <summary>A decoded image: 8-bit straight RGBA pixels, row-major, <c>Width*Height*4</c> bytes.</summary>
public sealed record RgbaImage(int Width, int Height, byte[] Rgba);
