using System.Text.RegularExpressions;
using System.Xml.Linq;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog;

/// <summary>
/// Reads a local GameDatabase (the extracted <c>*.gd.xml</c> set) into a queryable model:
/// entity definitions, resolved GUID references, and the headline summary counts. This is a
/// faithful C# port of <c>scripts/analyze_database.ps1</c> so the engine that backs the
/// "Pagonia Land" app produces the same numbers the repo already tracks — with no PowerShell
/// at runtime. Reads only; never writes or publishes anything.
/// </summary>
public sealed partial class GameDatabaseAnalyzer
{
    /// <summary>The all-zero GUID the engine uses for an intentionally-empty reference.</summary>
    public const string NullGuid = "00000000-0000-0000-0000-000000000000";

    [GeneratedRegex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidPattern();

    /// <summary>Analyse every <c>*.xml</c> under <paramref name="gameDbRoot"/> (recursively).</summary>
    public AnalysisResult Analyze(string gameDbRoot)
    {
        var root = Path.GetFullPath(gameDbRoot);
        var documents = new List<(string RelativePath, XDocument Document)>();
        foreach (var file in Directory
                     .EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            documents.Add((Path.GetRelativePath(root, file), XDocument.Load(file)));
        }

        return AnalyzeDocuments(documents);
    }

    /// <summary>
    /// Analyse already-parsed documents. <paramref name="documents"/> pairs each file's
    /// game-relative path (its first segment is the package, e.g. <c>core</c>) with its parsed
    /// XML. Pure and side-effect-free — the unit-testable core.
    /// </summary>
    public AnalysisResult AnalyzeDocuments(IReadOnlyList<(string RelativePath, XDocument Document)> documents)
    {
        var entities = new List<EntityDefinition>();
        var definitions = new Dictionary<string, EntityDefinition>(StringComparer.Ordinal);

        foreach (var (relativePath, document) in documents)
        {
            var package = PackageOf(relativePath);

            foreach (var node in document.Descendants()
                         .Where(e => e.Name.LocalName == "Entity" && e.Attribute("Guid") != null))
            {
                var guid = (string)node.Attribute("Guid")!;
                var parent = ParentEntity(node);

                var entity = new EntityDefinition(
                    Guid: guid,
                    Name: (string?)node.Attribute("Name") ?? string.Empty,
                    Package: package,
                    File: relativePath,
                    GroupPath: GroupPath(node),
                    IsAbstract: (string?)node.Attribute("IsAbstract") == "true",
                    ParentEntityGuid: parent is null ? null : (string?)parent.Attribute("Guid"),
                    ParentEntityName: parent is null ? null : (string?)parent.Attribute("Name"),
                    ChildEntityCount: node.Element("Children")?.Elements("Entity").Count() ?? 0,
                    ValueTypes: node.Element("Values")?.Elements().Select(e => e.Name.LocalName).ToList()
                                ?? (IReadOnlyList<string>)Array.Empty<string>());

                entities.Add(entity);
                definitions.TryAdd(guid, entity);
            }
        }

        var references = new List<GuidReference>();

        foreach (var (relativePath, document) in documents)
        {
            var package = PackageOf(relativePath);

            foreach (var node in document.Descendants().Where(e => e.Name.LocalName != "Entity"))
            {
                var text = node.Value.Trim();
                if (!GuidPattern().IsMatch(text))
                {
                    continue;
                }

                definitions.TryGetValue(text, out var target);
                var source = ParentEntity(node);

                references.Add(new GuidReference(
                    SourceFile: relativePath,
                    SourcePackage: package,
                    SourceEntityGuid: source is null ? null : (string?)source.Attribute("Guid"),
                    SourceEntityName: source is null ? null : (string?)source.Attribute("Name"),
                    SourceElement: node.Name.LocalName,
                    Guid: text,
                    Resolved: target is not null,
                    NullGuid: text == NullGuid,
                    TargetGuid: target?.Guid,
                    TargetName: target?.Name,
                    TargetPackage: target?.Package,
                    TargetFile: target?.File));
            }
        }

        var summary = new AnalysisSummary(
            XmlFiles: documents.Count,
            TotalEntities: entities.Count,
            UniqueGuids: entities.Select(e => e.Guid).Distinct(StringComparer.Ordinal).Count(),
            GuidLikeReferences: references.Count,
            ResolvedReferences: references.Count(r => r.Resolved),
            NullGuidReferences: references.Count(r => r.NullGuid),
            OtherUnresolvedReferences: references.Count(r => !r.Resolved && !r.NullGuid),
            Packages: entities
                .GroupBy(e => e.Package, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new PackageEntityCount(g.Key, g.Count()))
                .ToList());

        return new AnalysisResult(entities, references, summary);
    }

    /// <summary>The package is the first path segment of the game-relative file path.</summary>
    private static string PackageOf(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    /// <summary>The nearest ancestor that is an <c>Entity</c> carrying a <c>Guid</c>.</summary>
    private static XElement? ParentEntity(XElement node) =>
        node.Ancestors().FirstOrDefault(a => a.Name.LocalName == "Entity" && a.Attribute("Guid") != null);

    /// <summary>The slash-joined names of the ancestor <c>EntityGroup</c>s (root-first).</summary>
    private static string GroupPath(XElement node) =>
        string.Join("/", node.Ancestors()
            .Where(a => a.Name.LocalName == "EntityGroup" && a.Attribute("Name") != null)
            .Select(a => (string)a.Attribute("Name")!)
            .Reverse());
}
