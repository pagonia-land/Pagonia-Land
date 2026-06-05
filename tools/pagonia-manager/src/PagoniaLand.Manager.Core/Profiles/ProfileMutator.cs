namespace PagoniaLand.Manager;

public sealed class ProfileMutationResult
{
    public ProfileFile Profile { get; init; } = new();
    public bool Mutated { get; init; }
    public IReadOnlyList<ManagerDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class ProfileMutator
{
    public ProfileMutationResult Enable(ProfileFile profile, string modId, string version)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var enabled = new List<ProfileEnabledMod>(profile.EnabledMods);
        var loadOrder = new List<string>(profile.LoadOrder);

        var existingIndex = enabled.FindIndex(mod =>
            string.Equals(mod.Id, modId, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            if (string.Equals(enabled[existingIndex].Version, version, StringComparison.Ordinal))
            {
                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Warning,
                    ManagerDiagnosticCodes.ModAlreadyEnabled,
                    $"Mod '{modId}' is already enabled at version '{version}'."));
                return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
            }

            // Preserve the existing per-mod tweak overrides across a version
            // change — tweak ids are stable across versions (a rename is handled
            // via the mod's `aliases:` field), so a version bump should not throw
            // away the user's configured values. Mirrors the Collection-field
            // preservation already guarded in WithLists.
            enabled[existingIndex] = new ProfileEnabledMod
            {
                Id = modId,
                Version = version,
                Tweaks = enabled[existingIndex].Tweaks,
            };
        }
        else
        {
            enabled.Add(new ProfileEnabledMod { Id = modId, Version = version });
            if (!loadOrder.Contains(modId, StringComparer.Ordinal))
            {
                loadOrder.Add(modId);
            }
        }

        return new ProfileMutationResult
        {
            Profile = WithLists(profile, enabled, loadOrder),
            Mutated = true,
            Diagnostics = diagnostics,
        };
    }

    public ProfileMutationResult Disable(ProfileFile profile, string modId)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var enabledIndex = profile.EnabledMods
            .ToList()
            .FindIndex(mod => string.Equals(mod.Id, modId, StringComparison.Ordinal));
        var inLoadOrder = profile.LoadOrder
            .Any(id => string.Equals(id, modId, StringComparison.Ordinal));

        if (enabledIndex < 0 && !inLoadOrder)
        {
            // Not in EnabledMods, not in LoadOrder — genuinely not enabled.
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Warning,
                ManagerDiagnosticCodes.ModNotEnabled,
                $"Mod '{modId}' is not enabled in the active profile."));
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        var enabled = new List<ProfileEnabledMod>(profile.EnabledMods);
        var loadOrder = new List<string>(profile.LoadOrder);
        if (enabledIndex >= 0)
        {
            enabled.RemoveAt(enabledIndex);
            loadOrder.RemoveAll(id => string.Equals(id, modId, StringComparison.Ordinal));
        }
        else
        {
            // Drift case: present in LoadOrder but not EnabledMods. Strip the
            // orphan from LoadOrder + surface as an info diagnostic so the
            // user knows what was cleaned up. Without this branch, the
            // interactive 'Disable' wizard offered the orphan as a choice
            // (it appeared in LoadOrder) but the service would reject it
            // with a ModNotEnabled warning — capability advertised, then
            // refused.
            loadOrder.RemoveAll(id => string.Equals(id, modId, StringComparison.Ordinal));
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.ProfileDriftCleaned,
                $"Removed orphaned load-order entry '{modId}' (was in load order but had no matching enabled-mod row)."));
        }

        return new ProfileMutationResult
        {
            Profile = WithLists(profile, enabled, loadOrder),
            Mutated = true,
            Diagnostics = diagnostics,
        };
    }

    public ProfileMutationResult MoveToPosition(ProfileFile profile, string modId, int position1Based)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var loadOrder = new List<string>(profile.LoadOrder);
        var currentIndex = loadOrder.FindIndex(id => string.Equals(id, modId, StringComparison.Ordinal));

        if (currentIndex < 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.MoveTargetNotInLoadOrder,
                $"Mod '{modId}' is not in the active profile's load order."));
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        if (position1Based < 1 || position1Based > loadOrder.Count)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.MovePositionOutOfRange,
                $"Position {position1Based} is out of range (valid: 1..{loadOrder.Count})."));
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        var newIndex = position1Based - 1;
        if (newIndex == currentIndex)
        {
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        loadOrder.RemoveAt(currentIndex);
        loadOrder.Insert(newIndex, modId);

        return new ProfileMutationResult
        {
            Profile = WithLists(profile, profile.EnabledMods, loadOrder),
            Mutated = true,
            Diagnostics = diagnostics,
        };
    }

    public ProfileMutationResult MoveBefore(ProfileFile profile, string modId, string anchorId)
        => MoveRelative(profile, modId, anchorId, placeAfter: false);

    public ProfileMutationResult MoveAfter(ProfileFile profile, string modId, string anchorId)
        => MoveRelative(profile, modId, anchorId, placeAfter: true);

    private static ProfileMutationResult MoveRelative(ProfileFile profile, string modId, string anchorId, bool placeAfter)
    {
        var diagnostics = new List<ManagerDiagnostic>();
        var loadOrder = new List<string>(profile.LoadOrder);
        var currentIndex = loadOrder.FindIndex(id => string.Equals(id, modId, StringComparison.Ordinal));

        if (currentIndex < 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.MoveTargetNotInLoadOrder,
                $"Mod '{modId}' is not in the active profile's load order."));
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        if (string.Equals(modId, anchorId, StringComparison.Ordinal))
        {
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        loadOrder.RemoveAt(currentIndex);

        var anchorIndex = loadOrder.FindIndex(id => string.Equals(id, anchorId, StringComparison.Ordinal));
        if (anchorIndex < 0)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.MoveAnchorNotInLoadOrder,
                $"Anchor mod '{anchorId}' is not in the active profile's load order."));
            return new ProfileMutationResult { Profile = profile, Diagnostics = diagnostics };
        }

        var insertIndex = placeAfter ? anchorIndex + 1 : anchorIndex;
        loadOrder.Insert(insertIndex, modId);

        return new ProfileMutationResult
        {
            Profile = WithLists(profile, profile.EnabledMods, loadOrder),
            Mutated = true,
            Diagnostics = diagnostics,
        };
    }

    private static ProfileFile WithLists(
        ProfileFile profile,
        IEnumerable<ProfileEnabledMod> enabled,
        IEnumerable<string> loadOrder)
        => new()
        {
            ProfileVersion = profile.ProfileVersion,
            Name = profile.Name,
            Collection = profile.Collection,
            EnabledMods = enabled.ToList(),
            LoadOrder = loadOrder.ToList(),
        };
}
