namespace PagoniaLand.Paker;

/// <summary>
/// Rich result of <see cref="PakPatcher.PatchAndReport"/>. Carries the
/// diagnostic list (same set the thin <see cref="PakPatcher.Patch(string,string,IReadOnlyList{string})"/>
/// overloads return) plus structured side effects that callers — primarily the
/// CLI's JSON-report builder — would otherwise have to parse out of
/// diagnostic message text.
/// </summary>
public sealed record PakPatchResult(
    IReadOnlyList<PakDiagnostic> Diagnostics,
    IReadOnlyList<PakPatchGdBinUpdate> GdbinUpdates)
{
    public bool Success => Diagnostics.All(d => d.Severity != PakDiagnosticSeverity.Error);
}

/// <summary>
/// One row per module whose <c>&lt;m&gt;/&lt;m&gt;.gd.bin</c> was implicitly
/// updated to register newly-added <c>*.gd.xml</c> paths. <see cref="Added"/>
/// is ordered the same way the rebuilt index ordered them
/// (StringComparer.Ordinal sort over the user-supplied paths) so the report
/// is deterministic.
/// </summary>
public sealed record PakPatchGdBinUpdate(
    string EntryName,
    IReadOnlyList<string> Added);
