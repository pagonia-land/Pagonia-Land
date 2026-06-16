using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// Inspects a pak and reports what it contributes — independent signals, not a
/// single "kind" label: a compiled GameDatabase (<c>&lt;m&gt;.gd.bin</c>), maps
/// (<c>&lt;m&gt;/usermaps/*.popmap</c>), and any root-level file overrides (the
/// Pattern B <c>system.json</c> pattern), plus the module's name and
/// dependencies. A pak can do several at once (a published editor map ships a
/// GameDatabase and a map), so the signals are reported separately rather than
/// collapsed into one mutually-exclusive bucket.
///
/// It never opens the on-disk pak data beyond what <see cref="PakReader"/>
/// already does — the index + the bytes of the one manifest.json entry are enough.
/// </summary>
public sealed class PakClassifier
{
    private readonly PakReader _reader = new();

    public PakClassifyResult Classify(Stream pakStream)
    {
        ArgumentNullException.ThrowIfNull(pakStream);
        var diagnostics = new List<PakDiagnostic>();

        var readResult = _reader.OpenIndex(pakStream);
        diagnostics.AddRange(readResult.Diagnostics);
        if (!readResult.Success || readResult.Index is null)
        {
            return new PakClassifyResult(
                Name: null, ModuleFolder: null,
                Dependencies: [], GdbScopes: [], PopmapCount: 0,
                OverridesAtRoot: [], Diagnostics: diagnostics);
        }

        // Identify candidate module folders: any entry whose path is
        // exactly "<segment>/manifest.json" with no further slashes in
        // <segment>. Most paks have exactly one; pak with zero is unknown
        // / config-only; pak with multiple is reported but we still classify
        // by the alphabetically-first one so downstream tools have something
        // to work with.
        var moduleFolders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in readResult.Index.Entries)
        {
            var folder = TryExtractModuleFolderFromManifestPath(entry.Filename);
            if (folder is not null) moduleFolders.Add(folder);
        }

        if (moduleFolders.Count == 0)
        {
            // No module manifest found. Surface any root-level files anyway so
            // tooling that wants to look at a "naked" pak still gets useful info.
            var rootOverrides = CollectOverridesAtRoot(readResult.Index, moduleFolder: null);
            return new PakClassifyResult(
                Name: null, ModuleFolder: null,
                Dependencies: [], GdbScopes: [], PopmapCount: 0,
                OverridesAtRoot: rootOverrides, Diagnostics: diagnostics);
        }

