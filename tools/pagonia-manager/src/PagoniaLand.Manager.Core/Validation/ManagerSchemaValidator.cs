using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace PagoniaLand.Manager;

public static class ManagerReportKinds
{
    public const string Install = "install";
    public const string Uninstall = "uninstall";
    public const string Deploy = "deploy";
    public const string Rollback = "rollback";
    public const string CollectionInstall = "collectionInstall";
    public const string Status = "status";
    public const string DeployStatus = "deployStatus";
    public const string TweakList = "tweakList";
    public const string TweakSet = "tweakSet";
    public const string TweakReset = "tweakReset";
    public const string ExpansionsList = "expansionsList";
    public const string ExpansionsSet = "expansionsSet";
    public const string Updates = "updates";

    public static readonly IReadOnlyList<string> All =
    [
        Install, Uninstall, Deploy, Rollback, CollectionInstall, Status, DeployStatus,
        TweakList, TweakSet, TweakReset, ExpansionsList, ExpansionsSet, Updates,
    ];
}

public sealed class ManagerSchemaValidator
{
    private const string ResourcePrefix = "PagoniaLand.Manager.Schemas.";

    private static readonly Dictionary<string, string> KindToFile = new(StringComparer.Ordinal)
    {
        [ManagerReportKinds.Install] = "install-report.schema.json",
        [ManagerReportKinds.Uninstall] = "uninstall-report.schema.json",
        [ManagerReportKinds.Deploy] = "deploy-report.schema.json",
        [ManagerReportKinds.Rollback] = "rollback-report.schema.json",
        [ManagerReportKinds.CollectionInstall] = "collection-install-report.schema.json",
        [ManagerReportKinds.Status] = "status-report.schema.json",
        [ManagerReportKinds.DeployStatus] = "deploy-status-report.schema.json",
        [ManagerReportKinds.TweakList] = "tweak-list.schema.json",
        [ManagerReportKinds.TweakSet] = "tweak-set.schema.json",
        [ManagerReportKinds.TweakReset] = "tweak-reset.schema.json",
        [ManagerReportKinds.ExpansionsList] = "expansions-list-report.schema.json",
        [ManagerReportKinds.ExpansionsSet] = "expansions-set-report.schema.json",
        [ManagerReportKinds.Updates] = "updates-report.schema.json",
    };

    public IReadOnlyList<ManagerDiagnostic> ValidateReport(string kind, string reportPath)
    {
        var diagnostics = new List<ManagerDiagnostic>();

        if (!KindToFile.TryGetValue(kind, out var schemaFile))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.SchemaValidationFailed,
                $"Unknown report kind '{kind}'. Valid kinds: {string.Join(", ", ManagerReportKinds.All)}."));
            return diagnostics;
        }

        if (!File.Exists(reportPath))
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.SchemaValidationFailed,
                $"Report file '{reportPath}' not found.",
                reportPath));
            return diagnostics;
        }

        JsonNode? node;
        try
        {
            var jsonText = File.ReadAllText(reportPath);
            node = JsonNode.Parse(jsonText);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.SchemaValidationFailed,
                $"Failed to parse report as JSON: {ex.Message}",
                reportPath));
            return diagnostics;
        }

        var schema = LoadEmbeddedSchema(schemaFile);
        EvaluationResults results;
        try
        {
            // JsonSchema.Net 9 evaluates a JsonElement (was JsonNode in 7.x). Round-trip the
            // parsed node through its JSON text; both calls are reflection-free (AOT-safe).
            using var doc = JsonDocument.Parse(node is null ? "null" : node.ToJsonString());
            results = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        }
        catch (Exception ex)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Error,
                ManagerDiagnosticCodes.SchemaValidationFailed,
                $"Schema evaluation failed: {ex.Message}",
                reportPath));
            return diagnostics;
        }

        if (results.IsValid)
        {
            diagnostics.Add(new ManagerDiagnostic(
                ManagerDiagnosticSeverity.Info,
                ManagerDiagnosticCodes.SchemaValidationOk,
                $"Report at '{reportPath}' conforms to the '{kind}' schema.",
                reportPath));
            return diagnostics;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        CollectHierarchicalErrors(results, reportPath, seen, diagnostics);
        return diagnostics;
    }

    private static void CollectHierarchicalErrors(
        EvaluationResults result,
        string filePath,
        HashSet<string> seen,
        List<ManagerDiagnostic> diagnostics)
    {
        if (result.IsValid)
        {
            return;
        }

        if (result.Errors is { Count: > 0 })
        {
            var instanceLocation = result.InstanceLocation.ToString();
            var locationHint = string.IsNullOrEmpty(instanceLocation) ? "(root)" : instanceLocation;

            foreach (var (keyword, message) in result.Errors)
            {
                var key = $"{locationHint}|{keyword}|{message}";
                if (!seen.Add(key))
                {
                    continue;
                }

                diagnostics.Add(new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Error,
                    ManagerDiagnosticCodes.SchemaValidationFailed,
                    $"{locationHint}: {message} [{keyword}]",
                    filePath));
            }
        }

        foreach (var child in result.Details ?? [])
        {
            CollectHierarchicalErrors(child, filePath, seen, diagnostics);
        }
    }

    private static JsonSchema LoadEmbeddedSchema(string fileName)
    {
        var assembly = typeof(ManagerSchemaValidator).Assembly;
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema '{resourceName}' not found. " +
                "Check the <EmbeddedResource> entries in PagoniaLand.Manager.Core.csproj.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        // JsonSchema.Net 9 registers each evaluated schema by $id and refuses to overwrite it
        // with a different document — so re-parsing the same report schema on a later
        // ValidateReport call threw "Overwriting registered schemas is not permitted". Build
        // into a fresh local registry (which still falls back to the global one) so each parse
        // is isolated. The report schemas are self-contained (only internal #/$defs refs), so a
        // per-schema registry resolves everything it needs.
        return JsonSchema.FromText(reader.ReadToEnd(), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
    }
}
