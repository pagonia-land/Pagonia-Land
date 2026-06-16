using PagoniaLand.Manager;
using PagoniaLand.Patcher;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class PlanDeployWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Plan + Deploy to Game[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return;
        }

        if (!AdvancedHelpers.TryPromptGameRoot(session, out var gameRoot)) { return; }

        // First-deploy onboarding nudge: if an expansion is installed but we don't
        // know whether the player owns it, ask once before planning — so the gate
        // below gives an honest answer instead of a blanket "unknown" warning.
        OfferOwnershipNudge(layout, gameRoot);

        // Probe the path BEFORE planning. The patcher resolves operations
        // against extracted XML files; a live install (with pak/*.pak) needs
        // its paks extracted into a cache first so the patcher has something
        // to look at. patchSourceRoot is what the planner reads from; it
        // diverges from gameRoot on a live install (cache wins for reads).
        var detected = GameLayoutDetector.Detect(gameRoot);
        var patchSourceRoot = gameRoot;
        switch (detected.Kind)
        {
            case GameLayoutKind.Unrecognised:
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold red][aqua]{Markup.Escape(gameRoot)}[/] is not a recognised Pioneers of Pagonia folder.[/]");
                AnsiConsole.MarkupLine("[dim]Expected either a live install (with [aqua]pak/*.pak[/]) or an extracted GameDatabase root (with [aqua]core/gdb/*.gd.xml[/]).[/]");
                return;

            case GameLayoutKind.LiveInstall:
                // Confidence check: show the version read off the exe so the user
                // can confirm they pointed at the right, current install.
                var versionLabel = detected.GameProductVersion is { } detectedVersion
                    ? $"v{detectedVersion}"
                    : "version unknown";
                AnsiConsole.MarkupLine(
                    $"[green]Pioneers of Pagonia[/] — [aqua]{Markup.Escape(versionLabel)}[/] detected at [dim]{Markup.Escape(gameRoot)}[/]");
                AnsiConsole.WriteLine();

                CacheEnsureResult? ensureResult = null;
                using (var stages = new StagePrinter())
                {
                    // Selective extract: figure out which
                    // paks the active profile actually touches and only ensure
                    // those. Null fallback (e.g. profile missing) preserves
                    // the historical "extract everything" behaviour.
                    var requiredPaks = PakRequirementAnalyzer.ComputeRequiredPaks(layout, profileName: null);
                    ensureResult = new PakCacheService().Ensure(layout, detected,
                        requiredPakBasenames: requiredPaks,
                        progress: new StageProgress(stages.Start));
                }

                AnsiConsole.WriteLine();
                DiagnosticsRenderer.Render(ensureResult!.Diagnostics);
                if (!ensureResult.Success)
                {
                    AnsiConsole.MarkupLine("[red]Pak extraction failed — see diagnostics above. Plan aborted.[/]");
                    return;
                }
                // Plan against the cache. Deploy() re-runs detection and routes the
                // same install to DeployToLiveInstall, which re-uses this warm cache
                // (cache-hit short-circuit, no second extract) before rebuilding paks.
                patchSourceRoot = ensureResult.CacheRoot;
                break;

            case GameLayoutKind.ExtractedLayout:
                // Patcher reads directly from gameRoot — no cache needed.
                break;
        }

        // Plan first — show what would happen, with conflict surfaces, before
        // committing to anything.
        // Resolve ownership from the REAL game root (not patchSourceRoot, which is the
        // extract cache on a live install) so the plan's ownership gate sees the true
        // on-disk paks. The not-owned / unknown advisories render inline below.
        var expansions = ExpansionOwnershipService.ResolveForInstall(layout, gameRoot);

        PlanProfileResult? planResult = null;
        using (var planStage = new StagePrinter())
        {
            planStage.Start("Planning");
            planResult = new PlanProfileService().Plan(layout, patchSourceRoot, profileName: null, detected.GameProductVersion, expansions);
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(planResult!.ManagerDiagnostics);

        if (DiagnosticsRenderer.HasErrors(planResult.ManagerDiagnostics))
        {
            AnsiConsole.MarkupLine("[red]Plan failed before reaching the patcher. See diagnostics above.[/]");
            return;
        }

        RenderPlanTree(planResult);

        var patcherPlan = planResult.PatcherPlan;
        var patcherErrorCount = patcherPlan is null ? 0
            : patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(mp => mp.Diagnostics))
                .Count(d => d.Severity == PatchDiagnosticSeverity.Error);

        // Early-abort if the patcher already has errors. Without this, the wizard
        // would prompt "Deploy?" and then DeployService refuses with
        // 'deployBlockedByErrors' anyway — but now the user has already seen the
        // errors above (rendered by RenderPlanTree), so make the no-go path
        // explicit instead of pretending deploy is still on the table.
        if (patcherErrorCount > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold red]Cannot deploy:[/] the patcher reported [red]{patcherErrorCount}[/] error(s) (shown above). Most commonly this means a mod targets a game version that does not match the install you pointed at.");
            AnsiConsole.MarkupLine("[dim]Disable the affected mod ('Manage active profile' -> Disable a mod) or update it, then retry.[/]");
            return;
        }

        var hasConflicts = patcherPlan is not null
            && (patcherPlan.Conflicts.Count > 0 || patcherPlan.EntryConflicts.Count > 0);

        // Mirror exactly what DeployService gates on (DeployBlockedByWarnings): any manager
        // warning that isn't a non-blocking advisory, plus any patcher warning. Previously the
        // wizard only prompted on patcher *conflicts*, so a manager-level warning — most often a
        // mod whose gameDatabaseVersion doesn't match the install — fell straight through to a
        // hard "pass --accept-warnings" abort, forcing the user to quit and re-run on the scripted
        // CLI. Now they get the same inline opt-in the drift/conflict paths already offer.
        var managerWarnings = planResult.ManagerDiagnostics
            .Any(d => d.Severity == ManagerDiagnosticSeverity.Warning && !ExpansionGate.IsNonBlockingAdvisory(d.Code));
        var patcherWarnings = patcherPlan is not null
            && patcherPlan.Diagnostics.Concat(patcherPlan.ModPlans.SelectMany(mp => mp.Diagnostics))
                .Any(d => d.Severity == PatchDiagnosticSeverity.Warning);

        bool acceptWarnings = false;
        if (hasConflicts || managerWarnings || patcherWarnings)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(hasConflicts
                ? "[yellow]The plan has warnings (shown above), including conflicts where mods may overwrite each other's writes.[/]"
                : "[yellow]The plan has warnings (shown above) — e.g. a mod targeting a different game version than the install. They won't stop the writes, but it's worth a look first.[/]");
            var resolve = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("How do you want to proceed?")
                    .AddChoices("Abort", "Deploy anyway (accept the warnings)"));
            if (resolve == "Abort")
            {
                AnsiConsole.MarkupLine("[dim]Aborted. Disable the affected mod ('Manage active profile' -> Disable a mod) or update it, then retry.[/]");
                return;
            }
            acceptWarnings = true;
        }

        AnsiConsole.WriteLine();
        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What now?")
                .AddChoices("Dry-run first (no game files touched)", "Deploy", "Abort"));

        if (action == "Abort")
        {
            AnsiConsole.MarkupLine("[dim]Aborted. No files were written.[/]");
            return;
        }

        var dryRun = action.StartsWith("Dry", StringComparison.Ordinal);

        DeployResult? deployResult = null;
        using (var deployStages = new StagePrinter())
        {
            // Each stage label opens a new line; while the stage is running
            // (the actual deploy work, e.g. multi-second pak rebuild), the
            // ticker appends dots every 500 ms so the user can see the
            // program is still doing something. Newline + next label happens
            // when DeployService calls progress() again with the next stage.
            deployStages.Start(dryRun ? "Dry-running deploy" : "Deploying");
            deployResult = new DeployService().Deploy(
                layout, gameRoot, profileName: null, acceptWarnings, dryRun,
                progress: new StageProgress(deployStages.Start));
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(deployResult!.Diagnostics);
        AnsiConsole.WriteLine();

        // Live-state drift block: some game files changed since the last deploy.
        // Offer to overwrite them (--force) rather than making the user drop to the
        // scripted CLI. Only reachable on a real deploy — dry-run never blocks.
        if (deployResult.Outcome == DeployOutcome.Failed
            && deployResult.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.DeployBlockedByDrift))
        {
            AnsiConsole.MarkupLine("[yellow]Some live game files changed since the last deploy (shown above) — another tool or a hand-edit touched them.[/]");
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Overwrite the changed file(s)?")
                    .AddChoices("Abort (keep the out-of-band changes)", "Overwrite them (--force)"));
            if (choice.StartsWith("Overwrite", StringComparison.Ordinal))
            {
                using (var forceStages = new StagePrinter())
                {
                    forceStages.Start("Deploying (force)");
                    deployResult = new DeployService().Deploy(
                        layout, gameRoot, profileName: null, acceptWarnings, dryRun, acceptDrift: true,
                        progress: new StageProgress(forceStages.Start));
                }
                AnsiConsole.WriteLine();
                DiagnosticsRenderer.Render(deployResult.Diagnostics);
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Aborted. The changed files were left as-is.[/]");
                return;
            }
        }

        switch (deployResult.Outcome)
        {
            case DeployOutcome.Completed:
                if (deployResult.RebuiltPakCount > 0)
                {
                    AnsiConsole.MarkupLine($"[bold green]Deployed[/] — [aqua]{deployResult.RebuiltPakCount}[/] pak(s) rebuilt + [aqua]{deployResult.AddedFileCount}[/] overlay pak(s).");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[bold green]Deployed[/] — [aqua]{deployResult.ModifiedFileCount}[/] modified + [aqua]{deployResult.AddedFileCount}[/] added file(s).");
                }
                AnsiConsole.MarkupLine($"  [dim]Profile: {Markup.Escape(deployResult.ProfileName ?? "?")}[/]");
                AnsiConsole.MarkupLine($"  [dim]Manifest: {Markup.Escape(deployResult.ManifestPath ?? "?")}[/]");
                AnsiConsole.MarkupLine($"  [dim]To undo: use the Roll back wizard.[/]");
                break;
            case DeployOutcome.DryRun:
                if (deployResult.RebuiltPakCount > 0)
                {
                    AnsiConsole.MarkupLine($"[bold yellow]Dry-run complete[/] — would rebuild [aqua]{deployResult.RebuiltPakCount}[/] pak(s) and add [aqua]{deployResult.AddedFileCount}[/] overlay pak(s). Nothing written.");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[bold yellow]Dry-run complete[/] — would modify [aqua]{deployResult.ModifiedFileCount}[/], add [aqua]{deployResult.AddedFileCount}[/]. Nothing written.");
                }
                break;
            default:
                AnsiConsole.MarkupLine("[bold red]Deploy failed.[/] See diagnostics above.");
                break;
        }
    }

    // One-time onboarding nudge. Fires only when a declarable expansion is present
    // on disk but its ownership is still unknown AND the nudge hasn't been offered
    // for this install. "Ask me later" leaves it unknown; either way the install is
    // marked offered so the user isn't re-prompted on every future deploy.
    private static void OfferOwnershipNudge(StoreLayout layout, string gameRoot)
    {
        var service = new ExpansionOwnershipService();
        if (!service.ShouldOfferNudge(layout, gameRoot))
        {
            return;
        }

        var unknowns = service.List(layout, gameRoot).Expansions
            .Where(e => ExpansionPackages.IsDeclarable(e.Package) && e.Present && e.Ownership == OwnershipState.Unknown)
            .ToList();
        if (unknowns.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]One quick question[/]").LeftJustified());
        AnsiConsole.MarkupLine("[dim]The game ships every DLC pak to every player, so whether a DLC mod takes effect for you depends on what you actually own. Asked once — change it any time under Settings -> Game expansions.[/]");
        AnsiConsole.WriteLine();

        const string Owned = "Yes, I own it";
        const string NotOwned = "No, I don't own it";
        const string Later = "Ask me later";

        foreach (var e in unknowns)
        {
            var label = FriendlyName(e.Package);
            var answer = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]{label}[/] ([aqua]{e.Package}[/]) is installed — do you own it? [dim]This decides whether its mods take effect for you.[/]")
                    .HighlightStyle(new Style(foreground: Color.Aqua))
                    .AddChoices(Owned, NotOwned, Later));

            if (answer == Owned)
            {
                DiagnosticsRenderer.Render(service.Set(layout, gameRoot, e.Package, OwnershipState.Owned).Diagnostics);
            }
            else if (answer == NotOwned)
            {
                DiagnosticsRenderer.Render(service.Set(layout, gameRoot, e.Package, OwnershipState.NotOwned).Diagnostics);
            }
            // "Ask me later" → leave it unknown.
        }

        // Record that the nudge was offered so "ask me later" isn't re-asked every deploy.
        service.MarkNudgeOffered(layout, gameRoot);
        AnsiConsole.WriteLine();
    }

    private static string FriendlyName(string package) => package switch
    {
        ExpansionPackages.Dlc1 => "Meadowsong",
        ExpansionPackages.Decorations1 => "Decorations",
        _ => package,
    };

    // PromptGameRoot moved to AdvancedHelpers.PromptGameRoot so the prompt + session
    // reuse logic stays consistent across PlanDeploy / Rollback / AdvancedGameOps.

    private static void RenderPlanTree(PlanProfileResult plan)
    {
        if (plan.PatcherPlan is null) return;

        var root = new Tree($"[bold]Plan for profile[/] [aqua]{Markup.Escape(plan.ProfileName ?? "?")}[/]");
        foreach (var modPlan in plan.PatcherPlan.ModPlans)
        {
            var modNode = root.AddNode(
                $"[aqua]{Markup.Escape(modPlan.Mod.Manifest.Id)}[/]@{Markup.Escape(modPlan.Mod.Manifest.Version)} " +
                $"[dim]({modPlan.Writes.Count} write(s), {modPlan.EntryWrites.Count} entry op(s))[/]");

            foreach (var write in modPlan.Writes.Take(5))
            {
                modNode.AddNode(
                    $"[dim]{Markup.Escape(write.File)}[/] " +
                    $"[dim]{Markup.Escape(write.Component)}/{Markup.Escape(write.Path)}: " +
                    $"{Markup.Escape(write.OldValue)} -> {Markup.Escape(write.NewValue)}[/]");
            }

            if (modPlan.Writes.Count > 5)
            {
                modNode.AddNode($"[dim](… +{modPlan.Writes.Count - 5} more writes)[/]");
            }

            // Per-mod patcher diagnostics (targetNotFound, valueMismatch, ...).
            // Without these visible, a mod showing "0 writes" looks identical to
            // a healthy no-op when in fact the patcher couldn't resolve any target.
            foreach (var diagnostic in modPlan.Diagnostics)
            {
                modNode.AddNode(FormatPatchDiagnostic(diagnostic));
            }
        }

        // Top-level patcher diagnostics (not scoped to a single mod plan).
        foreach (var diagnostic in plan.PatcherPlan.Diagnostics)
        {
            root.AddNode(FormatPatchDiagnostic(diagnostic));
        }

        var conflictCount = plan.PatcherPlan.Conflicts.Count + plan.PatcherPlan.EntryConflicts.Count;
        if (conflictCount > 0)
        {
            var conflictsNode = root.AddNode($"[bold red]{conflictCount} conflict(s)[/]");
            foreach (var conflict in plan.PatcherPlan.Conflicts.Take(5))
            {
                conflictsNode.AddNode($"[red]{Markup.Escape(conflict.Type)}[/]: [dim]{Markup.Escape(conflict.TargetKey)}[/]");
            }
            foreach (var entry in plan.PatcherPlan.EntryConflicts.Take(5))
            {
                conflictsNode.AddNode($"[red]entry {Markup.Escape(entry.Type)}[/]: [dim]{Markup.Escape(entry.Path)}[/]");
            }
        }

        AnsiConsole.Write(root);
    }

    private static string FormatPatchDiagnostic(PatchDiagnostic diagnostic)
    {
        var (marker, color) = diagnostic.Severity switch
        {
            PatchDiagnosticSeverity.Error => ("[x]", "red"),
            PatchDiagnosticSeverity.Warning => ("[!]", "yellow"),
            _ => ("[i]", "dim"),
        };
        var line = $"[{color}]{Markup.Escape(marker)}[/] [dim][[{Markup.Escape(diagnostic.Code)}]][/] {Markup.Escape(diagnostic.Message)}";
        if (!string.IsNullOrEmpty(diagnostic.Path))
        {
            line += $" [dim](at {Markup.Escape(diagnostic.Path)})[/]";
        }
        return line;
    }
}