        var moduleFolder = moduleFolders.Min!;
        if (moduleFolders.Count > 1)
        {
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Warning,
                DiagnosticCodes.ClassifyMultipleModules,
                $"Pak declares {moduleFolders.Count} module folders ({string.Join(", ", moduleFolders)}); " +
                $"classifying by the alphabetically first one ('{moduleFolder}'). Shipped paks ship exactly one."));
        }

        // Read manifest.json bytes through the pak reader and parse for Name + Dependencies.
        var manifestEntry = readResult.Index.Entries.First(e => e.Filename == $"{moduleFolder}/manifest.json");
        string? name = null;
        IReadOnlyList<string> dependencies = [];
        try
        {
            using var manifestBytes = new MemoryStream();
            _reader.ExtractEntry(pakStream, manifestEntry, manifestBytes);
            manifestBytes.Position = 0;
            var manifest = JsonSerializer.Deserialize(manifestBytes, PakClassifyManifestJsonContext.Default.PakManifestJson);
            if (manifest is not null)
            {
                name = manifest.Name;
                dependencies = manifest.Dependencies ?? [];
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            diagnostics.Add(new PakDiagnostic(
                PakDiagnosticSeverity.Warning,
                DiagnosticCodes.ClassifyManifestUnreadable,
                $"Could not parse '{moduleFolder}/manifest.json': {ex.Message}",
                Path: $"{moduleFolder}/manifest.json"));
        }

        // Inventory the module's contributions, by SCOPE of the GameDatabase
        // content. A gd.bin lists the *.gd.xml resources the module ships, so an
        // empty gd.bin (the editor emits a module-level one even for a map-only
        // mod) lists nothing and is correctly NOT counted as content.
        var usermapsPrefix = $"{moduleFolder}/usermaps/";
        var scopes = new List<string>();

        // global: a module-level <m>.gd.bin (under <m>/ as shipped paks do, or at
        // the pak root as tools.pak does) that actually lists at least one resource.
        bool ModuleGdBinHasContent(string filename) =>
            readResult.Index.Entries.FirstOrDefault(e => e.Filename == filename) is { } e
            && _reader.GdBinHasEntries(pakStream, e);
        if (ModuleGdBinHasContent($"{moduleFolder}/{moduleFolder}.gd.bin")
            || ModuleGdBinHasContent($"{moduleFolder}.gd.bin"))
        {
            scopes.Add("global");
        }

        // map-scoped: a <m>/usermaps/*.gd.bin with entities, or a raw
        // <m>/usermaps/*.gd.xml — the per-map "hosted game database".
        var hasMapScopedGdb = readResult.Index.Entries.Any(e =>
            e.Filename.StartsWith(usermapsPrefix, StringComparison.Ordinal)
            && e.Filename.EndsWith(".gd.bin", StringComparison.OrdinalIgnoreCase)
            && _reader.GdBinHasEntries(pakStream, e));
        var hasMapScopedXml = readResult.Index.Entries.Any(e =>
            e.Filename.StartsWith(usermapsPrefix, StringComparison.Ordinal)
            && e.Filename.EndsWith(".gd.xml", StringComparison.OrdinalIgnoreCase));
        if (hasMapScopedGdb || hasMapScopedXml)
        {
            scopes.Add("map-scoped");
        }

        var popmapCount = readResult.Index.Entries.Count(e =>
            e.Filename.StartsWith(usermapsPrefix, StringComparison.Ordinal)
            && e.Filename.EndsWith(".popmap", StringComparison.OrdinalIgnoreCase));

        var overridesAtRoot = CollectOverridesAtRoot(readResult.Index, moduleFolder);

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakClassified,
            $"Inspected module '{moduleFolder}' (name='{name ?? "?"}', " +
            $"gdb=[{string.Join(", ", scopes)}], popmaps={popmapCount}, " +
            $"overridesAtRoot={overridesAtRoot.Count}, deps=[{string.Join(", ", dependencies)}]).",
            Path: moduleFolder));

        return new PakClassifyResult(
            name, moduleFolder, dependencies, scopes, popmapCount,
            overridesAtRoot, diagnostics);
    }

    private static string? TryExtractModuleFolderFromManifestPath(string entryName)
    {
        const string suffix = "/manifest.json";
        if (!entryName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
        var prefix = entryName[..^suffix.Length];
        // The prefix must be a single path segment (no slashes inside).
        if (prefix.Length == 0 || prefix.Contains('/') || prefix.Contains('\\')) return null;
        return prefix;
    }

    private static List<string> CollectOverridesAtRoot(PakIndex index, string? moduleFolder)
    {
        var result = new List<string>();
        var moduleGdBinAtRoot = moduleFolder is null ? null : $"{moduleFolder}.gd.bin";
        foreach (var entry in index.Entries)
        {
            if (entry.Filename.Contains('/', StringComparison.Ordinal)) continue;
            // Don't list entries that are conceptually part of the module skeleton
            // even though they live at the root: tools.pak puts its <m> folder marker,
            // <m>.gd.bin, and files.json at the pak root. Those are how a shipped pak
            // ships, not Pattern-B overrides. (system.json is intentionally NOT filtered
            // here — it is a real engine-wide override and should be listed.)
            if (moduleFolder is not null && entry.Filename.Equals(moduleFolder, StringComparison.Ordinal)) continue;
            if (moduleGdBinAtRoot is not null && entry.Filename.Equals(moduleGdBinAtRoot, StringComparison.Ordinal)) continue;
            if (entry.Filename.Equals("files.json", StringComparison.Ordinal)) continue;
            result.Add(entry.Filename);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}

internal sealed class PakManifestJson
{
    public string? Name { get; init; }
    public string? Summary { get; init; }
    public string? Author { get; init; }
    public string? Image { get; init; }
    public List<string>? Dependencies { get; init; }
}

// Shipped paks (core, dlc1, decorations1, tools) plus mod.io paks all use
// PascalCase keys in manifest.json (Name, Summary, ...), which matches our
// model property names exactly — no case-insensitive option needed.
[JsonSerializable(typeof(PakManifestJson))]
internal sealed partial class PakClassifyManifestJsonContext : JsonSerializerContext;
