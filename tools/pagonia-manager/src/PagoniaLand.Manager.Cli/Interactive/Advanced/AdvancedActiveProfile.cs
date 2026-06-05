using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedActiveProfile
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Active Profile[/]", "enable", "disable", "move", "status", "Back");

            var layout = session.GetLayout();
            var svc = new ActiveProfileService();

            switch (pick)
            {
                case "enable":
                {
                    AdvancedHelpers.Header("Active Profile → enable");
                    var installed = new ModLister().List(layout);
                    if (installed.Count == 0) { AnsiConsole.MarkupLine("[yellow]No mods installed.[/]"); Pause(); break; }
                    var modId = AnsiConsole.Prompt(
                        new SelectionPrompt<string>().Title("Enable:")
                            .AddChoices(installed.Select(m => m.Id).Distinct()));
                    var r = svc.Enable(layout, modId, requestedVersion: null);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success && r.Profile is not null)
                        AnsiConsole.MarkupLine($"[green]Load order:[/] {Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder))}");
                    Pause();
                    break;
                }
                case "disable":
                {
                    AdvancedHelpers.Header("Active Profile → disable");
                    var show = svc.Show(layout);
                    if (show.Profile is null)
                    {
                        AnsiConsole.MarkupLine("[yellow]No active profile.[/]"); Pause(); break;
                    }
                    // Union EnabledMods + LoadOrder so a drift orphan (entry in
                    // load order without a matching enabled-mod row) is
                    // reachable for cleanup. Matches ManageActiveProfileWizard.
                    var choices = show.Profile.EnabledMods.Select(m => m.Id)
                        .Concat(show.Profile.LoadOrder)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (choices.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]No enabled mods to disable.[/]"); Pause(); break;
                    }
                    var modId = AnsiConsole.Prompt(
                        new SelectionPrompt<string>().Title("Disable:")
                            .AddChoices(choices));
                    var r = svc.Disable(layout, modId);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success && r.Profile is not null)
                        AnsiConsole.MarkupLine($"[green]Load order:[/] {(r.Profile.LoadOrder.Count == 0 ? "(empty)" : Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder)))}");
                    Pause();
                    break;
                }
                case "move":
                {
                    AdvancedHelpers.Header("Active Profile → move");
                    var show = svc.Show(layout);
                    if (show.Profile is null || show.Profile.LoadOrder.Count < 2)
                    {
                        AnsiConsole.MarkupLine("[yellow]Need at least 2 enabled mods to reorder.[/]"); Pause(); break;
                    }
                    var modId = AnsiConsole.Prompt(
                        new SelectionPrompt<string>().Title("Move which mod:")
                            .AddChoices(show.Profile.LoadOrder));
                    var anchor = AnsiConsole.Prompt(
                        new SelectionPrompt<string>().Title("Place it relative to:")
                            .AddChoices(show.Profile.LoadOrder.Where(id => id != modId)));
                    var where = AnsiConsole.Prompt(
                        new SelectionPrompt<string>().Title("Before or after?")
                            .AddChoices("before", "after"));

                    var r = where == "before"
                        ? svc.MoveBefore(layout, modId, anchor)
                        : svc.MoveAfter(layout, modId, anchor);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Success && r.Profile is not null)
                        AnsiConsole.MarkupLine($"[green]Load order:[/] {Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder))}");
                    Pause();
                    break;
                }
                case "status":
                {
                    AdvancedHelpers.Header("Active Profile → status");
                    var r = svc.Show(layout);
                    if (r.Profile is null) { AnsiConsole.MarkupLine("[yellow]No active profile.[/]"); Pause(); break; }
                    AnsiConsole.MarkupLine($"[bold]Profile:[/] [aqua]{Markup.Escape(r.ProfileName ?? "?")}[/]");
                    AnsiConsole.MarkupLine($"  enabled: {r.Profile.EnabledMods.Count}");
                    if (r.Profile.LoadOrder.Count == 0)
                    {
                        AnsiConsole.MarkupLine("  [dim](no enabled mods)[/]");
                    }
                    else
                    {
                        var i = 1;
                        foreach (var id in r.Profile.LoadOrder)
                        {
                            var em = r.Profile.EnabledMods.FirstOrDefault(m => m.Id == id);
                            AnsiConsole.MarkupLine($"    {i,2}. [aqua]{Markup.Escape(id)}[/]@{Markup.Escape(em?.Version ?? "?")}");
                            i++;
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
