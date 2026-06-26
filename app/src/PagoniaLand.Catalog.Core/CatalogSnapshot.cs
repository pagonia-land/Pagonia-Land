using PagoniaLand.Catalog.Assets;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.Catalog;

/// <summary>
/// A generated catalog's data — every domain's entries plus the per-pak summary, with no
/// bitmaps. This is what the app renders and what the disk cache stores (icons are cached
/// alongside, keyed by asset path).
/// </summary>
public sealed record CatalogSnapshot(
    IReadOnlyList<ResourceEntry> Resources,
    IReadOnlyList<BuildingEntry> Buildings,
    IReadOnlyList<RecipeEntry> Recipes,
    IReadOnlyList<UnitEntry> Units,
    IReadOnlyList<ObjectiveEntry> Objectives,
    IReadOnlyList<PakSummary> Paks);
