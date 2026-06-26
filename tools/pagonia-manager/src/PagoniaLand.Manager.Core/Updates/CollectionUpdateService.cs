namespace PagoniaLand.Manager;

/// <summary>Why a <see cref="CollectionUpdateService.Update"/> ended the way it did.</summary>
public enum CollectionUpdateOutcome
{
    /// <summary>The collection was reinstalled at a newer version and its linked profile reseeded.</summary>
    Updated,

    /// <summary>The source already advertises the installed version (or older) — nothing to do.</summary>
    AlreadyCurrent,

    /// <summary>No installed collection with that id — nothing to update.</summary>
    NotInstalled,

    /// <summary>The installed collection has no <c>gh:</c> provenance sidecar to check / fetch from.</summary>
    NoRemoteSource,

    /// <summary>Detection or the fetch/install of the new version failed (see diagnostics).</summary>
    Failed,
}

/// <summary>Outcome of a transparent collection update.</summary>
public sealed class CollectionUpdateResult
{
    public CollectionUpdateOutcome Outcome { get; init; }
    public string CollectionId { get; init; } = string.Empty;
    public string? ProfileName { get; init; }
    public string? FromVersion { get; init; }
    public string? ToVersion { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>How a collection update treats the user's own per-mod tweak overrides.</summary>
public enum CollectionTweakPolicy
{
    /// <summary>Carry the user's genuine overrides forward; tweaks they hadn't changed follow
    /// the new curator defaults. The default — non-destructive, like the per-mod update.</summary>
    Merge,

    /// <summary>Discard all user overrides and reseed the curator defaults — the original
    /// <c>collection install --overwrite</c> behaviour.</summary>
    Reseed,

    /// <summary>Resolve each genuine conflict (user value vs the collection's new value) via the
    /// caller's callback. Used by the interactive wizard.</summary>
    Ask,
}

/// <summary>One genuine tweak override that the update would change, presented to an
/// <see cref="CollectionTweakPolicy.Ask"/> callback so the caller can decide per conflict.</summary>
public sealed record CollectionTweakConflict(
    string ModId, string TweakId, string TweakLabel, string YourValue, string CuratorValue);

/// <summary>The caller's per-conflict decision.</summary>
public enum CollectionTweakResolution
{
    KeepYours,
    TakeCurator,
}

/// <summary>A genuine user override on a collection's mod, surfaced by
/// <see cref="CollectionUpdateService.PreviewGenuineOverrides"/> so an interactive caller can
/// show what's at stake before choosing a policy.</summary>
public sealed record CollectionTweakOverride(
    string ModId, string TweakId, string TweakLabel, string YourValue);

/// <summary>
/// The transparent, opt-in collection update: move an installed collection from its current version
/// to the newer one its source repo advertises. The collection counterpart of <see cref="ModUpdateService"/>,
/// built on the same building blocks so the behaviour matches the rest of the manager:
///
/// <list type="number">
/// <item>detect — compare the installed collection version against the source repo's <c>index.yaml</c>
/// <c>collections[].version</c> at the default branch (mirror-first, the same check <c>outdated</c> uses);</item>
/// <item>re-fetch the collection at HEAD (<see cref="RemoteFetcher.FetchCollection"/>) and reinstall it
/// with <c>--overwrite</c> — the new collection version is its own directory, so the previous one stays
/// on disk as a rollback anchor, and each referenced mod version coexists the same way;</item>
/// <item>reseed the linked profile from the new collection (curator-default reseed semantics — the same
/// behaviour <c>collection install --overwrite</c> has, surfaced via <c>tweakOverridesResetByReinstall</c>
/// when it discards prior user tweak overrides).</item>
/// </list>
///
/// It never deletes the old version. Read-then-write: nothing changes unless a strictly-newer version
/// exists. Only a collection installed from a <c>gh:</c> repo (one carrying the provenance sidecar) can
/// be updated — a local-file install has no source to compare against.
/// </summary>
public sealed class CollectionUpdateService
{
    private readonly IRemoteContentFetcher _http;

    public CollectionUpdateService(IRemoteContentFetcher http)
    {
        _http = http;
    }

    public CollectionUpdateResult Update(
        StoreLayout layout,
        string collectionId,
        CollectionTweakPolicy tweakPolicy = CollectionTweakPolicy.Merge,
        Func<CollectionTweakConflict, CollectionTweakResolution>? resolveConflict = null,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        // The installed copy at its highest installed version — that's the one a newer publish races.
        var installed = new CollectionLister().List(layout)
            .Where(c => string.Equals(c.Id, collectionId, StringComparison.Ordinal))
            .Aggregate((InstalledCollection?)null, (best, c) =>
                best is null || ModVersion.IsNewer(c.Version, best.Version) ? c : best);

        if (installed is null)
        {
            return Result(CollectionUpdateOutcome.NotInstalled, collectionId, null, diagnostics,
                warning: ManagerDiagnosticCodes.CollectionUpdateNotInstalled,
                message: $"Collection '{collectionId}' isn't installed — nothing to update. Install it first with 'collection install'.");
        }

        var fromVersion = installed.Version;

        if (string.IsNullOrWhiteSpace(installed.Source)
            || !RemoteSourceParser.TryParse(installed.Source, out var parsed)
            || parsed is not GitHubSource source)
        {
            return Result(CollectionUpdateOutcome.NoRemoteSource, collectionId, null, diagnostics,
                warning: ManagerDiagnosticCodes.CollectionUpdateNoRemoteSource,
                message: $"Collection '{collectionId}@{fromVersion}' has no gh: source to update from (installed from a local file).");
        }

        // Detect: what does the source advertise at the default branch?
        var index = new RepoIndexFetcher(_http).Fetch(source with { Ref = "HEAD" }, cancellationToken);
        if (!index.Success || !index.HasIndex || index.Index is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                $"Couldn't reach {source.Owner}/{source.Repo} to check collection '{collectionId}' for an update."));
            return Result(CollectionUpdateOutcome.Failed, collectionId, null, diagnostics);
        }

        var entry = index.Index.Collections.FirstOrDefault(e => string.Equals(e.Id, collectionId, StringComparison.Ordinal));
        if (entry is null || string.IsNullOrWhiteSpace(entry.Version))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                $"{source.Owner}/{source.Repo} no longer lists collection '{collectionId}' with a version in index.yaml."));
            return Result(CollectionUpdateOutcome.Failed, collectionId, null, diagnostics);
        }

        // Mirror UpdateDetectionService's R5-019 guard: differing version strings where exactly one
        // parses can't be compared — don't claim "already current"; report the check couldn't run.
        if (!string.Equals(entry.Version, fromVersion, StringComparison.Ordinal)
            && ModVersion.TryParse(entry.Version, out _) != ModVersion.TryParse(fromVersion, out _))
        {
            diagnostics.Add(Warning(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                $"Could not compare versions for collection '{collectionId}': installed '{fromVersion}' vs advertised '{entry.Version}' (one isn't a parseable version)."));
            return Result(CollectionUpdateOutcome.Failed, collectionId, null, diagnostics);
        }

        if (!ModVersion.IsNewer(entry.Version, fromVersion))
        {
            // Also reached when the source advertises an older version (a rolled-back index), so name
            // both rather than mislabel the installed pin as "what the source advertises".
            diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionUpdateAlreadyCurrent,
                $"Collection '{collectionId}' is already current ({fromVersion}); the source advertises {entry.Version}."));
            return Result(CollectionUpdateOutcome.AlreadyCurrent, collectionId, null, diagnostics);
        }

        // The profile to reseed: the one this collection install created/links to. The default
        // install names it after the collection id; a `--as-profile` install named it otherwise,
        // so we recover the real name from the profile<->collection link and only fall back to the
        // collection id when nothing is linked.
        var profileName = ResolveLinkedProfile(layout, collectionId, diagnostics);

        // Capture the user's genuine tweak overrides on this collection's mods BEFORE the reseed,
        // so a Merge / Ask policy can carry them forward. "Genuine" = origin profile-override
        // against the OLD (still-pinned) collection — a value still equal to the curator's isn't
        // really the user's and just follows the update. Reseed skips the capture entirely.
        var capturedOverrides = tweakPolicy == CollectionTweakPolicy.Reseed
            ? new Dictionary<string, List<CapturedOverride>>(StringComparer.Ordinal)
            : CaptureGenuineOverrides(layout, profileName);

        // Re-fetch the collection at HEAD and reinstall with --overwrite — the new version
        // coexists on disk (its own version dir), the old one stays as the rollback anchor.
        var fetch = new RemoteFetcher(_http).FetchCollection(source with { Ref = "HEAD" }, cancellationToken);
        diagnostics.AddRange(fetch.Diagnostics);
        if (!fetch.Success || fetch.CollectionFilePath is null || fetch.ModsRoot is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                $"Couldn't fetch the new version of collection '{collectionId}' from {source.Owner}/{source.Repo}."));
            return Result(CollectionUpdateOutcome.Failed, collectionId, profileName, diagnostics);
        }

        CollectionInstallResult install;
        try
        {
            install = new CollectionInstallService().InstallWithOptions(
                layout, fetch.CollectionFilePath, fetch.ModsRoot,
                new CollectionInstallOptions
                {
                    ProfileNameOverride = profileName,
                    Overwrite = true,
                    RemoteModSources = new Dictionary<string, string>(fetch.ModSources, StringComparer.Ordinal),
                    RemoteCollectionSource = fetch.ResolvedCollectionSource,
                });
        }
        finally
        {
            if (fetch.TempDirectory is not null && Directory.Exists(fetch.TempDirectory))
            {
                try { Directory.Delete(fetch.TempDirectory, recursive: true); } catch { /* best-effort temp cleanup */ }
            }
        }

        // When we'll reconcile overrides ourselves (Merge / Ask), the install's blanket
        // "tweak overrides reset" info is misleading — drop it; our per-tweak kept/reset
        // diagnostics below are the accurate account. Reseed keeps it (it is the truth there).
        diagnostics.AddRange(tweakPolicy == CollectionTweakPolicy.Reseed
            ? install.Diagnostics
            : install.Diagnostics.Where(d => d.Code != ManagerDiagnosticCodes.TweakOverridesResetByReinstall));

        if (install.Outcome == CollectionInstallOutcome.Failed || string.IsNullOrWhiteSpace(install.CollectionVersion))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.CollectionUpdateCheckFailed,
                $"Installing the new version of collection '{collectionId}' failed; the previous install is unchanged."));
            return Result(CollectionUpdateOutcome.Failed, collectionId, profileName, diagnostics);
        }

        var toVersion = install.CollectionVersion!;

        // Reconcile the captured overrides against the freshly-reseeded profile (Merge / Ask only;
        // capturedOverrides is empty for Reseed).
        if (capturedOverrides.Count > 0)
        {
            ReapplyOverrides(layout, install.ProfileName ?? profileName, capturedOverrides, tweakPolicy, resolveConflict, diagnostics);
        }

        diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionUpdated,
            $"Updated collection '{collectionId}' {fromVersion} -> {toVersion} (profile '{install.ProfileName ?? profileName}'). "
            + $"The previous version is kept on disk for rollback (collection install the old manifest, or 'profile use' an older profile)."));

        return new CollectionUpdateResult
        {
            Outcome = CollectionUpdateOutcome.Updated,
            CollectionId = collectionId,
            ProfileName = install.ProfileName ?? profileName,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>
    /// The user's genuine tweak overrides on the collection's mods, before any update — for an
    /// interactive caller to show what's at stake and choose a policy. Empty when nothing is
    /// overridden (or the collection isn't installed). Read-only.
    /// </summary>
    public IReadOnlyList<CollectionTweakOverride> PreviewGenuineOverrides(StoreLayout layout, string collectionId)
    {
        // Read-only preview — the ambiguous-profile warning belongs to the apply path, not here.
        var profileName = ResolveLinkedProfile(layout, collectionId, new List<ManagerDiagnostic>());
        return CaptureGenuineOverrides(layout, profileName)
            .SelectMany(kv => kv.Value.Select(o => new CollectionTweakOverride(kv.Key, o.TweakId, o.Label, o.Value)))
            .ToList();
    }

    /// <summary>A captured pre-update override: the tweak's current id, its human label, and the
    /// user's stored value.</summary>
    private sealed record CapturedOverride(string TweakId, string Label, string Value);

    /// <summary>
    /// For each enabled mod on the linked profile, the tweaks whose stored value is a genuine
    /// user override (origin <c>profile-override</c> against the still-pinned old collection — i.e.
    /// it differs from the old curator default). Bare curator defaults are intentionally excluded:
    /// they just follow the update.
    /// </summary>
    private static Dictionary<string, List<CapturedOverride>> CaptureGenuineOverrides(StoreLayout layout, string profileName)
    {
        var result = new Dictionary<string, List<CapturedOverride>>(StringComparer.Ordinal);
        var profileStore = new ProfileStore();
        if (!profileStore.Exists(layout, profileName))
        {
            return result;
        }

        var tweakService = new TweakOverrideService();
        foreach (var mod in profileStore.Read(layout, profileName).EnabledMods)
        {
            if (mod.Tweaks is not { Count: > 0 })
            {
                continue;
            }

            var read = tweakService.Read(layout, profileName, mod.Id);
            if (!read.Success)
            {
                continue;
            }

            var overrides = read.Tweaks
                .Where(t => t.Origin == TweakValueOrigins.ProfileOverride)
                .Select(t => new CapturedOverride(t.Declaration.Id, t.Declaration.Label, t.Value))
                .ToList();
            if (overrides.Count > 0)
            {
                result[mod.Id] = overrides;
            }
        }

        return result;
    }

    /// <summary>
    /// After the reseed, reconcile each captured override against the new curator default: keep
    /// it (Merge), reset it (Reseed), or ask (Ask). A renamed tweak is mapped forward via its
    /// alias; a tweak the updated collection no longer declares, or a mod it dropped, is reported
    /// as reset. A kept value that fails validation against the updated mod falls back to the
    /// curator value.
    /// </summary>
    private static void ReapplyOverrides(
        StoreLayout layout,
        string profileName,
        Dictionary<string, List<CapturedOverride>> captured,
        CollectionTweakPolicy policy,
        Func<CollectionTweakConflict, CollectionTweakResolution>? resolveConflict,
        List<ManagerDiagnostic> diagnostics)
    {
        var tweakService = new TweakOverrideService();

        foreach (var (modId, overrides) in captured)
        {
            var read = tweakService.Read(layout, profileName, modId);
            if (!read.Success)
            {
                diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionTweakReset,
                    $"Mod '{modId}' is no longer part of the updated collection; {overrides.Count} of your tweak override(s) for it were not carried over."));
                continue;
            }

            var declarations = read.Tweaks.Select(t => t.Declaration).ToList();
            var storedMap = overrides.ToDictionary(o => o.TweakId, o => o.Value, StringComparer.Ordinal);
            // Map any override stored under a tweak's old (renamed) id forward to the current id.
            var (migrated, _, _) = TweakAliasMigrator.Migrate(modId, storedMap, declarations);

            foreach (var (tweakId, yourValue) in migrated ?? new Dictionary<string, string>(StringComparer.Ordinal))
            {
                var view = read.Tweaks.FirstOrDefault(t => string.Equals(t.Declaration.Id, tweakId, StringComparison.Ordinal));
                if (view is null)
                {
                    // The updated mod no longer declares this tweak (and not as an alias).
                    diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionTweakReset,
                        $"Tweak '{modId}:{tweakId}' no longer exists in the updated collection; your override was dropped."));
                    continue;
                }

                var curatorValue = view.Value; // post-reseed effective value (curator default / mod default)
                var valueConflict = !string.Equals(yourValue, curatorValue, StringComparison.Ordinal);

                // No value difference → don't prompt and don't announce a change, but still re-assert
                // the user's explicit mark below so their ownership survives the reseed (a later
                // curator change then won't silently sweep the value up).
                var resolution = !valueConflict
                    ? CollectionTweakResolution.KeepYours
                    : policy switch
                    {
                        CollectionTweakPolicy.Reseed => CollectionTweakResolution.TakeCurator,
                        CollectionTweakPolicy.Merge => CollectionTweakResolution.KeepYours,
                        _ => resolveConflict?.Invoke(new CollectionTweakConflict(modId, tweakId, view.Declaration.Label, yourValue, curatorValue))
                             ?? CollectionTweakResolution.KeepYours,
                    };

                if (resolution == CollectionTweakResolution.TakeCurator)
                {
                    diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionTweakReset,
                        $"Reset tweak '{modId}:{tweakId}' to the collection's value '{curatorValue}' (was '{yourValue}')."));
                    continue;
                }

                // Re-apply the value AND the explicit user mark (Set records it as a user override).
                var set = tweakService.Set(layout, profileName, modId, tweakId, yourValue);
                if (set.Success)
                {
                    if (valueConflict)
                    {
                        diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionTweakKept,
                            $"Kept your tweak '{modId}:{tweakId}' = '{yourValue}' (collection's value is '{curatorValue}')."));
                    }
                }
                else
                {
                    diagnostics.AddRange(set.Diagnostics);
                    diagnostics.Add(Info(ManagerDiagnosticCodes.CollectionTweakReset,
                        $"Could not keep your tweak '{modId}:{tweakId}' = '{yourValue}' against the updated mod; using the collection's value '{curatorValue}'."));
                }
            }
        }
    }

    /// <summary>
    /// The profile linked to <paramref name="collectionId"/> (its <c>collection:</c> field). Prefer
    /// the default-named profile (== the collection id) when present, else the single linked profile.
    /// When several profiles link it and none is default-named, pick the ordinally-first deterministically
    /// and warn — better than silently reseeding a freshly-created collection-id profile, which would
    /// strand every linked profile's tweaks. Falls back to the collection id only when nothing is linked.
    /// </summary>
    private static string ResolveLinkedProfile(StoreLayout layout, string collectionId, List<ManagerDiagnostic> diagnostics)
    {
        var linked = new ProfileLifecycleService().List(layout).Profiles
            .Where(p => string.Equals(p.Collection, collectionId, StringComparison.Ordinal))
            .ToList();

        if (linked.Any(p => string.Equals(p.Name, collectionId, StringComparison.Ordinal)))
        {
            return collectionId;
        }
        if (linked.Count == 1)
        {
            return linked[0].Name;
        }
        if (linked.Count > 1)
        {
            var chosen = linked.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).First();
            diagnostics.Add(new ManagerDiagnostic(ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.CollectionUpdateAmbiguousProfile,
                $"{linked.Count} profiles link collection '{collectionId}' and none is named after it; reseeding '{chosen}'. "
                + "Switch to the profile you want updated and re-run if that's not the one."));
            return chosen;
        }
        return collectionId; // nothing linked — the reseed will create it
    }

    private static CollectionUpdateResult Result(CollectionUpdateOutcome outcome, string collectionId, string? profileName,
        List<ManagerDiagnostic> diagnostics, string? warning = null, string? message = null)
    {
        if (warning is not null && message is not null)
        {
            diagnostics.Add(new ManagerDiagnostic(ManagerDiagnosticSeverity.Warning, warning, message, null));
        }
        return new CollectionUpdateResult { Outcome = outcome, CollectionId = collectionId, ProfileName = profileName, Diagnostics = diagnostics };
    }

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);
}
