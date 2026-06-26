using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// The opt-in front door for the versioning use cases (the scriptable `outdated` /
// `update` / `collection update` verbs are the engine). It runs one read-only update
// check across the store's gh:-sourced mods + collections, shows what's behind, and
// — for whatever the user picks — surfaces the delta, confirms, and applies the
// transparent + reversible update. Nothing is touched until a deliberate confirm.
internal static class UpdateWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Update Mods & Collections[/]").LeftJustified());
        AnsiConsole.WriteLine();

        var layout = session.GetLayout();
        if (!new StoreStateReader().Exists(layout))
        {
            AnsiConsole.MarkupLine("[yellow]Store not initialised. Use Advanced -> Store -> init first.[/]");
            return;
        }

        var state = new StoreStateReader().Read(layout);
        var activeProfile = string.IsNullOrWhiteSpace(state.ActiveProfile)
            ? StoreLayoutConstants.DefaultProfileName
            : state.ActiveProfile!;

        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");

        // One read-only check up front. The local working lists are mutated as updates
        // land (the applied item drops off) so we don't re-hit the network per pick;
        // a "Re-check" entry forces a fresh scan when the user wants one.
        UpdateCheckResult? check = null;
        AdvancedHelpers.Spin("Checking for updates...", () => { check = new UpdateDetectionService(http).Check(layout); });
        DiagnosticsRenderer.Render(check!.Diagnostics.Where(d => d.Severity != ManagerDiagnosticSeverity.Info).ToList());

        var mods = check.Updates.ToList();
        var collections = check.CollectionUpdates.ToList();
        CacheCounts(session, mods.Count, collections.Count);

        while (true)
        {
            if (mods.Count == 0 && collections.Count == 0)
            {
                AnsiConsole.WriteLine();
                var checkedTotal = check.CheckedCount + check.CheckedCollectionCount;
                var drifts = check.ContentDrifts.ToList();
                if (drifts.Count > 0)
                {
                    // Same-version content re-publish: not an `update` (no newer version to pin), but
                    // not "up to date" either — surface it instead of falsely claiming all is current,
                    // matching what the scriptable `outdated` reports.
                    AnsiConsole.MarkupLine($"[yellow]No version updates, but {drifts.Count} mod(s) changed content at the same version[/] [dim](re-install to refresh):[/]");
                    foreach (var drift in drifts)
                    {
                        AnsiConsole.MarkupLine($"  [aqua]{Markup.Escape(drift.Id)}[/] {Markup.Escape(drift.Version)} [dim](content drift)[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]Everything is up to date.[/] [dim]({checkedTotal} item(s) checked.)[/]");
                }
                return;
            }

            // Build the pick list: one labelled row per outdated mod / collection, plus a
            // "Re-check now" action. Esc / cancel finishes the wizard.
            const string Recheck = "Re-check now";
            var labels = new List<string>();
            var byLabel = new Dictionary<string, (bool IsCollection, string Id, string From, string To, string Gdb)>(StringComparer.Ordinal);

            foreach (var m in mods)
            {
                var label = $"mod         {m.Id}   {m.InstalledVersion} -> {m.AvailableVersion}";
                labels.Add(label);
                byLabel[label] = (false, m.Id, m.InstalledVersion, m.AvailableVersion, m.GameDatabaseVersion);
            }
            foreach (var c in collections)
            {
                var label = $"collection  {c.Id}   {c.InstalledVersion} -> {c.AvailableVersion}";
                labels.Add(label);
                byLabel[label] = (true, c.Id, c.InstalledVersion, c.AvailableVersion, c.GameDatabaseVersion);
            }
            labels.Add(Recheck);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]{mods.Count}[/] mod + [bold]{collections.Count}[/] collection update(s) available.");

            if (!AdvancedHelpers.TryPickOrCancel("[bold]Pick an item to update[/] [dim](Esc to finish)[/]:", labels, out var picked))
            {
                return;
            }

            if (string.Equals(picked, Recheck, StringComparison.Ordinal))
            {
                AdvancedHelpers.Spin("Re-checking for updates...", () => { check = new UpdateDetectionService(http).Check(layout); });
                DiagnosticsRenderer.Render(check!.Diagnostics.Where(d => d.Severity != ManagerDiagnosticSeverity.Info).ToList());
                mods = check.Updates.ToList();
                collections = check.CollectionUpdates.ToList();
                CacheCounts(session, mods.Count, collections.Count);
                continue;
            }

            var entry = byLabel[picked];
            ShowDelta(entry);

            if (entry.IsCollection)
            {
                var svc = new CollectionUpdateService(http);

                // Decide how the user's own tweak settings are treated. Only ask when they actually
                // have genuine overrides on this collection's mods — otherwise Merge is a no-op.
                var policy = CollectionTweakPolicy.Merge;
                Func<CollectionTweakConflict, CollectionTweakResolution>? resolve = null;
                var genuine = svc.PreviewGenuineOverrides(layout, entry.Id);
                if (genuine.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[yellow]You have {genuine.Count} personal tweak setting(s)[/] on this collection's mods:");
                    foreach (var g in genuine)
                    {
                        var label = g.TweakLabel.Length > 0 ? g.TweakLabel : g.TweakId;
                        AnsiConsole.MarkupLine($"  [aqua]{Markup.Escape(g.ModId)}[/] · {Markup.Escape(label)} = [aqua]{Markup.Escape(g.YourValue)}[/]");
                    }

                    const string KeepMine = "Keep my settings (take the collection's only where I didn't change anything)";
                    const string TakeCollection = "Use the collection's new values (discard my settings)";
                    const string DecideEach = "Decide for each changed setting";
                    var how = AdvancedHelpers.Pick("[bold]How should your tweak settings be handled?[/]", new[] { KeepMine, TakeCollection, DecideEach });
                    if (how == TakeCollection)
                    {
                        policy = CollectionTweakPolicy.Reseed;
                    }
                    else if (how == DecideEach)
                    {
                        policy = CollectionTweakPolicy.Ask;
                        resolve = c =>
                        {
                            var label = c.TweakLabel.Length > 0 ? c.TweakLabel : c.TweakId;
                            return AdvancedHelpers.Confirm(
                                $"[aqua]{Markup.Escape(c.ModId)}[/] · {Markup.Escape(label)}: keep your value [aqua]{Markup.Escape(c.YourValue)}[/]? [dim](collection now sets {Markup.Escape(c.CuratorValue)})[/]",
                                defaultValue: true)
                                ? CollectionTweakResolution.KeepYours
                                : CollectionTweakResolution.TakeCurator;
                        };
                    }
                    // KeepMine -> Merge (the default).
                }

                if (!AdvancedHelpers.Confirm($"Update collection [aqua]{Markup.Escape(entry.Id)}[/] to {Markup.Escape(entry.To)}?", defaultValue: true))
                {
                    AnsiConsole.MarkupLine("[dim]Skipped.[/]");
                    continue;
                }

                CollectionUpdateResult? result = null;
                if (policy == CollectionTweakPolicy.Ask)
                {
                    // Can't run the per-conflict prompts under the spinner ticker — call directly.
                    AnsiConsole.MarkupLine("[dim]Updating (you'll be asked about each changed setting)...[/]");
                    result = svc.Update(layout, entry.Id, policy, resolve);
                }
                else
                {
                    AdvancedHelpers.Spin($"Updating collection {entry.Id}...", () => { result = svc.Update(layout, entry.Id, policy, resolve); });
                }
                DiagnosticsRenderer.Render(result!.Diagnostics);
                if (result.Outcome == CollectionUpdateOutcome.Updated)
                {
                    AnsiConsole.MarkupLine($"[green]Updated[/] [aqua]{Markup.Escape(entry.Id)}[/] {Markup.Escape(result.FromVersion ?? "")} -> {Markup.Escape(result.ToVersion ?? "")}. [dim]The previous version is kept on disk for rollback.[/]");
                    collections.RemoveAll(c => string.Equals(c.Id, entry.Id, StringComparison.Ordinal));
                    CacheCounts(session, mods.Count, collections.Count);
                }
            }
            else
            {
                if (!AdvancedHelpers.Confirm($"Update mod [aqua]{Markup.Escape(entry.Id)}[/] to {Markup.Escape(entry.To)} in profile '{Markup.Escape(activeProfile)}'?", defaultValue: true))
                {
                    AnsiConsole.MarkupLine("[dim]Skipped.[/]");
                    continue;
                }

                ModUpdateResult? result = null;
                AdvancedHelpers.Spin($"Updating mod {entry.Id}...", () => { result = new ModUpdateService(http, state.AllowInsecureSources).Update(layout, entry.Id, activeProfile); });
                DiagnosticsRenderer.Render(result!.Diagnostics);
                if (result.Outcome == ModUpdateOutcome.Updated)
                {
                    AnsiConsole.MarkupLine($"[green]Updated[/] [aqua]{Markup.Escape(entry.Id)}[/] {Markup.Escape(result.FromVersion ?? "")} -> {Markup.Escape(result.ToVersion ?? "")}. [dim]The previous version is kept on disk for rollback.[/]");
                    mods.RemoveAll(m => string.Equals(m.Id, entry.Id, StringComparison.Ordinal));
                    CacheCounts(session, mods.Count, collections.Count);
                }
            }
        }
    }

    // Mirror the live outdated counts into the session so the Status dashboard can
    // surface "N update(s) available" without itself hitting the network.
    private static void CacheCounts(SessionState session, int mods, int collections)
    {
        session.OutdatedModCount = mods;
        session.OutdatedCollectionCount = collections;
    }

    private static void ShowDelta((bool IsCollection, string Id, string From, string To, string Gdb) entry)
    {
        var kind = entry.IsCollection ? "Collection" : "Mod";
        var lines = new List<string>
        {
            $"[bold]{kind}:[/] [aqua]{Markup.Escape(entry.Id)}[/]",
            $"[bold]Version:[/] {Markup.Escape(entry.From)} [aqua]->[/] {Markup.Escape(entry.To)}",
        };
        if (!string.IsNullOrWhiteSpace(entry.Gdb))
        {
            lines.Add($"[bold]gameDatabaseVersion:[/] {Markup.Escape(entry.Gdb)}");
        }
        lines.Add(entry.IsCollection
            ? "[dim]Reseeds the collection's curator defaults (your tweak overrides on its mods are reset). The old version is kept for rollback.[/]"
            : "[dim]Re-points the active profile's pin; your tweak overrides carry forward. The old version is kept for rollback.[/]");

        var panel = new Panel(string.Join("\n", lines))
        {
            Header = new PanelHeader("[bold]Update[/]"),
            Border = BoxBorder.Rounded,
        };
        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
    }
}
