using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class InstallModWizard
{
    public static void Run(SessionState session)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold aqua]Install a Mod[/]").LeftJustified());
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[dim]A local folder or .zip, or a remote source:[/]");
        AnsiConsole.MarkupLine("[dim]  [aqua]gh:owner/repo/mod-id[/] (GitHub)   ·   [aqua]modio:game/mod-id[/] (mod.io)   ·   an [aqua]https://…/mod.zip[/] URL[/]");
        AnsiConsole.WriteLine();

        if (!AdvancedHelpers.TryPromptText("[bold]Mod source[/]:", out var sourceSpec, spec =>
            {
                if (File.Exists(spec) || Directory.Exists(spec)) return ValidationResult.Success();
                if (RemoteSourceParser.TryParse(spec, out _)) return ValidationResult.Success();
                return ValidationResult.Error($"'{spec}' is not a local path or a remote source (gh: / modio: / https://…zip)");
            }))
        {
            return;
        }

        var layout = session.GetLayout();

        // A remote spec is fetched into a temp dir first (shared with the
        // scripted `install` command via InstallSourceResolver); a local path
        // installs verbatim. Both then flow through the same ModInstaller pass.
        var installSource = sourceSpec;
        string? remoteProvenance = null;
        string? remoteTempDir = null;
        if (RemoteSourceParser.TryParse(sourceSpec, out _))
        {
            using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
            var state = new StoreStateReader().Read(layout);
            RemoteSourceResolution? resolution = null;
            AdvancedHelpers.Spin("Fetching remote source...",
                () => { resolution = InstallSourceResolver.ResolveRemote(sourceSpec, layout, http, state.AllowInsecureSources); });
            DiagnosticsRenderer.Render(resolution!.Diagnostics);

            if (resolution.MapTypeSkipped)
            {
                AnsiConsole.MarkupLine($"[yellow]mod.io '{Markup.Escape(resolution.MapModName ?? "?")}' is a map — handled in-game, nothing to install.[/]");
                return;
            }
            if (resolution.Aborted)
            {
                AnsiConsole.MarkupLine("[red]Fetch failed — nothing installed.[/] See diagnostics above.");
                return;
            }
            installSource = resolution.InstallSource!;
            remoteProvenance = resolution.RemoteProvenance;
            remoteTempDir = resolution.TempDir;
        }

        // The install pipeline runs three patcher-validation passes + an extract for zips —
        // long enough to be worth a spinner so the user knows something's happening.
        InstallResult? result = null;
        try
        {
            AdvancedHelpers.Spin("Validating and installing...",
                () => { result = new ModInstaller().Install(installSource, layout, remoteProvenance); });
        }
        finally
        {
            if (remoteTempDir is not null)
            {
                try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, true); } }
                catch { /* best-effort cleanup; ModInstaller already copied what it needs */ }
            }
        }

        AnsiConsole.WriteLine();
        DiagnosticsRenderer.Render(result!.Diagnostics);
        AnsiConsole.WriteLine();

        switch (result.Outcome)
        {
            case InstallOutcome.Installed:
                AnsiConsole.MarkupLine($"[bold green]Installed[/] [aqua]{Markup.Escape(result.ModId!)}[/]@[aqua]{Markup.Escape(result.Version!)}[/]");
                AnsiConsole.MarkupLine($"  [dim]-> {Markup.Escape(result.InstallPath!)}[/]");
                OfferDependencies(layout, result.InstallPath!, remoteProvenance);
                OfferEnable(layout, result.ModId!);
                break;

            case InstallOutcome.AlreadyInstalled:
                AnsiConsole.MarkupLine($"[bold yellow]Already installed[/] [aqua]{Markup.Escape(result.ModId!)}[/]@[aqua]{Markup.Escape(result.Version!)}[/]");
                AnsiConsole.MarkupLine($"  [dim](existing files preserved)[/]");
                OfferDependencies(layout, result.InstallPath!, remoteProvenance);
                OfferEnable(layout, result.ModId!);
                break;

            default:
                AnsiConsole.MarkupLine("[bold red]Install failed.[/] See diagnostics above.");
                break;
        }
    }

    // After an install, offer to pull the mod's missing dependencies (transitively) from the same
    // repo / subscribed catalogs. Shared with BrowseCatalogsWizard. Opt-in; advisory on failure.
    internal static void OfferDependencies(StoreLayout layout, string installPath, string? remoteProvenance)
    {
        var manifest = new PagoniaLand.Patcher.ManifestReader().ReadMod(installPath).Value?.Manifest;
        if (manifest?.Dependencies is not { Count: > 0 } dependencies)
        {
            return;
        }

        var installed = new HashSet<string>(new ModLister().List(layout).Select(m => m.Id), StringComparer.Ordinal);
        var missing = dependencies.Where(d => !installed.Contains(d)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        var plural = missing.Count == 1;
        if (!AdvancedHelpers.Confirm(
                $"This mod needs {missing.Count} dependenc{(plural ? "y" : "ies")} you don't have ([aqua]{Markup.Escape(string.Join(", ", missing))}[/]). Install {(plural ? "it" : "them")} now?",
                defaultValue: true))
        {
            AnsiConsole.MarkupLine("[dim]Skipped — they'll show as missing dependencies until installed.[/]");
            return;
        }

        GitHubSource? sameRepo = null;
        if (remoteProvenance is not null && RemoteSourceParser.TryParse(remoteProvenance, out var parsed) && parsed is GitHubSource gh)
        {
            sameRepo = gh;
        }

        var state = new StoreStateReader().Read(layout);
        var subscriptions = new CatalogSubscriptionService().List(layout);
        AssistedDependencyResult? depResult = null;
        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
        AdvancedHelpers.Spin("Resolving and installing dependencies...",
            () => { depResult = new AssistedDependencyInstaller(http, state.AllowInsecureSources).InstallMissing(layout, dependencies, sameRepo, subscriptions, state.CatalogMaxDepth); });

        DiagnosticsRenderer.Render(depResult!.Diagnostics);
        if (depResult.InstalledDependencies.Count > 0)
        {
            AnsiConsole.MarkupLine($"[green]Pulled[/] {depResult.InstalledDependencies.Count} dependenc{(depResult.InstalledDependencies.Count == 1 ? "y" : "ies")}: [aqua]{Markup.Escape(string.Join(", ", depResult.InstalledDependencies))}[/]");
        }
    }

    // Shared with BrowseCatalogsWizard, which installs a mod from the catalog
    // drill-in and offers the same enable-in-profile follow-up.
    internal static void OfferEnable(StoreLayout layout, string modId)
    {
        AnsiConsole.WriteLine();
        var enableNow = AdvancedHelpers.Confirm(
            $"Enable [aqua]{Markup.Escape(modId)}[/] in the active profile now?", defaultValue: true);

        if (!enableNow)
        {
            AnsiConsole.MarkupLine("[dim]Skipped. Use 'Manage active profile' -> Enable a mod later if you want.[/]");
            return;
        }

        var result = new ActiveProfileService().Enable(layout, modId, requestedVersion: null);
        DiagnosticsRenderer.Render(result.Diagnostics);

        if (!result.Success || result.Profile is null)
        {
            AnsiConsole.MarkupLine("[red]Enable failed.[/]");
            return;
        }

        // Skip the green "Enabled" confirmation on a no-op outcome (mod already
        // enabled) — the warning diagnostic is already rendered above; a green
        // confirmation right after it would contradict the user.
        if (result.Mutated)
        {
            AnsiConsole.MarkupLine($"[bold green]Enabled[/] in profile [aqua]{Markup.Escape(result.ProfileName ?? "?")}[/].");
        }
        AnsiConsole.MarkupLine($"  [dim]Load order: {Markup.Escape(string.Join(" -> ", result.Profile.LoadOrder))}[/]");
    }
}
