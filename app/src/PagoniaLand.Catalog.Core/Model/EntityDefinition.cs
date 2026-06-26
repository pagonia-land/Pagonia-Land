namespace PagoniaLand.Catalog.Model;

/// <summary>
/// A single <c>&lt;Entity Guid="…"&gt;</c> definition from the GameDatabase, with the
/// metadata the catalog needs. Mirrors the per-entity record produced by
/// <c>scripts/analyze_database.ps1</c>.
/// </summary>
public sealed record EntityDefinition(
    string Guid,
    string Name,
    string Package,
    string File,
    string GroupPath,
    bool IsAbstract,
    string? ParentEntityGuid,
    string? ParentEntityName,
    int ChildEntityCount,
    IReadOnlyList<string> ValueTypes);
