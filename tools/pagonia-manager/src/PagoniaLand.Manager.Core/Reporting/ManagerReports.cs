using System.Text.Json;
using System.Text.Json.Nodes;

namespace PagoniaLand.Manager;

// AOT-safe JSON report builders. Each result type has a ToJson + WriteJson helper. JsonNode is
// reflection-free, so this whole file is trim-friendly without source generators.
//
// Shape conventions:
//  - top-level "manager.<x>" key naming uses camelCase
//  - "schemaVersion" is "0.1" on every report — the schemas-versioning hook for future migrations.
//    Each report schema versions independently, so a future additive field bumps only its own report.
//  - "diagnostics" is always an array of { severity, code, message, path? }
public static class ManagerReports
{
    public const string CurrentReportVersion = "0.1";

    private static readonly JsonSerializerOptions WriteIndented = new() { WriteIndented = true };

    public static string ToJson(InstallResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(UninstallResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(DeployResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(RollbackResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(CollectionInstallResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(ActiveProfileResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToJson(DeployStatusResult result, string gameRoot) => Build(result, gameRoot).ToJsonString(WriteIndented);
    public static string ToExpansionsListJson(ExpansionListResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToExpansionsSetJson(ExpansionSetResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToTweakListJson(TweakReadResult result) => Build(result).ToJsonString(WriteIndented);
    public static string ToTweakSetJson(TweakMutationResult result, string tweakId, string value) => BuildSet(result, tweakId, value).ToJsonString(WriteIndented);
    public static string ToTweakResetJson(TweakMutationResult result, string? tweakId) => BuildReset(result, tweakId).ToJsonString(WriteIndented);

    public static void WriteJson(string path, string json)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        AtomicFile.WriteAllText(path, json);
    }

    private static JsonObject Build(InstallResult result) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "install",
        ["outcome"] = result.Outcome.ToString(),
        ["modId"] = result.ModId,
        ["version"] = result.Version,
        ["manifestName"] = result.ManifestName,
        ["installPath"] = result.InstallPath,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject Build(UninstallResult result) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "uninstall",
        ["outcome"] = result.Outcome.ToString(),
        ["modId"] = result.ModId,
        ["version"] = result.Version,
        ["removedPath"] = result.RemovedPath,
        ["parentDirectoryPruned"] = result.ParentDirectoryPruned,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject Build(DeployResult result) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "deploy",
        ["outcome"] = result.Outcome.ToString(),
        ["gameFingerprint"] = result.GameFingerprint,
        ["timestamp"] = result.Timestamp,
        ["profile"] = result.ProfileName,
        ["modifiedFileCount"] = result.ModifiedFileCount,
        ["addedFileCount"] = result.AddedFileCount,
        // paks rebuilt during a live-install deploy. Zero on
        // extracted-layout deploys (which write loose XMLs into modifiedFileCount).
        ["rebuiltPakCount"] = result.RebuiltPakCount,
        ["manifestPath"] = result.ManifestPath,
        ["backupDirectory"] = result.BackupDirectory,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject Build(RollbackResult result) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "rollback",
        ["outcome"] = result.Outcome.ToString(),
        ["gameFingerprint"] = result.GameFingerprint,
        ["revertedTimestamp"] = result.RevertedTimestamp,
        ["revertedProfile"] = result.RevertedProfile,
        ["restoredFileCount"] = result.RestoredFileCount,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject Build(CollectionInstallResult result)
    {
        var installedMods = new JsonArray(result.InstalledMods
            .Select(m => (JsonNode?)new JsonObject { ["id"] = m.Id, ["version"] = m.Version })
            .ToArray());

        return new JsonObject
        {
            ["schemaVersion"] = CurrentReportVersion,
            ["reportKind"] = "collectionInstall",
            ["outcome"] = result.Outcome.ToString(),
            ["collectionId"] = result.CollectionId,
            ["collectionVersion"] = result.CollectionVersion,
            ["collectionName"] = result.CollectionName,
            ["profileName"] = result.ProfileName,
            ["manifestPath"] = result.ManifestPath,
            ["lockfilePath"] = result.LockfilePath,
            ["installedMods"] = installedMods,
            ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
        };
    }

    private static JsonObject Build(ActiveProfileResult result)
    {
        var enabledMods = new JsonArray((result.Profile?.EnabledMods ?? new List<ProfileEnabledMod>())
            .Select(m => (JsonNode?)new JsonObject { ["id"] = m.Id, ["version"] = m.Version })
            .ToArray());

        var loadOrder = new JsonArray((result.Profile?.LoadOrder ?? new List<string>())
            .Select(id => (JsonNode?)id)
            .ToArray());

        return new JsonObject
        {
            ["schemaVersion"] = CurrentReportVersion,
            ["reportKind"] = "status",
            ["success"] = result.Success,
            ["profile"] = result.ProfileName,
            ["collection"] = result.Profile?.Collection,
            ["enabledMods"] = enabledMods,
            ["loadOrder"] = loadOrder,
            ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
        };
    }

    private static JsonObject Build(DeployStatusResult result, string gameRoot)
    {
        var deploys = new JsonArray(result.Deploys
            .Select(d => (JsonNode?)new JsonObject
            {
                ["timestamp"] = d.Timestamp,
                ["profile"] = d.Profile,
                ["modCount"] = d.ModCount,
                ["fileCount"] = d.FileCount,
            })
            .ToArray());

        return new JsonObject
        {
            ["schemaVersion"] = CurrentReportVersion,
            ["reportKind"] = "deployStatus",
            ["gameRoot"] = Path.GetFullPath(gameRoot),
            ["gameFingerprint"] = result.GameFingerprint,
            // The install's real game version (read from the exe's ProductVersion —
            // the same string mods declare as gameDatabaseVersion). null = unknown.
            ["gameProductVersion"] = result.GameProductVersion,
            ["hasDeploys"] = result.HasDeploys,
            ["deploys"] = deploys,
            ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
        };
    }

    private static JsonObject Build(TweakReadResult result)
    {
        var tweaks = new JsonArray(result.Tweaks
            .Select(t => (JsonNode?)new JsonObject
            {
                ["id"] = t.Declaration.Id,
                ["type"] = t.Declaration.Type,
                ["label"] = t.Declaration.Label,
                ["description"] = t.Declaration.Description,
                ["default"] = t.Declaration.Default,
                ["value"] = t.Value,
                ["origin"] = t.Origin,
                ["min"] = t.Declaration.Min,
                ["max"] = t.Declaration.Max,
                ["step"] = t.Declaration.Step,
                ["values"] = new JsonArray(t.Declaration.Values
                    .Select(v => (JsonNode?)new JsonObject { ["value"] = v.Value, ["label"] = v.Label })
                    .ToArray()),
            })
            .ToArray());

        return new JsonObject
        {
            ["schemaVersion"] = CurrentReportVersion,
            ["reportKind"] = "tweakList",
            ["success"] = result.Success,
            ["profile"] = result.ProfileName,
            ["modId"] = result.ModId,
            ["modVersion"] = result.ModVersion,
            ["tweaks"] = tweaks,
            ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
        };
    }

    private static JsonObject BuildSet(TweakMutationResult result, string tweakId, string value) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "tweakSet",
        ["success"] = result.Success,
        ["mutated"] = result.Mutated,
        ["profile"] = result.ProfileName,
        ["modId"] = result.ModId,
        ["tweakId"] = tweakId,
        ["value"] = value,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject BuildReset(TweakMutationResult result, string? tweakId) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "tweakReset",
        ["success"] = result.Success,
        ["mutated"] = result.Mutated,
        ["profile"] = result.ProfileName,
        ["modId"] = result.ModId,
        ["tweakId"] = tweakId,
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    private static JsonObject Build(ExpansionListResult result)
    {
        var expansions = new JsonArray(result.Expansions
            .Select(e => (JsonNode?)new JsonObject
            {
                ["package"] = e.Package,
                ["present"] = e.Present,
                ["owned"] = OwnershipText(e.Ownership),
                ["effective"] = e.Effective,
            })
            .ToArray());

        return new JsonObject
        {
            ["schemaVersion"] = CurrentReportVersion,
            ["reportKind"] = "expansionsList",
            ["success"] = result.Success,
            ["gameRoot"] = result.GameRoot,
            ["gameFingerprint"] = result.GameFingerprint,
            ["expansions"] = expansions,
            ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
        };
    }

    private static JsonObject Build(ExpansionSetResult result) => new()
    {
        ["schemaVersion"] = CurrentReportVersion,
        ["reportKind"] = "expansionsSet",
        ["success"] = result.Success,
        ["mutated"] = result.Mutated,
        ["gameRoot"] = result.GameRoot,
        ["gameFingerprint"] = result.GameFingerprint,
        ["package"] = result.Package,
        ["owned"] = OwnershipText(result.State),
        ["diagnostics"] = DiagnosticsArray(result.Diagnostics),
    };

    // Tri-state ownership as a stable lowercase token in the JSON report.
    private static string OwnershipText(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "owned",
        OwnershipState.NotOwned => "not-owned",
        _ => "unknown",
    };

    private static JsonArray DiagnosticsArray(IReadOnlyList<ManagerDiagnostic> diagnostics)
        => new(diagnostics
            .Select(d => (JsonNode?)new JsonObject
            {
                ["severity"] = d.Severity.ToString(),
                ["code"] = d.Code,
                ["message"] = d.Message,
                ["path"] = d.Path,
            })
            .ToArray());
}
