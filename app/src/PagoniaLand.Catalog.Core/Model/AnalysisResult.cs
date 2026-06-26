namespace PagoniaLand.Catalog.Model;

/// <summary>Per-package entity tally for the summary.</summary>
public sealed record PackageEntityCount(string Package, int Entities);

/// <summary>
/// Headline validation counts for a parsed GameDatabase — the same numbers
/// <c>scripts/analyze_database.ps1</c> emits to <c>analysis-summary.json</c> and that the
/// repo tracks in <c>VALIDATION_BASELINE.md</c>.
/// </summary>
public sealed record AnalysisSummary(
    int XmlFiles,
    int TotalEntities,
    int UniqueGuids,
    int GuidLikeReferences,
    int ResolvedReferences,
    int NullGuidReferences,
    int OtherUnresolvedReferences,
    IReadOnlyList<PackageEntityCount> Packages);

/// <summary>The full output of an analysis pass: the entities, the references, and the summary.</summary>
public sealed record AnalysisResult(
    IReadOnlyList<EntityDefinition> Entities,
    IReadOnlyList<GuidReference> References,
    AnalysisSummary Summary);
