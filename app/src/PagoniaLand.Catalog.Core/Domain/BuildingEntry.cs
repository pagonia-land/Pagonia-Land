namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// A building projected from an entity carrying a <c>Building</c> component. Mirrors the
/// building row from <c>scripts/generate_catalog.ps1</c>: category + build menu group, icon,
/// construction costs, the builder unit (and an optional secondary builder unit), the production
/// recipes it runs (+ its worker), and any gather outputs. GUID-shaped references (category, cost
/// resources, recipes, workers) are resolved to names; amounts are rendered as "<c>amount name</c>".
/// </summary>
public sealed record BuildingEntry(
    string Package,
    string Name,
    string Guid,
    string File,
    string Category,
    string UiGroup,
    string Icon,
    IReadOnlyList<Reference> ConstructionCosts,
    Reference? Builder,
    Reference? SecondaryBuilder,
    IReadOnlyList<Reference> ProductionRecipes,
    Reference? ProductionWorker,
    string OptimalWorkStep,
    IReadOnlyList<Reference> GatherOutputs,
    IReadOnlyList<string> Components);
