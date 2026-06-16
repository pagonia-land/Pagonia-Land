namespace PagoniaLand.Paker;

/// <summary>
/// Output of <see cref="PakClassifier.Classify"/>: what a pak contributes,
/// reported as independent signals rather than a single label — a pak can do
/// several of these at once (a published editor map ships a GameDatabase *and*
/// a map). Always populated; consumers can rely on every non-string field being
/// a valid (possibly empty) instance.
/// </summary>
/// <param name="Name">Value of <c>Name</c> in <c>&lt;m&gt;/manifest.json</c>, or null if no manifest was detected / parseable.</param>
/// <param name="ModuleFolder">The single module folder discovered in the pak (<c>&lt;m&gt;</c>), or null if none / multiple.</param>
/// <param name="Dependencies">Value of <c>Dependencies</c> in <c>&lt;m&gt;/manifest.json</c>. Empty list if unavailable.</param>
/// <param name="GdbScopes">Which GameDatabase scopes the pak carries actual content for: <c>"global"</c> (a module-level <c>&lt;m&gt;.gd.bin</c> with entities — active in all game modes) and/or <c>"map-scoped"</c> (a <c>&lt;m&gt;/usermaps/*.gd.bin</c> or <c>*.gd.xml</c> — the per-map "hosted game database", active only for that map). <strong>Empty means the pak contributes no GameDatabase content</strong> — note an empty module-level <c>&lt;m&gt;.gd.bin</c> (the editor emits one even for a map-only mod) does not count, since it lists no <c>*.gd.xml</c> resources.</param>
/// <param name="PopmapCount">Number of <c>&lt;m&gt;/usermaps/*.popmap</c> entries — i.e. how many maps it bundles.</param>
/// <param name="OverridesAtRoot">In-pak paths at the root (no slash) that aren't standard pak metadata. The canonical example is <c>system.json</c> in the camera-zoom mod.</param>
/// <param name="Diagnostics">Reading diagnostics from <see cref="PakReader"/> plus any classifier-specific notes. Errors mean the pak couldn't be opened; warnings flag unusual layouts (e.g. multiple module folders).</param>
public sealed record PakClassifyResult(
    string? Name,
    string? ModuleFolder,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> GdbScopes,
    int PopmapCount,
    IReadOnlyList<string> OverridesAtRoot,
    IReadOnlyList<PakDiagnostic> Diagnostics)
{
    /// <summary>
    /// True when no error-severity diagnostic was raised — i.e. the pak parsed
    /// and its contributions were inventoried. False only for malformed input.
    /// </summary>
    public bool Success => Diagnostics.All(d => d.Severity != PakDiagnosticSeverity.Error);
}
