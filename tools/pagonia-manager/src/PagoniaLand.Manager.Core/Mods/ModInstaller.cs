using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public enum InstallOutcome
{
    Failed,
    Installed,
    AlreadyInstalled,
}

public sealed class InstallResult
{
    public InstallOutcome Outcome { get; init; } = InstallOutcome.Failed;
    public string? ModId { get; init; }
    public string? Version { get; init; }
    public string? ManifestName { get; init; }
    public string? InstallPath { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ModInstaller
{
    public const string SidecarFileName = ".manager-install.yaml";

    // AOT: ModInstaller WRITES InstallSidecar via YamlDotNet. ModLister has the
    // corresponding [DynamicDependency] for the READ path; this pin keeps the
    // write path safe even if a future refactor decouples the two classes.
    [DynamicDependency(
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields,
        typeof(InstallSidecar))]
    public ModInstaller()
    {
    }

    public InstallResult Install(string sourcePath, StoreLayout layout)
        => Install(sourcePath, layout, remoteSource: null);

    /// <summary>
    /// Same as <see cref="Install(string,StoreLayout)"/> but stamps the sidecar's
    /// <c>source</c> field with the resolved transport-neutral identifier of a
    /// remote fetch (e.g. <c>gh:owner/repo#&lt;sha&gt;/&lt;mod-id&gt;</c>). Pass
    /// null for purely local installs; the sidecar's <c>source</c> stays empty
    /// in that case.
    /// </summary>
    public InstallResult Install(string sourcePath, StoreLayout layout, string? remoteSource)
        => InstallAsync(sourcePath, layout, remoteSource, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Async overload of <see cref="Install(string,StoreLayout)"/> for callers
    /// (e.g. a GUI) that must not block their thread on disk IO. The synchronous
    /// <c>Install</c> overloads are thin wrappers over this. The token is honoured
    /// at the orchestration boundary (before the heavy copy); the inner copy loop
    /// stays uninterruptible for now.
    /// </summary>
    public Task<InstallResult> InstallAsync(string sourcePath, StoreLayout layout, CancellationToken cancellationToken = default)
        => InstallAsync(sourcePath, layout, remoteSource: null, cancellationToken);

    /// <summary>Async overload of <see cref="Install(string,StoreLayout,string?)"/>.</summary>
    public Task<InstallResult> InstallAsync(string sourcePath, StoreLayout layout, string? remoteSource, CancellationToken cancellationToken = default)
        => Task.Run(() => InstallCore(sourcePath, layout, remoteSource, cancellationToken), cancellationToken);

    private static InstallResult InstallCore(string sourcePath, StoreLayout layout, string? remoteSource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<ManagerDiagnostic>();

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModSourceNotFound,
                "Mod source path must not be empty."));
            return new InstallResult { Diagnostics = diagnostics };
        }

        var sourceIsDirectory = Directory.Exists(sourcePath);
        var sourceIsZip = File.Exists(sourcePath)
            && sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        if (!sourceIsDirectory && !File.Exists(sourcePath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModSourceNotFound,
                $"Mod source '{sourcePath}' does not exist."));
            return new InstallResult { Diagnostics = diagnostics };
        }

