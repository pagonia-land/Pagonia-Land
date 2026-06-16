using PagoniaLand.Manager;
using Spectre.Console;

namespace PagoniaLand.Manager.Cli.Interactive;

internal static class AdvancedCollections
{
    public static void Run(SessionState session)
    {
        while (true)
        {
            var pick = AdvancedHelpers.NavSelect("[bold]Collections[/]", "install", "list", "show", "uninstall", "Back");

            var layout = session.GetLayout();

            switch (pick)
            {
                case "install":
                {
                    AdvancedHelpers.Header("Collections → install");
                    if (!AdvancedHelpers.TryPromptText("[bold]Collection source[/] [dim](.yaml file path or 'gh:owner/repo[[#ref]]/id')[/]:", out var from)) { break; }
                    string? modsRoot = null;
                    Dictionary<string, string>? remoteModSources = null;
                    string? remoteTempDir = null;

                    var isRemote = RemoteSourceParser.TryParse(from, out var parsed) && parsed is GitHubSource;
                    if (isRemote)
                    {
                        // Remote-source: fetch the collection + every referenced
                        // mod into a temp dir; that dir becomes our mods-root
                        // for the install pipeline below.
                        var gh = (GitHubSource)parsed!;
                        RemoteCollectionFetchResult? fetch = null;
                        AdvancedHelpers.Spin($"Fetching collection from gh:{gh.Owner}/{gh.Repo}...", () =>
                        {
                            using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
                            fetch = new RemoteFetcher(http).FetchCollection(gh);
                        });
                        DiagnosticsRenderer.Render(fetch!.Diagnostics);
                        if (!fetch.Success || fetch.TempDirectory is null || fetch.CollectionFilePath is null || fetch.ModsRoot is null)
                        {
                            Pause();
                            break;
                        }
                        remoteTempDir = fetch.TempDirectory;
                        from = fetch.CollectionFilePath;
                        modsRoot = fetch.ModsRoot;
                        remoteModSources = new Dictionary<string, string>(fetch.ModSources, StringComparer.Ordinal);
                    }
                    else
                    {
                        if (!AdvancedHelpers.TryPromptExistingPath("[bold]Mods root[/] [dim](folder containing the referenced mods)[/]:", out var mr, mustBeDirectory: true)) { break; }
                        modsRoot = mr;
                    }

                    // Profile prompts. For remote-source installs we default to
                    // "yes, activate" because the headline use case is "link
                    // from Discord -> playing the publisher's exact setup in
                    // one go". Local installs keep the older default of
                    // leaving the active selection alone.
                    var customProfile = AdvancedHelpers.Confirm("Use a custom profile name?", defaultValue: false);
                    string? profileName = null;
                    if (customProfile)
                    {
                        if (!AdvancedHelpers.TryPromptText("Profile name:", out var pn))
                        {
                            // Backing out after a remote fetch would otherwise leak the
                            // temp dir the install's finally normally cleans up.
                            if (remoteTempDir is not null)
                            {
                                try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, true); } }
                                catch { /* best-effort cleanup */ }
                            }
                            break;
                        }
                        profileName = pn;
                    }
                    var activate = AdvancedHelpers.Confirm(
                        "Activate the new profile after install? [dim](next plan / deploy targets it)[/]",
                        defaultValue: isRemote);
                    var overwrite = AdvancedHelpers.Confirm(
                        "If a profile with this name already exists, overwrite it?",
                        defaultValue: false);

                    CollectionInstallResult? r = null;
                    try
                    {
                        AdvancedHelpers.Spin("Resolving + installing...", () =>
                        {
                            r = new CollectionInstallService().InstallWithOptions(layout, from, modsRoot!, new CollectionInstallOptions
                            {
                                ProfileNameOverride = profileName,
                                Activate = activate,
                                Overwrite = overwrite,
                                RemoteModSources = remoteModSources,
                            });
                        });
                    }
                    finally
                    {
                        if (remoteTempDir is not null)
                        {
                            try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, true); } }
                            catch { /* best-effort cleanup */ }
                        }
                    }

                    DiagnosticsRenderer.Render(r!.Diagnostics);
                    AnsiConsole.MarkupLine($"[bold]Outcome:[/] {r.Outcome}");
                    if (r.Outcome == CollectionInstallOutcome.Installed)
                    {
                        AnsiConsole.MarkupLine($"  [aqua]{Markup.Escape(r.CollectionId!)}[/]@[aqua]{Markup.Escape(r.CollectionVersion!)}[/]");
                        AnsiConsole.MarkupLine($"  profile: [aqua]{Markup.Escape(r.ProfileName ?? "?")}[/]");
                        if (r.ProfileActivated)
                        {
                            AnsiConsole.MarkupLine("  [green]profile is now ACTIVE — next plan / deploy targets it.[/]");
                        }
                    }
                    Pause();
                    break;
                }
                case "list":
                {
                    AdvancedHelpers.Header("Collections → list");
                    var list = new CollectionLister().List(layout);
                    if (list.Count == 0) { AnsiConsole.MarkupLine("[dim](none)[/]"); Pause(); break; }
                    var t = new Table().Border(TableBorder.Rounded)
                        .AddColumn("Id").AddColumn("Version").AddColumn("Mods").AddColumn("Installed");
                    foreach (var c in list)
                        t.AddRow(
                            $"[aqua]{Markup.Escape(c.Id)}[/]",
                            Markup.Escape(c.Version),
                            c.ResolvedModCount.ToString(),
                            Markup.Escape(c.GeneratedAt ?? "(unknown)"));
                    AnsiConsole.Write(t);
                    Pause();
                    break;
                }
                case "show":
                {
                    AdvancedHelpers.Header("Collections → show");
                    var list = new CollectionLister().List(layout);
                    if (list.Count == 0) { AnsiConsole.MarkupLine("[dim](none)[/]"); Pause(); break; }
                    if (!AdvancedHelpers.TryPickOrCancel("Show: [dim](Esc to cancel)[/]", list.Select(c => c.Id), out var id)) break;
                    var c = list.First(x => x.Id == id);
                    AnsiConsole.MarkupLine($"[bold]{Markup.Escape(c.Id)}@{Markup.Escape(c.Version)}[/]");
                    if (!string.IsNullOrEmpty(c.Name)) AnsiConsole.MarkupLine($"  name: {Markup.Escape(c.Name)}");
                    if (!string.IsNullOrEmpty(c.Author)) AnsiConsole.MarkupLine($"  author: {Markup.Escape(c.Author)}");
                    AnsiConsole.MarkupLine($"  gdb version: {Markup.Escape(c.GameDatabaseVersion ?? "(unknown)")}");
                    if (!string.IsNullOrEmpty(c.Description)) AnsiConsole.MarkupLine($"  description: {Markup.Escape(c.Description)}");
                    AnsiConsole.MarkupLine($"  mods: {c.ResolvedModCount}");
                    AnsiConsole.MarkupLine($"  installed: {Markup.Escape(c.GeneratedAt ?? "(unknown)")}");
                    Pause();
                    break;
                }
                case "uninstall":
                {
                    AdvancedHelpers.Header("Collections → uninstall");
                    var list = new CollectionLister().List(layout);
                    if (list.Count == 0) { AnsiConsole.MarkupLine("[dim](none)[/]"); Pause(); break; }
                    if (!AdvancedHelpers.TryPickOrCancel("Uninstall: [dim](Esc to cancel)[/]", list.Select(c => c.Id), out var id)) break;
                    if (!AdvancedHelpers.Confirm($"Really uninstall [aqua]{Markup.Escape(id)}[/]? (mods + profile are kept)", defaultValue: false))
                    {
                        AnsiConsole.MarkupLine("[dim]Aborted.[/]"); Pause(); break;
                    }
                    var r = new CollectionUninstaller().Uninstall(layout, id);
                    DiagnosticsRenderer.Render(r.Diagnostics);
                    if (r.Outcome == CollectionUninstallOutcome.Removed)
                        AnsiConsole.MarkupLine($"[green]Removed[/] collection [aqua]{Markup.Escape(id)}[/]");
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
