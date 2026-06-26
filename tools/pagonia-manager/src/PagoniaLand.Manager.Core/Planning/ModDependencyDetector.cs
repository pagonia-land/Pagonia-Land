using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

/// <summary>
/// Evaluates the mod manifest's declared <c>dependencies</c> and <c>incompatibleWith</c> across a
/// profile's enabled set — fields the manager parses but historically ignored. Advisory only
/// (warnings); it mutates nothing and never blocks a plan / deploy / enable:
///
/// <list type="bullet">
///   <item><b>Missing dependency</b> — an enabled mod requires another mod that isn't enabled in the
///   profile (<c>manager.modDependencyMissing</c>); the message says whether the dep is installed but
///   disabled, or not installed at all.</item>
///   <item><b>Incompatible pair</b> — two enabled mods where either lists the other in
///   <c>incompatibleWith</c> (<c>manager.modIncompatibleEnabled</c>), reported once per pair.</item>
/// </list>
///
/// <para>
/// With <c>focusModId</c> set (the enable flow), only relations involving that mod are reported, so
/// enabling one mod doesn't dump every pre-existing relation in the profile.
/// </para>
/// </summary>
public sealed class ModDependencyDetector
{
    /// <param name="enabledMods">The profile's enabled mods (manifests loaded). Order is irrelevant.</param>
    /// <param name="installedIds">Every installed mod id — to tell "installed but disabled" from "absent".</param>
    /// <param name="focusModId">When set, only emit diagnostics that involve this mod.</param>
    public IReadOnlyList<ManagerDiagnostic> Detect(
        IReadOnlyList<LoadedMod> enabledMods,
        IReadOnlySet<string> installedIds,
        string? focusModId = null)
    {
        var enabledIds = new HashSet<string>(enabledMods.Select(m => m.Manifest.Id), StringComparer.Ordinal);
        var diagnostics = new List<ManagerDiagnostic>();

        // Missing dependencies — an enabled mod needs another that isn't enabled.
        foreach (var mod in enabledMods)
        {
            if (focusModId is not null && !string.Equals(mod.Manifest.Id, focusModId, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var dependency in mod.Manifest.Dependencies)
            {
                if (enabledIds.Contains(dependency))
                {
                    continue;
                }

                var note = installedIds.Contains(dependency)
                    ? $"'{dependency}' is installed but not enabled — enable it"
                    : $"'{dependency}' is not installed";
                diagnostics.Add(Warning(ManagerDiagnosticCodes.ModDependencyMissing,
                    $"Mod '{mod.Manifest.Id}' depends on '{dependency}', which isn't enabled in this profile ({note})."));
            }
        }

        // Incompatible pairs — either side listing the other, deduped per unordered pair.
        for (var i = 0; i < enabledMods.Count; i++)
        {
            for (var j = i + 1; j < enabledMods.Count; j++)
            {
                var a = enabledMods[i].Manifest;
                var b = enabledMods[j].Manifest;

                if (focusModId is not null
                    && !string.Equals(a.Id, focusModId, StringComparison.Ordinal)
                    && !string.Equals(b.Id, focusModId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (a.IncompatibleWith.Contains(b.Id) || b.IncompatibleWith.Contains(a.Id))
                {
                    diagnostics.Add(Warning(ManagerDiagnosticCodes.ModIncompatibleEnabled,
                        $"Mods '{a.Id}' and '{b.Id}' are marked incompatible (one lists the other in incompatibleWith) but both are enabled. Disable one."));
                }
            }
        }

        return diagnostics;
    }

    /// <summary>The ids of enabled mods that declare <paramref name="modId"/> in their
    /// <c>dependencies</c> — i.e. the mods that would be left with an unmet dependency if
    /// <paramref name="modId"/> were disabled or uninstalled.</summary>
    public static IReadOnlyList<string> DependentsOf(IReadOnlyList<LoadedMod> enabledMods, string modId)
        => enabledMods
            .Where(m => !string.Equals(m.Manifest.Id, modId, StringComparison.Ordinal)
                && m.Manifest.Dependencies.Contains(modId))
            .Select(m => m.Manifest.Id)
            .ToList();

    private static ManagerDiagnostic Warning(string code, string message)
        => new(ManagerDiagnosticSeverity.Warning, code, message, null);
}
