namespace PagoniaLand.Manager;

/// <summary>Result of <c>expansions list</c> — the resolved
/// present/owned/effective triple for every canonical package on an install.</summary>
public sealed class ExpansionListResult
{
    public bool Success { get; init; }
    public string GameRoot { get; init; } = string.Empty;
    public string? GameFingerprint { get; init; }
    public IReadOnlyList<ExpansionState> Expansions { get; init; } = [];
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>Result of <c>expansions set</c> — writing one declarable expansion's
/// ownership into the per-install record.</summary>
public sealed class ExpansionSetResult
{
    public bool Success { get; init; }
    public bool Mutated { get; init; }
    public string GameRoot { get; init; } = string.Empty;
    public string? GameFingerprint { get; init; }
    public string Package { get; init; } = string.Empty;
    public OwnershipState State { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

/// <summary>The stored ownership declaration for one game install (no presence
/// read). <see cref="GameRoot"/> is the path last recorded for the fingerprint.</summary>
public sealed record DeclaredInstall(
    string Fingerprint,
    string GameRoot,
    OwnershipState Decorations1,
    OwnershipState Dlc1);

/// <summary>
/// Reads and writes per-game-install expansion ownership, and resolves the
/// present/owned/effective triple the planning gate consumes. Ownership lives at
/// store scope keyed by gameRoot fingerprint (see <see cref="StoreState.Installs"/>);
/// presence is detected fresh off disk each call.
/// </summary>
public sealed class ExpansionOwnershipService
{
    private readonly StoreStateReader _reader = new();
    private readonly StoreStateWriter _writer = new();

    /// <summary>
    /// Resolve every canonical package's <see cref="ExpansionState"/> for the
    /// install at <paramref name="gameRoot"/>: presence from disk, ownership from
    /// the stored record (absent ⇒ unknown), with an optional transient override
    /// applied. The single entry point plan / deploy / <c>expansions list</c> all
    /// share, so they agree on what "effective" means. Never throws on an
    /// uninitialised store — ownership simply reads as unknown.
    /// </summary>
    public static IReadOnlyList<ExpansionState> ResolveForInstall(
        StoreLayout layout,
        string gameRoot,
        IReadOnlyDictionary<string, OwnershipState>? overrides = null)
    {
        var presence = PackagePresenceDetector.Detect(gameRoot);
        var declared = ReadDeclared(layout, gameRoot);
        return ExpansionResolver.Resolve(presence, declared, overrides);
    }

    /// <summary>
    /// The declared ownership of every install the store has a record for —
    /// presence-agnostic (no disk read), so a status surface can show "what you
    /// told us you own" even when no game install is pointed at this session.
    /// Returns an empty list on an uninitialised store.
    /// </summary>
    public IReadOnlyList<DeclaredInstall> ListDeclaredInstalls(StoreLayout layout)
    {
        if (!_reader.Exists(layout))
        {
            return [];
        }

        return _reader.Read(layout).Installs
            .Select(kv => new DeclaredInstall(
                kv.Key,
                kv.Value.GameRoot,
                kv.Value.OwnedExpansions.For(ExpansionPackages.Decorations1),
                kv.Value.OwnedExpansions.For(ExpansionPackages.Dlc1)))
            .ToList();
    }

    /// <summary>List the resolved state for every canonical package on the install.</summary>
    public ExpansionListResult List(
        StoreLayout layout,
        string gameRoot,
        IReadOnlyDictionary<string, OwnershipState>? overrides = null)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        if (!ServicePreconditions.RequireGameRoot(gameRoot, diagnostics))
        {
            return new ExpansionListResult { GameRoot = gameRoot ?? string.Empty, Diagnostics = diagnostics };
        }

        var expansions = ResolveForInstall(layout, gameRoot, overrides);
        return new ExpansionListResult
        {
            Success = true,
            GameRoot = Path.GetFullPath(gameRoot),
            GameFingerprint = GameFingerprint.Compute(gameRoot),
            Expansions = expansions,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>
    /// Set the ownership of one declarable expansion (<c>decorations1</c> /
    /// <c>dlc1</c>) for the install at <paramref name="gameRoot"/>. Refuses
    /// <c>core</c> / <c>tools</c> (always owned) and any unknown package. Writes
    /// the per-install record read-modify-write, preserving all other state.
    /// </summary>
    public ExpansionSetResult Set(StoreLayout layout, string gameRoot, string package, OwnershipState state)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!ServicePreconditions.RequireGameRoot(gameRoot, diagnostics))
        {
            return new ExpansionSetResult { GameRoot = gameRoot ?? string.Empty, Package = package, State = state, Diagnostics = diagnostics };
        }
        if (!ServicePreconditions.RequireInitialisedStore(layout, diagnostics))
        {
            return new ExpansionSetResult { GameRoot = Path.GetFullPath(gameRoot), Package = package, State = state, Diagnostics = diagnostics };
        }

        if (!ExpansionPackages.IsDeclarable(package))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.ExpansionPackageNotDeclarable,
                ExpansionPackages.IsAlwaysOwned(package)
                    ? $"'{package}' is base game / editor data — it is always owned and cannot be set. Only {string.Join(" / ", ExpansionPackages.Declarable)} are declarable."
                    : $"'{package}' is not a declarable expansion. Use one of: {string.Join(" / ", ExpansionPackages.Declarable)}."));
            return new ExpansionSetResult { GameRoot = Path.GetFullPath(gameRoot), Package = package, State = state, Diagnostics = diagnostics };
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        var fullGameRoot = Path.GetFullPath(gameRoot);
        var current = _reader.Read(layout);

