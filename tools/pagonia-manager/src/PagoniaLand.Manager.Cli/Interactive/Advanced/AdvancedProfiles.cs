using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedProfiles
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Profiles[/]", "create", "list", "use", "copy", "export", "delete", "show", "Back");

            var layout = session.GetLayout();
            var svc = new ProfileLifecycleService();

            switch (pick)
            {
                case "create":
                {
                    AdvancedHelpers.Header("Profiles → create");
                    if (!AdvancedHelpers.TryPromptText("Profile name:", out var name,
                            n => ProfileNameValidator.IsValid(n, out var why)
                                ? ValidationResult.Success() : ValidationResult.Error(why)))
                    {
                        break;
                    }
                    var r = svc.Create(layout, name);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success) AnsiConsole.MarkupLine($"[green]Created[/] [aqua]{Markup.Escape(name)}[/]");
                    Pause();
                    break;
                }
                case "list":
                {
                    AdvancedHelpers.Header("Profiles → list");
                    var r = svc.List(layout);
                    AnsiConsole.MarkupLine($"[bold]Active:[/] [aqua]{r.ActiveProfile}[/]");
                    var t = new Table().Border(TableBorder.Rounded).AddColumn("Name").AddColumn("Default").AddColumn("Active").AddColumn("Enabled");
                    foreach (var p in r.Profiles)
                        t.AddRow(
                            $"[aqua]{Markup.Escape(p.Name)}[/]",
                            p.IsDefault ? "yes" : "",
                            p.IsActive ? "[green]yes[/]" : "",
                            p.EnabledModCount.ToString());
                    AnsiConsole.Write(t);
                    Pause();
                    break;
                }
                case "use":
                {
                    AdvancedHelpers.Header("Profiles → use");
                    var list = svc.List(layout);
                    if (list.Profiles.Count == 0) { AnsiConsole.MarkupLine("[yellow]No profiles.[/]"); Pause(); break; }
                    var name = AdvancedHelpers.Pick("Switch to:", list.Profiles.Select(p => p.Name));
                    var r = svc.Use(layout, name);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success) AnsiConsole.MarkupLine($"[green]Active profile is now[/] [aqua]{Markup.Escape(name)}[/]");
                    Pause();
                    break;
                }
                case "copy":
                {
                    AdvancedHelpers.Header("Profiles → copy");
                    var list = svc.List(layout);
                    if (list.Profiles.Count == 0) { AnsiConsole.MarkupLine("[yellow]No profiles.[/]"); Pause(); break; }
                    var source = AdvancedHelpers.Pick("Copy which profile:", list.Profiles.Select(p => p.Name));
                    if (!AdvancedHelpers.TryPromptText("New profile name:", out var target,
                            n => ProfileNameValidator.IsValid(n, out var why)
                                ? ValidationResult.Success() : ValidationResult.Error(why)))
                    {
                        break;
                    }
                    var activate = AdvancedHelpers.Confirm("Activate the copy?", defaultValue: false);
                    var r = svc.Copy(layout, source, target, activate);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success)
                        AnsiConsole.MarkupLine($"[green]Copied[/] [aqua]{Markup.Escape(source)}[/] → [aqua]{Markup.Escape(target)}[/]{(activate ? " (now active)" : string.Empty)}");
                    Pause();
                    break;
                }
                case "export":
                {
                    AdvancedHelpers.Header("Profiles → export");
                    var list = svc.List(layout);
                    // Only profiles with at least one enabled mod can produce a valid
                    // collection (mods minItems 1) — hide the rest from the picker.
                    var exportable = list.Profiles.Where(p => p.EnabledModCount > 0).Select(p => p.Name).ToList();
                    if (exportable.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No profile has enabled mods to export (a collection needs at least one mod).[/]");
                        Pause();
                        break;
                    }
                    var name = AdvancedHelpers.Pick("Export which profile:", exportable);
                    if (!AdvancedHelpers.TryPromptText("Write collection to file path:", out var outPath)) { break; }
                    var id = AdvancedHelpers.PromptText("Collection id (optional):", allowEmpty: true);
                    var displayName = AdvancedHelpers.PromptText("Collection name (optional):", allowEmpty: true);
                    var version = AdvancedHelpers.PromptText("Collection version (optional):", allowEmpty: true);
                    var r = new ProfileExportService().Export(layout, name, outPath, new ProfileExportOptions
                    {
                        Id = string.IsNullOrWhiteSpace(id) ? null : id,
                        Name = string.IsNullOrWhiteSpace(displayName) ? null : displayName,
                        Version = string.IsNullOrWhiteSpace(version) ? null : version,
                    });
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success)
                        AnsiConsole.MarkupLine($"[green]Exported[/] [aqua]{Markup.Escape(name)}[/] → {Markup.Escape(r.OutputPath ?? outPath)}");
                    Pause();
                    break;
                }
                case "delete":
                {
                    AdvancedHelpers.Header("Profiles → delete");
                    var list = svc.List(layout);
                    var deletable = list.Profiles.Where(p => !p.IsActive && !p.IsDefault).Select(p => p.Name).ToList();
                    if (deletable.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No deletable profiles (default + active are protected).[/]");
                        Pause();
                        break;
                    }
                    var name = AdvancedHelpers.Pick("Delete:", deletable);
                    if (!AdvancedHelpers.Confirm($"Really delete [aqua]{Markup.Escape(name)}[/]?", defaultValue: false))
                    {
                        AnsiConsole.MarkupLine("[dim]Aborted.[/]"); Pause(); break;
                    }
                    var r = svc.Delete(layout, name);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success) AnsiConsole.MarkupLine($"[green]Deleted[/] [aqua]{Markup.Escape(name)}[/]");
                    Pause();
                    break;
                }
                case "show":
                {
                    AdvancedHelpers.Header("Profiles → show");
                    var list = svc.List(layout);
                    if (list.Profiles.Count == 0) { AnsiConsole.MarkupLine("[yellow]No profiles.[/]"); Pause(); break; }
                    var name = AdvancedHelpers.Pick("Show:", list.Profiles.Select(p => p.Name));
                    var r = svc.Show(layout, name);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success && r.Profile is not null)
                    {
                        AnsiConsole.MarkupLine($"[bold]Profile:[/] [aqua]{Markup.Escape(r.Profile.Name)}[/]");
                        AnsiConsole.MarkupLine($"  version: {r.Profile.ProfileVersion}");
                        if (!string.IsNullOrEmpty(r.Profile.Collection))
                            AnsiConsole.MarkupLine($"  collection: [aqua]{Markup.Escape(r.Profile.Collection)}[/]");
                        AnsiConsole.MarkupLine($"  enabled: {r.Profile.EnabledMods.Count}");
                        if (r.Profile.LoadOrder.Count > 0)
                        {
                            AnsiConsole.MarkupLine("  load order:");
                            var i = 1;
                            foreach (var id in r.Profile.LoadOrder)
                            {
                                var em = r.Profile.EnabledMods.FirstOrDefault(m => m.Id == id);
                                AnsiConsole.MarkupLine($"    {i,2}. [aqua]{Markup.Escape(id)}[/]@{Markup.Escape(em?.Version ?? "?")}");
                                i++;
                            }
                        }
                    }
                    Pause();
                    break;
                }
                default: return;
            }
        }
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
