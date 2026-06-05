using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Paker;

/// <summary>
/// Decides which of the four shapes (<see cref="PakKinds"/>) a given pak
/// matches, by reading its index, locating the single <c>&lt;m&gt;/manifest.json</c>
/// inside (if any), checking for module-side files (<c>files.json</c>,
/// <c>&lt;m&gt;/&lt;m&gt;.gd.bin</c>), counting popmaps under <c>&lt;m&gt;/usermaps/</c>,
/// and noting any entries that live at the pak root (the Pattern B
/// <c>system.json</c> override pattern).
///
/// The classifier never opens the on-disk pak data beyond what
/// <see cref="PakReader"/> already does — index + the bytes of the one
/// manifest.json entry are enough.
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
                PakKinds.Unknown, Name: null, ModuleFolder: null,
                Dependencies: [], HasGdBin: false, PopmapCount: 0,
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
                PakKinds.Unknown, Name: null, ModuleFolder: null,
                Dependencies: [], HasGdBin: false, PopmapCount: 0,
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

        // Inventory the module's contributions. Both files.json and the
        // .gd.bin can live either under <m>/ (the common shipped layout) or
        // at the pak root (tools.pak puts its .gd.bin at the root while
        // keeping files.json under tools/). Accept either location.
        var hasFilesJson = readResult.Index.Entries.Any(e =>
            e.Filename == $"{moduleFolder}/files.json" || e.Filename == "files.json");
        var hasGdBin = readResult.Index.Entries.Any(e =>
            e.Filename == $"{moduleFolder}/{moduleFolder}.gd.bin"
            || e.Filename == $"{moduleFolder}.gd.bin");
        var usermapsPrefix = $"{moduleFolder}/usermaps/";
        var popmapCount = readResult.Index.Entries.Count(e =>
            e.Filename.StartsWith(usermapsPrefix, StringComparison.Ordinal)
            && e.Filename.EndsWith(".popmap", StringComparison.OrdinalIgnoreCase));

        var overridesAtRoot = CollectOverridesAtRoot(readResult.Index, moduleFolder);

        // Decision: GameDatabase contribution wins (module), then popmap (user-map),
        // then anything else with a manifest is treated as overlay. Order matters
        // for paks that mix shapes (e.g. a future campaign mod with new buildings
        // AND user maps would classify as "module" so its rule additions take
        // priority over the map browser surface).
        string kind;
        if (hasFilesJson && hasGdBin)
        {
            kind = PakKinds.Module;
        }
        else if (popmapCount > 0)
        {
            kind = PakKinds.UserMap;
        }
        else
        {
            kind = PakKinds.Overlay;
        }

        diagnostics.Add(new PakDiagnostic(
            PakDiagnosticSeverity.Info,
            DiagnosticCodes.PakClassified,
            $"Classified as '{kind}' (module='{moduleFolder}', name='{name ?? "?"}', " +
            $"gdbin={(hasGdBin ? "yes" : "no")}, popmaps={popmapCount}, " +
            $"overridesAtRoot={overridesAtRoot.Count}, deps=[{string.Join(", ", dependencies)}]).",
            Path: moduleFolder));

        return new PakClassifyResult(
            kind, name, moduleFolder, dependencies, hasGdBin, popmapCount,
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
