using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace PagoniaLand.Patcher;

public sealed class ManifestReader
{
    private readonly IDeserializer _deserializer = PatcherYaml.CreateDeserializer();

    // YamlDotNet activates every model type and every collection type it reaches via
    // Activator.CreateInstance. The AOT compiler needs to see each concrete type as a root;
    // otherwise the parameterless constructor of e.g. PatchOperation or List<string> is dropped
    // and deserialisation fails at runtime with "Failed to create an instance of type ...".
    // DynamicDependency attaches the rooting to the constructor so the linker keeps these
    // types and their public members as long as ManifestReader is used.
    private const DynamicallyAccessedMemberTypes Shape =
        DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields;

    [DynamicDependency(Shape, typeof(ModManifest))]
    [DynamicDependency(Shape, typeof(PatchSet))]
    [DynamicDependency(Shape, typeof(EntryOperations))]
    [DynamicDependency(Shape, typeof(EntryFileMapping))]
    [DynamicDependency(Shape, typeof(PakMetadata))]
    [DynamicDependency(Shape, typeof(TweakDeclaration))]
    [DynamicDependency(Shape, typeof(TweakEnumValue))]
    [DynamicDependency(Shape, typeof(PatchFile))]
    [DynamicDependency(Shape, typeof(PatchOperation))]
    [DynamicDependency(Shape, typeof(PatchTarget))]
    [DynamicDependency(Shape, typeof(CollectionManifest))]
    [DynamicDependency(Shape, typeof(CollectionMod))]
    [DynamicDependency(Shape, typeof(CollectionLock))]
    [DynamicDependency(Shape, typeof(LockedMod))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<string>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<PatchSet>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<PatchOperation>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<EntryFileMapping>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<TweakDeclaration>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<TweakEnumValue>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<CollectionMod>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(List<LockedMod>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Dictionary<string, string>))]
    public ManifestReader()
    {
    }

    public ReadResult<ModManifest> ReadModManifest(string modDirectory)
        => ReadYamlFile<ModManifest>(System.IO.Path.Combine(modDirectory, "mod.yaml"), DiagnosticCodes.ModManifestReadFailed);

    public ReadResult<PatchFile> ReadPatchFile(string path)
        => ReadYamlFile<PatchFile>(path, DiagnosticCodes.PatchFileReadFailed);

    /// <remarks>This is a plain deserialize — it does <b>not</b> evaluate <c>collectionFormatVersion</c>.
    /// The MAJOR.MINOR tier gate (newer-minor reads with a note, newer/retired major refused) is the
    /// caller's responsibility: any consumer reading a public collection file must run the shared
    /// format-version policy (as the resolver / schema-validate paths do) before trusting it.</remarks>
    public ReadResult<CollectionManifest> ReadCollectionManifest(string path)
        => ReadYamlFile<CollectionManifest>(path, DiagnosticCodes.CollectionReadFailed);

    /// <remarks>This is a plain deserialize — it does <b>not</b> evaluate <c>collectionLockVersion</c>.
    /// The MAJOR.MINOR tier gate is the caller's responsibility (see <see cref="ReadCollectionManifest"/>).</remarks>
    public ReadResult<CollectionLock> ReadCollectionLock(string path)
        => ReadYamlFile<CollectionLock>(path, DiagnosticCodes.CollectionLockReadFailed);

    // YamlDotNet uses reflection to construct the target type and walk its properties. Mark every
    // T that flows through ReadYamlFile with DynamicallyAccessedMembers so the trimmer keeps the
    // constructors and properties of every YAML model and its transitively referenced model types.
    public ReadResult<LoadedMod> ReadMod(string modDirectory)
    {
        var manifestResult = ReadModManifest(modDirectory);
        if (!manifestResult.Success || manifestResult.Value is null)
        {
            return ReadResult<LoadedMod>.Failed(manifestResult.Diagnostics.ToArray());
        }

        var diagnostics = new List<PatchDiagnostic>(manifestResult.Diagnostics);
        var patchFiles = new List<LoadedPatchFile>();

        foreach (var patchPath in manifestResult.Value.Patches)
        {
            var fullPatchPath = System.IO.Path.Combine(modDirectory, patchPath);
            var patchResult = ReadPatchFile(fullPatchPath);
            diagnostics.AddRange(patchResult.Diagnostics);

            if (patchResult.Value is not null)
            {
                patchFiles.Add(new LoadedPatchFile(fullPatchPath, patchResult.Value));
            }
        }

        // Patch sets carry the same kind of patch files as the top-level `patches:`
        // list, but each set is gated on a package being present (the planner skips
        // an optional set when its required package is absent — see PatchPlanner).
        // Loading them here is what makes a declared patch set actually apply.
        foreach (var patchSet in manifestResult.Value.PatchSets)
        {
            foreach (var patchPath in patchSet.Patches)
            {
                var fullPatchPath = System.IO.Path.Combine(modDirectory, patchPath);
                var patchResult = ReadPatchFile(fullPatchPath);
                diagnostics.AddRange(patchResult.Diagnostics);

                if (patchResult.Value is not null)
                {
                    patchFiles.Add(new LoadedPatchFile(
                        fullPatchPath,
                        patchResult.Value,
                        patchSet.RequiresPackages,
                        patchSet.Optional));
                }
            }
        }

        var loadedMod = new LoadedMod(modDirectory, manifestResult.Value, patchFiles);
        return diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error)
            ? ReadResult<LoadedMod>.Failed(diagnostics.ToArray())
            : ReadResult<LoadedMod>.Ok(loadedMod, diagnostics.ToArray());
    }

    private ReadResult<T> ReadYamlFile<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] T>(string path, string errorCode)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ReadResult<T>.Failed(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    DiagnosticCodes.FileNotFound,
                    $"File not found: {path}",
                    path));
            }

            var yaml = File.ReadAllText(path);
            var value = _deserializer.Deserialize<T>(yaml);

            if (value is null)
            {
                return ReadResult<T>.Failed(new PatchDiagnostic(
                    PatchDiagnosticSeverity.Error,
                    errorCode,
                    $"YAML file did not contain a readable {typeof(T).Name}.",
                    path));
            }

            return ReadResult<T>.Ok(value, new PatchDiagnostic(
                PatchDiagnosticSeverity.Info,
                DiagnosticCodes.FileRead,
                $"Read {typeof(T).Name}: {path}",
                path));
        }
        catch (YamlException exception)
        {
            var detail = exception.InnerException is null
                ? exception.Message
                : $"{exception.Message} ({exception.InnerException.GetType().Name}: {exception.InnerException.Message})";
            return ReadResult<T>.Failed(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                errorCode,
                $"YAML parse error: {detail}",
                path));
        }
        catch (IOException exception)
        {
            return ReadResult<T>.Failed(new PatchDiagnostic(
                PatchDiagnosticSeverity.Error,
                errorCode,
                $"Could not read file: {exception.Message}",
                path));
        }
    }
}

public sealed record LoadedMod(
    string Directory,
    ModManifest Manifest,
    IReadOnlyList<LoadedPatchFile> PatchFiles);

public sealed record LoadedPatchFile(
    string Path,
    PatchFile PatchFile,
    // Non-null only for patch files that came from a `patchSets:` entry. The
    // planner applies the file only when every listed package is present under
    // the game root; an optional set is skipped silently when it is not.
    IReadOnlyList<string>? RequiresPackages = null,
    bool Optional = false);
