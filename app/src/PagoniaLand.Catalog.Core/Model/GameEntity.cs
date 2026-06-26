using System.Xml.Linq;

namespace PagoniaLand.Catalog.Model;

/// <summary>
/// A parsed <c>&lt;Entity Guid="…"&gt;</c> that keeps its XML element, so the domain
/// projections (resources, buildings, recipes, …) can read into its components. This is
/// the richer counterpart to <see cref="EntityDefinition"/> (which is data-only): the
/// projection layer needs the live element to pull component fields like a resource's icon
/// or a building's costs.
/// </summary>
public sealed class GameEntity
{
    public GameEntity(string guid, string name, string package, string file, XElement element)
    {
        Guid = guid;
        Name = name;
        Package = package;
        File = file;
        Element = element;
    }

    public string Guid { get; }
    public string Name { get; }
    public string Package { get; }
    public string File { get; }

    /// <summary>The underlying <c>&lt;Entity&gt;</c> element.</summary>
    public XElement Element { get; }

    /// <summary>The entity's <c>&lt;Values&gt;</c> component container, if present.</summary>
    public XElement? Values => Element.Element("Values");

    /// <summary>The local names of the entity's direct components (children of <c>&lt;Values&gt;</c>).</summary>
    public IReadOnlyList<string> ValueTypes =>
        Values?.Elements().Select(e => e.Name.LocalName).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>True when the entity carries a direct component with this name.</summary>
    public bool HasComponent(string name) => Values?.Element(name) is not null;

    /// <summary>The entity's direct component element with this name, or null.</summary>
    public XElement? Component(string name) => Values?.Element(name);
}
