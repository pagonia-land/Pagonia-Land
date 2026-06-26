using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

// One-stop wizard for everyday profile work: toggle mods on/off, reorder
// the load order, peek at the current profile state, hop to a different
// profile, or create a fresh one. Bundles what was previously split across
// the Active-Profile + Profiles sub-menus into a single discovery path,
// so a modder who knows "I need to disable mod X" can land here straight
// from the main menu without a category drill-down.
internal static class ManageActiveProfileWizard
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
            AnsiConsole.Write(new Rule("[bold aqua]Manage Active Profile[/]").LeftJustified());

            var state = new StoreStateReader().Read(layout);
            var active = state.ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
            AnsiConsole.MarkupLine($"[dim]Active profile:[/] [aqua]{Markup.Escape(active)}[/]");
            AnsiConsole.WriteLine();

            const string Enable = "Enable a mod";
            const string Disable = "Disable a mod";
            const string Reorder = "Reorder load order";
            const string Configure = "Configure this mod (tweaks)";
            const string Copy = "Copy this profile (snapshot / experiment)";
            const string Export = "Export this profile as a collection";
            const string Show = "Show profile details";
            const string Create = "Create a new profile";
            const string Switch = "Switch active profile";
            const string Back = "Back";

            // The tweaks entry only appears when an enabled mod actually exposes
            // tweaks — otherwise it's a dead end the user would bounce off.
            var choices = new List<string> { Enable, Disable };
            // Reorder needs at least two enabled mods to do anything — hide the dead
            // end otherwise, the same "hide the dead end" rule as Configure / Export.
            if (ActiveProfileCanReorder(layout))
            {
                choices.Add(Reorder);
            }
            if (ConfigureModWizard.HasConfigurableMods(layout))
            {
                choices.Add(Configure);
            }
            choices.Add(Copy);
            // Export only appears when the active profile has at least one enabled
            // mod — a collection requires mods (minItems 1), so an empty profile
            // can't be exported. Same "hide the dead end" rule as Configure above.
            if (ActiveProfileHasMods(layout))
            {
                choices.Add(Export);
            }
            choices.AddRange([Show, Create, Switch, Back]);

            var pick = AdvancedHelpers.NavSelect("[bold]What about this profile?[/]", choices.ToArray());

            switch (pick)
            {
                case Enable: RunEnable(layout); break;
                case Disable: RunDisable(layout); break;
                case Reorder: RunReorder(layout); break;
                case Configure: ConfigureModWizard.Run(layout); break;
                case Copy: RunCopy(layout); break;
                case Export: RunExport(layout); break;
                case Show: RunShow(layout); break;
                case Create: if (ProfileSetupWizard.Run(session)) { Pause(); } break;
                case Switch: RunSwitch(layout); break;
                default: return;
            }
        }
    }

    private static void RunEnable(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var installed = new ModLister().List(layout);
        if (installed.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No mods installed.[/]"); Pause(); return;
        }
        if (!AdvancedHelpers.TryPickOrCancel("Enable which mod? [dim](Esc to cancel)[/]", installed.Select(m => m.Id).Distinct(), out var modId)) return;
        var r = new ActiveProfileService().Enable(layout, modId, requestedVersion: null);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success && r.Profile is not null)
            AnsiConsole.MarkupLine($"[green]Load order:[/] {Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder))}");
        Pause();
    }

    private static void RunDisable(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var svc = new ActiveProfileService();
        var show = svc.Show(layout);
        if (show.Profile is null)
        {
            AnsiConsole.MarkupLine("[yellow]No active profile.[/]"); Pause(); return;
        }
        // Union EnabledMods + LoadOrder. A drifted profile (entry in LoadOrder
        // without a matching EnabledMods row, or vice versa) would otherwise
        // leave one of them unreachable for cleanup — the user has no
        // interactive path to fix the drift without this union.
        var choices = show.Profile.EnabledMods.Select(m => m.Id)
            .Concat(show.Profile.LoadOrder)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (choices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No enabled mods to disable.[/]"); Pause(); return;
        }
        if (!AdvancedHelpers.TryPickOrCancel("Disable which mod? [dim](Esc to cancel)[/]", choices, out var modId)) return;
        var r = svc.Disable(layout, modId);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success && r.Profile is not null)
            AnsiConsole.MarkupLine($"[green]Load order:[/] {(r.Profile.LoadOrder.Count == 0 ? "(empty)" : Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder)))}");
        Pause();
    }

    private static void RunReorder(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var svc = new ActiveProfileService();
        var show = svc.Show(layout);
        if (show.Profile is null || show.Profile.LoadOrder.Count < 2)
        {
            AnsiConsole.MarkupLine("[yellow]Need at least 2 enabled mods to reorder.[/]"); Pause(); return;
        }

        // Show which positions are dependency-pinned (governed by loadAfter/loadBefore) vs free, and
        // — if constraints would reorder the manual order at deploy — the effective order, so the
        // user's manual choice is never silently overridden without saying so.
        ShowLoadOrderConstraints(layout, show.Profile);

        // Every step is cancellable (Esc) so the user can back out of the reorder
        // at any prompt instead of being forced to complete a move they opened by
        // mistake — same back-out affordance as the free-text prompts.
        if (!AdvancedHelpers.TryPickOrCancel("Move which mod? [dim](Esc to cancel)[/]", show.Profile.LoadOrder, out var modId))
        {
            return;
        }
        if (!AdvancedHelpers.TryPickOrCancel("Place it relative to: [dim](Esc to cancel)[/]", show.Profile.LoadOrder.Where(id => id != modId), out var anchor))
        {
            return;
        }
        if (!AdvancedHelpers.TryPickOrCancel("Before or after? [dim](Esc to cancel)[/]", new[] { "before", "after" }, out var where, search: false))
        {
            return;
        }

        var r = where == "before"
            ? svc.MoveBefore(layout, modId, anchor)
            : svc.MoveAfter(layout, modId, anchor);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success && r.Profile is not null)
            AnsiConsole.MarkupLine($"[green]Load order:[/] {Markup.Escape(string.Join(" -> ", r.Profile.LoadOrder))}");
        Pause();
    }

    // Annotate the load order with loadAfter/loadBefore constraints: which mods are pinned, and the
    // effective deploy-time order when constraints reorder the manual one.
    private static void ShowLoadOrderConstraints(StoreLayout layout, ProfileFile profile)
    {
        var reader = new PagoniaLand.Patcher.ManifestReader();
        var inputs = new List<LoadOrderInput>();
        foreach (var id in profile.LoadOrder)
        {
            var enabled = profile.EnabledMods.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.Ordinal));
            var manifest = enabled is not null && Directory.Exists(layout.ModVersionDirectory(enabled.Id, enabled.Version))
                ? reader.ReadMod(layout.ModVersionDirectory(enabled.Id, enabled.Version)).Value?.Manifest
                : null;
            inputs.Add(new LoadOrderInput(id, manifest?.LoadAfter ?? [], manifest?.LoadBefore ?? []));
        }

        var resolved = new LoadOrderResolver().Resolve(inputs);
        if (resolved.Constrained.Count == 0)
        {
            return; // no constraints — the manual order is the whole story; nothing to annotate
        }

        var annotated = profile.LoadOrder.Select(id => resolved.Constrained.Contains(id)
            ? $"[aqua]{Markup.Escape(id)}[/] [dim](pinned)[/]"
            : Markup.Escape(id));
        AnsiConsole.MarkupLine($"[bold]Manual order:[/] {string.Join("  ", annotated)}");

        if (resolved.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderAdjusted))
        {
            AnsiConsole.MarkupLine($"[yellow]Effective at deploy[/] [dim](loadAfter/loadBefore applied):[/] {Markup.Escape(string.Join(" -> ", resolved.Order))}");
        }
        if (resolved.Diagnostics.Any(d => d.Code == ManagerDiagnosticCodes.LoadOrderCycle))
        {
            AnsiConsole.MarkupLine("[red]A loadAfter/loadBefore cycle was detected — those mods stay in your manual order.[/]");
        }
        AnsiConsole.MarkupLine("[dim](pinned) mods are positioned by their loadAfter/loadBefore at deploy; your manual order breaks ties.[/]");
        AnsiConsole.WriteLine();
    }

    private static bool ActiveProfileHasMods(StoreLayout layout)
    {
        var show = new ActiveProfileService().Show(layout);
        return show.Profile is not null && show.Profile.EnabledMods.Count > 0;
    }

    private static bool ActiveProfileCanReorder(StoreLayout layout)
    {
        var show = new ActiveProfileService().Show(layout);
        return show.Profile is not null && show.Profile.LoadOrder.Count >= 2;
    }

    private static void RunCopy(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var source = new StoreStateReader().Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
        if (!AdvancedHelpers.TryPromptText($"Copy '[aqua]{Markup.Escape(source)}[/]' to new profile name:", out var target,
                n => ProfileNameValidator.IsValid(n, out var why)
                    ? ValidationResult.Success() : ValidationResult.Error(why)))
        {
            return;
        }
        var activate = AdvancedHelpers.Confirm("Switch to the copy now?", defaultValue: false);
        var r = new ProfileLifecycleService().Copy(layout, source, target, activate);
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success)
            AnsiConsole.MarkupLine($"[green]Copied to[/] [aqua]{Markup.Escape(target)}[/]{(activate ? " (now active)" : string.Empty)}");
        Pause();
    }

    private static void RunExport(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var source = new StoreStateReader().Read(layout).ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
        if (!AdvancedHelpers.TryPromptText("Write collection to file path (e.g. [aqua].\\my-setup.collection.yaml[/]):", out var outPath)) { return; }
        var displayName = AdvancedHelpers.PromptText("Collection display name (optional):", allowEmpty: true);
        var r = new ProfileExportService().Export(layout, source, outPath,
            new ProfileExportOptions { Name = string.IsNullOrWhiteSpace(displayName) ? null : displayName });
        DiagnosticsRenderer.Render(r.Diagnostics);
        if (r.Success)
            AnsiConsole.MarkupLine($"[green]Exported to[/] [aqua]{Markup.Escape(r.OutputPath ?? outPath)}[/] (collection '[aqua]{Markup.Escape(r.CollectionId ?? "?")}[/]')");
        Pause();
    }

    private static void RunShow(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var r = new ActiveProfileService().Show(layout);
        if (r.Profile is null) { AnsiConsole.MarkupLine("[yellow]No active profile.[/]"); Pause(); return; }
        AnsiConsole.MarkupLine($"[bold]Profile:[/] [aqua]{Markup.Escape(r.ProfileName ?? "?")}[/]");
        AnsiConsole.MarkupLine($"  enabled: {r.Profile.EnabledMods.Count}");
        if (r.Profile.LoadOrder.Count == 0)
        {
            AnsiConsole.MarkupLine("  [dim](no enabled mods)[/]");
        }
        else
        {
            var i = 1;
            foreach (var id in r.Profile.LoadOrder)
            {
                var em = r.Profile.EnabledMods.FirstOrDefault(m => m.Id == id);
                AnsiConsole.MarkupLine($"    {i,2}. [aqua]{Markup.Escape(id)}[/]@{Markup.Escape(em?.Version ?? "?")}");
                i++;
            }
        }
        Pause();
    }

    private static void RunSwitch(StoreLayout layout)
    {
        AnsiConsole.WriteLine();
        var list = new ProfileLifecycleService().List(layout);
        if (list.Profiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No profiles to switch to.[/]"); Pause(); return;
        }
        var state = new StoreStateReader().Read(layout);
        var active = state.ActiveProfile ?? StoreLayoutConstants.DefaultProfileName;
        if (!AdvancedHelpers.TryPickOrCancel("Switch active profile to: [dim](Esc to cancel)[/]", list.Profiles.Select(p => p.Name), out var pick)) return;
        if (string.Equals(pick, active, StringComparison.Ordinal))
        {
            AnsiConsole.MarkupLine($"[dim]'{Markup.Escape(pick)}' is already active.[/]");
            Pause();
            return;
        }
        var r = new ProfileLifecycleService().Use(layout, pick);
        DiagnosticsRenderer.Render(r.Diagnostics);
        Pause();
    }

    private static void Pause()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key...[/]");
        Console.ReadKey(intercept: true);
    }
}