        var existing = current.Installs.TryGetValue(fingerprint, out var record)
            ? record
            : new InstallRecord { GameRoot = fullGameRoot };
        var beforeState = existing.OwnedExpansions.For(package);
        var updatedOwned = existing.OwnedExpansions.With(package, state);

        // Preserve NudgeOffered across the write — declaring ownership through the
        // Game Expansions screen must not re-arm the onboarding nudge.
        WriteInstall(layout, current, fingerprint,
            new InstallRecord { GameRoot = fullGameRoot, OwnedExpansions = updatedOwned, NudgeOffered = existing.NudgeOffered });

        diagnostics.Add(new ManagerDiagnostic(
            ManagerDiagnosticSeverity.Info,
            ManagerDiagnosticCodes.ExpansionOwnershipSet,
            $"Set '{package}' ownership to {Describe(state)} for this install ({fingerprint})."));

        return new ExpansionSetResult
        {
            Success = true,
            Mutated = beforeState != state,
            GameRoot = fullGameRoot,
            GameFingerprint = fingerprint,
            Package = package,
            State = state,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>
    /// True when the interactive onboarding nudge should fire for this install:
    /// the store is initialised, the nudge hasn't been offered before, and at
    /// least one declarable expansion is **present but ownership unknown** (there
    /// is something real to ask about). False on an uninitialised store.
    /// </summary>
    public bool ShouldOfferNudge(StoreLayout layout, string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !_reader.Exists(layout))
        {
            return false;
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        if (_reader.Read(layout).Installs.TryGetValue(fingerprint, out var record) && record.NudgeOffered == true)
        {
            return false;
        }

        return ResolveForInstall(layout, gameRoot).Any(e =>
            ExpansionPackages.IsDeclarable(e.Package)
            && e.Present
            && e.Ownership == OwnershipState.Unknown);
    }

    /// <summary>
    /// Record that the onboarding nudge has been offered for this install, so an
    /// "ask me later" answer (which leaves ownership <c>unknown</c>) doesn't
    /// re-prompt on the next deploy. Idempotent; no-op on an uninitialised store.
    /// </summary>
    public void MarkNudgeOffered(StoreLayout layout, string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !_reader.Exists(layout))
        {
            return;
        }

        var fingerprint = GameFingerprint.Compute(gameRoot);
        var fullGameRoot = Path.GetFullPath(gameRoot);
        var current = _reader.Read(layout);
        var existing = current.Installs.TryGetValue(fingerprint, out var record)
            ? record
            : new InstallRecord { GameRoot = fullGameRoot };
        if (existing.NudgeOffered == true)
        {
            return;
        }

        WriteInstall(layout, current, fingerprint,
            new InstallRecord { GameRoot = fullGameRoot, OwnedExpansions = existing.OwnedExpansions, NudgeOffered = true });
    }

    // Read-modify-write one install record into state.yaml, carrying every other
    // store field forward verbatim (so a deploy stamp / catalog edit elsewhere is
    // never clobbered). The single write path shared by Set + MarkNudgeOffered.
    private void WriteInstall(StoreLayout layout, StoreState current, string fingerprint, InstallRecord record)
    {
        var installs = new Dictionary<string, InstallRecord>(current.Installs) { [fingerprint] = record };
        _writer.Write(layout, new StoreState
        {
            StoreVersion = current.StoreVersion,
            ActiveProfile = current.ActiveProfile,
            LastDeploy = current.LastDeploy,
            DefaultGameRoot = current.DefaultGameRoot,
            SubscribedCatalogs = current.SubscribedCatalogs,
            CatalogMaxDepth = current.CatalogMaxDepth,
            AllowInsecureSources = current.AllowInsecureSources,
            CatalogCacheStalenessHours = current.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = current.AllowInsecureCatalogSources,
            Installs = installs,
        });
    }

    private static OwnedExpansions? ReadDeclared(StoreLayout layout, string gameRoot)
    {
        if (!new StoreStateReader().Exists(layout))
        {
            return null;
        }

        var state = new StoreStateReader().Read(layout);
        var fingerprint = GameFingerprint.Compute(gameRoot);
        return state.Installs.TryGetValue(fingerprint, out var record) ? record.OwnedExpansions : null;
    }

    private static string Describe(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "owned",
        OwnershipState.NotOwned => "not-owned",
        _ => "unknown",
    };
}
