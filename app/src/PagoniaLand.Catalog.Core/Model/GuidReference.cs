namespace PagoniaLand.Catalog.Model;

/// <summary>
/// A GUID-like reference: any non-<c>Entity</c> element whose trimmed text is exactly a
/// GUID. A reference is <see cref="Resolved"/> when its GUID matches a defined entity,
/// <see cref="NullGuid"/> when it is the all-zero placeholder, otherwise it is an
/// "other-unresolved" reference. Mirrors the per-reference record from
/// <c>scripts/analyze_database.ps1</c> — including that a wrapper element holding a single
/// GUID-leaf child counts as its own reference alongside the leaf.
/// </summary>
public sealed record GuidReference(
    string SourceFile,
    string SourcePackage,
    string? SourceEntityGuid,
    string? SourceEntityName,
    string SourceElement,
    string Guid,
    bool Resolved,
    bool NullGuid,
    string? TargetGuid,
    string? TargetName,
    string? TargetPackage,
    string? TargetFile);
