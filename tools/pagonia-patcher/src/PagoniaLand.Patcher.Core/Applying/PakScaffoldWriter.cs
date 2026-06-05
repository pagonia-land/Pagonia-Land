using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Patcher;

/// <summary>
/// Writes the four metadata files the engine needs to recognise a folder
/// inside an unpacked pak as a Pattern B overlay module:
/// <list type="bullet">
///   <item><description><c>&lt;Name&gt;/manifest.json</c> — Name, Summary, Author, Image, Dependencies.</description></item>
///   <item><description><c>&lt;Name&gt;/files.json</c> — pointer table that maps the <c>GameDatabase</c> key to the module's <c>.gd.bin</c>. Only written when the module actually ships <c>*.gd.xml</c> files; config-only or asset-only overlays skip it (matches the System / camera-zoom mod from mod.io).</description></item>
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

        // 2. files.json + 3. <name>.gd.bin — only when *.gd.xml is present.
        var gdXmlPaths = CollectGdXmlPaths(outputGameRoot, moduleDir);
        if (gdXmlPaths.Count > 0)
        {
            var gdBinRelativePath = $"{name}/{name}.gd.bin";

            var filesJson = new FilesJson(
                Files: [new FilesJsonEntry(Key: "GameDatabase", Paths: [gdBinRelativePath])]);
            var filesPath = System.IO.Path.Combine(moduleDir, "files.json");
            File.WriteAllText(filesPath, JsonSerializer.Serialize(filesJson, PakScaffoldJsonContext.Default.FilesJson));

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

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.ScaffoldWritten,
            gdXmlPaths.Count > 0
                ? $"Wrote Pattern B scaffold for '{name}' ({gdXmlPaths.Count} *.gd.xml registered)."
                : $"Wrote Pattern B scaffold for '{name}' (no *.gd.xml; files.json + .gd.bin skipped).",
            moduleDir));

        return diagnostics;
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
