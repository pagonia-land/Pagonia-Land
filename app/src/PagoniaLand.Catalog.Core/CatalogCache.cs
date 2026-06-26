using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PagoniaLand.Catalog.Assets;

namespace PagoniaLand.Catalog;

/// <summary>
/// A local disk cache of a generated catalog, keyed by a fingerprint of the install's source
/// files (pak files, or gd.xml for an extracted layout). A warm restart on an unchanged game
/// loads the snapshot + decoded icons from disk instead of re-reading paks and re-decoding BC7;
/// any game change moves the fingerprint and the next Generate rebuilds + re-caches. Best-effort:
/// a cache read/write failure never breaks generation.
/// </summary>
public static class CatalogCache
{
    /// <summary>Where cache files live; overridable (e.g. for tests).</summary>
    public static string Directory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PagoniaLand", "cache");

    // Bump whenever the cached model changes shape OR its projected content changes (e.g. a
    // builder fix), so caches written by an older build are ignored (and cleaned up) rather than
    // served stale. Kept at 1 for a release (shipped users start with no cache); bump during dev.
    private const int SchemaVersion = 1;

    private static string CatalogPath(string fingerprint) => Path.Combine(Directory, $"{fingerprint}.v{SchemaVersion}.catalog.json");

    private static string IconsPath(string fingerprint) => Path.Combine(Directory, $"{fingerprint}.v{SchemaVersion}.icons.bin");

    private static string SearchIndexPath(string fingerprint) => Path.Combine(Directory, $"{fingerprint}.v{SchemaVersion}.search-index.json");

    // The online catalog browser's items use camelCase property names.
    private static readonly JsonSerializerOptions SearchIndexJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>True if a current-schema cache already exists for the install's fingerprint.</summary>
    public static bool HasCache(string root)
    {
        try
        {
            var fingerprint = Fingerprint(root);
            return File.Exists(CatalogPath(fingerprint)) && File.Exists(IconsPath(fingerprint));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>A stable hash of the metadata of the files the catalog is built from.</summary>
    public static string Fingerprint(string root)
    {
        var builder = new StringBuilder();
        foreach (var file in SourceFiles(root).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            builder.Append(info.Name).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    /// <summary>Load the cached snapshot + icons for the install, or false on a miss / any error.</summary>
    public static bool TryLoad(string root, out CatalogSnapshot? snapshot, out Dictionary<string, RgbaImage> icons)
    {
        snapshot = null;
        icons = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var fingerprint = Fingerprint(root);
            var jsonPath = CatalogPath(fingerprint);
            var iconPath = IconsPath(fingerprint);
            if (!File.Exists(jsonPath) || !File.Exists(iconPath))
            {
                return false;
            }

            snapshot = JsonSerializer.Deserialize<CatalogSnapshot>(File.ReadAllBytes(jsonPath));
            if (snapshot is null)
            {
                return false;
            }

            icons = ReadIcons(iconPath);
            return true;
        }
        catch
        {
            snapshot = null;
            icons = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
            return false;
        }
    }

    /// <summary>Persist the snapshot + icons + search index for the install's fingerprint (best-effort).</summary>
    public static void Save(string root, CatalogSnapshot snapshot, IReadOnlyDictionary<string, RgbaImage> icons, SearchIndexDocument searchIndex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var fingerprint = Fingerprint(root);
            CleanStaleVersions(fingerprint);
            File.WriteAllBytes(CatalogPath(fingerprint), JsonSerializer.SerializeToUtf8Bytes(snapshot));
            WriteIcons(IconsPath(fingerprint), icons);
            File.WriteAllBytes(SearchIndexPath(fingerprint), JsonSerializer.SerializeToUtf8Bytes(searchIndex, SearchIndexJson));
        }
        catch
        {
            // Caching is an optimisation; never fail a generation because the cache couldn't be written.
        }
    }

    // Keep only the cache files for the current fingerprint + schema version; drop everything else
    // (older schema versions, and caches for previous game versions / installs). Otherwise the
    // cache dir grows by a full ~32 MB icon set on every game update.
    private static readonly string[] CacheSuffixes = { ".catalog.json", ".icons.bin", ".search-index.json" };

    private static void CleanStaleVersions(string fingerprint)
    {
        var keep = $"{fingerprint}.v{SchemaVersion}.";
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory))
        {
            var name = Path.GetFileName(file);
            var isCacheFile = CacheSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
            if (isCacheFile && !name.StartsWith(keep, StringComparison.Ordinal))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }

    private static IEnumerable<string> SourceFiles(string root) =>
        GameInstallLocator.Detect(root) switch
        {
            GameInstallKind.LiveInstall => System.IO.Directory.EnumerateFiles(Path.Combine(root, "pak"), "*.pak"),
            GameInstallKind.PakDirectory => System.IO.Directory.EnumerateFiles(root, "*.pak"),
            GameInstallKind.ExtractedLayout => System.IO.Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories),
            _ => Enumerable.Empty<string>(),
        };

    // Icons are stored as a deflate-compressed stream of (path, width, height, rgba) — flat game
    // art compresses well, keeping the cache far smaller than the raw RGBA.
    private static void WriteIcons(string path, IReadOnlyDictionary<string, RgbaImage> icons)
    {
        using var file = File.Create(path);
        using var deflate = new DeflateStream(file, CompressionLevel.Fastest);
        using var writer = new BinaryWriter(deflate);
        writer.Write(icons.Count);
        foreach (var (key, image) in icons)
        {
            writer.Write(key);
            writer.Write(image.Width);
            writer.Write(image.Height);
            writer.Write(image.Rgba.Length);
            writer.Write(image.Rgba);
        }
    }

    private static Dictionary<string, RgbaImage> ReadIcons(string path)
    {
        var icons = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        using var file = File.OpenRead(path);
        using var deflate = new DeflateStream(file, CompressionMode.Decompress);
        using var reader = new BinaryReader(deflate);
        var count = reader.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var key = reader.ReadString();
            var width = reader.ReadInt32();
            var height = reader.ReadInt32();
            var length = reader.ReadInt32();
            icons[key] = new RgbaImage(width, height, reader.ReadBytes(length));
        }

        return icons;
    }
}
