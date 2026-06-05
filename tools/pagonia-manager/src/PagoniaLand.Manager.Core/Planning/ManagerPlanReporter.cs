using System.Text.Json;
using System.Text.Json.Nodes;
using PagoniaLand.Patcher;

namespace PagoniaLand.Manager;

public sealed class ManagerPlanReporter
{
    private static readonly JsonSerializerOptions WriteIndented = new() { WriteIndented = true };

    public void WriteReports(PlanProfileResult plan, string? markdownPath, string? jsonPath)
    {
        if (!string.IsNullOrWhiteSpace(markdownPath))
        {
            var directory = Path.GetDirectoryName(markdownPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AtomicFile.WriteAllText(markdownPath, ToMarkdown(plan));
        }

        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            var directory = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AtomicFile.WriteAllText(jsonPath, ToJson(plan));
        }
    }

    public string ToJson(PlanProfileResult plan)
    {
        var managerNode = new JsonObject
        {
            ["profile"] = plan.ProfileName,
            ["gameRoot"] = plan.GameRoot,
            ["success"] = plan.Success,
            ["diagnostics"] = new JsonArray(plan.ManagerDiagnostics
                .Select(d => (JsonNode?)new JsonObject
                {
                    ["severity"] = d.Severity.ToString(),
                    ["code"] = d.Code,
                    ["message"] = d.Message,
                    ["path"] = d.Path,
                })
                .ToArray()),
        };

        JsonNode? patcherNode = null;
        if (plan.PatcherPlan is not null)
        {
            var patcherJson = new PatchPlanReporter().ToJson(plan.PatcherPlan, planSource: "managerProfile");
            patcherNode = JsonNode.Parse(patcherJson);
        }

        var wrapper = new JsonObject
        {
            ["manager"] = managerNode,
            ["patcher"] = patcherNode,
        };

        return wrapper.ToJsonString(WriteIndented);
    }

    public string ToMarkdown(PlanProfileResult plan)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Pagonia Land Manager — Plan Report");
        sb.AppendLine();
        sb.AppendLine($"Profile: {plan.ProfileName ?? "(none)"}");
        sb.AppendLine($"Game root: {plan.GameRoot}");
        sb.AppendLine($"Result: {(plan.Success ? "OK" : "Blocked")}");
        sb.AppendLine();

        sb.AppendLine("## Manager Diagnostics");
        if (plan.ManagerDiagnostics.Count == 0)
        {
            sb.AppendLine("(none)");
        }
        else
        {
            foreach (var d in plan.ManagerDiagnostics)
            {
                sb.AppendLine($"- [{d.Severity}] [{d.Code}] {d.Message}");
            }
        }
        sb.AppendLine();

        if (plan.PatcherPlan is not null)
        {
            sb.AppendLine("## Patcher Plan");
            sb.AppendLine();
            sb.Append(new PatchPlanReporter().ToMarkdown(plan.PatcherPlan, planSource: "managerProfile"));
        }
        else
        {
            sb.AppendLine("## Patcher Plan");
            sb.AppendLine();
            sb.AppendLine("(not produced — aborted by manager-level errors above)");
        }

        return sb.ToString();
    }
}
