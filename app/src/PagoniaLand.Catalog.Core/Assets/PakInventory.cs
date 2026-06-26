using PagoniaLand.Paker;

namespace PagoniaLand.Catalog.Assets;

/// <summary>
/// A quick per-pak overview of an install: opens each pak's index (no extraction) and counts
/// what it contributes — GameDatabase XML and image/texture assets. Makes visible that the
/// catalog is built from <em>all</em> paks (core + DLC + decorations + tools), not just
/// <c>core.pak</c>. Local-only; reads nothing but the indexes.
/// </summary>
public static class PakInventory
{
    /// <summary>Summaries for every readable pak in the install, largest first; empty for a source with no paks.</summary>
    public static IReadOnlyList<PakSummary> Scan(string root)
    {
        var pakPaths = GameInstallLocator.Detect(root) switch
        {
            GameInstallKind.LiveInstall => Directory.EnumerateFiles(Path.Combine(root, "pak"), "*.pak"),
            GameInstallKind.PakDirectory => Directory.EnumerateFiles(root, "*.pak"),
            _ => null,
        };

        if (pakPaths is null)
        {
            return Array.Empty<PakSummary>();
        }

        var reader = new PakReader();
        var summaries = new List<PakSummary>();
        foreach (var pakPath in pakPaths)
        {
            using var stream = File.OpenRead(pakPath);
            var result = reader.OpenIndex(stream);
            if (result.Index is null)
            {
                continue; // unreadable pak — skip rather than fail the overview
            }

            var entries = result.Index.Entries;
            summaries.Add(new PakSummary(
                Path.GetFileName(pakPath),
                new FileInfo(pakPath).Length,
                entries.Count,
                entries.Count(e => e.Filename.EndsWith(".gd.xml", StringComparison.OrdinalIgnoreCase)),
                entries.Count(e => e.Filename.EndsWith(".image", StringComparison.OrdinalIgnoreCase)),
                entries.Count(e => e.Filename.EndsWith(".texture", StringComparison.OrdinalIgnoreCase))));
        }

        return summaries
            .OrderByDescending(s => s.SizeBytes)
            .ToList();
    }
}
