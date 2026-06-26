using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// Projects the building catalog from a <see cref="GameDatabase"/>: every entity with a
/// <c>Building</c> component, with its construction costs / builder / production recipes /
/// gather outputs resolved. A faithful slice of <c>scripts/generate_catalog.ps1</c>'s
/// building catalog.
/// </summary>
public static class BuildingCatalogBuilder
{
    public static IReadOnlyList<BuildingEntry> Build(GameDatabase database)
    {
        var rows = new List<BuildingEntry>();

        foreach (var entity in database.Entities)
        {
            var building = entity.Component("Building");
            if (building is null)
            {
                continue;
            }

            var buildable = entity.Component("Buildable");
            var buildup = entity.Component("AspectBuildup");
            var production = entity.Component("AspectProduction");

            rows.Add(new BuildingEntry(
                Package: entity.Package,
                Name: entity.Name,
                Guid: entity.Guid,
                File: entity.File,
                Category: database.ResolveName(DomainText.Text(buildable, "Category")),
                UiGroup: DomainText.Text(buildable, "UiBuildingGroup"),
                Icon: DomainText.Text(building, "Icon"),
                ConstructionCosts: DomainText.Aggregate(DomainText.ResourceReferences(database, buildup, "Costs")),
                Builder: DomainText.EmploymentReference(database, buildup),
                // Some buildings (e.g. Construction Camp) list a SecondaryUnit alongside the primary
                // builder Unit — an alternative builder type. It carries no own <Amount> (the single
                // Employment/Amount is the primary's), so project it as a plain unit reference.
                SecondaryBuilder: DomainText.Reference(database, DomainText.Text(buildup?.Element("Employment"), "SecondaryUnit")),
                ProductionRecipes: DomainText.References(database, DomainText.Contents(production, "Recipes").Select(c => c.Element("Recipe"))),
                ProductionWorker: DomainText.EmploymentReference(database, production),
                OptimalWorkStep: DomainText.Text(production?.Element("Efficiency"), "TimeOfOptimalWorkStep"),
                GatherOutputs: DomainText.Aggregate(DomainText.References(database, DomainText.Contents(entity.Component("AspectGatherer"), "ResourceToGather").Select(c => c.Element("GatherResource")))),
                Components: entity.ValueTypes));
        }

        return rows;
    }
}
