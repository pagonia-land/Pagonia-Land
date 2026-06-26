using System.Xml.Linq;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// Projects the resource catalog from a <see cref="GameDatabase"/>: every entity with a
/// <c>&lt;ResourceDescription&gt;</c> component, with its category resolved and its declared
/// tags resolved to names. A faithful slice of <c>scripts/generate_catalog.ps1</c>'s resource
/// catalog (the first domain projection; buildings/recipes/units/objectives follow the same
/// shape).
/// </summary>
public static class ResourceCatalogBuilder
{
    public static IReadOnlyList<ResourceEntry> Build(GameDatabase database)
    {
        var rows = new List<ResourceEntry>();

        foreach (var entity in database.Entities)
        {
            var description = entity.Component("ResourceDescription");
            if (description is null)
            {
                continue;
            }

            rows.Add(new ResourceEntry(
                Package: entity.Package,
                Name: entity.Name,
                Guid: entity.Guid,
                File: entity.File,
                Category: database.ResolveName(ChildText(description, "ResourceCategory")),
                NameKey: ChildText(description, "Name"),
                NamePluralKey: ChildText(description, "NamePlural"),
                DescriptionKey: ChildText(description, "Description"),
                Icon: ChildText(description, "Icon"),
                CarryType: ChildText(description, "CarryType"),
                Mesh: ChildText(description, "Mesh"),
                UiDisplay: ChildText(description, "UiDisplay"),
                SortOrder: ChildText(description, "SortOrder"),
                WealthValue: ChildText(description, "WealthValue"),
                StealValue: ChildText(description, "StealValue"),
                Tags: ResolveTags(database, description),
                Components: entity.ValueTypes));
        }

        return rows;
    }

    /// <summary>The trimmed text of a direct child element, or the empty string if absent.</summary>
    private static string ChildText(XElement parent, string child) =>
        parent.Element(child)?.Value.Trim() ?? string.Empty;

    /// <summary>Resolve the <c>Tags/Item/Content/Tag</c> GUID list to entity names.</summary>
    private static IReadOnlyList<string> ResolveTags(GameDatabase database, XElement description)
    {
        var tags = new List<string>();
        foreach (var tag in description.Elements("Tags").Elements("Item").Elements("Content").Elements("Tag"))
        {
            var guid = tag.Value.Trim();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                tags.Add(database.ResolveName(guid));
            }
        }

        return tags;
    }
}
