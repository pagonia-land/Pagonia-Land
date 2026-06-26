using System.Xml.Linq;
using PagoniaLand.Catalog.Model;
using PagoniaLand.Paker;

namespace PagoniaLand.Catalog;

/// <summary>
/// Reads a GameDatabase from a path that may be a live install (<c>&lt;root&gt;/pak/*.pak</c>),
/// a folder of <c>*.pak</c> files, or a pre-extracted <c>game-gdb</c> layout — extracting the
/// <c>*.gd.xml</c> from paks in-memory via <see cref="PakReader"/> when needed. This is the
/// "point at your install, done" path; it never writes or publishes anything.
/// </summary>
public sealed class GameInstallReader
{
    private const string PakFolderName = "pak";
    private const string GdXmlSuffix = ".gd.xml";

    private readonly PakReader _pak = new();
    private readonly GameDatabaseReader _reader = new();

    /// <summary>Read the install at <paramref name="root"/> into a <see cref="GameDatabase"/>.</summary>
    public GameDatabase Read(string root) => _reader.ReadDocuments(ReadDocuments(root));

    /// <summary>
    /// Produce the parsed <c>(game-relative path, document)</c> list for the install at
    /// <paramref name="root"/>, choosing the source by its detected layout.
    /// </summary>
    public IReadOnlyList<(string RelativePath, XDocument Document)> ReadDocuments(string root)
    {
        switch (GameInstallLocator.Detect(root))
        {
            case GameInstallKind.LiveInstall:
                return ReadPaks(Directory.EnumerateFiles(Path.Combine(root, PakFolderName), "*.pak"));
            case GameInstallKind.PakDirectory:
                return ReadPaks(Directory.EnumerateFiles(root, "*.pak"));
            case GameInstallKind.ExtractedLayout:
                return ReadExtracted(root);
            default:
                throw new DirectoryNotFoundException(
                    $"Not a recognised Pioneers of Pagonia install, pak folder, or extracted game-gdb: {root}");
        }
    }

    private IReadOnlyList<(string, XDocument)> ReadPaks(IEnumerable<string> pakPaths)
    {
        var documents = new List<(string, XDocument)>();

        foreach (var pakPath in pakPaths.OrderBy(p => p, StringComparer.Ordinal))
        {
            using var stream = File.OpenRead(pakPath);
            var result = _pak.OpenIndex(stream);
            if (result.Index is null)
            {
                continue; // unreadable pak — skip rather than fail the whole install
            }

            foreach (var entry in result.Index.Entries)
            {
                // The in-pak path already carries the package prefix, e.g.
                // "core/gdb/resources.gd.xml" — exactly the game-gdb-relative path.
                if (!entry.Filename.EndsWith(GdXmlSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var buffer = new MemoryStream();
                _pak.ExtractEntry(stream, entry, buffer);
                buffer.Position = 0;
                documents.Add((entry.Filename, XDocument.Load(buffer)));
            }
        }

        return documents;
    }

    private static IReadOnlyList<(string, XDocument)> ReadExtracted(string root)
    {
        var full = Path.GetFullPath(root);
        var documents = new List<(string, XDocument)>();
        foreach (var file in Directory
                     .EnumerateFiles(full, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            documents.Add((Path.GetRelativePath(full, file), XDocument.Load(file)));
        }

        return documents;
    }
}
