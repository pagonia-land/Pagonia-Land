using System.Collections.Generic;
using System.Globalization;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>A recipe catalog row: slim grid columns + the full detail sections.</summary>
public sealed class RecipeRow : CatalogRow
{
    private static readonly IReadOnlyList<Reference> None = System.Array.Empty<Reference>();

    public required RecipeEntry Recipe { get; init; }

    /// <summary>Buildings that run this recipe — a reverse reference injected at build time.</summary>
    public IReadOnlyList<Reference> RunIn { get; init; } = None;

    public override string Name => Recipe.Name;
    public override string Guid => Recipe.Guid;
    public string Identifier => Recipe.Identifier;

    public override IReadOnlyList<DetailSection> Detail => Sections(
        Field("Identifier", Recipe.Identifier),
        Field("Default state", Recipe.DefaultState),
        Refs("Inputs", Recipe.Inputs),
        Refs("Outputs", Recipe.Outputs),
        Refs("Run in", RunIn),
        Field("Work steps", Recipe.WorkSteps.ToString(CultureInfo.InvariantCulture)),
        List("Step types", Recipe.StepTypes),
        Field("Package", Recipe.Package),
        Field("GUID", Recipe.Guid));
}
