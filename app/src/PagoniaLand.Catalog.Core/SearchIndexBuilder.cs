using PagoniaLand.Catalog.Domain;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog;

/// <summary>
/// Builds a search index in the online catalog browser's format from the parsed database plus the
/// five-domain snapshot: one item per entity (broad coverage) and a richer item per resource /
/// building / recipe / unit / objective. This is a <em>partial</em> index — the full online index
/// is a ~35-projection composite from the PowerShell pipeline (localization, asset references,
/// system projections, …). It covers the app's own search; it is not the full online dataset.
/// </summary>
public static class SearchIndexBuilder
{
    public static SearchIndexDocument Build(GameDatabase database, CatalogSnapshot snapshot, string generatedAt)
    {
        var items = new List<SearchIndexItem>();

        foreach (var entity in database.Entities)
        {
            items.Add(Item("entity", entity.Name, entity.Guid, entity.Package, entity.File, new Dictionary<string, string>
            {
                ["EntityName"] = entity.Name,
                ["Components"] = string.Join(", ", entity.ValueTypes),
            }));
        }

        foreach (var r in snapshot.Resources)
        {
            items.Add(Item("resource", r.Name, r.Guid, r.Package, r.File, new Dictionary<string, string>
            {
                ["Category"] = r.Category,
                ["CarryType"] = r.CarryType,
                ["Tags"] = string.Join(", ", r.Tags),
            }));
        }

        foreach (var b in snapshot.Buildings)
        {
            items.Add(Item("building", b.Name, b.Guid, b.Package, b.File, new Dictionary<string, string>
            {
                ["Category"] = b.Category,
                ["Group"] = b.UiGroup,
                ["Costs"] = Names(b.ConstructionCosts),
                ["Recipes"] = Names(b.ProductionRecipes),
            }));
        }

        foreach (var rc in snapshot.Recipes)
        {
            items.Add(Item("recipe", rc.Name, rc.Guid, rc.Package, rc.File, new Dictionary<string, string>
            {
                ["Identifier"] = rc.Identifier,
                ["Inputs"] = Names(rc.Inputs),
                ["Outputs"] = Names(rc.Outputs),
            }));
        }

        foreach (var u in snapshot.Units)
        {
            items.Add(Item("unit", u.Name, u.Guid, u.Package, u.File, new Dictionary<string, string>
            {
                ["RecruitmentCosts"] = Names(u.RecruitmentCosts),
                ["Tags"] = string.Join(", ", u.Tags),
            }));
        }

        foreach (var o in snapshot.Objectives)
        {
            items.Add(Item("objective", o.Name, o.Guid, o.Package, o.File, new Dictionary<string, string>
            {
                ["Category"] = o.Category,
                ["Hidden"] = o.Hidden,
                ["Types"] = string.Join(", ", o.ObjectiveTypes),
            }));
        }

        return new SearchIndexDocument(generatedAt, items.Count, items);
    }

    private static string Names(IReadOnlyList<Reference> references) => string.Join(", ", references.Select(r => r.Display));

    private static SearchIndexItem Item(string type, string name, string guid, string package, string file, Dictionary<string, string> fields)
    {
        var kind = char.ToUpperInvariant(type[0]) + type[1..];

        // Terms = the standard identity bits + the domain-specific field values, for free-text search.
        var terms = string.Join("; ", new[] { guid, package, kind, name, file }
            .Concat(fields.Values)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct());

        fields["Package"] = package;
        fields["Kind"] = kind;
        fields["Guid"] = guid;
        fields["File"] = file;

        return new SearchIndexItem(type, name, $"{kind} | {name}", package, guid, file, terms, fields);
    }
}
