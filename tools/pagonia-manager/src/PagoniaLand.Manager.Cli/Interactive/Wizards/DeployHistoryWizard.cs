using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Surfaces `deploy-status` + `deploy-list` together as one view: "what's
// the latest deploy + what's the full history for this game install".
// Lets a modder eyeball whether their last deploy is still in the store
// and what profile / timestamp / mod-count it captured, without dropping
// into the All-commands menu.
internal static class DeployHistoryWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Deploy History[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return;
        }

        var gameRoot = AdvancedHelpers.PromptGameRoot(session);
        var result = new DeployStatusService().List(layout, gameRoot);
        DiagnosticsRenderer.Render(result.Diagnostics);

        if (!result.HasDeploys)
        {
            AnsiConsole.MarkupLine("[dim]No deploys recorded for this game install.[/]");
            return;
        }

        var latest = result.Deploys[0];
        AnsiConsole.MarkupLine($"[bold]Latest deploy:[/] [aqua]{Markup.Escape(latest.Timestamp)}[/]");
        AnsiConsole.MarkupLine($"  profile: [aqua]{Markup.Escape(latest.Profile)}[/]   mods: {latest.ModCount}   files: {latest.FileCount}");
        AnsiConsole.WriteLine();

        if (result.Deploys.Count > 1)
        {
            AnsiConsole.MarkupLine($"[bold]All deploys[/] [dim]({result.Deploys.Count} total, newest first):[/]");
            var t = new Table().Border(TableBorder.Rounded)
                .AddColumn("Timestamp").AddColumn("Profile").AddColumn("Mods").AddColumn("Files");
            foreach (var d in result.Deploys)
            {
                t.AddRow(
                    Markup.Escape(d.Timestamp),
                    $"[aqua]{Markup.Escape(d.Profile)}[/]",
                    d.ModCount.ToString(),
                    d.FileCount.ToString());
            }
            AnsiConsole.Write(t);
        }

        AnsiConsole.MarkupLine("[dim]Use the Roll back wizard to undo the latest; use 'Clean up old deploy backups' to trim older entries.[/]");
    }
}
