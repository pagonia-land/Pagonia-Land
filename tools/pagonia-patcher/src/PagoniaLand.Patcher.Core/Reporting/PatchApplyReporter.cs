using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PagoniaLand.Patcher;

public sealed class PatchApplyReporter
{
    public void WriteReports(
        CombinedPatchPlan plan,
        IReadOnlyList<PatchDiagnostic> applyDiagnostics,
        string outputGameRoot,
        string? markdownPath,
        string? jsonPath,
        string planSource = "directMods")
    {
        if (!string.IsNullOrWhiteSpace(markdownPath))
        {
            WriteFile(markdownPath, ToMarkdown(plan, applyDiagnostics, outputGameRoot, planSource));
        }

        if (!string.IsNullOrWhiteSpace(jsonPath))
        {
            WriteFile(jsonPath, ToJson(plan, applyDiagnostics, outputGameRoot, planSource));
        }
    }

    public string ToJson(
        CombinedPatchPlan plan,
        IReadOnlyList<PatchDiagnostic> applyDiagnostics,
        string outputGameRoot,
        string planSource = "directMods")
    {
        var counts = CountWriteOutcomes(applyDiagnostics);
        var entryCounts = CountEntryOutcomes(applyDiagnostics);

        var report = new PatchApplyReport(
            planSource,
            counts.Failed == 0 && entryCounts.Failed == 0,
            outputGameRoot,
            plan.ModPlans.Count,
            plan.Writes.Count,
            counts.Applied,
            counts.Failed,
            plan.ModPlans.Select(modPlan => new PatchApplyModReport(
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
            plan.Writes.Select(write => new PatchApplyWriteReport(
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
            plan.EntryWrites.Select(entry => new PatchApplyEntryReport(
                entry.ModId,
                entry.Operation.ToString(),
                entry.Path,
                entry.SourceFile)).ToList(),
            new PatchApplyEntryCountsReport(
                plan.EntryWrites.Count,
                entryCounts.Replaced,
                entryCounts.Added,
                entryCounts.Deleted,
                entryCounts.Failed),
            applyDiagnostics
                .Select(diagnostic => new PatchApplyDiagnosticReport(
                    diagnostic.Severity.ToString(),
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.Path))
                .ToList());

        return JsonSerializer.Serialize(report, PatchApplyJsonContext.Default.PatchApplyReport);
    }

    public string ToMarkdown(
        CombinedPatchPlan plan,
        IReadOnlyList<PatchDiagnostic> applyDiagnostics,
        string outputGameRoot,
        string planSource = "directMods")
    {
        var counts = CountWriteOutcomes(applyDiagnostics);
        var entryCounts = CountEntryOutcomes(applyDiagnostics);
        var success = counts.Failed == 0 && entryCounts.Failed == 0;

        var builder = new StringBuilder();
        builder.AppendLine("# Pagonia Land Patch Apply Report");
        builder.AppendLine();
        builder.AppendLine($"Source: {planSource}");
        builder.AppendLine($"Result: {(success ? "OK" : "Failed")}");
        builder.AppendLine($"Output: {outputGameRoot}");
        builder.AppendLine($"Mods: {plan.ModPlans.Count}");
        builder.AppendLine($"Planned writes: {plan.Writes.Count}");
        builder.AppendLine($"Applied writes: {counts.Applied}");
        builder.AppendLine($"Failed writes: {counts.Failed}");
        builder.AppendLine();

        builder.AppendLine("## Mods");
        builder.AppendLine();
        builder.AppendLine("| Mod | Version | Writes |");
        builder.AppendLine("| --- | --- | ---: |");

        foreach (var modPlan in plan.ModPlans)
        {
            builder.AppendLine($"| {Escape(modPlan.Mod.Manifest.Name)} | {Escape(modPlan.Mod.Manifest.Version)} | {modPlan.Writes.Count} |");
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

        if (plan.EntryWrites.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Entry Operations");
            builder.AppendLine();
            builder.AppendLine($"Planned: {plan.EntryWrites.Count} (replaced {entryCounts.Replaced}, added {entryCounts.Added}, deleted {entryCounts.Deleted}, failed {entryCounts.Failed})");
            builder.AppendLine();
            builder.AppendLine("| Mod | Operation | Path | Source |");
            builder.AppendLine("| --- | --- | --- | --- |");

            foreach (var entry in plan.EntryWrites)
            {
                builder.AppendLine($"| {Escape(entry.ModId)} | {entry.Operation} | {Escape(entry.Path)} | {Escape(entry.SourceFile ?? string.Empty)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Diagnostics");
        builder.AppendLine();

        foreach (var diagnostic in applyDiagnostics)
        {
            builder.AppendLine($"- {diagnostic.Severity}: `{diagnostic.Code}` - {diagnostic.Message}");
        }

        return builder.ToString();
    }

    private static (int Applied, int Failed) CountWriteOutcomes(IReadOnlyList<PatchDiagnostic> applyDiagnostics)
    {
        var applied = 0;
        var failed = 0;

        foreach (var diagnostic in applyDiagnostics)
        {
            switch (diagnostic.Code)
            {
                case DiagnosticCodes.PatchApplied:
                    applied++;
                    break;
                case DiagnosticCodes.ApplyTargetMissing:
                case DiagnosticCodes.ApplyOldValueMismatch:
                case DiagnosticCodes.ApplyBlocked:
                    failed++;
                    break;
            }
        }

        return (applied, failed);
    }

    private static (int Replaced, int Added, int Deleted, int Failed) CountEntryOutcomes(IReadOnlyList<PatchDiagnostic> applyDiagnostics)
    {
        var replaced = 0;
        var added = 0;
        var deleted = 0;
        var failed = 0;
        foreach (var diagnostic in applyDiagnostics)
        {
            switch (diagnostic.Code)
            {
                case DiagnosticCodes.EntryReplaced:
                    replaced++;
                    break;
                case DiagnosticCodes.EntryAdded:
                    added++;
                    break;
                case DiagnosticCodes.EntryDeleted:
                    deleted++;
                    break;
                case DiagnosticCodes.EntrySourceUnreadable:
                case DiagnosticCodes.EntrySourceMissing:
                    failed++;
                    break;
            }
        }
        return (replaced, added, deleted, failed);
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

public sealed record PatchApplyReport(
    string PlanSource,
    bool Success,
    string OutputGameRoot,
    int ModCount,
    int PlanWriteCount,
    int AppliedWriteCount,
    int FailedWriteCount,
    IReadOnlyList<PatchApplyModReport> Mods,
    IReadOnlyList<PatchApplyWriteReport> Writes,
    IReadOnlyList<PatchApplyEntryReport> Entries,
    PatchApplyEntryCountsReport EntryCounts,
    IReadOnlyList<PatchApplyDiagnosticReport> Diagnostics);

public sealed record PatchApplyModReport(
    string Id,
    string Name,
    string Version,
    int WriteCount,
    int EntryCount,
    IReadOnlyList<ResolvedTweakReport> ResolvedTweaks);

public sealed record PatchApplyWriteReport(
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

public sealed record PatchApplyEntryReport(
    string Mod,
    string Operation,
    string Path,
    string? SourceFile);

public sealed record PatchApplyEntryCountsReport(
    int Planned,
    int Replaced,
    int Added,
    int Deleted,
    int Failed);

public sealed record PatchApplyDiagnosticReport(
    string Severity,
    string Code,
    string Message,
    string? Path);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PatchApplyReport))]
internal sealed partial class PatchApplyJsonContext : JsonSerializerContext;
