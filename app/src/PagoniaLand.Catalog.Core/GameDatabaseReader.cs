using System.Xml.Linq;
using PagoniaLand.Catalog.Model;

namespace PagoniaLand.Catalog;

/// <summary>
/// Reads the extracted <c>*.gd.xml</c> set into a <see cref="GameDatabase"/> — entities with
/// their XML elements retained, for the domain projection layer. Complements
/// <see cref="GameDatabaseAnalyzer"/> (which produces the data-only analysis summary); both
/// can run off the same parsed documents so the app parses once.
/// </summary>
public sealed class GameDatabaseReader
{
    /// <summary>Read every <c>*.xml</c> under <paramref name="gameDbRoot"/> (recursively).</summary>
    public GameDatabase Read(string gameDbRoot)
    {
        var root = Path.GetFullPath(gameDbRoot);
        var documents = new List<(string RelativePath, XDocument Document)>();
        foreach (var file in Directory
                     .EnumerateFiles(root, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            documents.Add((Path.GetRelativePath(root, file), XDocument.Load(file)));
        }

        return ReadDocuments(documents);
    }

    /// <summary>Build a <see cref="GameDatabase"/> from already-parsed documents (the testable core).</summary>
    public GameDatabase ReadDocuments(IReadOnlyList<(string RelativePath, XDocument Document)> documents)
    {
        var entities = new List<GameEntity>();
        foreach (var (relativePath, document) in documents)
        {
            var package = PackageOf(relativePath);
            foreach (var node in document.Descendants()
                         .Where(e => e.Name.LocalName == "Entity" && e.Attribute("Guid") != null))
            {
                entities.Add(new GameEntity(
                    guid: (string)node.Attribute("Guid")!,
                    name: (string?)node.Attribute("Name") ?? string.Empty,
                    package: package,
                    file: relativePath,
                    element: node));
            }
        }

        return new GameDatabase(entities);
    }

    private static string PackageOf(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }
}
