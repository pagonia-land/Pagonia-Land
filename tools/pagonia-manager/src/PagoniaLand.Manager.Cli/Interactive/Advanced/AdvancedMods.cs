using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedMods
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Mods[/]", "install", "uninstall", "list", "Back");

            switch (pick)
            {
                case "install": RunInstall(session); break;
                case "uninstall": RunUninstall(session); break;
                case "list": RunList(session); break;
                default: return;
            }
        }
    }

    private static void RunInstall(SessionState session)
    {
        AdvancedHelpers.Header("Mods → install");
        var src = AdvancedHelpers.PromptExistingPath("[bold]Mod source[/] [dim](folder or .zip)[/]:");
        var layout = session.GetLayout();
        InstallResult? r = null;
        AdvancedHelpers.Spin("Installing...", () => { r = new ModInstaller().Install(src, layout); });
        DiagnosticsRenderer.Render(r!.Diagnostics);
        AnsiConsole.MarkupLine($"[bold]Outcome:[/] {r.Outcome}");
        if (r.Outcome == InstallOutcome.Installed || r.Outcome == InstallOutcome.AlreadyInstalled)
        {
            AnsiConsole.MarkupLine($"  [aqua]{Markup.Escape(r.ModId!)}[/]@[aqua]{Markup.Escape(r.Version!)}[/]");
        }
        Pause();
    }

    private static void RunUninstall(SessionState session)
    {
        AdvancedHelpers.Header("Mods → uninstall");
        var layout = session.GetLayout();
        var installed = new ModLister().List(layout);
        if (installed.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No mods installed.[/]");
            Pause();
            return;
        }

        var modId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Mod to uninstall[/]")
                .HighlightStyle(new Style(foreground: Color.Aqua))
                .AddChoices(installed.Select(m => m.Id).Distinct()));

        var versions = installed.Where(m => m.Id == modId).Select(m => m.Version).ToList();
        string? version = null;
        if (versions.Count > 1)
        {
            version = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Which version?[/]")
                    .AddChoices(versions));
        }

        UninstallResult? r = null;
        AdvancedHelpers.Spin("Uninstalling...", () => { r = new ModUninstaller().Uninstall(modId, version, layout); });
        DiagnosticsRenderer.Render(r!.Diagnostics);
        AnsiConsole.MarkupLine($"[bold]Outcome:[/] {r.Outcome}");
        Pause();
    }

    private static void RunList(SessionState session)
    {
        AdvancedHelpers.Header("Mods → list");
        var layout = session.GetLayout();
        var mods = new ModLister().List(layout);
        if (mods.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](no mods installed)[/]");
        }
        else
        {
            var t = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
                .AddColumn("Id").AddColumn("Version").AddColumn("Installed");
            foreach (var m in mods)
                t.AddRow($"[aqua]{Markup.Escape(m.Id)}[/]", Markup.Escape(m.Version), Markup.Escape(m.InstalledAt ?? "(unknown)"));
            AnsiConsole.Write(t);
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
