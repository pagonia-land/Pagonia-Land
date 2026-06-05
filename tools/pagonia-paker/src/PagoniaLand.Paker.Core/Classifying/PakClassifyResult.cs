namespace PagoniaLand.Paker;

/// <summary>
/// Output of <see cref="PakClassifier.Classify"/>. Always populated even when
/// the kind is <see cref="PakKinds.Unknown"/>; consumers can rely on every
/// non-string field being a valid (possibly empty) instance.
/// </summary>
/// <param name="Kind">One of <see cref="PakKinds"/>: <c>module</c>, <c>user-map</c>, <c>overlay</c>, or <c>unknown</c>.</param>
/// <param name="Name">Value of <c>Name</c> in <c>&lt;m&gt;/manifest.json</c>, or null if no manifest was detected / parseable.</param>
/// <param name="ModuleFolder">The single module folder discovered in the pak (<c>&lt;m&gt;</c>), or null if none / multiple.</param>
/// <param name="Dependencies">Value of <c>Dependencies</c> in <c>&lt;m&gt;/manifest.json</c>. Empty list if unavailable.</param>
/// <param name="HasGdBin">True if the pak ships <c>&lt;m&gt;/&lt;m&gt;.gd.bin</c>.</param>
/// <param name="PopmapCount">Number of <c>&lt;m&gt;/usermaps/*.popmap</c> entries.</param>
/// <param name="OverridesAtRoot">In-pak paths at the root (no slash) that aren't standard pak metadata. The canonical example is <c>system.json</c> in the camera-zoom mod.</param>
/// <param name="Diagnostics">Reading diagnostics from <see cref="PakReader"/> plus any classifier-specific notes. Errors mean the pak couldn't be opened; warnings flag unusual layouts (e.g. multiple module folders).</param>
public sealed record PakClassifyResult(
    string Kind,
    string? Name,
    string? ModuleFolder,
    IReadOnlyList<string> Dependencies,
    bool HasGdBin,
    int PopmapCount,
    IReadOnlyList<string> OverridesAtRoot,
    IReadOnlyList<PakDiagnostic> Diagnostics)
{
    /// <summary>
    /// True when no error-severity diagnostic was raised — i.e. the pak
    /// parsed and a (possibly <see cref="PakKinds.Unknown"/>) classification
    /// was produced. False only for malformed input.
    /// </summary>
    public bool Success => Diagnostics.All(d => d.Severity != PakDiagnosticSeverity.Error);
}
