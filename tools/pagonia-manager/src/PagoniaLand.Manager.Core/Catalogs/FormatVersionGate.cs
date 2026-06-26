using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Applies the patcher's shared <see cref="FormatVersionPolicy"/> to the public
/// author↔consumer files the manager reads on its own (repo <c>index.yaml</c> and
/// <c>catalog.yaml</c>) — the formats that don't already flow through the patcher's
/// <c>SchemaValidator</c> / <c>CollectionResolver</c>. Running the same policy here is
/// what makes the manager and the patcher agree on accept / reject / migrate: a newer
/// same-major minor reads (with an info-level recommend-update note, unknown optional
/// fields ignored — the manager's deserializers already <c>IgnoreUnmatchedProperties</c>),
/// while a newer/retired major or a malformed value is refused with an actionable error.
///
/// <para>
/// The verdict's diagnostic is lifted verbatim via <see cref="ManagerDiagnostic.From(PatchDiagnostic)"/>,
/// so the user sees the same <c>formatMinorAhead</c> / <c>formatMajorUnsupported</c> codes the
/// patcher emits — one vocabulary across the toolchain.
/// </para>
///
/// <para>
/// The manager's own internal formats (<c>state.yaml</c> / profiles / report schemas) are
/// deliberately <b>not</b> handled here — they never cross between author and consumer, so
/// they don't need the forward-compatible reader. Their "written by a newer manager" refusal
/// has shipped separately in <see cref="Storage.InternalFormatVersionGuard"/> (wired into the
/// store/profile readers); only the write-stamping + auto-migration half is deferred to the
/// first real <c>storeVersion</c> / <c>profileVersion</c> bump.
/// </para>
/// </summary>
internal static class FormatVersionGate
{
    private static readonly FormatVersionPolicy Policy = new();

    /// <summary>
    /// Evaluate a repo index's <c>indexFormatVersion</c>, append any diagnostic to
    /// <paramref name="diagnostics"/>, and return whether reading should proceed.
    /// </summary>
    public static bool TryAcceptRepoIndex(string? declared, List<ManagerDiagnostic> diagnostics)
        => TryAccept(ManagedFormat.RepoIndex, declared, diagnostics);

    /// <summary>
    /// Evaluate a catalog's <c>catalogFormatVersion</c>, append any diagnostic to
    /// <paramref name="diagnostics"/>, and return whether reading should proceed.
    /// </summary>
    public static bool TryAcceptCatalog(string? declared, List<ManagerDiagnostic> diagnostics)
        => TryAccept(ManagedFormat.Catalog, declared, diagnostics);

    /// <summary>
    /// For abort-on-reject readers that have no diagnostics channel for the info tier
    /// (the remote install path resolves a path then throws on any problem): returns the
    /// rejection diagnostic to surface when a repo index can't be read, or null when it can.
    /// A tolerated newer-minor returns null (its info note is dropped on this path).
    /// </summary>
    public static ManagerDiagnostic? RejectRepoIndex(string? declared)
    {
        var verdict = Policy.Evaluate(ManagedFormat.RepoIndex, declared);
        return verdict.Accepted ? null : ManagerDiagnostic.From(verdict.Diagnostic!);
    }

    private static bool TryAccept(ManagedFormat format, string? declared, List<ManagerDiagnostic> diagnostics)
    {
        var verdict = Policy.Evaluate(format, declared);
        if (verdict.Diagnostic is not null)
        {
            diagnostics.Add(ManagerDiagnostic.From(verdict.Diagnostic));
        }
        return verdict.Accepted;
    }
}
