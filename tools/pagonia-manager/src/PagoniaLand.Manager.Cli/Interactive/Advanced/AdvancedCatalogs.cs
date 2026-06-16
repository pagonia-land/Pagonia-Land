using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedCatalogs
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Catalogs[/]", "list", "add", "remove", "browse", "show", "refresh", "Back");

            var layout = session.GetLayout();

            switch (pick)
            {
                case "list":
                {
                    AdvancedHelpers.Header("Catalogs → list");
                    var subs = new CatalogSubscriptionService().List(layout);
                    if (subs.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[dim](no catalog subscriptions)[/]");
                    }
                    else
                    {
                        foreach (var s in subs)
                        {
                            AnsiConsole.MarkupLine($"  - [aqua]{Markup.Escape(s.Canonical)}[/]");
                        }
                    }
                    Pause();
                    break;
                }
                case "add":
                {
                    AdvancedHelpers.Header("Catalogs → add");
                    if (!AdvancedHelpers.TryPromptText("Source [dim](gh:owner/repo[[#ref]][[/path]], https://host/.../catalog.yaml, or file:absolute-or-relative-path)[/]:", out var spec)) { break; }
                    var result = new CatalogSubscriptionService().Add(layout, spec);
                    DiagnosticsRenderer.Render(result.Diagnostics);
                    Pause();
                    break;
                }
                case "remove":
                {
                    AdvancedHelpers.Header("Catalogs → remove");
                    var subs = new CatalogSubscriptionService().List(layout);
                    if (subs.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[dim](nothing to remove — no subscriptions)[/]");
                        Pause();
                        break;
                    }
                    if (!AdvancedHelpers.TryPickOrCancel("Remove which subscription? [dim](Esc to cancel)[/]", subs.Select(s => s.Canonical), out var canonical)) break;
                    var result = new CatalogSubscriptionService().Remove(layout, canonical);
                    DiagnosticsRenderer.Render(result.Diagnostics);
                    Pause();
                    break;
                }
                case "browse":
                {
                    AdvancedHelpers.Header("Catalogs → browse");
                    var subs = new CatalogSubscriptionService().List(layout);
                    if (subs.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[dim](no subscriptions yet — add one first)[/]");
                        Pause();
                        break;
                    }

                    var state = new StoreStateReader().Read(layout);
                    CatalogAggregateResult? aggregate = null;
                    AdvancedHelpers.Spin("Aggregating subscribed catalogs...", () =>
                    {
                        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
                        var aggregator = new CatalogAggregator(new CachingCatalogFetcher(http, layout, state.CatalogCacheStalenessHours, state.AllowInsecureCatalogSources));
                        aggregate = aggregator.Aggregate(subs, state.CatalogMaxDepth);
                    });
                    DiagnosticsRenderer.Render(aggregate!.Diagnostics);

                    if (aggregate.Repos.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[dim](no repos found across subscribed catalogs)[/]");
                    }
                    else
                    {
                        var table = new Table().Border(TableBorder.Rounded)
                            .AddColumn("Repo").AddColumn("Summary").AddColumn("Vouched by");
                        foreach (var r in aggregate.Repos)
                        {
                            table.AddRow(
                                $"[aqua]{Markup.Escape($"{r.Owner}/{r.Repo}")}[/]",
                                Markup.Escape(r.Summary),
                                r.VouchedBy.Count.ToString());
                        }
                        AnsiConsole.Write(table);
                    }
                    Pause();
                    break;
                }
                case "show":
                {
                    AdvancedHelpers.Header("Catalogs → show");
                    if (!AdvancedHelpers.TryPromptText("Source [dim](gh:owner/repo[[#ref]][[/path]], https://host/.../catalog.yaml, or file:absolute-or-relative-path)[/]:", out var spec)) { break; }
                    if (!CatalogSourceParser.TryParse(spec, out var src))
                    {
                        AnsiConsole.MarkupLine($"[red]'{Markup.Escape(spec)}' is not a recognised catalog source.[/]");
                        Pause();
                        break;
                    }
                    CatalogFetchResult? fetch = null;
                    var stateForShow = new StoreStateReader().Read(layout);
                    AdvancedHelpers.Spin("Fetching catalog...", () =>
                    {
                        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
                        fetch = new CachingCatalogFetcher(http, layout, stateForShow.CatalogCacheStalenessHours, stateForShow.AllowInsecureCatalogSources).Fetch(src);
                    });
                    DiagnosticsRenderer.Render(fetch!.Diagnostics);

                    if (fetch.Success && fetch.Catalog is not null)
                    {
                        var meta = fetch.Catalog.CatalogMeta;
                        if (meta is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(meta.Name))
                                AnsiConsole.MarkupLine($"  name: [aqua]{Markup.Escape(meta.Name)}[/]");
                            if (!string.IsNullOrWhiteSpace(meta.Maintainer))
                                AnsiConsole.MarkupLine($"  maintainer: {Markup.Escape(meta.Maintainer)}");
                            if (!string.IsNullOrWhiteSpace(meta.Description))
                                AnsiConsole.MarkupLine($"  description: {Markup.Escape(meta.Description)}");
                        }
                        AnsiConsole.MarkupLine($"  repos: {fetch.Catalog.Repos.Count}");
                        AnsiConsole.MarkupLine($"  federated catalogs: {fetch.Catalog.Catalogs.Count}");
                    }
                    Pause();
                    break;
                }
                case "refresh":
                {
                    AdvancedHelpers.Header("Catalogs → refresh");
                    var subs = new CatalogSubscriptionService().List(layout);
                    if (subs.Count == 0)
                    {
                        AnsiConsole.MarkupLine("[dim](no catalog subscriptions to refresh)[/]");
                        Pause();
                        break;
                    }

                    var stateForRefresh = new StoreStateReader().Read(layout);
                    AdvancedHelpers.Spin($"Refreshing {subs.Count} subscribed catalog(s)...", () =>
                    {
                        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
                        var fetcher = new CachingCatalogFetcher(http, layout, stateForRefresh.CatalogCacheStalenessHours, stateForRefresh.AllowInsecureCatalogSources);
                        foreach (var s in subs)
                        {
                            var r = fetcher.Fetch(s, forceRefresh: true);
                            DiagnosticsRenderer.Render(r.Diagnostics);
                        }
                    });
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
