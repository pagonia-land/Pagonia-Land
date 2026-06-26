using System.Collections.Generic;
using PagoniaLand.Catalog.Domain;

namespace PagoniaLand.App;

/// <summary>A unit catalog row: slim grid columns + the full detail sections.</summary>
public sealed class UnitRow : CatalogRow
{
    private static readonly IReadOnlyList<Reference> None = System.Array.Empty<Reference>();

    public required UnitEntry Unit { get; init; }

    // Reverse references, computed across the catalog and injected at build time (clickable).
    public IReadOnlyList<Reference> Builds { get; init; } = None;
    public IReadOnlyList<Reference> WorksIn { get; init; } = None;
    public IReadOnlyList<Reference> RecruitedInto { get; init; } = None;
    public IReadOnlyList<Reference> InObjectives { get; init; } = None;

    public override string Name => Unit.Name;
    public override string Guid => Unit.Guid;
    public string RecruitedFrom => Unit.SourceRecruitableUnit?.Name ?? string.Empty;

    public override IReadOnlyList<DetailSection> Detail => Sections(
        Refs("Recruitment costs", Unit.RecruitmentCosts),
        Field("Needs manual recruitment", Unit.NeedsManualRecruitment),
        Ref("Recruited from", Unit.SourceRecruitableUnit),
        Refs("Recruited into", RecruitedInto),
        Refs("Builds", Builds),
        Refs("Works in", WorksIn),
        Refs("In objectives", InObjectives),
        List("Tags", Unit.Tags),
        Field("Package", Unit.Package),
        Field("GUID", Unit.Guid));
}
