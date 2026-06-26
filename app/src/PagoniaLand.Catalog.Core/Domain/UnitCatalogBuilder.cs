using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// Projects the unit catalog from a <see cref="GameDatabase"/>: every entity with a <c>Unit</c>
/// component, with its recruitment costs, source recruitable unit, and tags resolved. A
/// faithful slice of <c>scripts/generate_catalog.ps1</c>.
/// </summary>
public static class UnitCatalogBuilder
{
    public static IReadOnlyList<UnitEntry> Build(GameDatabase database)
    {
        var rows = new List<UnitEntry>();

        foreach (var entity in database.Entities)
        {
            var unit = entity.Component("Unit");
            if (unit is null)
            {
                continue;
            }

            var values = entity.Values;
            var recruitment = entity.Component("RecruitmentCost");

            // Tags come from two component shapes: TaggedUnit/Tags/Item/Content/Tag and UnitTags/Item/Content/Tag.
            var taggedUnit = DomainText.Contents(values?.Element("TaggedUnit"), "Tags").Select(c => c.Element("Tag"));
            var unitTags = DomainText.Contents(values, "UnitTags").Select(c => c.Element("Tag"));

            rows.Add(new UnitEntry(
                Package: entity.Package,
                Name: entity.Name,
                Guid: entity.Guid,
                File: entity.File,
                Icon: DomainText.Text(unit, "Icon"),
                RecruitmentCosts: DomainText.Aggregate(DomainText.ResourceReferences(database, recruitment, "ResourceCosts")),
                NeedsManualRecruitment: DomainText.Text(recruitment, "NeedsManualRecruitment"),
                SourceRecruitableUnit: DomainText.Reference(database, DomainText.Text(recruitment, "SourceRecruitableUnit")),
                Tags: DomainText.Names(database, taggedUnit.Concat(unitTags)),
                Components: entity.ValueTypes));
        }

        return rows;
    }
}
