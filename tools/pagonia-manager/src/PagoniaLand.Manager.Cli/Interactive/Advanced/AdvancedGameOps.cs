using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedGameOps
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Game Ops[/]", "plan", "deploy", "rollback", "deploy-status", "deploy-list", "expansions", "deploys clean", "deploys list-orphans", "Back");

            var layout = session.GetLayout();
            switch (pick)
            {
                case "plan": RunPlan(layout, session); break;
                case "deploy": RunDeploy(layout, session); break;
                case "rollback": RunRollback(layout, session); break;
                case "deploy-status": RunDeployStatus(layout, session); break;
                case "deploy-list": RunDeployList(layout, session); break;
                case "expansions": GameExpansionsWizard.Run(session); break;
                case "deploys clean": RunDeploysClean(layout); break;
                case "deploys list-orphans": RunDeploysListOrphans(layout); break;
                default: return;
            }
        }
    }

    // Thin alias around the cross-wizard helper so the local call sites stay terse.
    // Returns false when the user backs out of the game-root prompt (empty line).
    private static bool TryPromptGame(SessionState s, out string game) => AdvancedHelpers.TryPromptGameRoot(s, out game);

    private static void RunPlan(StoreLayout layout, SessionState s)
    {
        AdvancedHelpers.Header("Game Ops → plan");
        if (!TryPromptGame(s, out var game)) { return; }
        var installVersion = GameVersionReader.TryRead(game, out var detectedVersion, out _) ? detectedVersion : null;
        // Resolve the install's expansion ownership so the plan's ownership gate fires
        // here too (parity with the scripted `plan`).
        var expansions = ExpansionOwnershipService.ResolveForInstall(layout, game);
        PlanProfileResult? r = null;
        AdvancedHelpers.Spin("Planning...", () => { r = new PlanProfileService().Plan(layout, game, profileName: null, installVersion, expansions); });
        DiagnosticsRenderer.Render(r!.ManagerDiagnostics);
        if (r.PatcherPlan is not null)
        {
            AnsiConsole.MarkupLine($"  [bold]Mods:[/] {r.PatcherPlan.ModPlans.Count}  [bold]Writes:[/] {r.PatcherPlan.Writes.Count}  [bold]Conflicts:[/] {r.PatcherPlan.Conflicts.Count + r.PatcherPlan.EntryConflicts.Count}");
        }
        AnsiConsole.MarkupLine($"[bold]Result:[/] {(r.Success ? "[green]OK[/]" : "[red]Blocked[/]")}");
        Pause();
    }

    private static void RunDeploy(StoreLayout layout, SessionState s)
    {
        AdvancedHelpers.Header("Game Ops → deploy");
        if (!TryPromptGame(s, out var game)) { return; }
        var dryRun = AdvancedHelpers.Confirm("Dry-run only (no files written)?", defaultValue: false);
        var acceptWarnings = AdvancedHelpers.Confirm("Accept warnings (proceed despite blocking-warning diagnostics)?", defaultValue: false);
        DeployResult? r = null;
        AdvancedHelpers.Spin(dryRun ? "Dry-running..." : "Deploying...",
            () => { r = new DeployService().Deploy(layout, game, profileName: null, acceptWarnings, dryRun); });
        DiagnosticsRenderer.Render(r!.Diagnostics);
        AnsiConsole.MarkupLine($"[bold]Outcome:[/] {r.Outcome}  [bold]Modified:[/] {r.ModifiedFileCount}  [bold]Added:[/] {r.AddedFileCount}");
        Pause();
    }

    private static void RunRollback(StoreLayout layout, SessionState s)
    {
        AdvancedHelpers.Header("Game Ops → rollback");
        if (!TryPromptGame(s, out var game)) { return; }
        if (!AdvancedHelpers.Confirm("Roll back the latest deploy?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[dim]Aborted.[/]"); Pause(); return;
        }
        RollbackResult? r = null;
        AdvancedHelpers.Spin("Rolling back...", () => { r = new RollbackService().Rollback(layout, game); });
        DiagnosticsRenderer.Render(r!.Diagnostics);
        AnsiConsole.MarkupLine($"[bold]Outcome:[/] {r.Outcome}  [bold]Files:[/] {r.RestoredFileCount}");
        Pause();
    }

    private static void RunDeployStatus(StoreLayout layout, SessionState s)
    {
        AdvancedHelpers.Header("Game Ops → deploy-status");
        if (!TryPromptGame(s, out var game)) { return; }
        var r = new DeployStatusService().List(layout, game);
        if (!r.HasDeploys) { AnsiConsole.MarkupLine("[dim]No deploys for this game install.[/]"); Pause(); return; }
        var latest = r.Deploys[0];
        AnsiConsole.MarkupLine($"[bold]Latest:[/] [aqua]{Markup.Escape(latest.Timestamp)}[/] profile [aqua]{Markup.Escape(latest.Profile)}[/]");
        AnsiConsole.MarkupLine($"  mods: {latest.ModCount}  files: {latest.FileCount}");
        Pause();
    }

    private static void RunDeployList(StoreLayout layout, SessionState s)
    {
        AdvancedHelpers.Header("Game Ops → deploy-list");
        if (!TryPromptGame(s, out var game)) { return; }
        var r = new DeployStatusService().List(layout, game);
        if (!r.HasDeploys) { AnsiConsole.MarkupLine("[dim]No deploys for this game install.[/]"); Pause(); return; }
        var t = new Table().Border(TableBorder.Rounded)
            .AddColumn("Timestamp").AddColumn("Profile").AddColumn("Mods").AddColumn("Files");
        foreach (var d in r.Deploys)
            t.AddRow(Markup.Escape(d.Timestamp), $"[aqua]{Markup.Escape(d.Profile)}[/]", d.ModCount.ToString(), d.FileCount.ToString());
        AnsiConsole.Write(t);
        Pause();
    }

    private static void RunDeploysClean(StoreLayout layout)
    {
        AdvancedHelpers.Header("Game Ops → deploys clean");
        var keep = AnsiConsole.Prompt(
            new TextPrompt<int>("Keep how many newest deploys per game install?")
                .Validate(n => n >= 0 ? ValidationResult.Success() : ValidationResult.Error("must be >= 0")));
        var dryRun = AdvancedHelpers.Confirm("Dry-run only (preview, no deletes)?", defaultValue: true);
        if (!dryRun && !AdvancedHelpers.Confirm($"Remove all but the newest {keep} per install (the current lastDeploy is always protected)?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[dim]Aborted.[/]"); Pause(); return;
        }

        // Store-wide clean (no --game scope): the service still keeps the N newest
        // per fingerprint and refuses to remove the live lastDeploy.
        var r = new DeployCleanService().Clean(layout, keep, gameRoot: null, dryRun);
        DiagnosticsRenderer.Render(r.Diagnostics);
        AnsiConsole.MarkupLine($"[bold]Mode:[/] {(r.DryRun ? "dry-run" : "removing")}  [bold]Removed:[/] {r.RemovedCount}  [bold]Kept:[/] {r.KeptCount}" +
            (r.RefusedCount > 0 ? $"  [bold]Refused:[/] {r.RefusedCount} (lastDeploy protected)" : string.Empty));
        Pause();
    }

    private static void RunDeploysListOrphans(StoreLayout layout)
    {
        AdvancedHelpers.Header("Game Ops → deploys list-orphans");
        var orphans = new OrphanedDeployFinder().FindAll(layout);
        if (orphans.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](no orphaned deploys — every deploy directory still matches a live install fingerprint)[/]");
            Pause();
            return;
        }

        var t = new Table().Border(TableBorder.Rounded)
            .AddColumn("Fingerprint").AddColumn("Recorded game root").AddColumn("Deploys").AddColumn("Why stale");
        foreach (var o in orphans)
        {
            var reason = o.Reason switch
            {
                OrphanReason.GameRootGone => "game root gone",
                OrphanReason.GameUpdated => "game updated (fingerprint changed)",
                _ => "unknown",
            };
            t.AddRow(Markup.Escape(o.Fingerprint), $"[aqua]{Markup.Escape(o.RecordedGameRoot)}[/]", o.TotalDeployCount.ToString(), reason);
        }
        AnsiConsole.Write(t);
        AnsiConsole.MarkupLine("[dim]Remove with[/] [aqua]deploys clean[/] [dim](scoped per game root) or the[/] [aqua]Clean up old deploy backups[/] [dim]main-menu wizard.[/]");
        Pause();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
