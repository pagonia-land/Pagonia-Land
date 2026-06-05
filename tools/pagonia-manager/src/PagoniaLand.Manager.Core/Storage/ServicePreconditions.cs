namespace PagoniaLand.Manager;

/// <summary>
/// Service-level preflight helpers — pulls the two preconditions that the deploy /
/// rollback / status / plan / profile-mutation services were duplicating verbatim
/// into one place. Each helper takes a mutable diagnostics list and returns true
/// when the precondition is satisfied (caller proceeds), false when it isn't
/// (caller has just had the diagnostic appended and should bail with the
/// service's Failed/empty result).
///
/// Why centralise: a future tweak (better wording, structured location field,
/// recovery hint, etc.) used to require touching 4+ service files in sync.
/// </summary>
public static class ServicePreconditions
{
    private static readonly StoreStateReader _stateReader = new();

    /// <summary>
    /// Asserts the player-supplied game-root path is non-empty and points at an
    /// existing directory. On failure, appends a single
    /// <see cref="ManagerDiagnosticCodes.GameRootMissing"/> diagnostic.
    /// </summary>
    public static bool RequireGameRoot(string? gameRoot, List<ManagerDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.GameRootMissing,
                string.IsNullOrWhiteSpace(gameRoot)
                    ? "Game root path must not be empty."
                    : $"Game root '{gameRoot}' does not exist."));
            return false;
        }
        return true;
    }

    /// <summary>
    /// Asserts the store has been initialised (its state.yaml exists). On
    /// failure, appends a <see cref="ManagerDiagnosticCodes.StoreNotInitialised"/>
    /// diagnostic with the canonical "run store init" hint.
    /// </summary>
    public static bool RequireInitialisedStore(StoreLayout layout, List<ManagerDiagnostic> diagnostics)
    {
        if (!_stateReader.Exists(layout))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.StoreNotInitialised,
                $"Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first."));
            return false;
        }
        return true;
    }
}
