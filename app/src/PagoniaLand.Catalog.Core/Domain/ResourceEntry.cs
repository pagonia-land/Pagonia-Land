namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// A resource (good/commodity) projected from an entity carrying a <c>&lt;ResourceDescription&gt;</c>
/// component. Mirrors the resource row produced by <c>scripts/generate_catalog.ps1</c>.
/// GUID-shaped fields (e.g. <see cref="Category"/>) are resolved to names; localization keys
/// (<see cref="NameKey"/> …) are kept as keys (no localization data in scope).
/// </summary>
public sealed record ResourceEntry(
    string Package,
    string Name,
    string Guid,
    string File,
    string Category,
    string NameKey,
    string NamePluralKey,
    string DescriptionKey,
    string Icon,
    string CarryType,
    string Mesh,
    string UiDisplay,
    string SortOrder,
    string WealthValue,
    string StealValue,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Components);
