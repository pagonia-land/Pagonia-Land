using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// Surfaces `catalog browse` (the federated aggregate view across every
// subscribed catalog) as a first-class main-menu use case, then lets the user
// drill into any listed repo to see the mods + collections it publishes —
// instead of dead-ending at the repo level and pointing them at scripted mode.
// Without a subscription, walks the user through adding one before the browse —
// otherwise the empty result would be a dead end.
internal static class BrowseCatalogsWizard
{
    private const string Back = "Back";

    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Browse Community Catalogs[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return;
        }

        var subs = new CatalogSubscriptionService().List(layout);
        if (subs.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No catalog subscriptions yet. A catalog is a curated list of mod repos that publishers share.[/]");
            AnsiConsole.WriteLine();
            if (!AdvancedHelpers.Confirm("Add a subscription now?", defaultValue: true))
            {
                AnsiConsole.MarkupLine("[dim]Aborted.[/]");
                return;
            }
            if (!AdvancedHelpers.TryPromptText("Catalog source [dim](gh:owner/repo, https://host/catalog.yaml, or file:path)[/]:", out var spec)) { return; }
            var add = new CatalogSubscriptionService().Add(layout, spec);
            DiagnosticsRenderer.Render(add.Diagnostics);
            if (!add.Success) { return; }
            subs = new CatalogSubscriptionService().List(layout);
        }

        var state = new StoreStateReader().Read(layout);
        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
        var fetcher = new CachingCatalogFetcher(http, layout, state.CatalogCacheStalenessHours, state.AllowInsecureCatalogSources);
        var aggregator = new CatalogAggregator(fetcher);

        CatalogAggregateResult? result = null;
        AdvancedHelpers.Spin($"Aggregating {subs.Count} subscribed catalog(s)...",
            () => { result = aggregator.Aggregate(subs, state.CatalogMaxDepth); });
        DiagnosticsRenderer.Render(result!.Diagnostics);

