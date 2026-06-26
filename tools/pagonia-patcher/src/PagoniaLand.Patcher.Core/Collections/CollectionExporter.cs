using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class CollectionExporter
{
    private readonly ManifestReader _reader = new();
    private readonly ISerializer _serializer = PatcherYaml.CreateSerializer();

    public ReadResult<CollectionManifest> Export(
        IReadOnlyList<string> modDirectories,
        CollectionExportOptions options)
    {
        var diagnostics = new List<PatchDiagnostic>();
        var loadedMods = new List<LoadedMod>();

        if (modDirectories.Count == 0)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionExportNoMods,
                "At least one mod directory is required to export a collection."));
        }

        foreach (var modDirectory in modDirectories)
        {
            var result = _reader.ReadMod(modDirectory);
            diagnostics.AddRange(result.Diagnostics);

            if (result.Value is not null)
            {
                loadedMods.Add(result.Value);
            }
        }

        var duplicateModIds = loadedMods
            .GroupBy(mod => mod.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicateModId in duplicateModIds)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionExportDuplicateMod,
                $"Cannot export collection with duplicate mod id '{duplicateModId}'."));
        }

        var gameDatabaseVersions = loadedMods
            .Select(mod => mod.Manifest.GameDatabaseVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (gameDatabaseVersions.Count > 1 && string.IsNullOrWhiteSpace(options.GameDatabaseVersion))
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionExportMixedGameDatabaseVersions,
                $"Cannot infer one GameDatabase version from mods with different versions: {string.Join(", ", gameDatabaseVersions)}."));
        }
        else if (gameDatabaseVersions.Count > 0 && !string.IsNullOrWhiteSpace(options.GameDatabaseVersion))
        {
            foreach (var mod in loadedMods.Where(mod => !string.Equals(mod.Manifest.GameDatabaseVersion, options.GameDatabaseVersion, StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Warning,
                    DiagnosticCodes.CollectionExportGameDatabaseOverride,
                    $"Mod '{mod.Manifest.Id}' targets GameDatabase '{mod.Manifest.GameDatabaseVersion}', but the exported collection uses '{options.GameDatabaseVersion}'."));
            }
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error))
        {
            return ReadResult<CollectionManifest>.Failed(diagnostics.ToArray());
        }

        if (loadedMods.Count == 0)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                DiagnosticCodes.CollectionExportNoLoadedMods,
                "Cannot export a collection because no mods were loaded successfully."));
            return ReadResult<CollectionManifest>.Failed(diagnostics.ToArray());
        }

        var gameDatabaseVersion = string.IsNullOrWhiteSpace(options.GameDatabaseVersion)
            ? loadedMods[0].Manifest.GameDatabaseVersion
            : options.GameDatabaseVersion;

        var collection = new CollectionManifest
        {
            CollectionFormatVersion = FormatVersionPolicy.CurrentVersion(ManagedFormat.Collection),
            Id = options.Id,
            Name = options.Name,
            Version = string.IsNullOrWhiteSpace(options.Version) ? "0.1.0" : options.Version,
            Author = string.IsNullOrWhiteSpace(options.Author) ? "Pagonia Land" : options.Author,
            GameDatabaseVersion = gameDatabaseVersion,
            Description = string.IsNullOrWhiteSpace(options.Description) ? "Exported local mod set." : options.Description,
            ConflictPolicy = string.IsNullOrWhiteSpace(options.ConflictPolicy) ? "strict" : options.ConflictPolicy,
            Mods = loadedMods
                .Select(mod => new CollectionMod
                {
                    Id = mod.Manifest.Id,
                    Version = mod.Manifest.Version,
                    Source = NormalizePath(mod.Directory),
                    Required = true,
                    Enabled = true,
                    RequiresPackages = mod.Manifest.RequiredPackages.ToList(),
                })
                .ToList(),
            LoadOrder = loadedMods
                .Select(mod => mod.Manifest.Id)
                .ToList(),
        };

        diagnostics.Add(new PatchDiagnostic(
            PatchDiagnosticSeverity.Info,
            DiagnosticCodes.CollectionExportReady,
            $"Exported collection '{collection.Id}' with {collection.Mods.Count} mod(s)."));

        return ReadResult<CollectionManifest>.Ok(collection, diagnostics.ToArray());
    }

    public void WriteCollection(CollectionManifest collection, string path)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, _serializer.Serialize(collection));
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}

public sealed record CollectionExportOptions(
    string Id,
    string Name,
    string Version,
    string Author,
    string? GameDatabaseVersion,
    string Description,
    string ConflictPolicy);
