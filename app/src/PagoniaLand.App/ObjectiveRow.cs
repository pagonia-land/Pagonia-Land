using System.Collections.Generic;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>An objective catalog row: slim grid columns + the full detail sections.</summary>
public sealed class ObjectiveRow : CatalogRow
{
    private static readonly IReadOnlyList<Reference> None = System.Array.Empty<Reference>();

    public required ObjectiveEntry Objective { get; init; }

    // The objective's references, classified by domain across the catalog (clickable).
    public IReadOnlyList<Reference> RelatedObjectives { get; init; } = None;
    public IReadOnlyList<Reference> Buildings { get; init; } = None;
    public IReadOnlyList<Reference> Units { get; init; } = None;
    public IReadOnlyList<Reference> Resources { get; init; } = None;

    public override string Name => Objective.Name;
    public override string Guid => Objective.Guid;
    public string Category => Objective.Category;
    public string Hidden => Objective.Hidden;

    public override IReadOnlyList<DetailSection> Detail => Sections(
        Field("Category", Objective.Category),
        Field("Hidden", Objective.Hidden),
        Field("Sort order", Objective.SortOrder),
        Refs("Related objectives", RelatedObjectives),
        Refs("Buildings", Buildings),
        Refs("Units", Units),
        Refs("Resources", Resources),
        Field("Title key", Objective.Title),
        Field("Description key", Objective.Description),
        List("Objective types", Objective.ObjectiveTypes),
        Field("Package", Objective.Package),
        Field("GUID", Objective.Guid));
}
