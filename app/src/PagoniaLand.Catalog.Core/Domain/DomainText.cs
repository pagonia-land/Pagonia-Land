using System.Globalization;
using System.Xml.Linq;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog.Domain;

/// <summary>Shared helpers for the domain projection builders: reading component child text,
/// resolving GUID references to names, and the recurring <c>…/Item/Content</c> list shape.</summary>
internal static class DomainText
{
    /// <summary>The trimmed text of a direct child element, or the empty string if absent.</summary>
    public static string Text(XElement? parent, string child) =>
        parent?.Element(child)?.Value.Trim() ?? string.Empty;

    /// <summary>True when a GUID field is absent: blank/whitespace, or the engine's all-zero
    /// "intentionally empty" null GUID (<see cref="GameDatabaseAnalyzer.NullGuid"/>), which resolves
    /// to no entity. Treating the null GUID as present would emit a blank, unresolved reference row
    /// where the authoritative catalog shows "(none)".</summary>
    public static bool IsAbsentGuid(string? guid) =>
        string.IsNullOrWhiteSpace(guid)
        || string.Equals(guid.Trim(), GameDatabaseAnalyzer.NullGuid, StringComparison.OrdinalIgnoreCase);

    /// <summary>"amount name", or just the name — the amount is suppressed when it means "one"
    /// (absent, or a value that reads as 1), so a single unit renders the same whether the source
    /// carries an explicit <c>Amount</c> of 1 (construction costs) or none (recipe steps): a count
    /// appears only for real multiplicity (≥2). Empty when there is no name.</summary>
    public static string AmountName(string amount, string name) =>
        string.IsNullOrWhiteSpace(name) ? string.Empty
        : IsImpliedSingle(amount) ? name
        : $"{amount.Trim()} {name}";

    /// <summary>True when an amount means "one" and should not be shown: blank/whitespace, or a
    /// value that parses to 1. Single source of truth for the "don't print a leading 1" rule, shared
    /// by <see cref="AmountName"/> and <see cref="Reference.Display"/>.</summary>
    public static bool IsImpliedSingle(string? amount) =>
        string.IsNullOrWhiteSpace(amount)
        || (double.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) && n == 1d);

    /// <summary>The <c>&lt;Content&gt;</c> elements under a <c>container/Item/Content</c> list.</summary>
    public static IEnumerable<XElement> Contents(XElement? parent, string container) =>
        (parent?.Element(container)?.Elements("Item") ?? Enumerable.Empty<XElement>()).Elements("Content");

    /// <summary>Resolve a sequence of GUID-bearing elements to entity names (skipping blanks).</summary>
    public static IReadOnlyList<string> Names(GameDatabase database, IEnumerable<XElement?> guidElements)
    {
        var names = new List<string>();
        foreach (var element in guidElements)
        {
            var guid = element?.Value.Trim();
            if (!IsAbsentGuid(guid))
            {
                names.Add(database.ResolveName(guid!));
            }
        }

        return names;
    }

    /// <summary>Resolve a <c>container/Item/Content</c> resource-cost list to "amount resource" strings.</summary>
    public static IReadOnlyList<string> ResourceAmounts(GameDatabase database, XElement? parent, string container)
    {
        var items = new List<string>();
        foreach (var content in Contents(parent, container))
        {
            var resource = Text(content, "Resource");
            if (IsAbsentGuid(resource))
            {
                resource = Text(content, "Description");
            }

            if (!IsAbsentGuid(resource))
            {
                items.Add(AmountName(Text(content, "Amount"), database.ResolveName(resource)));
            }
        }

        return items;
    }

    /// <summary>The component's <c>Employment</c> as "amount unit".</summary>
    public static string Employment(GameDatabase database, XElement? component)
    {
        var employment = component?.Element("Employment");
        return employment is null
            ? string.Empty
            : AmountName(Text(employment, "Amount"), database.ResolveName(Text(employment, "Unit")));
    }

    // ---- reference-carrying variants (name + GUID) for cross-navigation ----

    /// <summary>A reference to the entity at <paramref name="guid"/>, or null if blank.</summary>
    public static Reference? Reference(GameDatabase database, string guid) =>
        IsAbsentGuid(guid) ? null : new Reference(database.ResolveName(guid), guid.Trim());

    /// <summary>Resolve a sequence of GUID-bearing elements to references (skipping blanks).</summary>
    public static IReadOnlyList<Reference> References(GameDatabase database, IEnumerable<XElement?> guidElements)
    {
        var refs = new List<Reference>();
        foreach (var element in guidElements)
        {
            var reference = Reference(database, element?.Value ?? string.Empty);
            if (reference is not null)
            {
                refs.Add(reference);
            }
        }

        return refs;
    }

    /// <summary>Resolve a <c>container/Item/Content</c> resource-cost list to references (name + GUID + amount).</summary>
    public static IReadOnlyList<Reference> ResourceReferences(GameDatabase database, XElement? parent, string container)
    {
        var refs = new List<Reference>();
        foreach (var content in Contents(parent, container))
        {
            var resource = Text(content, "Resource");
            if (IsAbsentGuid(resource))
            {
                resource = Text(content, "Description");
            }

            if (!IsAbsentGuid(resource))
            {
                refs.Add(new Reference(database.ResolveName(resource), resource, Text(content, "Amount")));
            }
        }

        return refs;
    }

    /// <summary>The component's <c>Employment</c> as a reference (unit name + GUID + amount), or null.</summary>
    public static Reference? EmploymentReference(GameDatabase database, XElement? component)
    {
        var employment = component?.Element("Employment");
        var unit = Text(employment, "Unit");
        return IsAbsentGuid(unit)
            ? null
            : new Reference(database.ResolveName(unit), unit, Text(employment, "Amount"));
    }

    /// <summary>Collapse repeated references to the same entity into one, summing their amounts.
    /// Recipes list one production step per unit (5 Copper Ore = 5 separate steps), and some
    /// gather/cost lists repeat a resource — both render as a stack of identical lines otherwise.
    /// A single entry is preserved as-is; a blank or non-numeric amount counts as one, so the
    /// collapsed line shows the summed quantity (e.g. "5 Copper Ore").
    /// <para>A count is shown <b>only</b> for real multiplicity (≥2). A quantity of one is never
    /// printed — whether it comes from a repeat-once recipe step (no <c>Amount</c>) or a cost with an
    /// explicit <c>Amount</c> of 1 — so "1 Softwood Trunk" renders as "Softwood Trunk", uniform across
    /// every domain. The suppression rule is <see cref="IsImpliedSingle"/>, applied at the render
    /// points (<see cref="AmountName"/> / <see cref="Reference.Display"/>), so it holds here too.</para></summary>
    public static IReadOnlyList<Reference> Aggregate(IReadOnlyList<Reference> references)
    {
        var order = new List<string>();
        var groups = new Dictionary<string, List<Reference>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references)
        {
            if (!groups.TryGetValue(reference.Guid, out var list))
            {
                list = new List<Reference>();
                groups[reference.Guid] = list;
                order.Add(reference.Guid);
            }

            list.Add(reference);
        }

        var result = new List<Reference>();
        foreach (var guid in order)
        {
            var list = groups[guid];
            if (list.Count == 1)
            {
                result.Add(list[0]);
                continue;
            }

            var total = list.Sum(r => int.TryParse(r.Amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1);
            result.Add(list[0] with { Amount = total.ToString(CultureInfo.InvariantCulture) });
        }

        return result;
    }
}
