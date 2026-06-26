using System.Collections.Generic;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>A building catalog row: slim grid columns + the full detail sections.</summary>
public sealed class BuildingRow : CatalogRow
{
    public required BuildingEntry Building { get; init; }

    /// <summary>Objectives that reference this building — a reverse reference injected at build time.</summary>
    public IReadOnlyList<Reference> InObjectives { get; init; } = System.Array.Empty<Reference>();

    public override string Name => Building.Name;
    public override string Guid => Building.Guid;
    public string Category => Building.Category;
    public string Group => Building.UiGroup;

    public override IReadOnlyList<DetailSection> Detail => Sections(
        Field("Category", Building.Category),
        Field("Build menu group", Building.UiGroup),
        Refs("Construction costs", Building.ConstructionCosts),
        Ref("Built by", Building.Builder),
        Ref("Also built by", Building.SecondaryBuilder),
        Refs("Produces (recipes)", Building.ProductionRecipes),
        Ref("Production worker", Building.ProductionWorker),
        Field("Optimal work step", Building.OptimalWorkStep),
        Refs("Gathers", Building.GatherOutputs),
        Refs("In objectives", InObjectives),
        Field("Package", Building.Package),
        Field("GUID", Building.Guid));
}
