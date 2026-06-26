using PagoniaLand.Catalog.Assets;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.Catalog;

/// <summary>
/// Generates a full <see cref="CatalogSnapshot"/> (all five domains + pak summaries) and the
/// decoded icons for an install. This is the slow path — pak read, gd.xml parse, and BC7 icon
/// decode — which <see cref="CatalogCache"/> lets a warm restart skip.
/// </summary>
public static class CatalogGenerator
{
    public static (CatalogSnapshot Snapshot, Dictionary<string, RgbaImage> Icons, SearchIndexDocument SearchIndex) Generate(string root)
    {
        var database = new GameInstallReader().Read(root);

        var resources = ResourceCatalogBuilder.Build(database);
        var buildings = BuildingCatalogBuilder.Build(database);
        var recipes = RecipeCatalogBuilder.Build(database);
        var units = UnitCatalogBuilder.Build(database);
        var objectives = ObjectiveCatalogBuilder.Build(database);
        var paks = PakInventory.Scan(root);

        var icons = DecodeIcons(root, resources, buildings, units);

        var snapshot = new CatalogSnapshot(resources, buildings, recipes, units, objectives, paks);
        var searchIndex = SearchIndexBuilder.Build(database, snapshot, DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        return (snapshot, icons, searchIndex);
    }

    private static Dictionary<string, RgbaImage> DecodeIcons(
        string root,
        IReadOnlyList<ResourceEntry> resources,
        IReadOnlyList<BuildingEntry> buildings,
        IReadOnlyList<UnitEntry> units)
    {
        var icons = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        var assets = AssetReader.ForInstall(root);
        if (assets is null)
        {
            return icons;
        }

        var paths = resources.Select(r => r.Icon)
            .Concat(buildings.Select(b => b.Icon))
            .Concat(units.Select(u => u.Icon))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var image = assets.LoadImage(path);
            if (image is not null)
            {
                icons[path] = image;
            }
        }

        return icons;
    }
}
