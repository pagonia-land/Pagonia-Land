using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Patcher;

public sealed class PatchPlanReporter
{
    public void WriteReports(CombinedPatchPlan plan, string? markdownPath, string? jsonPath, string planSource = "directMods")
    {
        if (!string.IsNullOrWhiteSpace(markdownPath))
        {
            WriteFile(markdownPath, ToMarkdown(plan, planSource));
        }

        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            WriteFile(jsonPath, ToJson(plan, planSource));
        }
    }

    public string ToJson(CombinedPatchPlan plan, string planSource = "directMods")
    {
        var report = new PatchPlanReport(
            planSource,
            plan.Success,
            plan.ModPlans.Count,
            plan.Writes.Count,
            plan.Conflicts.Count,
            plan.ModPlans.Select(modPlan => new PatchPlanModReport(
                modPlan.Mod.Manifest.Id,
                modPlan.Mod.Manifest.Name,
                modPlan.Mod.Manifest.Version,
                modPlan.Writes.Count,
                modPlan.EntryWrites.Count,
                modPlan.ResolvedTweaks.Select(tweak => new ResolvedTweakReport(
                    tweak.TweakId,
                    tweak.SourceValue,
                    tweak.ResolvedValue,
                    tweak.Origin)).ToList())).ToList(),
            plan.Writes.Select(write => new PatchPlanWriteReport(
                write.OperationId,
                write.OperationType,
                write.File,
                write.EntityGuid,
                write.EntityName,
                write.Component,
                write.Path,
                write.Attribute,
                write.OldValue,
                write.NewValue)).ToList(),
            plan.Conflicts.Select(conflict => new PatchPlanConflictReport(
                conflict.Type,
                conflict.TargetKey,
                conflict.Writes.Select(write => write.OperationId).ToList())).ToList(),
            plan.EntryWrites.Select(entry => new PatchPlanEntryReport(
                entry.ModId,
                entry.Operation.ToString(),
                entry.Path,
                entry.SourceFile)).ToList(),
            plan.EntryConflicts.Select(conflict => new PatchPlanEntryConflictReport(
                conflict.Type,
                conflict.Path,
                conflict.Writes.Select(w => w.ModId).Distinct().ToList())).ToList(),
            plan.Diagnostics.Concat(plan.ModPlans.SelectMany(modPlan => modPlan.Diagnostics))
                .Select(diagnostic => new PatchPlanDiagnosticReport(
                    diagnostic.Severity.ToString(),
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.Path))
                .ToList());

        return JsonSerializer.Serialize(report, PatchPlanJsonContext.Default.PatchPlanReport);
    }

    public string ToMarkdown(CombinedPatchPlan plan, string planSource = "directMods")
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Pagonia Land Patch Plan");
        builder.AppendLine();
        builder.AppendLine($"Source: {planSource}");
        builder.AppendLine($"Result: {(plan.Success ? "OK" : "Blocked")}");
        builder.AppendLine($"Mods: {plan.ModPlans.Count}");
        builder.AppendLine($"Writes: {plan.Writes.Count}");
        builder.AppendLine($"Conflicts: {plan.Conflicts.Count}");
        if (plan.EntryWrites.Count > 0 || plan.EntryConflicts.Count > 0)
        {
            builder.AppendLine($"Entry operations: {plan.EntryWrites.Count}");
            builder.AppendLine($"Entry conflicts: {plan.EntryConflicts.Count}");
        }
        builder.AppendLine();

        builder.AppendLine("## Mods");
        builder.AppendLine();
        builder.AppendLine("| Mod | Version | Writes |");
        builder.AppendLine("| --- | --- | ---: |");

        foreach (var modPlan in plan.ModPlans)
        {
            builder.AppendLine($"| {Escape(modPlan.Mod.Manifest.Name)} | {Escape(modPlan.Mod.Manifest.Version)} | {modPlan.Writes.Count} |");
        }

        if (plan.ModPlans.Any(modPlan => modPlan.ResolvedTweaks.Count > 0))
        {
            builder.AppendLine();
            builder.AppendLine("## Tweaks");
            builder.AppendLine();
            builder.AppendLine("| Mod | Tweak | Value | Origin |");
            builder.AppendLine("| --- | --- | --- | --- |");

            foreach (var modPlan in plan.ModPlans)
            {
                foreach (var tweak in modPlan.ResolvedTweaks)
                {
                    builder.AppendLine($"| {Escape(modPlan.Mod.Manifest.Id)} | {Escape(tweak.TweakId)} | {Escape(tweak.ResolvedValue)} | {Escape(tweak.Origin)} |");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Writes");
        builder.AppendLine();

        if (plan.Writes.Count == 0)
        {
            builder.AppendLine("No writes.");
        }
        else
        {
            builder.AppendLine("| Operation | Target | Change |");
            builder.AppendLine("| --- | --- | --- |");

            foreach (var write in plan.Writes)
            {
                var target = $"{write.EntityName}/{write.Component}/{write.Path}";
                builder.AppendLine($"| {Escape(write.OperationId)} | {Escape(target)} | {Escape(write.OldValue)} -> {Escape(write.NewValue)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Conflicts");
        builder.AppendLine();

        if (plan.Conflicts.Count == 0)
        {
            builder.AppendLine("No conflicts.");
        }
        else
        {
            foreach (var conflict in plan.Conflicts)
            {
                builder.AppendLine($"- {conflict.Type}: `{conflict.TargetKey}`");

                foreach (var write in conflict.Writes)
                {
                    builder.AppendLine($"  - {write.OperationId}: {write.OldValue} -> {write.NewValue}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Diagnostics");
        builder.AppendLine();

        foreach (var diagnostic in plan.Diagnostics.Concat(plan.ModPlans.SelectMany(modPlan => modPlan.Diagnostics)))
        {
            builder.AppendLine($"- {diagnostic.Severity}: `{diagnostic.Code}` - {diagnostic.Message}");
        }

        return builder.ToString();
    }

    private static void WriteFile(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content);
    }

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal);
}

public sealed record PatchPlanReport(
    string PlanSource,
    bool Success,
    int ModCount,
    int WriteCount,
    int ConflictCount,
    IReadOnlyList<PatchPlanModReport> Mods,
    IReadOnlyList<PatchPlanWriteReport> Writes,
    IReadOnlyList<PatchPlanConflictReport> Conflicts,
    IReadOnlyList<PatchPlanEntryReport> Entries,
    IReadOnlyList<PatchPlanEntryConflictReport> EntryConflicts,
    IReadOnlyList<PatchPlanDiagnosticReport> Diagnostics);

public sealed record PatchPlanModReport(
    string Id,
    string Name,
    string Version,
    int WriteCount,
    int EntryCount,
    IReadOnlyList<ResolvedTweakReport> ResolvedTweaks);

public sealed record ResolvedTweakReport(
    string TweakId,
    string SourceValue,
    string ResolvedValue,
    string Origin);

public sealed record PatchPlanWriteReport(
    string OperationId,
    string OperationType,
    string File,
    string EntityGuid,
    string EntityName,
    string Component,
    string Path,
    string? Attribute,
    string OldValue,
    string NewValue);

public sealed record PatchPlanConflictReport(
    string Type,
    string TargetKey,
    IReadOnlyList<string> OperationIds);

public sealed record PatchPlanEntryReport(
    string Mod,
    string Operation,
    string Path,
    string? SourceFile);

public sealed record PatchPlanEntryConflictReport(
    string Type,
    string Path,
    IReadOnlyList<string> Mods);

public sealed record PatchPlanDiagnosticReport(
    string Severity,
    string Code,
    string Message,
    string? Path);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PatchPlanReport))]
internal sealed partial class PatchPlanJsonContext : JsonSerializerContext;
