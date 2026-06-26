namespace PagoniaLand.Manager;

/// <summary>Why an <see cref="ModUpdateService.Update"/> ended the way it did.</summary>
public enum ModUpdateOutcome
{
    /// <summary>The active profile's pin was moved to a newer installed version.</summary>
    Updated,

    /// <summary>The source already advertises the pinned version (or older) — nothing to do.</summary>
    AlreadyCurrent,

    /// <summary>The mod isn't enabled in the active profile, so there's no pin to move.</summary>
    NotEnabled,

    /// <summary>The pinned mod has no <c>gh:</c> provenance to check / fetch from.</summary>
    NoRemoteSource,

    /// <summary>Detection or the fetch/install of the new version failed (see diagnostics).</summary>
    Failed,
}

/// <summary>Outcome of a transparent mod update.</summary>
public sealed class ModUpdateResult
{
    public ModUpdateOutcome Outcome { get; init; }
    public string ModId { get; init; } = string.Empty;
    public string? ProfileName { get; init; }
    public string? FromVersion { get; init; }
    public string? ToVersion { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = Array.Empty<ManagerDiagnostic>();
}

/// <summary>
/// The transparent, opt-in mod update: move the active profile's pin for one mod from its current
/// version to the newer one its source repo advertises. Always reuses the existing building blocks so
/// the behaviour matches the rest of the manager:
///
/// <list type="number">
/// <item>detect — compare the pinned version against the source's <c>index.yaml</c> at the default
/// branch (mirror-first, the same check <c>outdated</c> uses);</item>
/// <item>install the new version through the normal remote-install path — it <b>coexists</b> with the
/// old version on disk (each version is its own directory), so the old one stays as a rollback anchor;</item>
/// <item>re-point the profile pin via <see cref="ProfileMutator.Enable"/>, which preserves the mod's
/// per-mod tweak overrides across the version change (a renamed tweak is migrated forward by the
/// existing on-read alias migration, so no value is lost).</item>
/// </list>
///
/// It never deletes the old version and never touches a mod the active profile hasn't enabled — there's
/// no pin to move in that case. Read-then-write: nothing changes unless a strictly-newer version exists.
/// </summary>
public sealed class ModUpdateService
{
    private readonly IRemoteContentFetcher _http;
    private readonly bool _allowInsecureSources;

    public ModUpdateService(IRemoteContentFetcher http, bool allowInsecureSources)
    {
        _http = http;
        _allowInsecureSources = allowInsecureSources;
    }

    public ModUpdateResult Update(StoreLayout layout, string modId, string profileName, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var profileStore = new ProfileStore();

        // A typo'd --profile must not throw out of ProfileStore.Read (which crashes the CLI with a
        // stack trace + runtime exit code); surface a clean profileMissing like PlanProfileService does.
        if (!profileStore.Exists(layout, profileName))
        {
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics,
                warning: ManagerDiagnosticCodes.ProfileMissing,
                message: $"Profile '{profileName}' does not exist.");
        }

        var profile = profileStore.Read(layout, profileName);

