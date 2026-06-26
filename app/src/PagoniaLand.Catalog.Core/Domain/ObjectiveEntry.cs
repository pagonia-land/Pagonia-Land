namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// An objective projected from an entity carrying a <c>GeneralObjective</c> component: its
/// category, whether it is hidden, its sort order within the category, the objective sub-types
/// it carries (e.g. <c>ObjectiveMilestone</c>), and its title/description localisation keys.
/// </summary>
public sealed record ObjectiveEntry(
    string Package,
    string Name,
    string Guid,
    string File,
    string Category,
    string Hidden,
    string SortOrder,
    string Title,
    string Description,
    IReadOnlyList<string> ObjectiveTypes,
    IReadOnlyList<Reference> References,
    IReadOnlyList<string> Components);
