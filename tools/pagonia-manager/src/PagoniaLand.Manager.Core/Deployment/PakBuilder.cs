using System.Text.Json;
using System.Text.Json.Nodes;
using PagoniaLand.Paker;

namespace PagoniaLand.Manager;

public sealed class PakBuildResult
{
    public bool Success { get; init; }
    public string? PakPath { get; init; }
    public int EntryCount { get; init; }
    public long ByteSize { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class PakBuilder
{
    // Builds <outputPakPath> from the loose-file scaffold at <scaffoldRoot>/<scaffoldName>/.
    // The scaffold layout (manifest.json + files.json + <name>.gd.bin + memory.bin + any
    // *.gd.xml dropped in by the patcher) is what PakScaffoldWriter from Patcher.Core
    // produces during apply.
    //
    // Implementation: synthesise a transient pakinfo.json that PakPacker can consume, with
    // every file as an entry whose filename is relative to <scaffoldRoot> (so the in-pak
    // paths come out as `<scaffoldName>/<rel>`). Compression: every entry is gzipped — the
    // engine reads gzipped entries transparently and shipped paks use compression.
    public PakBuildResult Build(string scaffoldRoot, string scaffoldName, string outputPakPath, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var scaffoldDir = Path.Combine(scaffoldRoot, scaffoldName);

        if (!Directory.Exists(scaffoldDir))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.PakScaffoldMissing,
                $"Pattern B scaffold '{scaffoldDir}' is missing — PatchApplier didn't write it. " +
                "Check that the mod's `pak.name` matches and that the scaffold step succeeded."));
            return new PakBuildResult { Diagnostics = diagnostics };
        }

        var files = Directory.EnumerateFiles(scaffoldDir, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.PakBuildFailed,
                $"Scaffold '{scaffoldDir}' is empty — nothing to pack."));
            return new PakBuildResult { Diagnostics = diagnostics };
        }

        var pakInfoNode = new JsonObject
        {
            ["version"] = 2,
            ["count"] = files.Count,
            ["entries"] = BuildEntriesArray(scaffoldRoot, files),
        };

        var pakInfoPath = Path.Combine(scaffoldRoot, $".pak-build-{scaffoldName}.json");
        File.WriteAllText(pakInfoPath, pakInfoNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPakPath))!);
            var packerDiagnostics = new PakPacker().Pack(pakInfoPath, outputPakPath, PakFilter.All, jobs: 1, cancellationToken);
            diagnostics.AddRange(packerDiagnostics.Select(ManagerDiagnostic.From));

            if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
            {
                return new PakBuildResult { Diagnostics = diagnostics };
            }

            var byteSize = new FileInfo(outputPakPath).Length;
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.PakBuildSucceeded,
                $"Built '{outputPakPath}' ({files.Count} entries, {byteSize} bytes) from scaffold '{scaffoldName}'."));

            return new PakBuildResult
            {
                Success = true,
                PakPath = outputPakPath,
                EntryCount = files.Count,
                ByteSize = byteSize,
                Diagnostics = diagnostics,
            };
        }
        finally
        {
            try { if (File.Exists(pakInfoPath)) File.Delete(pakInfoPath); }
            catch (IOException) { }
        }
    }

    private static JsonArray BuildEntriesArray(string scaffoldRoot, List<string> files)
    {
        var entries = new JsonArray();
        for (var i = 0; i < files.Count; i++)
        {
            var relative = Path.GetRelativePath(scaffoldRoot, files[i]).Replace('\\', '/');
            entries.Add(new JsonObject
            {
                ["index"] = i,
                ["pos"] = i,
                ["compressed"] = true,
                ["filename"] = relative,
                ["begin"] = 0,
                ["end"] = 0,
                ["size"] = 0,
                ["size_compressed"] = 0,
            });
        }
        return entries;
    }
}
