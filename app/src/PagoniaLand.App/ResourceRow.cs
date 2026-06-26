using System.Collections.Generic;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>A resource catalog row: slim grid columns + the full detail sections.</summary>
public sealed class ResourceRow : CatalogRow
{
    private static readonly IReadOnlyList<Reference> None = System.Array.Empty<Reference>();

    public required ResourceEntry Resource { get; init; }

    // Reverse references, computed across the catalog and injected at build time (clickable).
    public IReadOnlyList<Reference> ProducedBy { get; init; } = None;
    public IReadOnlyList<Reference> GatheredBy { get; init; } = None;
    public IReadOnlyList<Reference> ConsumedBy { get; init; } = None;
    public IReadOnlyList<Reference> UsedToBuild { get; init; } = None;
    public IReadOnlyList<Reference> UsedToRecruit { get; init; } = None;
    public IReadOnlyList<Reference> InObjectives { get; init; } = None;

    public override string Name => Resource.Name;
    public override string Guid => Resource.Guid;
    public string Category => Resource.Category;
    public string CarryType => Resource.CarryType;

    public override IReadOnlyList<DetailSection> Detail => Sections(
        Field("Category", Resource.Category),
        Field("Carry type", Resource.CarryType),
        Refs("Produced by", ProducedBy),
        Refs("Gathered by", GatheredBy),
        Refs("Consumed by", ConsumedBy),
        Refs("Used to build", UsedToBuild),
        Refs("Used to recruit", UsedToRecruit),
        Refs("In objectives", InObjectives),
        Field("Mesh", Resource.Mesh),
        List("Tags", Resource.Tags),
        Field("Package", Resource.Package),
        Field("Name key", Resource.NameKey),
        Field("GUID", Resource.Guid));
}