        if (!sourceIsDirectory && !sourceIsZip)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ModSourceNotAFolderOrZip,
                $"Mod source '{sourcePath}' is not a folder or .zip archive."));
            return new InstallResult { Diagnostics = diagnostics };
        }

        string validationRoot;
        string? tempExtractRoot = null;

        if (sourceIsDirectory)
        {
            validationRoot = sourcePath;
        }
        else
        {
            tempExtractRoot = Path.Combine(
                Path.GetTempPath(),
                $"pagonia-manager-stage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtractRoot);
            try
            {
                ZipFile.ExtractToDirectory(sourcePath, tempExtractRoot);
            }
            catch (InvalidDataException ex)
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModSourceNotAFolderOrZip,
                    $"Failed to extract '{sourcePath}': {ex.Message}"));
                SafeDelete(tempExtractRoot);
                return new InstallResult { Diagnostics = diagnostics };
            }

            validationRoot = tempExtractRoot;
        }

        try
        {
            var modYamlPath = Path.Combine(validationRoot, "mod.yaml");
            if (!File.Exists(modYamlPath))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.ModManifestMissing,
                    $"No mod.yaml at source root '{validationRoot}'."));
                return new InstallResult { Diagnostics = diagnostics };
            }

            var reader = new ManifestReader();
            var readResult = reader.ReadMod(validationRoot);
            diagnostics.AddRange(readResult.Diagnostics.Select(ManagerDiagnostic.From));

            if (!readResult.Success || readResult.Value is null)
            {
                return new InstallResult { Diagnostics = diagnostics };
            }

            var loaded = readResult.Value;
            var manifest = loaded.Manifest;

            var validator = new ManifestValidator();
            diagnostics.AddRange(validator.ValidateMod(loaded).Select(ManagerDiagnostic.From));

            var schemaValidator = new SchemaValidator();
            diagnostics.AddRange(schemaValidator.ValidateMod(validationRoot).Select(ManagerDiagnostic.From));

            // Conflict-minimising authoring advisor: advise on
            // the mod's own overlay *.gd.xml so a GameDatabase-overlay mod surfaces
            // its inter-mod conflict risk at install time. Advisory only — it emits
            // no Error, so it never blocks the install. Base-free here (the store
            // has no game root); the base-aware checks live under Advanced → Mods.
            var overlay = OverlayGdbReader.ReadFromMod(loaded);
            diagnostics.AddRange(overlay.Diagnostics.Select(ManagerDiagnostic.From));
            diagnostics.AddRange(new EntityRelationAdvisor().Advise(overlay).Select(ManagerDiagnostic.From));

            if (diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
            {
                return new InstallResult
                {
                    Diagnostics = diagnostics,
                    ModId = manifest.Id,
                    Version = manifest.Version,
                    ManifestName = manifest.Name,
                };
            }

            var targetDir = layout.ModVersionDirectory(manifest.Id, manifest.Version);
            if (Directory.Exists(targetDir))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ModAlreadyInstalled,
                    $"Mod '{manifest.Id}' version '{manifest.Version}' is already installed at '{targetDir}'."));
                return new InstallResult
                {
                    Outcome = InstallOutcome.AlreadyInstalled,
                    ModId = manifest.Id,
                    Version = manifest.Version,
                    ManifestName = manifest.Name,
                    InstallPath = targetDir,
                    Diagnostics = diagnostics,
                };
            }

            // Last yield point before the commit write. After this the copy is
            // treated as atomic-enough (a cancel mid-copy would leave a partial
            // version dir; deepening the token into the copy loop is future work).
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
            CopyDirectoryRecursive(validationRoot, targetDir);

            var sidecar = new InstallSidecar
            {
                InstalledAt = DateTimeOffset.UtcNow.ToString("o"),
                SourcePath = Path.GetFullPath(sourcePath),
                SourceType = sourceIsDirectory ? "folder" : "zip",
                ManifestName = manifest.Name,
                Source = remoteSource ?? string.Empty,
            };
            AtomicFile.WriteAllText(
                Path.Combine(targetDir, SidecarFileName),
                ManagerYaml.CreateSerializer().Serialize(sidecar));

            return new InstallResult
            {
                Outcome = InstallOutcome.Installed,
                ModId = manifest.Id,
                Version = manifest.Version,
                ManifestName = manifest.Name,
                InstallPath = targetDir,
                Diagnostics = diagnostics,
            };
        }
        finally
        {
            if (tempExtractRoot is not null)
            {
                SafeDelete(tempExtractRoot);
            }
        }
    }

    private static void CopyDirectoryRecursive(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(target, fileName), overwrite: false);
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(source))
        {
            var subdirectoryName = Path.GetFileName(subdirectory);
            CopyDirectoryRecursive(subdirectory, Path.Combine(target, subdirectoryName));
        }
    }

    private static void SafeDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
