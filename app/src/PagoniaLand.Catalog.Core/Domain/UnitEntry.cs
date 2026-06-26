namespace PagoniaLand.Catalog.Domain;

/// <summary>
/// A unit projected from an entity carrying a <c>Unit</c> component. Mirrors
/// <c>scripts/generate_catalog.ps1</c>'s unit catalog: its icon, recruitment costs (resolved
/// to "amount resource"), whether it needs manual recruitment, the source recruitable unit,
/// and its tags.
/// </summary>
public sealed record UnitEntry(
    string Package,
    string Name,
    string Guid,
    string File,
    string Icon,
    IReadOnlyList<Reference> RecruitmentCosts,
    string NeedsManualRecruitment,
    Reference? SourceRecruitableUnit,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Components);
