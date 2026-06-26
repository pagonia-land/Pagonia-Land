namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// A production recipe projected from an entity carrying a <c>ProductionRecipe</c> component.
/// Mirrors <c>scripts/generate_catalog.ps1</c>'s recipe catalog: its identifier + default state,
/// the input/output resources (resolved to "amount resource"), the number of work steps, and
/// the distinct step types.
/// </summary>
public sealed record RecipeEntry(
    string Package,
    string Name,
    string Guid,
    string File,
    string Identifier,
    string DefaultState,
    IReadOnlyList<Reference> Inputs,
    IReadOnlyList<Reference> Outputs,
    int WorkSteps,
    IReadOnlyList<string> StepTypes,
    IReadOnlyList<string> Components);
