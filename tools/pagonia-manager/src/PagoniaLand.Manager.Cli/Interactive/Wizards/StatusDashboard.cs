using PagoniaLand.Manager;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace PagoniaLand.Manager.Cli.Interactive;

// Read-only first wizard. Renders a snapshot of the current store + active
// profile + installed mods + (optionally) last deploy. No prompts, no
// mutations — safe to land in isolation and verifies the rendering helpers
// work against real service results.
internal static class StatusDashboard
{
    public static void Render(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Current State[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var resolution = StoreRootResolver.Resolve(session.StoreOverride);
        var layout = new StoreLayout(resolution.Root);
        var inspector = new StoreInspector().Inspect(layout);

        RenderStorePanel(resolution, inspector);

        if (!inspector.Initialised)
        {
            AnsiConsole.MarkupLine("[yellow]The store at this path is not initialised yet.[/]");
            AnsiConsole.MarkupLine("[dim]Use Advanced -> Store -> init to set it up.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        RenderInstalledModsTable(layout);

        AnsiConsole.WriteLine();
        RenderActiveProfilePanel(layout, inspector.ActiveProfile);

        AnsiConsole.WriteLine();
        RenderExpansionsPanel(layout, session);

        AnsiConsole.WriteLine();
        RenderDeployPanel(layout, session.GameRoot);

        // surface orphaned deploys (game install moved or updated
        // since the deploy was recorded). Doesn't fire unless there's at least
        // one orphan, so a clean store stays uncluttered.
        var orphans = new OrphanedDeployFinder().FindAll(layout);
        if (orphans.Count > 0)
        {
            AnsiConsole.WriteLine();
            RenderOrphanedDeploysPanel(orphans);
        }

        // total <store>/deploys/ size hint. Surfaces only when the
        // total crosses a soft threshold so the dashboard stays quiet for
        // users with healthy disk usage.
        var deploysSize = DeployCleanService.ComputeDeploysSize(layout);
        if (deploysSize >= DeploysStorageHighThresholdBytes)
        {
            AnsiConsole.WriteLine();
            RenderDeploysStoragePanel(layout, deploysSize);
        }
    }

    // soft threshold above which the dashboard surfaces a "consider running
    // deploys clean" hint. A live deploy backs up the whole canonical pak set, and
    // core.pak alone is ~5 GB, so a single deploy already costs ~5 GB of backups.
    // Set the nag at 15 GB (~3 deploy generations): a deploy or two — where the
    // only backup is the protected lastDeploy and there's nothing to reclaim yet —
    // shouldn't trigger it. Raise this if your install's paks are larger.
    private const long DeploysStorageHighThresholdBytes = 15L * 1024 * 1024 * 1024;

    private static void RenderDeploysStoragePanel(StoreLayout layout, long sizeBytes)
    {
        var sizeGb = sizeBytes / (1024d * 1024d * 1024d);
        var body =
            $"[bold]<store>/deploys/[/] currently holds [yellow]~{sizeGb:F1} GB[/] of pak backups.\n" +
            $"  [dim]Run [aqua]pagonia-manager deploys clean --keep <N>[/] to trim per-fingerprint to the N newest entries.[/]\n" +
            $"  [dim]Add [aqua]--dry-run[/] first to see what would be removed.[/]";

        var renderables = new List<IRenderable> { new Markup(body) };

        // Break the total down by game-install fingerprint so it's obvious which
        // install's backups dominate. Only worth showing when there's more than one.
        var byFingerprint = DeployCleanService.ComputeDeploysSizeByFingerprint(layout);
        if (byFingerprint.Count >= 2)
        {
            var palette = new[] { Color.Aqua, Color.Yellow, Color.Green, Color.Fuchsia, Color.Orange1, Color.Blue, Color.Red, Color.Grey };
            var chart = new BreakdownChart().Width(60).ShowPercentage();
            for (var i = 0; i < byFingerprint.Count; i++)
            {
                var gb = byFingerprint[i].Bytes / (1024d * 1024d * 1024d);
                chart.AddItem($"{Markup.Escape(byFingerprint[i].Fingerprint)} ({gb:F1} GB)", gb, palette[i % palette.Length]);
            }
            renderables.Add(new Markup("\n[dim]By game install (fingerprint):[/]"));
            renderables.Add(chart);
        }

        AnsiConsole.Write(new Panel(new Rows(renderables))
            .Header("[bold yellow]Deploy backups storage[/]")
            .BorderColor(Color.Yellow));
    }

    private static void RenderOrphanedDeploysPanel(IReadOnlyList<OrphanedDeploy> orphans)
    {
        var rows = orphans.Take(5).Select(o =>
        {
            var reasonText = o.Reason switch
            {
                OrphanReason.GameRootGone => "gameRoot gone",
                OrphanReason.GameUpdated => "game updated",
                _ => "unknown",
            };
            return $"  [dim]{Markup.Escape(o.Fingerprint)}[/] — [yellow]{reasonText}[/] — {Markup.Escape(o.RecordedGameRoot)}";
        });

        var body = string.Join('\n', rows);
        if (orphans.Count > 5)
        {
            body += $"\n  [dim](+ {orphans.Count - 5} more — see 'pagonia-manager deploys list-orphans' for the full list)[/]";
        }
        else
        {
            body += "\n  [dim]Run 'pagonia-manager deploys list-orphans' for details.[/]";
        }

        AnsiConsole.Write(new Panel(new Markup(body))
            .Header($"[bold yellow]Orphaned deploys ({orphans.Count})[/]")
            .BorderColor(Color.Yellow));
    }

    private static void RenderStorePanel(StoreRootResolver.Resolution resolution, StoreInfo info)
    {
        var grid = new Grid().AddColumn().AddColumn();
        grid.AddRow("[bold]Root[/]", $"[aqua]{Markup.Escape(info.Root)}[/]");
        grid.AddRow("[bold]Source[/]", DescribeSource(resolution.Source));
        grid.AddRow("[bold]Status[/]",
            info.Initialised ? "[green]initialised[/]" : "[yellow]not initialised[/]");
        if (info.Initialised)
        {
            grid.AddRow("[bold]Store version[/]", $"[aqua]{info.StoreVersion}[/]");
            grid.AddRow("[bold]Active profile[/]", $"[aqua]{Markup.Escape(info.ActiveProfile ?? "(none)")}[/]");
            grid.AddRow("[bold]Counts[/]",
                $"mods [aqua]{info.InstalledModCount}[/]  •  profiles [aqua]{info.ProfileCount}[/]  •  collections [aqua]{info.CollectionCount}[/]");
        }

        AnsiConsole.Write(new Panel(grid).Header("[bold]Store[/]").BorderColor(Color.Aqua));
    }

    private static void RenderInstalledModsTable(StoreLayout layout)
    {
        var mods = new ModLister().List(layout);
        if (mods.Count == 0)
        {
            AnsiConsole.Write(new Panel("[dim](no mods installed)[/]")
                .Header("[bold]Installed Mods[/]")
                .BorderColor(Color.Grey));
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]Installed Mods[/]")
            .AddColumn("Id")
            .AddColumn("Version")
            .AddColumn("Installed");

        foreach (var mod in mods)
        {
            table.AddRow(
                $"[aqua]{Markup.Escape(mod.Id)}[/]",
                Markup.Escape(mod.Version),
                Markup.Escape(mod.InstalledAt ?? "(unknown)"));
        }

        AnsiConsole.Write(table);
    }

    private static void RenderActiveProfilePanel(StoreLayout layout, string? activeProfileName)
    {
        var result = new ActiveProfileService().Show(layout);
        if (!result.Success || result.Profile is null)
        {
            AnsiConsole.Write(new Panel("[yellow]Could not load active profile.[/]")
                .Header("[bold]Active Profile[/]")
                .BorderColor(Color.Yellow));
            return;
        }

        var profile = result.Profile;
        var header = $"[bold]Active Profile: [aqua]{Markup.Escape(result.ProfileName ?? "?")}[/][/]";

        if (profile.LoadOrder.Count == 0)
        {
            AnsiConsole.Write(new Panel("[dim](no mods enabled in this profile)[/]")
                .Header(header)
                .BorderColor(Color.Aqua));
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Aqua)
            .Title(header)
            .AddColumn("#")
            .AddColumn("Mod Id")
            .AddColumn("Version");

        var index = 1;
        foreach (var modId in profile.LoadOrder)
        {
            var enabled = profile.EnabledMods.FirstOrDefault(m =>
                string.Equals(m.Id, modId, StringComparison.Ordinal));
            table.AddRow(
                index.ToString(),
                $"[aqua]{Markup.Escape(modId)}[/]",
                Markup.Escape(enabled?.Version ?? "(missing)"));
            index++;
        }

        AnsiConsole.Write(table);
    }

    // DLC expansion ownership. When a game install resolves (session value, stored
    // default, or platform default), show the live Present/Owned/Effective table for
    // it; otherwise fall back to whatever ownership is declared in the store so the
    // user can still see "what did I say I own" without pointing at an install.
    private static void RenderExpansionsPanel(StoreLayout layout, SessionState session)
    {
        var service = new ExpansionOwnershipService();
        var resolved = GameRootResolver.Resolve(layout, session.GameRoot);

        if (resolved.HasPath)
        {
            var list = service.List(layout, resolved.Path!);
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Aqua)
                .Title($"[bold]Game Expansions[/] [dim]({Markup.Escape(resolved.Path!)})[/]")
                .AddColumn("Package")
                .AddColumn("Present")
                .AddColumn("Owned")
                .AddColumn("Effective");

            foreach (var e in list.Expansions)
            {
                var owned = ExpansionPackages.IsAlwaysOwned(e.Package) ? "[dim]always[/]" : ColorizeOwnership(e.Ownership);
                table.AddRow(
                    $"[aqua]{Markup.Escape(e.Package)}[/]",
                    e.Present ? "[green]yes[/]" : "[dim]no[/]",
                    owned,
                    e.Effective ? "[green]yes[/]" : "[dim]no[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]  Edit under Settings → Game expansions. core / tools are base game + editor data (always owned).[/]");
            return;
        }

        // No game install to resolve presence against — show stored declarations only.
        var declared = service.ListDeclaredInstalls(layout)
            .Where(d => d.Decorations1 != OwnershipState.Unknown || d.Dlc1 != OwnershipState.Unknown)
            .ToList();

        if (declared.Count == 0)
        {
            AnsiConsole.Write(new Panel("[dim]No DLC ownership declared yet — set it under Settings → Game expansions (or it's asked on first deploy).[/]")
                .Header("[bold]Game Expansions[/]")
                .BorderColor(Color.Grey));
            return;
        }

        var declaredTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Aqua)
            .Title("[bold]Game Expansions[/] [dim](declared — no game install set this session to resolve Present/Effective)[/]")
            .AddColumn("Install")
            .AddColumn("decorations1")
            .AddColumn("dlc1");

        foreach (var d in declared)
        {
            declaredTable.AddRow(
                $"[dim]{Markup.Escape(d.GameRoot)}[/]",
                ColorizeOwnership(d.Decorations1),
                ColorizeOwnership(d.Dlc1));
        }

        AnsiConsole.Write(declaredTable);
    }

    private static string ColorizeOwnership(OwnershipState state) => state switch
    {
        OwnershipState.Owned => "[green]owned[/]",
        OwnershipState.NotOwned => "[yellow]not owned[/]",
        _ => "[dim]unknown[/]",
    };

    private static void RenderDeployPanel(StoreLayout layout, string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
        {
            AnsiConsole.Write(new Panel("[dim]No game install set this session. Use Plan + Deploy to point at one.[/]")
                .Header("[bold]Deploys[/]")
                .BorderColor(Color.Grey));
            return;
        }

        var result = new DeployStatusService().List(layout, gameRoot);
        if (!result.HasDeploys)
        {
            AnsiConsole.Write(new Panel($"[dim]No deploys yet for [/][aqua]{Markup.Escape(gameRoot)}[/]")
                .Header("[bold]Deploys[/]")
                .BorderColor(Color.Grey));
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .Title($"[bold]Deploys for [/][aqua]{Markup.Escape(gameRoot)}[/]")
            .AddColumn("Timestamp")
            .AddColumn("Profile")
            .AddColumn("Mods", c => c.RightAligned())
            .AddColumn("Files", c => c.RightAligned());

        foreach (var entry in result.Deploys)
        {
            table.AddRow(
                Markup.Escape(entry.Timestamp),
                $"[aqua]{Markup.Escape(entry.Profile)}[/]",
                entry.ModCount.ToString(),
                entry.FileCount.ToString());
        }

        AnsiConsole.Write(table);
    }

    private static string DescribeSource(StoreRootResolver.ResolutionSource source) => source switch
    {
        StoreRootResolver.ResolutionSource.Flag => "[dim]--store flag[/]",
        StoreRootResolver.ResolutionSource.EnvironmentVariable => $"[dim]{StoreRootResolver.EnvironmentVariableName} env[/]",
        StoreRootResolver.ResolutionSource.PlatformDefault => "[dim]platform default[/]",
        _ => "[red]unknown[/]",
    };
}
