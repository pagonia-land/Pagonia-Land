using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Bundles the "things a user configures once and forgets" surface into a
// single menu: the persistent default game folder + catalog subscription
// management. Without this entry, catalog add/remove would only be
// reachable from Advanced -> Catalogs, which is a discovery problem
// for users who don't think of catalog management as a "raw command".
internal static class SettingsWizard
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
            AnsiConsole.Write(new Rule("[bold aqua]Settings[/]").LeftJustified());
            AnsiConsole.WriteLine();

            const string GameFolder = "Default game folder";
            const string CatalogSubs = "Catalog subscriptions";
            const string GameExpansions = "Game expansions (DLC ownership)";
            const string Back = "Back";

            var pick = AdvancedHelpers.NavSelect("[bold]What to configure?[/]", GameFolder, CatalogSubs, GameExpansions, Back);

            switch (pick)
            {
                case GameFolder: DefaultGameFolderWizard.Run(session); break;
                case CatalogSubs: RunCatalogSubs(layout); break;
                case GameExpansions: GameExpansionsWizard.Run(session); break;
                default: return;
            }
        }
    }

    private static void RunCatalogSubs(StoreLayout layout)
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold aqua]Catalog Subscriptions[/]").LeftJustified());
            AnsiConsole.WriteLine();

            var subs = new CatalogSubscriptionService().List(layout);
            if (subs.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]No subscriptions yet.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold]Current subscriptions ({subs.Count}):[/]");
                foreach (var s in subs)
                {
                    AnsiConsole.MarkupLine($"  - [aqua]{Markup.Escape(s.Canonical)}[/]");
                }
            }
            AnsiConsole.WriteLine();

            const string Add = "Add a subscription";
            const string Remove = "Remove a subscription";
            const string Refresh = "Refresh all (force re-fetch)";
            const string Back = "Back";

            var choices = subs.Count == 0
                ? new[] { Add, Back }
                : new[] { Add, Remove, Refresh, Back };

            var pick = AdvancedHelpers.NavSelect("[bold]Action[/]", choices);

            switch (pick)
            {
                case Add: RunAdd(layout); break;
                case Remove: RunRemove(layout, subs); break;
                case Refresh: RunRefreshAll(layout, subs); break;
                default: return;
            }
        }
    }

    private static void RunAdd(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        if (!AdvancedHelpers.TryPromptText("Catalog source [dim](gh:owner/repo[[#ref]][[/path]], https://host/.../catalog.yaml, or file:absolute-or-relative-path)[/]:", out var spec)) { return; }
        var r = new CatalogSubscriptionService().Add(layout, spec);
        DiagnosticsRenderer.Render(r.Diagnostics);
        Pause();
    }

    private static void RunRemove(StoreLayout layout, IReadOnlyList<CatalogSource> subs)
    {
        AnsiConsole.WriteLine();
        var canonical = AdvancedHelpers.Pick("Remove which subscription?", subs.Select(s => s.Canonical));
        var r = new CatalogSubscriptionService().Remove(layout, canonical);
        DiagnosticsRenderer.Render(r.Diagnostics);
        Pause();
    }

    private static void RunRefreshAll(StoreLayout layout, IReadOnlyList<CatalogSource> subs)
    {
        AnsiConsole.WriteLine();
        var state = new StoreStateReader().Read(layout);
        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
        var fetcher = new CachingCatalogFetcher(http, layout, state.CatalogCacheStalenessHours, state.AllowInsecureCatalogSources);

        var succeeded = 0;
        AdvancedHelpers.Spin($"Refreshing {subs.Count} catalog(s)...", () =>
        {
            foreach (var s in subs)
            {
                var r = fetcher.Fetch(s, forceRefresh: true);
                DiagnosticsRenderer.Render(r.Diagnostics);
                if (r.Success) { succeeded++; }
            }
        });

        // Honest summary: contradicting the per-source error rendering with a
        // blanket green "Done." was misleading. Colour reflects the actual
        // success ratio.
        var line = $"Refreshed {succeeded}/{subs.Count} catalog(s).";
        if (succeeded == subs.Count) { AnsiConsole.MarkupLine($"[green]{line}[/]"); }
        else if (succeeded == 0) { AnsiConsole.MarkupLine($"[red]{line}[/]"); }
        else { AnsiConsole.MarkupLine($"[yellow]{line}[/]"); }
        Pause();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