        var pinned = profile.EnabledMods.FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.Ordinal));
        if (pinned is null)
        {
            return Result(ModUpdateOutcome.NotEnabled, modId, profileName, diagnostics, warning: ManagerDiagnosticCodes.ModUpdateNotEnabled,
                message: $"Mod '{modId}' isn't enabled in profile '{profileName}' — nothing to update. Install + enable it first.");
        }

        var fromVersion = pinned.Version;

        // The provenance lives on the *installed* copy of the pinned version (its sidecar).
        var installedPin = new ModLister().List(layout)
            .FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.Ordinal)
                && string.Equals(m.Version, fromVersion, StringComparison.Ordinal));
        if (installedPin is null || string.IsNullOrWhiteSpace(installedPin.Source)
            || !RemoteSourceParser.TryParse(installedPin.Source, out var parsed)
            || parsed is not GitHubSource source)
        {
            return Result(ModUpdateOutcome.NoRemoteSource, modId, profileName, diagnostics, warning: ManagerDiagnosticCodes.ModUpdateNoRemoteSource,
                message: $"Mod '{modId}@{fromVersion}' has no gh: source to update from (local install, or the pinned version isn't on disk).");
        }

        // Detect: what does the source advertise at the default branch?
        var index = new RepoIndexFetcher(_http).Fetch(source with { Ref = "HEAD" }, cancellationToken);
        if (!index.Success || !index.HasIndex || index.Index is null)
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                $"Couldn't reach {source.Owner}/{source.Repo} to check '{modId}' for an update."));
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics);
        }

        var entry = index.Index.Mods.FirstOrDefault(e => string.Equals(e.Id, modId, StringComparison.Ordinal));
        if (entry is null || string.IsNullOrWhiteSpace(entry.Version))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                $"{source.Owner}/{source.Repo} no longer lists '{modId}' with a version in index.yaml."));
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics);
        }

        // Mirror UpdateDetectionService's R5-019 guard: if the two version strings differ but exactly
        // one is parseable, IsNewer can't compare them — don't fall through and claim "already
        // current"; surface that the check couldn't run (a real update may hide behind an unparseable
        // version string), so the apply path agrees with what `outdated`'s detection reported.
        if (!string.Equals(entry.Version, fromVersion, StringComparison.Ordinal)
            && ModVersion.TryParse(entry.Version, out _) != ModVersion.TryParse(fromVersion, out _))
        {
            diagnostics.Add(Warning(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                $"Could not compare versions for '{modId}': installed '{fromVersion}' vs advertised '{entry.Version}' (one isn't a parseable version)."));
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics);
        }

        if (!ModVersion.IsNewer(entry.Version, fromVersion))
        {
            // Reaches here on equality AND when the source advertises an older version (a rolled-back
            // index), so name both rather than mislabel the installed pin as "what the source advertises".
            diagnostics.Add(Info(ManagerDiagnosticCodes.ModUpdateAlreadyCurrent,
                $"'{modId}' is already current ({fromVersion}); the source advertises {entry.Version}."));
            return Result(ModUpdateOutcome.AlreadyCurrent, modId, profileName, diagnostics);
        }

        // Install the new version from the same source at HEAD — coexists with the old one.
        var basePart = string.IsNullOrEmpty(source.BasePath) ? string.Empty : $":{source.BasePath}";
        var spec = $"gh:{source.Owner}/{source.Repo}{basePart}/{source.ModSpec ?? modId}";
        var resolution = InstallSourceResolver.ResolveRemote(spec, layout, _http, _allowInsecureSources);
        if (resolution is null || resolution.Aborted || resolution.InstallSource is null)
        {
            diagnostics.AddRange(resolution?.Diagnostics ?? Array.Empty<ManagerDiagnostic>());
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                $"Couldn't fetch the new version of '{modId}' from {source.Owner}/{source.Repo}."));
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics);
        }

        InstallResult install;
        try
        {
            install = new ModInstaller().Install(resolution.InstallSource, layout, resolution.RemoteProvenance);
        }
        finally
        {
            if (resolution.TempDir is not null && Directory.Exists(resolution.TempDir))
            {
                try { Directory.Delete(resolution.TempDir, recursive: true); } catch { /* best-effort temp cleanup */ }
            }
        }

        diagnostics.AddRange(install.Diagnostics);
        if (install.Outcome == InstallOutcome.Failed || string.IsNullOrWhiteSpace(install.Version))
        {
            diagnostics.Add(Error(ManagerDiagnosticCodes.ModUpdateCheckFailed,
                $"Installing the new version of '{modId}' failed; the profile pin is unchanged."));
            return Result(ModUpdateOutcome.Failed, modId, profileName, diagnostics);
        }

        var toVersion = install.Version;

        // Re-point the pin (preserving tweak overrides). The old version stays on disk as a rollback
        // anchor — prune it later with `uninstall <id> --version <old>` if you want.
        var mutation = new ProfileMutator().Enable(profile, modId, toVersion);
        diagnostics.AddRange(mutation.Diagnostics);
        if (!mutation.Mutated)
        {
            // The pin already pointed at toVersion (e.g. moved out-of-band) — don't claim an update
            // that didn't happen. The new version is installed on disk; the pin simply didn't move.
            diagnostics.Add(Info(ManagerDiagnosticCodes.ModUpdateAlreadyCurrent,
                $"'{modId}' is already pinned to {toVersion} in profile '{profileName}'; nothing to re-point."));
            return Result(ModUpdateOutcome.AlreadyCurrent, modId, profileName, diagnostics);
        }

        profileStore.Write(layout, mutation.Profile);

        diagnostics.Add(Info(ManagerDiagnosticCodes.ModUpdated,
            $"Updated '{modId}' {fromVersion} -> {toVersion} in profile '{profileName}'. The previous version is kept on disk for rollback (enable {modId} --version {fromVersion})."));

        return new ModUpdateResult
        {
            Outcome = ModUpdateOutcome.Updated,
            ModId = modId,
            ProfileName = profileName,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Diagnostics = diagnostics,
        };
    }

    private static ModUpdateResult Result(ModUpdateOutcome outcome, string modId, string profileName, List<ManagerDiagnostic> diagnostics,
        string? warning = null, string? message = null)
    {
        if (warning is not null && message is not null)
        {
            diagnostics.Add(new ManagerDiagnostic(ManagerDiagnosticSeverity.Warning, warning, message, null));
        }
        return new ModUpdateResult { Outcome = outcome, ModId = modId, ProfileName = profileName, Diagnostics = diagnostics };
    }

    private static ManagerDiagnostic Info(string code, string message)
        => new(ManagerDiagnosticSeverity.Info, code, message, null);

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);

    private static ManagerDiagnostic Error(string code, string message)
        => new(ManagerDiagnosticSeverity.Error, code, message, null);
}
