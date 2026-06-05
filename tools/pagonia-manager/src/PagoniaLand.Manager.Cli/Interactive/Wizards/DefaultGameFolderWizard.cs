using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

/// <summary>
/// main-menu wizard for inspecting + changing the
/// persisted <c>state.yaml.defaultGameRoot</c>. Surfaces what the resolver
/// would pick today (with its source label) so the user can tell whether
/// they're seeing a stored default, a Windows Steam fallback, or nothing
/// at all. Lets them either keep the current value, enter a new one, or
/// clear it entirely (handy after uninstalling the game).
/// </summary>
internal static class DefaultGameFolderWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Default Game Folder[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first so there's a state.yaml to write the default into.[/]");
            return;
        }

        // Resolve WITHOUT a session override so the user sees the persistent
        // state alone, not whatever they happened to type earlier this run.
        var resolved = GameRootResolver.Resolve(layout, sessionOverride: null);
        RenderCurrent(resolved);

        AnsiConsole.WriteLine();
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]What now?[/]")
                .AddChoices("Set a new default", "Clear the default", "Cancel"));

        switch (action)
        {
            case "Set a new default":
                SetNewDefault(session, layout);
                break;
            case "Clear the default":
                ClearDefault(session, layout);
                break;
            default:
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                break;
        }
    }

    private static void RenderCurrent(ResolvedGameRoot resolved)
    {
        if (!resolved.HasPath)
        {
            AnsiConsole.MarkupLine("[bold]Current default:[/] [dim](not set)[/]");
            AnsiConsole.MarkupLine("[dim]No path stored in state.yaml, and no Windows Steam default detected on disk.[/]");
            return;
        }

        var sourceDescription = resolved.Source switch
        {
            GameRootSource.StoredDefault => "from state.yaml (set by a previous wizard run)",
            GameRootSource.PlatformDefault => "Windows Steam default (auto-detected, not yet persisted)",
            _ => "unknown",
        };
        AnsiConsole.MarkupLine($"[bold]Current default:[/] [aqua]{Markup.Escape(resolved.Path!)}[/]");
        AnsiConsole.MarkupLine($"[dim]Source: {sourceDescription}[/]");
    }

    private static void SetNewDefault(SessionState session, StoreLayout layout)
    {
        var path = AdvancedHelpers.PromptExistingPath(
            "[bold]New default game folder[/] (live [aqua]pak/[/] folder or extracted [aqua]game-gdb/[/]-style layout):",
            mustBeDirectory: true);

        session.GameRoot = path;
        var wrote = GameRootResolver.SetStoredDefault(layout, path);
        if (wrote)
        {
            AnsiConsole.MarkupLine($"[green]Saved[/] [aqua]{Markup.Escape(path)}[/] as the default.");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No change — that path was already the stored default.[/]");
        }
    }

    private static void ClearDefault(SessionState session, StoreLayout layout)
    {
        if (!AdvancedHelpers.Confirm("Clear the stored default game folder?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
            return;
        }

        session.GameRoot = null;
        var wrote = GameRootResolver.SetStoredDefault(layout, null);
        if (wrote)
        {
            AnsiConsole.MarkupLine("[yellow]Default game folder cleared[/] from state.yaml.");
            AnsiConsole.MarkupLine("[dim]Next wizard run will ask you for a path (or suggest the Windows Steam default if that folder exists).[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]No change — there was no stored default to clear.[/]");
        }
    }
}
