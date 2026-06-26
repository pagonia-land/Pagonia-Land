using System.Xml;
using System.Xml.Linq;

namespace PagoniaLand.Patcher;

/// <summary>
/// A read-only index of a reference GameDatabase (core/dlc, an unpacked game
/// root): every entity by GUID, plus the set of GUIDs referenced anywhere in
/// that set. The C# analogue of <c>scripts/analyze_database.ps1</c> for the
/// base-aware authoring advisor — it additionally scans element *attribute* values
/// for GUID references, not just leaf text. XLinq only, no reflection — AOT-clean.
///
/// It powers two checks the base-free advisor can't make on its own:
/// whether an entity a mod wants to <c>Unload</c> is still referenced by the
/// shipped database, and what an inherited entity looks like so a wholesale
/// <c>Replace</c> can be compared against it.
/// </summary>
public sealed class ReferenceGdbIndex
{
    private readonly Dictionary<string, XElement> _entitiesByGuid;
    private readonly HashSet<string> _referencedGuids;

    private ReferenceGdbIndex(Dictionary<string, XElement> entitiesByGuid, HashSet<string> referencedGuids)
    {
        _entitiesByGuid = entitiesByGuid;
        _referencedGuids = referencedGuids;
    }

    /// <summary>Number of distinct entities indexed (for diagnostics/logging).</summary>
    public int EntityCount => _entitiesByGuid.Count;

    /// <summary>Loads every <c>*.gd.xml</c> under a game root into the index.</summary>
    public static ReferenceGdbIndex Load(string gameRoot)
    {
        var entitiesByGuid = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        var referencedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(gameRoot))
        {
            return new ReferenceGdbIndex(entitiesByGuid, referencedGuids);
        }

        // The enumeration itself is lazy: an IOException / access error can surface mid-iteration
        // (e.g. a subtree becomes unreadable). Catch around the loop so we keep whatever was indexed
        // so far rather than aborting the whole advisory pass — consistent with the per-file skip below.
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(gameRoot, "*.gd.xml", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            return new ReferenceGdbIndex(entitiesByGuid, referencedGuids);
        }

        foreach (var path in paths)
        {
            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (Exception ex) when (ex is XmlException or System.IO.IOException or UnauthorizedAccessException)
            {
                // A malformed, locked, or unreadable reference file is skipped rather than
                // aborting the whole advisory pass; the base-free rules still apply.
                continue;
            }

            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (string.Equals(attribute.Name.LocalName, "Guid", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddIfGuid(referencedGuids, attribute.Value);
                }

                if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
                {
                    AddIfGuid(referencedGuids, element.Value);
                }

                if (string.Equals(element.Name.LocalName, "Entity", StringComparison.Ordinal))
                {
                    var guid = (string?)element.Attribute("Guid");
                    if (!string.IsNullOrWhiteSpace(guid))
                    {
                        // First definition wins; the shipped DB has no duplicate GUIDs.
                        _ = entitiesByGuid.TryAdd(guid, element);
                    }
                }
            }
        }

        return new ReferenceGdbIndex(entitiesByGuid, referencedGuids);
    }

    /// <summary>True when <paramref name="guid"/> is referenced by some entity in the reference set.
    /// The argument is trimmed before the lookup because the set stores trimmed values (via
    /// <c>AddIfGuid</c>), so a whitespace-padded <c>InheritedGuid</c> still matches.</summary>
    public bool IsReferenced(string? guid)
        => !string.IsNullOrWhiteSpace(guid) && _referencedGuids.Contains(guid.Trim());

    /// <summary>Gets the inherited entity element for <paramref name="guid"/>, if the reference set
    /// defines it. The argument is trimmed before the lookup — symmetric with <see cref="IsReferenced"/>
    /// — because the dictionary keys are the reference DB's unpadded GUIDs, so a whitespace-padded
    /// <c>InheritedGuid</c> still matches (otherwise the Replace-could-be-Incremental advisory goes
    /// silently quiet while the Unload check still fires).</summary>
    public XElement? GetEntity(string? guid)
        => !string.IsNullOrWhiteSpace(guid) && _entitiesByGuid.TryGetValue(guid.Trim(), out var element)
            ? element
            : null;

    private static void AddIfGuid(HashSet<string> set, string value)
    {
        var trimmed = value.Trim();
        // Skip the all-zero sentinel GUID: it parses, but the DB uses it as a "no reference"
        // placeholder, not a real reference (mirrors analyze_database.ps1's nullGuid handling).
        if (Guid.TryParseExact(trimmed, "D", out var parsed) && parsed != Guid.Empty)
        {
            set.Add(trimmed);
        }
    }
}
