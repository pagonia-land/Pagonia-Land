using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Wraps the two retention-management CLI verbs into one self-explanatory
// menu: see the orphaned deploys (whose game install moved or updated)
// + trim per-fingerprint deploy directories down to the N most recent.
// The current-state.yaml.lastDeploy entry is protected by the underlying
// service so a careless "keep 0" can't strand the user with no rollback path.
internal static class CleanBackupsWizard
{
    public static void Run(SessionState session)
    {
        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return;
        }

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold aqua]Clean Up Old Deploy Backups[/]").LeftJustified());
            AnsiConsole.MarkupLine("[dim]Deploy backups grow over time. This trims older entries while protecting the active deploy.[/]");
            AnsiConsole.WriteLine();

            const string ShowOrphans = "Show orphaned deploys (game install moved or updated)";
            const string Trim = "Trim backups to N most recent";
            const string Back = "Back";

            var pick = AdvancedHelpers.NavSelect("[bold]What to do?[/]", ShowOrphans, Trim, Back);

            switch (pick)
            {
                case ShowOrphans: RunShowOrphans(layout); break;
                case Trim: RunTrim(layout, session); break;
                default: return;
            }
        }
    }

    private static void RunShowOrphans(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var orphans = new OrphanedDeployFinder().FindAll(layout);

        if (orphans.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No orphaned deploys.[/] [dim]Every deploy directory still matches a live install fingerprint.[/]");
            Pause();
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]{orphans.Count} orphaned deploy fingerprint(s) found.[/]");
        AnsiConsole.WriteLine();

        foreach (var orphan in orphans)
        {
            var reasonText = orphan.Reason switch
            {
                OrphanReason.GameRootGone => "recorded gameRoot no longer exists",
                OrphanReason.GameUpdated => "fingerprint changed (likely a game update)",
                _ => "unknown",
            };
            AnsiConsole.MarkupLine($"  [aqua]fingerprint:[/] {Markup.Escape(orphan.Fingerprint)}");
            AnsiConsole.MarkupLine($"    gameRoot:   [dim]{Markup.Escape(orphan.RecordedGameRoot)}[/]");
            AnsiConsole.MarkupLine($"    deploys:    {orphan.TotalDeployCount}   latest: {Markup.Escape(orphan.LatestTimestamp ?? "?")}");
            AnsiConsole.MarkupLine($"    reason:     [yellow]{reasonText}[/]");
            AnsiConsole.WriteLine();
        }
        AnsiConsole.MarkupLine("[dim]Use 'Trim backups' below to remove the entries you no longer need.[/]");
        Pause();
    }

    private static void RunTrim(StoreLayout layout, SessionState session)
    {
        AnsiConsole.WriteLine();
        var scoped = AdvancedHelpers.Confirm("Scope to a single game install (otherwise: every fingerprint)?", defaultValue: false);
        string? gameRoot = null;
        if (scoped)
        {
            if (!AdvancedHelpers.TryPromptGameRoot(session, out var gr)) { return; }
            gameRoot = gr;
        }

        var keep = AnsiConsole.Prompt(
            new TextPrompt<int>("[bold]Keep how many recent deploys per fingerprint?[/] [dim](0 removes everything except the current-state.yaml.lastDeploy)[/]")
                .Validate(n => n >= 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Must be 0 or greater.[/]")));

        var dryRun = AdvancedHelpers.Confirm("Dry-run first (show what WOULD be removed, but don't delete)?", defaultValue: true);

        DeployCleanResult? r = null;
        AdvancedHelpers.Spin(dryRun ? "Dry-running cleanup..." : "Removing old deploys...",
            () => { r = new DeployCleanService().Clean(layout, keep, gameRoot, dryRun); });
        DiagnosticsRenderer.Render(r!.Diagnostics);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Mode:[/]    {(r.DryRun ? "[yellow]dry-run[/] (no changes written)" : "[green]applied[/]")}");
        AnsiConsole.MarkupLine($"[bold]Keep:[/]    {keep}");
        AnsiConsole.MarkupLine($"[bold]Removed:[/] {r.RemovedCount}");
        AnsiConsole.MarkupLine($"[bold]Kept:[/]    {r.KeptCount}");
        if (r.RefusedCount > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Refused:[/] [yellow]{r.RefusedCount}[/] [dim](current lastDeploy is always protected)[/]");
        }

        if (dryRun && r.RemovedCount > 0)
        {
            AnsiConsole.WriteLine();
            if (AdvancedHelpers.Confirm($"Apply removal of {r.RemovedCount} backup(s) now?", defaultValue: false))
            {
                DeployCleanResult? applied = null;
                AdvancedHelpers.Spin("Removing...",
                    () => { applied = new DeployCleanService().Clean(layout, keep, gameRoot, dryRun: false); });
                DiagnosticsRenderer.Render(applied!.Diagnostics);
                AnsiConsole.MarkupLine($"[green]Removed:[/] {applied.RemovedCount}");
            }
        }
        Pause();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
