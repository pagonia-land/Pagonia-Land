namespace PagoniaLand.Manager;

/// <summary>Where the resolved game-root path came from. Surfaced to the
/// wizard so its confirmation prompt can name the source ("default from
/// state.yaml" vs "platform default") instead of just showing a path.</summary>
public enum GameRootSource
{
    /// <summary>Per-shell session override the user typed earlier this run.</summary>
    Session,

    /// <summary>Persisted <c>state.yaml.defaultGameRoot</c> from a prior run.</summary>
    StoredDefault,

    /// <summary>OS-conditional fallback (Windows: the standard Steam install path,
    /// only when that directory actually exists on disk).</summary>
    PlatformDefault,

    /// <summary>Nothing resolved — caller has to prompt the user from scratch.</summary>
    NotSet,
}

/// <summary>Result of <see cref="GameRootResolver.Resolve"/>. <see cref="Path"/>
/// is non-null when <see cref="Source"/> is anything other than
/// <see cref="GameRootSource.NotSet"/>.</summary>
public sealed record ResolvedGameRoot(string? Path, GameRootSource Source)
{
    public bool HasPath => Source != GameRootSource.NotSet && !string.IsNullOrEmpty(Path);
}

/// <summary>
/// Decides which game-root path to suggest to the user. Three-tier precedence:
/// the in-memory session override wins, then a persisted
/// <c>state.yaml.defaultGameRoot</c>, then a platform-default Steam path on
/// Windows. Each lower tier is only consulted when the higher one is missing
/// or no longer points at an existing directory — a stale default that the
/// user moved / deleted falls through cleanly instead of being suggested back.
/// <para>The actual disk check happens at resolve time, not at write time, so
/// adding the Pioneers folder later (e.g. after Steam install) makes the
/// platform default suddenly available without any manager-side state update.</para>
/// </summary>
public static class GameRootResolver
{
    /// <summary>
    /// Resolve the best available game-root path. <paramref name="sessionOverride"/>
    /// is the wizard's in-memory <c>SessionState.GameRoot</c> (null if the user
    /// hasn't entered anything this run). <paramref name="layout"/> may be a
    /// non-initialised store — the resolver falls through to platform-default
    /// in that case so a brand-new install still gets a useful suggestion.
    /// </summary>
    public static ResolvedGameRoot Resolve(StoreLayout layout, string? sessionOverride)
    {
        if (!string.IsNullOrWhiteSpace(sessionOverride) && Directory.Exists(sessionOverride))
        {
            return new ResolvedGameRoot(sessionOverride, GameRootSource.Session);
        }

        var stored = ReadStoredDefault(layout);
        if (!string.IsNullOrWhiteSpace(stored) && Directory.Exists(stored))
        {
            return new ResolvedGameRoot(stored, GameRootSource.StoredDefault);
        }

        var platform = GameLayoutConstants.WindowsSteamDefaultPath;
        if (!string.IsNullOrEmpty(platform) && Directory.Exists(platform))
        {
            return new ResolvedGameRoot(platform, GameRootSource.PlatformDefault);
        }

        return new ResolvedGameRoot(null, GameRootSource.NotSet);
    }

    /// <summary>
    /// Persist <paramref name="gameRoot"/> as the user's preferred default in
    /// <c>state.yaml.defaultGameRoot</c>. Read-modify-write so other fields
    /// (active profile, last deploy) are preserved. No-op if the store isn't
    /// initialised — the wizard handles initialise-first elsewhere.
    /// </summary>
    /// <returns><c>true</c> if the value was written, <c>false</c> if either
    /// the store wasn't initialised or the value was already what's on disk
    /// (saves a redundant write when the user re-confirms the same path).</returns>
    public static bool SetStoredDefault(StoreLayout layout, string? gameRoot)
    {
        var reader = new StoreStateReader();
        if (!reader.Exists(layout)) return false;

        var current = reader.Read(layout);
        if (string.Equals(current.DefaultGameRoot, gameRoot, StringComparison.Ordinal))
        {
            return false;
        }

        new StoreStateWriter().Write(layout, new StoreState
        {
            StoreVersion = current.StoreVersion,
            ActiveProfile = current.ActiveProfile,
            LastDeploy = current.LastDeploy,
            DefaultGameRoot = gameRoot,
            SubscribedCatalogs = current.SubscribedCatalogs,
            CatalogMaxDepth = current.CatalogMaxDepth,
            AllowInsecureSources = current.AllowInsecureSources,
            CatalogCacheStalenessHours = current.CatalogCacheStalenessHours,
            AllowInsecureCatalogSources = current.AllowInsecureCatalogSources,
            Installs = current.Installs,
        });
        return true;
    }

    /// <summary>
    /// Best-effort read of <c>state.yaml.defaultGameRoot</c>. Returns null if
    /// the store isn't initialised, the file is missing, or the file is
    /// unreadable — the resolver treats every failure mode as "no stored
    /// default", since callers always have one more fallback tier to try.
    /// </summary>
    private static string? ReadStoredDefault(StoreLayout layout)
    {
        try
        {
            var reader = new StoreStateReader();
            if (!reader.Exists(layout)) return null;
            return reader.Read(layout).DefaultGameRoot;
        }
        catch
        {
            // Corrupt state.yaml is the StoreStateReader's job to surface;
            // for our purposes here it just means "no default available".
            return null;
        }
    }
}
