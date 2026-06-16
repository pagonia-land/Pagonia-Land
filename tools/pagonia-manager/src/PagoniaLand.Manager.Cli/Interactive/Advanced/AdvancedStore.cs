using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedStore
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Store[/]", "init", "info", "Back");

            switch (pick)
            {
                case "init": RunInit(session); break;
                case "info": RunInfo(session); break;
                default: return;
            }
        }
    }

    private static void RunInit(SessionState session)
    {
        AdvancedHelpers.Header("Store → init");
        var layout = session.GetLayout();
        var result = new StoreInitializer().Initialize(layout);
        AnsiConsole.MarkupLine($"[bold]Root:[/] [aqua]{Markup.Escape(result.Root)}[/]");
        AnsiConsole.MarkupLine($"[bold]Store version:[/] {result.StoreVersion}");
        AnsiConsole.MarkupLine($"  state.yaml: [aqua]{(result.CreatedState ? "created" : "exists")}[/]");
        AnsiConsole.MarkupLine($"  default profile: [aqua]{(result.CreatedDefaultProfile ? "created" : "exists")}[/]");
        AnsiConsole.MarkupLine($"  new directories: {result.CreatedDirectories.Count}");
        Pause();
    }

    private static void RunInfo(SessionState session)
    {
        AdvancedHelpers.Header("Store → info");
        var layout = session.GetLayout();
        var info = new StoreInspector().Inspect(layout);
        AnsiConsole.MarkupLine($"[bold]Root:[/] [aqua]{Markup.Escape(info.Root)}[/]");
        if (!info.Initialised)
        {
            AnsiConsole.MarkupLine("[yellow]Status:[/] not initialised — run init first.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[bold]Status:[/] [green]initialised[/]");
            AnsiConsole.MarkupLine($"  store version: [aqua]{info.StoreVersion}[/]");
            AnsiConsole.MarkupLine($"  active profile: [aqua]{Markup.Escape(info.ActiveProfile ?? "(none)")}[/]");
            AnsiConsole.MarkupLine($"  mods: {info.InstalledModCount} • profiles: {info.ProfileCount} • collections: {info.CollectionCount}");
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
