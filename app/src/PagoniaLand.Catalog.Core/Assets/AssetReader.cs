using PagoniaLand.Paker;

namespace PagoniaLand.Catalog.Assets;

/// <summary>
/// Resolves an entity's asset reference (e.g. a resource's <c>Icon</c> path like
/// <c>core/gui/icons/items/icon_xyz.image</c>) to a decoded <see cref="RgbaImage"/> by
/// locating the matching pak entry, extracting it, and decoding the RTEX texture. Builds a
/// path→entry index over the install's paks once, then extracts + decodes lazily with a cache.
/// Local-only — reads the user's own paks, never publishes anything.
/// </summary>
public sealed class AssetReader
{
    private readonly Dictionary<string, (string Pak, PakEntry Entry)> _byPath;
    private readonly Dictionary<string, RgbaImage?> _imageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly PakReader _pak = new();

    private AssetReader(Dictionary<string, (string, PakEntry)> byPath) => _byPath = byPath;

    /// <summary>Build a reader for a live install / pak folder, or null for a source with no paks.</summary>
    public static AssetReader? ForInstall(string root)
    {
        var pakPaths = GameInstallLocator.Detect(root) switch
        {
            GameInstallKind.LiveInstall => Directory.EnumerateFiles(Path.Combine(root, "pak"), "*.pak"),
            GameInstallKind.PakDirectory => Directory.EnumerateFiles(root, "*.pak"),
            _ => null,
        };

        if (pakPaths is null)
        {
            return null;
        }

        var reader = new PakReader();
        var index = new Dictionary<string, (string, PakEntry)>(StringComparer.OrdinalIgnoreCase);
        foreach (var pakPath in pakPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(pakPath);
            var result = reader.OpenIndex(stream);
            if (result.Index is null)
            {
                continue;
            }

            foreach (var entry in result.Index.Entries)
            {
                index[entry.Filename] = (pakPath, entry); // later pak wins, matching load order
            }
        }

        return new AssetReader(index);
    }

    /// <summary>Decode the image at <paramref name="assetPath"/>, or null if missing/undecodable. Cached.</summary>
    public RgbaImage? LoadImage(string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        if (_imageCache.TryGetValue(assetPath, out var cached))
        {
            return cached;
        }

        RgbaImage? image = null;
        if (_byPath.TryGetValue(assetPath, out var location))
        {
            try
            {
                var texture = RtexTexture.Parse(ExtractRaw(location));
                image = texture is null ? null : TextureDecoder.Decode(texture);
            }
            catch
            {
                // Honour the documented contract: a missing/undecodable asset returns null. A single
                // corrupt .image/.texture (or a decoder failure on a malformed block) must not abort
                // the whole catalog generation — skip this one icon and cache the miss.
                image = null;
            }
        }

        _imageCache[assetPath] = image;
        return image;
    }

    private byte[] ExtractRaw((string Pak, PakEntry Entry) location)
    {
        using var stream = File.OpenRead(location.Pak);
        using var buffer = new MemoryStream();
        _pak.ExtractEntry(stream, location.Entry, buffer);
        return buffer.ToArray();
    }
}
