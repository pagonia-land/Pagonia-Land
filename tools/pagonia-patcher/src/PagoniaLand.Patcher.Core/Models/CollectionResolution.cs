namespace PagoniaLand.Patcher;

public sealed record CollectionResolution(
    CollectionManifest Collection,
    IReadOnlyList<ResolvedCollectionMod> Mods,
    CollectionLock Lock,
    IReadOnlyList<PatchDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}

public sealed record ResolvedCollectionMod(
    CollectionMod CollectionMod,
    LoadedMod LoadedMod,
    string SourceCollectionId,
    string LocalPath,
    string Sha256);

public sealed record CollectionSetResolution(
    IReadOnlyList<CollectionManifest> Collections,
    IReadOnlyList<ResolvedCollectionMod> Mods,
    IReadOnlyList<PatchDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}

public sealed record LockResolution(
    CollectionLock Lock,
    IReadOnlyList<LoadedMod> Mods,
    IReadOnlyList<PatchDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}
