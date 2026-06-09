using PagoniaLand.Manager;
using PagoniaLand.Patcher;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedMods
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Mods[/]", "install", "uninstall", "list", "advise", "Back");

            switch (pick)
            {
                case "install": RunInstall(session); break;
                case "uninstall": RunUninstall(session); break;
                case "list": RunList(session); break;
                case "advise": RunAdvise(session); break;
                default: return;
            }
        }
    }

    private static void RunInstall(SessionState session)
    {
        AdvancedHelpers.Header("Mods → install");
        if (!AdvancedHelpers.TryPromptExistingPath("[bold]Mod source[/] [dim](folder or .zip)[/]:", out var src)) { return; }
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

        var modId = AdvancedHelpers.Pick("[bold]Mod to uninstall[/]", installed.Select(m => m.Id).Distinct());

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

    // Conflict-minimising authoring advisor over an installed mod's overlay
    // *.gd.xml (patcher Phase 5). Base-free by default; offers an optional game
    // root to switch on the base-aware checks (cross-DB unload + replace-could-
    // be-incremental).
    private static void RunAdvise(SessionState session)
    {
        AdvancedHelpers.Header("Mods → advise");
        var layout = session.GetLayout();
        var installed = new ModLister().List(layout);
        if (installed.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No mods installed.[/]");
            Pause();
            return;
        }

        var modId = AdvancedHelpers.Pick("[bold]Mod to inspect[/]", installed.Select(m => m.Id).Distinct());
        var versions = installed.Where(m => m.Id == modId).Select(m => m.Version).ToList();
        var version = versions.Count > 1
            ? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("[bold]Which version?[/]").AddChoices(versions))
            : versions[0];

        var read = new ManifestReader().ReadMod(layout.ModVersionDirectory(modId, version));
        if (!read.Success || read.Value is null)
        {
            DiagnosticsRenderer.Render(read.Diagnostics.Select(ManagerDiagnostic.From).ToList());
            Pause();
            return;
        }

        // Optional base-aware pass: a game root unlocks the cross-database unload
        // check and the replace-could-be-incremental diff. Declining (or an empty
        // game-root prompt) keeps it base-free.
        ReferenceGdbIndex? reference = null;
        if (AdvancedHelpers.Confirm("Run base-aware checks against a game install?", defaultValue: false)
            && AdvancedHelpers.TryPromptGameRoot(session, out var gameRoot))
        {
            AdvancedHelpers.Spin("Indexing game database...", () => reference = ReferenceGdbIndex.Load(gameRoot));
        }

        var overlay = OverlayGdbReader.ReadFromMod(read.Value);
        var findings = overlay.Diagnostics
            .Concat(new EntityRelationAdvisor().Advise(overlay, reference))
            .ToList();

        if (findings.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No overlay findings — this mod uses additive modes, or ships no GameDatabase overlay.[/]");
        }
        else
        {
            DiagnosticsRenderer.Render(findings.Select(ManagerDiagnostic.From).ToList());
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
