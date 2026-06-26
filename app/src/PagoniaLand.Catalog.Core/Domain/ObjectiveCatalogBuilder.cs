using System.Text.RegularExpressions;
using System.Xml.Linq;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// Projects the objective catalog from a <see cref="GameDatabase"/>: every entity with a
/// <c>GeneralObjective</c> component, with its category resolved, its visibility/sort order, the
/// objective sub-types it carries, and its title/description localisation keys.
/// </summary>
public static class ObjectiveCatalogBuilder
{
    private static readonly Regex GuidPattern =
        new("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

    public static IReadOnlyList<ObjectiveEntry> Build(GameDatabase database)
    {
        var rows = new List<ObjectiveEntry>();

        foreach (var entity in database.Entities)
        {
            var general = entity.Component("GeneralObjective");
            if (general is null)
            {
                continue;
            }

            // The objective sub-types are the entity's components named Objective* (the kind of
            // objective it is); GeneralObjective itself is the shared header, not a sub-type.
            var objectiveTypes = entity.ValueTypes
                .Where(v => v.StartsWith("Objective", StringComparison.Ordinal))
                .ToList();

            // Title/Description live on those sub-components (e.g. ObjectiveMilestone/Title) — take
            // the first non-empty of each.
            var title = string.Empty;
            var description = string.Empty;
            foreach (var component in entity.Values?.Elements() ?? Enumerable.Empty<XElement>())
            {
                if (!component.Name.LocalName.StartsWith("Objective", StringComparison.Ordinal))
                {
                    continue;
                }

                if (title.Length == 0)
                {
                    title = DomainText.Text(component, "Title");
                }

                if (description.Length == 0)
                {
                    description = DomainText.Text(component, "Description");
                }
            }

            rows.Add(new ObjectiveEntry(
                Package: entity.Package,
                Name: entity.Name,
                Guid: entity.Guid,
                File: entity.File,
                Category: database.ResolveName(DomainText.Text(general, "Category")),
                Hidden: DomainText.Text(general, "Hidden"),
                SortOrder: DomainText.Text(general, "SortOrder"),
                Title: title,
                Description: description,
                ObjectiveTypes: objectiveTypes,
                References: CollectReferences(database, entity, DomainText.Text(general, "Category")),
                Components: entity.ValueTypes));
        }

        return rows;
    }

    // Every other entity the objective points at: scan its component leaf values for GUIDs and
    // resolve the ones that name a real entity. The app groups these by domain (buildings / units /
    // resources / objectives) into clickable links. Self and the category are excluded.
    private static IReadOnlyList<Reference> CollectReferences(GameDatabase database, GameEntity entity, string categoryGuid)
    {
        var references = new List<Reference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { entity.Guid };
        if (categoryGuid.Length > 0)
        {
            seen.Add(categoryGuid);
        }

        foreach (var element in entity.Values?.Descendants() ?? Enumerable.Empty<XElement>())
        {
            if (element.HasElements)
            {
                continue; // only leaf values carry a GUID
            }

            var value = element.Value.Trim();
            if (!GuidPattern.IsMatch(value) || !seen.Add(value))
            {
                continue;
            }

            var name = database.ResolveName(value);
            if (name.Length > 0)
            {
                references.Add(new Reference(name, value));
            }
        }

        return references;
    }
}
