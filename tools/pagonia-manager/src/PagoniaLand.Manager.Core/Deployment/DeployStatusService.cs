namespace PagoniaLand.Manager;

public sealed class DeployStatusResult
{
    public string GameFingerprint { get; init; } = string.Empty;

    /// <summary>The live install's ProductVersion (its real
    /// <c>gameDatabaseVersion</c>), or <c>null</c> when the exe is missing or
    /// carries no version resource (extracted layouts, fixtures). Reports render
    /// null as a dash.</summary>
    public string? GameProductVersion { get; init; }

    public bool HasDeploys => Deploys.Count > 0;
    public IReadOnlyList<DeployHistoryEntry> Deploys { get; init; } = [];
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class DeployStatusService
{
    private readonly DeployHistoryStore _historyStore = new();

    public DeployStatusResult List(StoreLayout layout, string gameRoot)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireGameRoot(gameRoot, diagnostics))
        {
            return new DeployStatusResult { Diagnostics = diagnostics };
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        // Read the install's real version off the exe (null for extracted layouts
        // / fixtures). Surfaced in the report so the user can confirm which game
        // version the recorded deploys targeted.
        var gameProductVersion = GameLayoutDetector.Detect(gameRoot).GameProductVersion;

        if (!_historyStore.Exists(layout, fingerprint))
        {
            return new DeployStatusResult
            {
                GameFingerprint = fingerprint,
                GameProductVersion = gameProductVersion,
            };
        }

        if (!_historyStore.TryRead(layout, fingerprint, out var history, out var historyError))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.DeployHistoryUnreadable,
                historyError));
            return new DeployStatusResult
            {
                GameFingerprint = fingerprint,
                GameProductVersion = gameProductVersion,
                Diagnostics = diagnostics,
            };
        }

        return new DeployStatusResult
        {
            GameFingerprint = fingerprint,
            GameProductVersion = gameProductVersion,
            Deploys = history.Deploys,
            Diagnostics = diagnostics,
        };
    }
}
