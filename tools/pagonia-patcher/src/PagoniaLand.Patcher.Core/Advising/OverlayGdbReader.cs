using System.Xml;
using System.Xml.Linq;

namespace PagoniaLand.Patcher;

/// <summary>
/// Reads a mod's own hand-authored overlay <c>*.gd.xml</c> files into an
/// <see cref="OverlayGdbModel"/> for the <see cref="EntityRelationAdvisor"/>.
/// This is intentionally read-only and tolerant: a missing or malformed
/// overlay file yields a warning diagnostic rather than throwing, so
/// <c>validate-mod</c> still completes.
/// </summary>
public sealed class OverlayGdbReader
{
    /// <summary>
    /// Discovers the overlay <c>*.gd.xml</c> files a mod ships through its
    /// <c>entries.add</c> / <c>entries.replace</c> source mappings (the
    /// authoritative list the apply step uses), resolved relative to the mod
    /// directory, and reads them.
    /// </summary>
    public static OverlayGdbModel ReadFromMod(LoadedMod mod)
    {
        var files = new List<string>();
        var entries = mod.Manifest.Entries;
        if (entries is not null)
        {
            foreach (var add in entries.Add)
            {
                AddIfGdXml(mod.Directory, add.Source, files);
            }

            foreach (var replace in entries.Replace)
            {
                AddIfGdXml(mod.Directory, replace.Source, files);
            }
        }

        return ReadFiles(files);
    }

    private static void AddIfGdXml(string modDirectory, string source, List<string> files)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        if (!source.EndsWith(".gd.xml", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        files.Add(Path.Combine(modDirectory, source));
    }

    /// <summary>
    /// Reads an explicit set of <c>*.gd.xml</c> files. Used directly by the
    /// dlc1 calibration test (which points the advisor at shipped content
    /// rather than a mod folder).
    /// </summary>
    public static OverlayGdbModel ReadFiles(IEnumerable<string> paths)
    {
        var entities = new List<OverlayEntity>();
        var referenceValues = new List<string>();
        var diagnostics = new List<PatchDiagnostic>();

        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Warning,
                    DiagnosticCodes.OverlayGdbFileMissing,
                    $"Overlay gd.xml declared by the mod was not found: {path}",
                    path));
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (XmlException exception)
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Warning,
                    DiagnosticCodes.OverlayGdbUnreadable,
                    $"Could not parse overlay gd.xml: {exception.Message}",
                    path));
                continue;
            }

            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    // The Guid attribute *defines* an entity; every other
                    // attribute value (incl. InheritedGuid) is a potential
                    // reference to some other entity.
                    if (string.Equals(attribute.Name.LocalName, "Guid", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    referenceValues.Add(attribute.Value);
                }

                // Leaf text can hold a GUID reference (e.g. <SomeRef>guid</SomeRef>).
                // Skip containers — XElement.Value would concatenate all descendants.
                if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                {
                    referenceValues.Add(element.Value);
                }

                if (string.Equals(element.Name.LocalName, "Entity", StringComparison.Ordinal))
                {
                    entities.Add(new OverlayEntity(
                        (string?)element.Attribute("Guid"),
                        (string?)element.Attribute("Name"),
                        (string?)element.Attribute("InheritanceMode"),
                        (string?)element.Attribute("InheritedGuid"),
                        path,
                        element));
                }
            }
        }

        return new OverlayGdbModel(entities, referenceValues, diagnostics);
    }
}
