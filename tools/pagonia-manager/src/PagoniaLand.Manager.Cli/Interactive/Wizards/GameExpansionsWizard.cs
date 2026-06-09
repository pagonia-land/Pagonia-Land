using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// The interactive "Game Expansions" screen: a transparent, checkable view of each
// expansion's Present / Owned / Effective state, with Owned a keyboard-flippable
// tri-state (owned / not owned / unknown) for the two declarable packages. Present
// is read-only (a fact); core / tools are shown as always-owned and non-editable.
// Reachable from Settings and mirrored under Advanced -> Game Ops, matching the CLI's
// `expansions list` / `expansions set`.
internal static class GameExpansionsWizard
{
    public static void Run(SessionState session)
    {
        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            Pause();
            return;
        }

        if (!AdvancedHelpers.TryPromptGameRoot(session, out var gameRoot)) { return; }
        var service = new ExpansionOwnershipService();

        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold aqua]Game Expansions[/]").LeftJustified());
            AnsiConsole.WriteLine();

            var list = service.List(layout, gameRoot);
            DiagnosticsRenderer.Render(list.Diagnostics);
            if (!list.Success)
            {
                Pause();
                return;
            }

            AnsiConsole.MarkupLine($"[dim]Install:[/] [aqua]{Markup.Escape(list.GameRoot)}[/]  [dim]({Markup.Escape(list.GameFingerprint ?? "?")})[/]");
            RenderTable(list.Expansions);
            AnsiConsole.MarkupLine("[dim]Present is detected from disk; Effective = Present and Owned (whether DLC content is active for you in solo play).[/]");
            AnsiConsole.MarkupLine("[dim]The game ships every pak to every player, so ownership is your call — core / tools are base game + editor data and always owned.[/]");
            AnsiConsole.WriteLine();

            // Only the declarable packages are editable; build the menu from them.
            var declarable = list.Expansions
                .Where(e => ExpansionPackages.IsDeclarable(e.Package))
                .ToList();

            var choices = declarable
                .Select(e => $"Set {DisplayName(e.Package)} ({e.Package}) — currently {Describe(e.Ownership)}")
                .Append("Back")
                .ToArray();

            var pick = AdvancedHelpers.NavSelect("[bold]Edit ownership[/]", choices);
            if (pick == "Back")
            {
                return;
            }

            // Map the chosen line back to its package via the menu order.
            var index = Array.IndexOf(choices, pick);
            if (index < 0 || index >= declarable.Count)
            {
                return;
            }
            var package = declarable[index].Package;

            var stateChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"Do you own [bold]{DisplayName(package)}[/] ([aqua]{package}[/])?")
                    .HighlightStyle(new Style(foreground: Color.Aqua))
                    .AddChoices(OwnedChoice, NotOwnedChoice, UnknownChoice));

            var newState = stateChoice switch
            {
                OwnedChoice => OwnershipState.Owned,
                NotOwnedChoice => OwnershipState.NotOwned,
                _ => OwnershipState.Unknown,
            };

            var result = service.Set(layout, gameRoot, package, newState);
            AnsiConsole.WriteLine();
            DiagnosticsRenderer.Render(result.Diagnostics);
            Pause();
        }
    }

    private const string OwnedChoice = "Owned";
    private const string NotOwnedChoice = "Not owned";
    private const string UnknownChoice = "Unknown (haven't decided)";

    private static void RenderTable(IReadOnlyList<ExpansionState> expansions)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Package")
            .AddColumn("Present")
            .AddColumn("Owned")
            .AddColumn("Effective");

        foreach (var e in expansions)
        {
            var owned = ExpansionPackages.IsAlwaysOwned(e.Package)
                ? "[dim]always[/]"
                : Colorize(e.Ownership);
            table.AddRow(
                $"[aqua]{Markup.Escape(e.Package)}[/]",
                YesNo(e.Present),
                owned,
                YesNo(e.Effective));
        }

        AnsiConsole.Write(table);
    }

    private static string YesNo(bool value) => value ? "[green]yes[/]" : "[dim]no[/]";

    private static string Colorize(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "[green]owned[/]",
        OwnershipState.NotOwned => "[yellow]not owned[/]",
        _ => "[dim]unknown[/]",
    };

    private static string Describe(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "owned",
        OwnershipState.NotOwned => "not owned",
        _ => "unknown",
    };

    // Friendly expansion names for the prompts; the table keeps the raw package id.
    private static string DisplayName(string package) => package switch
    {
        ExpansionPackages.Dlc1 => "Meadowsong",
        ExpansionPackages.Decorations1 => "Decorations",
        _ => package,
    };

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
