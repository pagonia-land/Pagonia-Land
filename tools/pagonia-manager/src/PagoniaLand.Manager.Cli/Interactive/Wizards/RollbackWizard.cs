using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class RollbackWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Roll Back Last Deploy[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised — nothing to roll back.[/]");
            return;
        }

        if (!AdvancedHelpers.TryPromptGameRoot(session, out var gameRoot)) { return; }

        // Show what would be reverted BEFORE doing anything, so the user can
        // bail out without surprise.
        var status = new DeployStatusService().List(layout, gameRoot);
        if (!status.HasDeploys)
        {
            AnsiConsole.MarkupLine("[yellow]No deploys recorded for this game install — nothing to roll back.[/]");
            return;
        }

        var latest = status.Deploys[0];
        var panel = new Panel(
            new Markup(
                $"[bold]Will revert this deploy:[/]\n" +
                $"  Timestamp: [aqua]{Markup.Escape(latest.Timestamp)}[/]\n" +
                $"  Profile:   [aqua]{Markup.Escape(latest.Profile)}[/]\n" +
                $"  Mods:      [aqua]{latest.ModCount}[/]\n" +
                $"  Files:     [aqua]{latest.FileCount}[/]"))
            .Header("[bold]Roll Back[/]")
            .BorderColor(Color.Yellow);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Default to NO — destructive prompts should require an explicit Yes.
        var confirm = AdvancedHelpers.Confirm(
            "Restore the game files this deploy modified, and delete the files it added?", defaultValue: false);

        if (!confirm)
        {
            AnsiConsole.MarkupLine("[dim]Aborted. Nothing was rolled back.[/]");
            return;
        }

        RollbackResult? result = null;
        using (var stages = new StagePrinter())
        {
            stages.Start("Rolling back");
            result = new RollbackService().Rollback(layout, gameRoot, progress: new StageProgress(stages.Start));
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(result!.Diagnostics);
        AnsiConsole.WriteLine();

        // Live-state drift refusal: some live files changed since the deploy, so
        // restoring would discard those changes. Offer to overwrite them (--force).
        if (result.Outcome == RollbackOutcome.Failed
            && result.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LiveStateDrift))
        {
            AnsiConsole.MarkupLine("[yellow]Some live game files changed since this deploy (shown above) — rolling back would discard those changes.[/]");
            var overwrite = AdvancedHelpers.Confirm(
                "Overwrite them and restore the backup anyway (--force)?", defaultValue: false);
            if (overwrite)
            {
                using (var stages = new StagePrinter())
                {
                    stages.Start("Rolling back (force)");
                    result = new RollbackService().Rollback(layout, gameRoot, acceptDrift: true, progress: new StageProgress(stages.Start));
                }
                AnsiConsole.WriteLine();
                DiagnosticsRenderer.Render(result.Diagnostics);
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Aborted. The changed files were left as-is.[/]");
                return;
            }
        }

        switch (result.Outcome)
        {
            case RollbackOutcome.Reverted:
                AnsiConsole.MarkupLine($"[bold green]Reverted[/] — [aqua]{result.RestoredFileCount}[/] file(s) restored / removed.");
                AnsiConsole.MarkupLine($"  [dim]Reverted timestamp: {Markup.Escape(result.RevertedTimestamp ?? "?")}[/]");
                break;
            case RollbackOutcome.NothingToRollback:
                AnsiConsole.MarkupLine("[yellow]Nothing to roll back.[/]");
                break;
            default:
                AnsiConsole.MarkupLine("[bold red]Rollback failed.[/] See diagnostics above.");
                break;
        }
    }

    // PromptGameRoot moved to AdvancedHelpers.PromptGameRoot for cross-wizard consistency.
}