        if (result.Repos.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](no repos found across subscribed catalogs)[/]");
            return;
        }

        RenderRepoTable(result.Repos);
        AnsiConsole.MarkupLine($"[dim]{result.Repos.Count} unique repo(s) across {result.VisitedSources.Count} catalog(s).[/]");
        AnsiConsole.WriteLine();

        // Drill-in: pick a repo to see the mods + collections it publishes. The
        // repo set is fixed for this browse, so build the label map once and
        // loop the selection so the user can inspect several before leaving.
        var byLabel = new Dictionary<string, AggregatedRepo>(StringComparer.Ordinal);
        var labels = new List<string>();
        foreach (var r in result.Repos)
        {
            var label = RepoSpec(r);
            if (byLabel.TryAdd(label, r)) { labels.Add(label); }
        }

        while (true)
        {
            var choice = AdvancedHelpers.NavSelect(
                "Inspect a repo's [aqua]mods + collections[/], or go back:",
                labels.Append(Back).ToArray());

            if (choice == Back) { return; }

            ShowRepoContents(http, layout, byLabel[choice]);
        }
    }

    private static void RenderRepoTable(IReadOnlyList<AggregatedRepo> repos)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Repo")
            .AddColumn("Summary")
            .AddColumn("Vouched by");
        foreach (var r in repos)
        {
            table.AddRow(
                $"[aqua]{Markup.Escape(RepoSpec(r))}[/]",
                Markup.Escape(r.Summary),
                r.VouchedBy.Count.ToString());
        }
        AnsiConsole.Write(table);
    }

    // Lists a repo's published mods + collections, then lets the user install
    // one straight through the existing remote pipeline — no scripted-mode
    // detour. Loops the item selection so several can be installed from the
    // same repo before going back.
    private static void ShowRepoContents(HttpRemoteContentFetcher http, StoreLayout layout, AggregatedRepo repo)
    {
        var spec = RepoSpec(repo);
        var source = new GitHubSource(repo.Owner, repo.Repo, "HEAD", ModSpec: null, BasePath: repo.IndexPath);

        RepoIndexFetchResult? fetch = null;
        AdvancedHelpers.Spin($"Fetching {spec} index...", () => { fetch = new RepoIndexFetcher(http).Fetch(source); });
        DiagnosticsRenderer.Render(fetch!.Diagnostics);

        AnsiConsole.WriteLine();
        if (!fetch.Success)
        {
            AnsiConsole.MarkupLine($"[red]Could not read {Markup.Escape(spec)}'s catalogue.[/] See diagnostics above.");
            AnsiConsole.WriteLine();
            return;
        }
        if (!fetch.HasIndex)
        {
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(spec)} publishes no index.yaml — nothing to browse.[/]");
            AnsiConsole.MarkupLine($"[dim]You can still install a mod by its repo-relative path: [aqua]install --from gh:{Markup.Escape(spec)}/<path>[/].[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var index = fetch.Index!;
        AnsiConsole.Write(new Rule($"[aqua]{Markup.Escape(spec)}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        if (index.Mods.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Mods[/]");
            AnsiConsole.Write(BuildModTable(index.Mods));
        }
        if (index.Collections.Count > 0)
        {
            if (index.Mods.Count > 0) { AnsiConsole.WriteLine(); }
            AnsiConsole.MarkupLine("[bold]Collections[/]");
            AnsiConsole.Write(BuildItemTable(index.Collections.Select(c => (c.Id, c.DisplayName, c.Version, c.Description))));
        }

        // Install picker: every mod + collection, keyed back to its (kind, id).
        // Ids are [a-z0-9._-] so they're markup-safe as selection choices.
        var byChoice = new Dictionary<string, (bool IsCollection, string Id)>(StringComparer.Ordinal);
        var choices = new List<string>();
        foreach (var m in index.Mods)
        {
            var c = $"Mod · {m.Id}";
            if (byChoice.TryAdd(c, (false, m.Id))) { choices.Add(c); }
        }
        foreach (var col in index.Collections)
        {
            var c = $"Collection · {col.Id}";
            if (byChoice.TryAdd(c, (true, col.Id))) { choices.Add(c); }
        }

        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](this repo's index.yaml lists no mods or collections)[/]");
            AnsiConsole.WriteLine();
            return;
        }

        AnsiConsole.WriteLine();
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect(
                "Install which [aqua]mod[/] or [aqua]collection[/]?  (or go back)",
                choices.Append(Back).ToArray());

            if (pick == Back) { return; }

            var (isCollection, id) = byChoice[pick];
            if (isCollection) { InstallSelectedCollection(http, layout, repo, id); }
            else { InstallSelectedMod(http, layout, repo, id); }
        }
    }

    // Mirrors the scripted `install --from gh:...` path: fetch into a temp dir,
    // run the local ModInstaller against it, clean up, then offer enable.
    private static void InstallSelectedMod(HttpRemoteContentFetcher http, StoreLayout layout, AggregatedRepo repo, string modId)
    {
        var gh = new GitHubSource(repo.Owner, repo.Repo, "HEAD", modId, repo.IndexPath);

        RemoteFetchResult? fetch = null;
        AdvancedHelpers.Spin($"Fetching {modId}...", () => { fetch = new RemoteFetcher(http).FetchMod(gh); });
        DiagnosticsRenderer.Render(fetch!.Diagnostics);
        if (!fetch.Success || fetch.TempDirectory is null)
        {
            AnsiConsole.MarkupLine("[red]Fetch failed — not installed.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        InstallResult? result = null;
        try
        {
            AdvancedHelpers.Spin("Validating and installing...",
                () => { result = new ModInstaller().Install(fetch.TempDirectory, layout, fetch.ResolvedSource); });
        }
        finally
        {
            TryDeleteDir(fetch.TempDirectory);
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(result!.Diagnostics);
        AnsiConsole.WriteLine();

        switch (result.Outcome)
        {
            case InstallOutcome.Installed:
                AnsiConsole.MarkupLine($"[bold green]Installed[/] [aqua]{Markup.Escape(result.ModId!)}[/]@[aqua]{Markup.Escape(result.Version!)}[/]");
                InstallModWizard.OfferEnable(layout, result.ModId!);
                break;
            case InstallOutcome.AlreadyInstalled:
                AnsiConsole.MarkupLine($"[bold yellow]Already installed[/] [aqua]{Markup.Escape(result.ModId!)}[/]@[aqua]{Markup.Escape(result.Version!)}[/]");
                InstallModWizard.OfferEnable(layout, result.ModId!);
                break;
            default:
                AnsiConsole.MarkupLine("[bold red]Install failed.[/] See diagnostics above.");
                break;
        }
        AnsiConsole.WriteLine();
    }

    // Mirrors the scripted `collection install --from gh:...` path: fetch the
    // collection + its mods into a temp tree, thread the per-mod resolved
    // sources through to the lockfile, install, clean up.
    private static void InstallSelectedCollection(HttpRemoteContentFetcher http, StoreLayout layout, AggregatedRepo repo, string collectionId)
    {
        var gh = new GitHubSource(repo.Owner, repo.Repo, "HEAD", collectionId, repo.IndexPath);

        RemoteCollectionFetchResult? fetch = null;
        AdvancedHelpers.Spin($"Fetching collection {collectionId}...", () => { fetch = new RemoteFetcher(http).FetchCollection(gh); });
        DiagnosticsRenderer.Render(fetch!.Diagnostics);
        if (!fetch.Success || fetch.TempDirectory is null || fetch.CollectionFilePath is null || fetch.ModsRoot is null)
        {
            AnsiConsole.MarkupLine("[red]Fetch failed — collection not installed.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var activate = AdvancedHelpers.Confirm("Activate the new profile now?", defaultValue: true);

        CollectionInstallResult? result = null;
        try
        {
            var options = new CollectionInstallOptions
            {
                Activate = activate,
                RemoteModSources = new Dictionary<string, string>(fetch.ModSources, StringComparer.Ordinal),
            };
            AdvancedHelpers.Spin("Resolving and installing collection...",
                () => { result = new CollectionInstallService().InstallWithOptions(layout, fetch.CollectionFilePath, fetch.ModsRoot, options); });
        }
        finally
        {
            TryDeleteDir(fetch.TempDirectory);
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(result!.Diagnostics);
        AnsiConsole.WriteLine();

        switch (result.Outcome)
        {
            case CollectionInstallOutcome.Installed:
                AnsiConsole.MarkupLine($"[bold green]Installed collection[/] [aqua]{Markup.Escape(result.CollectionId!)}[/]@[aqua]{Markup.Escape(result.CollectionVersion!)}[/]");
                AnsiConsole.MarkupLine($"  Profile [aqua]{Markup.Escape(result.ProfileName ?? "?")}[/] created.");
                if (result.ProfileActivated)
                {
                    AnsiConsole.MarkupLine("  [green]Profile is now active[/] — the next plan / deploy targets it.");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [dim]Activate later with: profile use {Markup.Escape(result.ProfileName ?? "?")}[/]");
                }
                break;
            case CollectionInstallOutcome.AlreadyInstalled:
                AnsiConsole.MarkupLine($"[bold yellow]Already installed[/] [aqua]{Markup.Escape(result.CollectionId!)}[/]@[aqua]{Markup.Escape(result.CollectionVersion!)}[/]");
                break;
            default:
                AnsiConsole.MarkupLine("[bold red]Collection install failed.[/] See diagnostics above.");
                break;
        }
        AnsiConsole.WriteLine();
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) { Directory.Delete(dir, recursive: true); } }
        catch { /* best-effort; the installer already copied what it needs */ }
    }

    private static Table BuildItemTable(IEnumerable<(string Id, string Name, string Version, string Description)> rows)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Name")
            .AddColumn("Version")
            .AddColumn("Description");
        foreach (var (id, name, version, description) in rows)
        {
            table.AddRow(
                $"[aqua]{Markup.Escape(id)}[/]",
                Markup.Escape(name),
                Markup.Escape(version),
                Markup.Escape(Truncate(description, 64)));
        }
        return table;
    }

    // Mods get an extra Safety column — the whole reason the index mirrors each
    // mod.yaml's safety flags is so a user can weigh them here, before installing.
    private static Table BuildModTable(IEnumerable<RepoIndexMod> mods)
    {
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("Id")
            .AddColumn("Name")
            .AddColumn("Version")
            .AddColumn("Safety")
            .AddColumn("Description");
        foreach (var m in mods)
        {
            table.AddRow(
                $"[aqua]{Markup.Escape(m.Id)}[/]",
                Markup.Escape(m.DisplayName),
                Markup.Escape(m.Version),
                Markup.Escape(FormatSafety(m.SafetyFlags)),
                Markup.Escape(Truncate(m.Description, 56)));
        }
        return table;
    }

    // Compact, predictable rendering of the four flags. An absent block means the
    // catalog didn't advertise safety ("—"); an absent or unknown field shows "?".
    private static string FormatSafety(RepoIndexSafetyFlags? safety)
    {
        if (safety is null)
        {
            return "—";
        }

        static string V(string? value) => value switch
        {
            "true" => "yes",
            "false" => "no",
            _ => "?",
        };

        return $"new-game: {V(safety.RequiresNewGame)} · remove: {V(safety.SafeToRemove)} · mp: {V(safety.MultiplayerSafe)} · camp: {V(safety.CampaignSafe)}";
    }

    private static string RepoSpec(AggregatedRepo r)
        => r.IndexPath.Length > 0 ? $"{r.Owner}/{r.Repo}:{r.IndexPath}" : $"{r.Owner}/{r.Repo}";

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) { return value ?? string.Empty; }
        return value[..(max - 1)] + "…";
    }
}
