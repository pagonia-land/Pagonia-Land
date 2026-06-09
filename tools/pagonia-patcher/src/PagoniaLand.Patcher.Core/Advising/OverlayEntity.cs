using System.Xml.Linq;

namespace PagoniaLand.Patcher;

/// <summary>
/// A single <c>&lt;Entity&gt;</c> parsed from a mod's hand-authored overlay
/// <c>*.gd.xml</c>, reduced to the fields the conflict-minimising authoring
/// advisor reasons about. <see cref="Element"/> is the underlying XML element,
/// kept so the base-aware advisor can diff a <c>Replace</c> against the
/// inherited base. Read-only — the advisor never mutates game XML.
/// </summary>
public sealed record OverlayEntity(
    string? Guid,
    string? Name,
    string? InheritanceMode,
    string? InheritedGuid,
    string SourceFile,
    XElement? Element = null);

/// <summary>
/// The parsed view of a mod's overlay GameDatabase XML: the entities it
/// declares plus every GUID-bearing value (attribute values other than the
/// structural <c>Guid</c> definition, and leaf element text) used to detect
/// references. <see cref="Diagnostics"/> carries any read/parse problems.
/// </summary>
public sealed record OverlayGdbModel(
    IReadOnlyList<OverlayEntity> Entities,
    IReadOnlyList<string> ReferenceValues,
    IReadOnlyList<PatchDiagnostic> Diagnostics);
