using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Patcher;

/// <summary>
/// Writes the four metadata files the engine needs to recognise a folder
/// inside an unpacked pak as a Pattern B overlay module:
/// <list type="bullet">
///   <item><description><c>&lt;Name&gt;/manifest.json</c> — Name, Summary, Author, Image, Dependencies.</description></item>
///   <item><description><c>&lt;Name&gt;/files.json</c> — pointer table. Maps the <c>GameDatabase</c> key to the module's <c>.gd.bin</c> when the module ships <c>*.gd.xml</c>, and the <c>Localization</c> key to the module's <c>localization/</c> folder <b>only when that folder actually has compiled <c>loca_&lt;lang&gt;.bin</c> content</b>. Written when at least one such resource is present; a config-only or asset-only overlay with neither skips it (matches the System / camera-zoom mod from mod.io). NOTE: the 1.4.0 editor instead emits the <c>Localization</c> key <i>unconditionally</i> whenever it writes <c>files.json</c> — pointing at a non-existent <c>localization/</c> folder when the module ships no loca (confirmed against the CatDog / "Eye of The Spire" mod.io mods). We deliberately diverge to avoid writing a dangling pointer; both shapes load.</description></item>
///   <item><description><c>&lt;Name&gt;/&lt;Name&gt;.gd.bin</c> — index listing every <c>*.gd.xml</c> the module ships, in ordinal order. Only written when at least one <c>*.gd.xml</c> is present. Uses <see cref="PagoniaLand.Paker.GdBinWriter"/> from Paker.Core so the byte format stays canonical.</description></item>
///   <item><description><c>&lt;Name&gt;/memory.bin</c> — 28-byte opaque blob. Shipped paks contain non-zero values that look like per-category memory-allocation stats; a fresh scaffold writes 28 zero bytes, which the engine appears to tolerate for new modules. Manual in-game smoke remains the validation step until proven otherwise.</description></item>
/// </list>
/// The walker reads <c>&lt;outputGameRoot&gt;/&lt;Name&gt;/**/*.gd.xml</c> after
/// the patch + entry-ops pass has written everything, so newly-added XML
/// entries from <c>entries.add</c> are picked up automatically.
/// </summary>
public sealed class PakScaffoldWriter
{
    public IReadOnlyList<PatchDiagnostic> Write(string outputGameRoot, PakMetadata pak)
    {
        ArgumentNullException.ThrowIfNull(outputGameRoot);
        ArgumentNullException.ThrowIfNull(pak);

        var diagnostics = new List<PatchDiagnostic>();

        if (string.IsNullOrWhiteSpace(pak.Name))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ScaffoldNameMissing,
                "Pattern B overlay scaffold requires `pak.name` in the mod manifest."));
            return diagnostics;
        }

        var name = pak.Name.Trim();
        if (name.Contains('/') || name.Contains('\\') || name.Contains(".."))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.ScaffoldNameInvalid,
                $"`pak.name` must be a single path segment without slashes or '..' (got '{pak.Name}')."));
            return diagnostics;
        }

        var moduleDir = System.IO.Path.Combine(outputGameRoot, name);
        Directory.CreateDirectory(moduleDir);

        var dependencies = pak.Dependencies.Count > 0
            ? new List<string>(pak.Dependencies)
            : ["core"];

        // 1. manifest.json
        var manifestJson = new ManifestJson(
            Name: name,
            Summary: pak.Summary,
            Author: pak.Author,
            Image: pak.Image,
            Dependencies: dependencies);
        var manifestPath = System.IO.Path.Combine(moduleDir, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifestJson, PakScaffoldJsonContext.Default.ManifestJson));

        // 2. files.json — written when the module contributes any resource the
        //    engine reaches through it: GameDatabase XML and/or a localization
        //    folder. Each present resource adds one pointer entry; an asset-only
        //    overlay (no XML, no localization) skips the file, like System.pak.
        var gdXmlPaths = CollectGdXmlPaths(outputGameRoot, moduleDir);
        var hasLocalization = LocalizationFolderHasContent(moduleDir);

        var fileEntries = new List<FilesJsonEntry>();
        if (gdXmlPaths.Count > 0)
        {
            fileEntries.Add(new FilesJsonEntry(Key: "GameDatabase", Paths: [$"{name}/{name}.gd.bin"]));
        }
        if (hasLocalization)
        {
            // Matches the shipped paks (dlc1, …) and EE's editor mods: the
            // Localization key points at the folder, not the individual
            // loca_<lang>.bin files inside it.
            fileEntries.Add(new FilesJsonEntry(Key: "Localization", Paths: [$"{name}/localization"]));
        }

        if (fileEntries.Count > 0)
        {
            var filesJson = new FilesJson(Files: fileEntries);
            var filesPath = System.IO.Path.Combine(moduleDir, "files.json");
            File.WriteAllText(filesPath, JsonSerializer.Serialize(filesJson, PakScaffoldJsonContext.Default.FilesJson));
        }

        // 3. <name>.gd.bin — only when *.gd.xml is present (the index lists them).
        if (gdXmlPaths.Count > 0)
        {
            var index = PagoniaLand.Paker.GdBinIndex.CreateEmpty();
            foreach (var path in gdXmlPaths)
            {
                index = index.WithEntryAdded(path);
            }
            index = index.WithComputedHeader();

            using var gdBinStream = File.Create(System.IO.Path.Combine(moduleDir, $"{name}.gd.bin"));
            new PagoniaLand.Paker.GdBinWriter().Write(gdBinStream, index);
        }

        // 4. memory.bin — 28 zero bytes. Shipped paks carry non-zero allocation
        // stats here; a fresh scaffold writes zeros and trusts the engine to
        // either ignore the blob or repopulate it on first load.
        File.WriteAllBytes(System.IO.Path.Combine(moduleDir, "memory.bin"), new byte[28]);

        var resourceSummary = (gdXmlPaths.Count, hasLocalization) switch
        {
            ( > 0, true) => $"{gdXmlPaths.Count} *.gd.xml + localization registered",
            ( > 0, false) => $"{gdXmlPaths.Count} *.gd.xml registered",
            (_, true) => "localization registered; no *.gd.xml, .gd.bin skipped",
            _ => "no *.gd.xml or localization; files.json + .gd.bin skipped",
        };
        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.ScaffoldWritten,
            $"Wrote Pattern B scaffold for '{name}' ({resourceSummary}).",
            moduleDir));

        return diagnostics;
    }

    /// <summary>
    /// Whether the module ships a non-empty <c>localization/</c> folder (at least
    /// one compiled <c>loca_&lt;lang&gt;.bin</c> or other file). Shipped paks point
    /// the <c>files.json</c> <c>Localization</c> key at this folder, so the scaffold
    /// adds that pointer whenever the author placed localization content there.
    /// </summary>
    private static bool LocalizationFolderHasContent(string moduleDir)
    {
        var localizationDir = System.IO.Path.Combine(moduleDir, "localization");
        return Directory.Exists(localizationDir)
            && Directory.EnumerateFiles(localizationDir, "*", SearchOption.AllDirectories).Any();
    }

    /// <summary>
    /// Walk the module directory for every <c>*.gd.xml</c>, return them as
    /// in-pak paths (relative to <paramref name="outputGameRoot"/>, forward
    /// slashes, sorted ordinally for determinism). Files outside the module
    /// directory are not included even if they exist in the output tree.
    /// </summary>
    private static List<string> CollectGdXmlPaths(string outputGameRoot, string moduleDir)
    {
        var result = new List<string>();
        if (!Directory.Exists(moduleDir)) return result;

        foreach (var file in Directory.EnumerateFiles(moduleDir, "*.gd.xml", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(outputGameRoot, file).Replace('\\', '/');
            result.Add(relative);
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    internal sealed record ManifestJson(
        string Name,
        string Summary,
        string Author,
        string Image,
        List<string> Dependencies);

    internal sealed record FilesJson(List<FilesJsonEntry> Files);

    internal sealed record FilesJsonEntry(string Key, List<string> Paths);
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PakScaffoldWriter.ManifestJson))]
[JsonSerializable(typeof(PakScaffoldWriter.FilesJson))]
internal sealed partial class PakScaffoldJsonContext : JsonSerializerContext;
