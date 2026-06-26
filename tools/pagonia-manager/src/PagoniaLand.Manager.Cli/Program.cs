using PagoniaLand.Manager;
using PagoniaLand.Manager.Cli;
using PagoniaLand.Manager.Cli.Interactive;

// Force UTF-8 stdout so non-ASCII glyphs (Spectre tables/panels use box drawing
// + bullets like '•'; cp437/cp850 drop the bullet which leaves panel borders
// 2 cells short and visibly misaligned). Process-local, reverts on exit.
Console.OutputEncoding = System.Text.Encoding.UTF8;

// No args + an interactive terminal -> launch the wizard shell. With stdin
// redirected (CI pipes, `pagonia-manager </dev/null`, etc.) keep the
// scripted-friendly usage screen so automation doesn't hang on a prompt.
if (args.Length == 0)
{
    if (Console.IsInputRedirected)
    {
        PrintUsage();
        return ManagerExitCodes.Usage;
    }

    return InteractiveShell.Run();
}

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine($"{ManagerInfo.ProductName} {ManagerInfo.Version}");
    return ManagerExitCodes.Success;
}

if (args is ["--info"])
{
    Console.WriteLine($"{ManagerInfo.ProductName} {ManagerInfo.Version}");
    Console.WriteLine($"  patcher core: {BackingCoreInfo.PatcherProductName} {BackingCoreInfo.PatcherVersion}");
    Console.WriteLine($"  paker core:   {BackingCoreInfo.PakerProductName} {BackingCoreInfo.PakerVersion}");
    return ManagerExitCodes.Success;
}

if (args.Length >= 2 && args[0] == "store" && args[1] == "init")
{
    return RunStoreInit(args[2..]);
}

if (args.Length >= 2 && args[0] == "store" && args[1] == "info")
{
    return RunStoreInfo(args[2..]);
}

if (args.Length >= 1 && args[0] == "install")
{
    return RunInstall(args[1..]);
}

if (args.Length >= 1 && args[0] == "uninstall")
{
    return RunUninstall(args[1..]);
}

if (args.Length >= 1 && args[0] == "list")
{
    return RunList(args[1..]);
}

if (args.Length >= 1 && args[0] == "enable")
{
    return RunEnable(args[1..]);
}

if (args.Length >= 1 && args[0] == "disable")
{
    return RunDisable(args[1..]);
}

if (args.Length >= 1 && args[0] == "move")
{
    return RunMove(args[1..]);
}

if (args.Length >= 1 && args[0] == "status")
{
    return RunStatus(args[1..]);
}

if (args.Length >= 1 && args[0] == "doctor")
{
    return RunDoctor(args[1..]);
}

if (args.Length >= 1 && args[0] == "outdated")
{
    return RunOutdated(args[1..]);
}

if (args.Length >= 1 && args[0] == "update")
{
    return RunUpdate(args[1..]);
}

if (args.Length >= 1 && args[0] == "plan")
{
    return RunPlan(args[1..]);
}

if (args.Length >= 1 && args[0] == "deploy")
{
    return RunDeploy(args[1..]);
}

if (args.Length >= 1 && args[0] == "rollback")
{
    return RunRollback(args[1..]);
}

if (args.Length >= 1 && args[0] == "deploy-status")
{
    return RunDeployStatus(args[1..]);
}

if (args.Length >= 1 && args[0] == "deploy-list")
{
    return RunDeployList(args[1..]);
}

if (args.Length >= 1 && args[0] == "schema-validate")
{
    return RunSchemaValidate(args[1..]);
}

if (args.Length >= 2 && args[0] == "profile")
{
    return args[1] switch
    {
        "create" => RunProfileCreate(args[2..]),
        "list" => RunProfileList(args[2..]),
        "use" => RunProfileUse(args[2..]),
        "copy" => RunProfileCopy(args[2..]),
        "export" => RunProfileExport(args[2..]),
        "delete" => RunProfileDelete(args[2..]),
        "show" => RunProfileShow(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

if (args.Length >= 2 && args[0] == "deploys")
{
    return args[1] switch
    {
        "list-orphans" => RunDeploysListOrphans(args[2..]),
        "clean" => RunDeploysClean(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

if (args.Length >= 2 && args[0] == "collection")
{
    return args[1] switch
    {
        "install" => RunCollectionInstall(args[2..]),
        "list" => RunCollectionList(args[2..]),
        "show" => RunCollectionShow(args[2..]),
        "uninstall" => RunCollectionUninstall(args[2..]),
        "update" => RunCollectionUpdate(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

if (args.Length >= 2 && args[0] == "tweak")
{
    return args[1] switch
    {
        "list" => RunTweakList(args[2..]),
        "set" => RunTweakSet(args[2..]),
        "reset" => RunTweakReset(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

if (args.Length >= 2 && args[0] == "expansions")
{
    return args[1] switch
    {
        "list" => RunExpansionsList(args[2..]),
        "set" => RunExpansionsSet(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

if (args.Length >= 2 && args[0] == "catalog")
{
    return args[1] switch
    {
        "list" => RunCatalogList(args[2..]),
        "add" => RunCatalogAdd(args[2..]),
        "remove" => RunCatalogRemove(args[2..]),
        "browse" => RunCatalogBrowse(args[2..]),
        "show" => RunCatalogShow(args[2..]),
        "refresh" => RunCatalogRefresh(args[2..]),
        _ => PrintUsageAndFail(),
    };
}

PrintUsage();
return ManagerExitCodes.Usage;

static int PrintUsageAndFail()
{
    PrintUsage();
    return ManagerExitCodes.Usage;
}

static int RunStoreInit(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager store init [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);

    try
    {
        var result = new StoreInitializer().Initialize(layout, seedDefaultCatalog: true);
        Console.WriteLine($"Store root:       {result.Root}");
        Console.WriteLine($"  source:         {DescribeSource(resolution.Source)}");
        Console.WriteLine($"  store version:  {result.StoreVersion}");
        Console.WriteLine($"  state.yaml:     {(result.CreatedState ? "created" : "exists")}");
        Console.WriteLine($"  default profile:{(result.CreatedDefaultProfile ? " created" : " exists")}");
        Console.WriteLine($"  new directories: {result.CreatedDirectories.Count}");
        if (result.SeededDefaultCatalog)
        {
            Console.WriteLine($"  [{ManagerDiagnosticCodes.DefaultCatalogSeeded}] subscribed to the official catalog: {CatalogConstants.OfficialCatalogSource}");
            Console.WriteLine($"    browse it with 'pagonia-manager catalog browse'; opt out with 'pagonia-manager catalog remove {CatalogConstants.OfficialCatalogSource}'");
        }
        return ManagerExitCodes.Success;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"store init failed: {ex.Message}");
        return ManagerExitCodes.Error;
    }
}

static int RunStoreInfo(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager store info [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);

    try
    {
        var info = new StoreInspector().Inspect(layout);
        Console.WriteLine($"Store root:    {info.Root}");
        Console.WriteLine($"  source:      {DescribeSource(resolution.Source)}");
        if (!info.Initialised)
        {
            Console.WriteLine("  status:      not initialised (run 'pagonia-manager store init')");
            return ManagerExitCodes.Success;
        }

        Console.WriteLine($"  status:      initialised");
        Console.WriteLine($"  store ver:   {info.StoreVersion}");
        Console.WriteLine($"  active prof: {info.ActiveProfile ?? "(none)"}");
        Console.WriteLine($"  mods:        {info.InstalledModCount}");
        Console.WriteLine($"  profiles:    {info.ProfileCount}");
        Console.WriteLine($"  collections: {info.CollectionCount}");
        return ManagerExitCodes.Success;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"store info failed: {ex.Message}");
        return ManagerExitCodes.Error;
    }
}

static int RunInstall(string[] tail)
{
    string? sourcePath = null;
    string? storePath = null;
    string? jsonPath = null;
    var withDeps = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--from" && i + 1 < tail.Length) { sourcePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--with-deps") { withDeps = true; i += 1; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager install --from <folder|zip|gh:owner/repo[#ref]/mod-id|https://.../mod.zip|modio:<game>/<mod-id>[#<version>]> [--with-deps] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(sourcePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager install --from <folder|zip|gh:owner/repo[#ref]/mod-id|https://.../mod.zip|modio:<game>/<mod-id>[#<version>]> [--with-deps] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    // Detect remote-source specs (gh:... / https://github.com/.../tree/... /
    // any https://...zip or http://...zip). Remote: fetch into a temp dir,
    // then install from that dir; the ResolvedSource string lands in the
    // sidecar's `source` field so `pagonia-manager list` shows provenance.
    string? remoteSource = null;
    string installSource = sourcePath;
    string? remoteTempDir = null;

    if (RemoteSourceParser.TryParse(sourcePath, out _))
    {
        using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
        var state = new StoreStateReader().Read(layout);
        var resolution = InstallSourceResolver.ResolveRemote(sourcePath, layout, http, state.AllowInsecureSources)!;
        PrintDiagnostics(resolution.Diagnostics);

        if (resolution.MapTypeSkipped)
        {
            // mod.io Map-type: clean success exit — maps are handled in-game.
            Console.WriteLine($"mod.io mod '{resolution.MapModName}' is a map — handled in-game, not by 'install'. No files were downloaded.");
            return ManagerExitCodes.Success;
        }
        if (resolution.Aborted)
        {
            return ManagerExitCodes.Error;
        }

        installSource = resolution.InstallSource!;
        remoteSource = resolution.RemoteProvenance;
        remoteTempDir = resolution.TempDir;
    }

    InstallResult result;
    try
    {
        result = new ModInstaller().Install(installSource, layout, remoteSource);
    }
    finally
    {
        // Best-effort cleanup of the remote temp dir regardless of install
        // outcome — ModInstaller has already copied the bytes it needs into
        // <store>/mods/<id>/<version>/, so the temp tree is no longer load-bearing.
        if (remoteTempDir is not null)
        {
            try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, recursive: true); } }
            catch { /* swallowed: cleanup failure doesn't affect the install */ }
        }
    }

    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
        Console.WriteLine($"JSON report: {jsonPath}");
    }

    // --with-deps: after a successful install, pull the mod's missing dependencies (transitively)
    // from the same repo, then subscribed catalogs. Advisory — failures warn, the install stands.
    if (withDeps
        && (result.Outcome == InstallOutcome.Installed || result.Outcome == InstallOutcome.AlreadyInstalled)
        && result.InstallPath is not null)
    {
        PullDependencies(layout, result.InstallPath, remoteSource);
    }

    switch (result.Outcome)
    {
        case InstallOutcome.Installed:
            Console.WriteLine($"Installed {result.ModId}@{result.Version} -> {result.InstallPath}");
            return ManagerExitCodes.Success;
        case InstallOutcome.AlreadyInstalled:
            Console.WriteLine($"Already installed: {result.ModId}@{result.Version} at {result.InstallPath}");
            return ManagerExitCodes.Success;
        default:
            return ManagerExitCodes.Error;
    }
}

// Pull a just-installed mod's missing dependencies (transitively) — same repo first, then subscribed
// catalogs. Advisory: any failure is a warning, never fails the original install.
static void PullDependencies(StoreLayout layout, string installPath, string? remoteSource)
{
    var manifest = new PagoniaLand.Patcher.ManifestReader().ReadMod(installPath).Value?.Manifest;
    if (manifest?.Dependencies is not { Count: > 0 } dependencies)
    {
        return;
    }

    GitHubSource? sameRepo = null;
    if (remoteSource is not null && RemoteSourceParser.TryParse(remoteSource, out var parsed) && parsed is GitHubSource gh)
    {
        sameRepo = gh;
    }

    var state = new StoreStateReader().Read(layout);
    var subscriptions = new CatalogSubscriptionService().List(layout);

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var depResult = new AssistedDependencyInstaller(http, state.AllowInsecureSources)
        .InstallMissing(layout, dependencies, sameRepo, subscriptions, state.CatalogMaxDepth);

    PrintDiagnostics(depResult.Diagnostics);
    if (depResult.InstalledDependencies.Count > 0)
    {
        Console.WriteLine($"Pulled {depResult.InstalledDependencies.Count} dependenc{(depResult.InstalledDependencies.Count == 1 ? "y" : "ies")}: {string.Join(", ", depResult.InstalledDependencies)}");
    }
}

static int RunUninstall(string[] tail)
{
    string? modId = null;
    string? version = null;
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--version" && i + 1 < tail.Length) { version = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (modId is null && !tail[i].StartsWith("-")) { modId = tail[i]; i++; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager uninstall <mod-id> [--version <v>] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(modId))
    {
        Console.Error.WriteLine("Usage: pagonia-manager uninstall <mod-id> [--version <v>] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    var result = new ModUninstaller().Uninstall(modId, version, layout);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
        Console.WriteLine($"JSON report: {jsonPath}");
    }

    if (result.Outcome == UninstallOutcome.Removed)
    {
        Console.WriteLine($"Removed {result.ModId}@{result.Version} from {result.RemovedPath}");
        if (result.ParentDirectoryPruned)
        {
            Console.WriteLine($"  parent directory pruned (no other versions of {result.ModId} remain)");
        }
        return ManagerExitCodes.Success;
    }

    return ManagerExitCodes.Error;
}

static int RunList(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager list [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    var mods = new ModLister().List(layout);
    Console.WriteLine($"Store root:    {layout.Root}");
    Console.WriteLine($"  source:      {DescribeSource(resolution.Source)}");
    Console.WriteLine($"  installed:   {mods.Count}");
    Console.WriteLine();

    if (mods.Count == 0)
    {
        Console.WriteLine("  (none)");
        return ManagerExitCodes.Success;
    }

    foreach (var mod in mods)
    {
        Console.WriteLine($"  {mod.Id}@{mod.Version}");
        if (!string.IsNullOrEmpty(mod.ManifestName))
        {
            Console.WriteLine($"      name:        {mod.ManifestName}");
        }

        Console.WriteLine($"      installed:   {mod.InstalledAt ?? "(unknown)"}");
        Console.WriteLine($"      source:      {mod.SourcePath ?? "(unknown)"} ({mod.SourceType ?? "?"})");
        Console.WriteLine($"      path:        {mod.InstallPath}");
    }

    return ManagerExitCodes.Success;
}

static int RunEnable(string[] tail)
{
    string? modId = null;
    string? version = null;
    string? storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--version" && i + 1 < tail.Length)
        {
            version = tail[i + 1];
            i += 2;
        }
        else if (tail[i] == "--store" && i + 1 < tail.Length)
        {
            storePath = tail[i + 1];
            i += 2;
        }
        else if (modId is null && !tail[i].StartsWith("-"))
        {
            modId = tail[i];
            i++;
        }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager enable <mod-id> [--version <v>] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(modId))
    {
        Console.Error.WriteLine("Usage: pagonia-manager enable <mod-id> [--version <v>] [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ActiveProfileService().Enable(layout, modId, version);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    var enabled = result.Profile!.EnabledMods.FirstOrDefault(mod =>
        string.Equals(mod.Id, modId, StringComparison.Ordinal));
    if (enabled is not null)
    {
        // Skip the "Enabled X" confirmation on a no-op outcome — the mutator already
        // emitted a warning explaining why nothing changed (e.g. modAlreadyEnabled),
        // and printing "Enabled X" right after that warning was contradictory.
        if (result.Mutated)
        {
            Console.WriteLine($"Enabled {enabled.Id}@{enabled.Version} in profile '{result.ProfileName}'.");
        }
        Console.WriteLine($"Load order: {string.Join(" -> ", result.Profile!.LoadOrder)}");
    }

    return ManagerExitCodes.Success;
}

static int RunDisable(string[] tail)
{
    string? modId = null;
    string? storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length)
        {
            storePath = tail[i + 1];
            i += 2;
        }
        else if (modId is null && !tail[i].StartsWith("-"))
        {
            modId = tail[i];
            i++;
        }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager disable <mod-id> [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(modId))
    {
        Console.Error.WriteLine("Usage: pagonia-manager disable <mod-id> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ActiveProfileService().Disable(layout, modId);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    // Skip the "Disabled X" confirmation on a no-op outcome — the mutator already
    // emitted a warning explaining why (e.g. modNotEnabled).
    if (result.Mutated)
    {
        Console.WriteLine($"Disabled '{modId}' in profile '{result.ProfileName}'.");
    }
    Console.WriteLine($"Load order: {(result.Profile!.LoadOrder.Count == 0 ? "(empty)" : string.Join(" -> ", result.Profile!.LoadOrder))}");
    return ManagerExitCodes.Success;
}

static int RunMove(string[] tail)
{
    string? modId = null;
    int? position = null;
    string? before = null;
    string? after = null;
    string? storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--position" && i + 1 < tail.Length && int.TryParse(tail[i + 1], out var parsed))
        {
            position = parsed;
            i += 2;
        }
        else if (tail[i] == "--before" && i + 1 < tail.Length)
        {
            before = tail[i + 1];
            i += 2;
        }
        else if (tail[i] == "--after" && i + 1 < tail.Length)
        {
            after = tail[i + 1];
            i += 2;
        }
        else if (tail[i] == "--store" && i + 1 < tail.Length)
        {
            storePath = tail[i + 1];
            i += 2;
        }
        else if (modId is null && !tail[i].StartsWith("-"))
        {
            modId = tail[i];
            i++;
        }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager move <mod-id> (--position <n>|--before <id>|--after <id>) [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    var anchorChoices = new[] { position is not null, before is not null, after is not null }.Count(x => x);
    if (string.IsNullOrWhiteSpace(modId) || anchorChoices != 1)
    {
        Console.Error.WriteLine("Usage: pagonia-manager move <mod-id> (--position <n>|--before <id>|--after <id>) [--store <path>]");
        Console.Error.WriteLine("       exactly one of --position, --before, --after is required");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var service = new ActiveProfileService();
    ActiveProfileResult result = position is not null
        ? service.MoveToPosition(layout, modId, position.Value)
        : before is not null
            ? service.MoveBefore(layout, modId, before)
            : service.MoveAfter(layout, modId, after!);

    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
    {
        return ManagerExitCodes.Error;
    }

    // Skip the "Moved X" confirmation on a no-op outcome (the mod was already at the
    // requested position). The defensive Error-diagnostic check above used to be the
    // only signal that something might have gone wrong; result.Mutated is the cleaner
    // gate now that ActiveProfileResult propagates it.
    if (result.Mutated)
    {
        Console.WriteLine($"Moved '{modId}' in profile '{result.ProfileName}'.");
    }
    Console.WriteLine($"Load order: {string.Join(" -> ", result.Profile!.LoadOrder)}");
    return ManagerExitCodes.Success;
}

static int RunStatus(string[] tail)
{
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager status [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    var result = new ActiveProfileService().Show(layout);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
    }

    Console.WriteLine($"Store root:    {layout.Root}");
    Console.WriteLine($"  source:      {DescribeSource(resolution.Source)}");

    if (!result.Success || result.Profile is null)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"  profile:     {result.ProfileName}");
    Console.WriteLine($"  enabled:     {result.Profile.EnabledMods.Count}");
    Console.WriteLine();

    if (result.Profile.LoadOrder.Count == 0)
    {
        Console.WriteLine("  (no enabled mods)");
        return ManagerExitCodes.Success;
    }

    var index = 1;
    foreach (var modId in result.Profile.LoadOrder)
    {
        var enabled = result.Profile.EnabledMods.FirstOrDefault(mod =>
            string.Equals(mod.Id, modId, StringComparison.Ordinal));
        var version = enabled?.Version ?? "(missing version)";
        Console.WriteLine($"  {index,2}. {modId}@{version}");
        index++;
    }

    return ManagerExitCodes.Success;
}

static int RunTweakList(string[] tail)
{
    string? modId = null;
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (modId is null && !tail[i].StartsWith("-")) { modId = tail[i]; i++; }
        else { return TweakListUsage(); }
    }

    if (string.IsNullOrWhiteSpace(modId))
    {
        return TweakListUsage();
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new TweakOverrideService().Read(layout, profileName, modId);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToTweakListJson(result));
    }

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Tweaks for '{result.ModId}'@{result.ModVersion} in profile '{result.ProfileName}':");
    if (result.Tweaks.Count == 0)
    {
        Console.WriteLine("  (this mod declares no tweaks)");
        return ManagerExitCodes.Success;
    }

    foreach (var tweak in result.Tweaks)
    {
        var d = tweak.Declaration;
        Console.WriteLine($"  {d.Id} = {tweak.Value}  [{tweak.Origin}]");
        Console.WriteLine($"      type: {d.Type}{DescribeTweakDomain(d)}, default: {d.Default}");
        if (!string.IsNullOrWhiteSpace(d.Label))
        {
            Console.WriteLine($"      {d.Label}");
        }
    }
    return ManagerExitCodes.Success;
}

static int RunTweakSet(string[] tail)
{
    var positionals = new List<string>();
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        // Permissive positional capture: a tweak value may legitimately start with
        // '-' (a negative number), so anything that isn't a recognised flag is a
        // positional. modId / tweakId / value, in that order.
        else { positionals.Add(tail[i]); i++; }
    }

    if (positionals.Count != 3)
    {
        Console.Error.WriteLine("Usage: pagonia-manager tweak set <mod-id> <tweak-id> <value> [--profile <name>] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var (modId, tweakId, value) = (positionals[0], positionals[1], positionals[2]);
    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new TweakOverrideService().Set(layout, profileName, modId, tweakId, value);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToTweakSetJson(result, tweakId, value));
    }

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Set '{modId}:{tweakId}' = '{value}' in profile '{result.ProfileName}'.");
    return ManagerExitCodes.Success;
}

static int RunTweakReset(string[] tail)
{
    var positionals = new List<string>();
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (!tail[i].StartsWith("-")) { positionals.Add(tail[i]); i++; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager tweak reset <mod-id> [<tweak-id>] [--profile <name>] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (positionals.Count is < 1 or > 2)
    {
        Console.Error.WriteLine("Usage: pagonia-manager tweak reset <mod-id> [<tweak-id>] [--profile <name>] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var modId = positionals[0];
    var tweakId = positionals.Count == 2 ? positionals[1] : null;
    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new TweakOverrideService().Reset(layout, profileName, modId, tweakId);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToTweakResetJson(result, tweakId));
    }

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    var scope = tweakId is null ? "all tweaks" : $"'{tweakId}'";
    Console.WriteLine(result.Mutated
        ? $"Reset {scope} for '{modId}' in profile '{result.ProfileName}'."
        : $"No stored override to reset for {scope} on '{modId}' in profile '{result.ProfileName}'.");
    return ManagerExitCodes.Success;
}

static int TweakListUsage()
{
    Console.Error.WriteLine("Usage: pagonia-manager tweak list <mod-id> [--profile <name>] [--store <path>] [--json <out>]");
    return ManagerExitCodes.Usage;
}

static int RunExpansionsList(string[] tail)
{
    string? gameRoot = null;
    string? storePath = null;
    string? jsonPath = null;
    var overrides = new Dictionary<string, OwnershipState>(StringComparer.OrdinalIgnoreCase);
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--assume-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.Owned)) return ManagerExitCodes.Usage; i += 2; }
        else if (tail[i] == "--assume-not-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.NotOwned)) return ManagerExitCodes.Usage; i += 2; }
        else { return ExpansionsListUsage(); }
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!TryResolveExpansionsGameRoot(layout, ref gameRoot)) return ManagerExitCodes.Error;

    var result = new ExpansionOwnershipService().List(layout, gameRoot!, overrides);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToExpansionsListJson(result));
    }

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Game root:   {result.GameRoot}");
    Console.WriteLine($"Fingerprint: {result.GameFingerprint}");
    Console.WriteLine();
    Console.WriteLine($"  {"Package",-14}{"Present",-10}{"Owned",-12}{"Effective"}");
    foreach (var e in result.Expansions)
    {
        var owned = e.Ownership switch
        {
            OwnershipState.Owned => "owned",
            OwnershipState.NotOwned => "not-owned",
            _ => "unknown",
        };
        // core/tools are always owned; flag them so the table reads honestly.
        if (ExpansionPackages.IsAlwaysOwned(e.Package)) { owned = "owned*"; }
        Console.WriteLine($"  {e.Package,-14}{(e.Present ? "yes" : "no"),-10}{owned,-12}{(e.Effective ? "yes" : "no")}");
    }
    if (result.Expansions.Any(e => ExpansionPackages.IsAlwaysOwned(e.Package)))
    {
        Console.WriteLine();
        Console.WriteLine("  * core / tools are base game + editor data — always owned.");
        Console.WriteLine("    Declare DLC ownership with 'pagonia-manager expansions set <decorations1|dlc1> <owned|not-owned>'.");
    }
    return ManagerExitCodes.Success;
}

static int RunExpansionsSet(string[] tail)
{
    var positionals = new List<string>();
    string? gameRoot = null;
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (!tail[i].StartsWith("-")) { positionals.Add(tail[i]); i++; }
        else { return ExpansionsSetUsage(); }
    }

    if (positionals.Count != 2)
    {
        return ExpansionsSetUsage();
    }

    var package = positionals[0];
    if (!TryParseOwnershipState(positionals[1], out var state))
    {
        Console.Error.WriteLine($"Unknown ownership '{positionals[1]}'. Use one of: owned, not-owned, unknown.");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!TryResolveExpansionsGameRoot(layout, ref gameRoot)) return ManagerExitCodes.Error;

    var result = new ExpansionOwnershipService().Set(layout, gameRoot!, package, state);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToExpansionsSetJson(result));
    }

    return result.Success ? ManagerExitCodes.Success : ManagerExitCodes.Error;
}

static int ExpansionsListUsage()
{
    Console.Error.WriteLine("Usage: pagonia-manager expansions list [--game <path>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <path>] [--json <out>]");
    return ManagerExitCodes.Usage;
}

static int ExpansionsSetUsage()
{
    Console.Error.WriteLine("Usage: pagonia-manager expansions set <decorations1|dlc1> <owned|not-owned|unknown> [--game <path>] [--store <path>] [--json <out>]");
    return ManagerExitCodes.Usage;
}

// Default --game to the active / stored / platform install when not passed
// explicitly (mirrors the plan/deploy wizards). Returns false (after printing a
// gameRootMissing error) when nothing resolves.
static bool TryResolveExpansionsGameRoot(StoreLayout layout, ref string? gameRoot)
{
    if (!string.IsNullOrWhiteSpace(gameRoot)) return true;

    var resolved = GameRootResolver.Resolve(layout, null);
    if (resolved.HasPath)
    {
        gameRoot = resolved.Path;
        return true;
    }

    Console.Error.WriteLine(
        $"[{ManagerDiagnosticCodes.GameRootMissing}] No game install resolved. Pass --game <path> or set a default with the Plan + Deploy wizard.");
    return false;
}

// Parse a single --assume-owned/--assume-not-owned package argument into the
// override map. Rejects non-declarable packages (core/tools are always owned, so
// overriding them is meaningless) with a usage message.
static bool TryAddOverride(Dictionary<string, OwnershipState> overrides, string package, OwnershipState state)
{
    if (!ExpansionPackages.IsDeclarable(package))
    {
        Console.Error.WriteLine(
            $"--assume-* only accepts a declarable expansion ({string.Join(" / ", ExpansionPackages.Declarable)}); got '{package}'.");
        return false;
    }
    overrides[package] = state;
    return true;
}

static bool TryParseOwnershipState(string value, out OwnershipState state)
{
    switch (value.ToLowerInvariant())
    {
        case "owned": state = OwnershipState.Owned; return true;
        case "not-owned": state = OwnershipState.NotOwned; return true;
        case "unknown": state = OwnershipState.Unknown; return true;
        default: state = OwnershipState.Unknown; return false;
    }
}

// Render a tweak's value domain for the human-readable `tweak list`: a numeric
// range, or an enum's allowed values. Empty for a plain boolean.
static string DescribeTweakDomain(PagoniaLand.Patcher.TweakDeclaration declaration)
{
    if (declaration.Type == "enum")
    {
        return declaration.Values.Count == 0
            ? string.Empty
            : $" (one of: {string.Join(", ", declaration.Values.Select(v => v.Value))})";
    }

    if (declaration.Type is "number" or "integer" && (declaration.Min is not null || declaration.Max is not null))
    {
        var min = declaration.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-∞";
        var max = declaration.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "+∞";
        return $" (range {min}..{max})";
    }

    return string.Empty;
}

static int RunProfileCreate(string[] tail)
{
    if (!TryParseNamedWithStore(tail, out var profileName, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile create <name> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileLifecycleService().Create(layout, profileName!);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Created profile '{result.ProfileName}' at {layout.ProfileFile(result.ProfileName!)}");
    return ManagerExitCodes.Success;
}

static int RunProfileList(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile list [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    var result = new ProfileLifecycleService().List(layout);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Store root:    {layout.Root}");
    Console.WriteLine($"  source:      {DescribeSource(resolution.Source)}");
    Console.WriteLine($"  active:      {result.ActiveProfile}");
    Console.WriteLine($"  profiles:    {result.Profiles.Count}");
    Console.WriteLine();

    foreach (var summary in result.Profiles)
    {
        var marker = summary.IsActive ? "*" : " ";
        var defaultMarker = summary.IsDefault ? " (default)" : string.Empty;
        var collectionMarker = string.IsNullOrEmpty(summary.Collection)
            ? string.Empty
            : $" [collection: {summary.Collection}]";
        Console.WriteLine($"  {marker} {summary.Name}{defaultMarker} - {summary.EnabledModCount} enabled{collectionMarker}");
    }

    return ManagerExitCodes.Success;
}

static int RunProfileUse(string[] tail)
{
    if (!TryParseNamedWithStore(tail, out var profileName, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile use <name> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileLifecycleService().Use(layout, profileName!);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Active profile set to '{result.ProfileName}'.");
    return ManagerExitCodes.Success;
}

static int RunProfileCopy(string[] tail)
{
    string? source = null;
    string? target = null;
    string? storePath = null;
    var activate = false;
    var i = 0;
    var ok = true;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--activate") { activate = true; i++; }
        else if (source is null && !tail[i].StartsWith('-')) { source = tail[i]; i++; }
        else if (target is null && !tail[i].StartsWith('-')) { target = tail[i]; i++; }
        else { ok = false; break; }
    }

    if (!ok || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile copy <source> <target> [--activate] [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileLifecycleService().Copy(layout, source!, target!, activate);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Copied profile to '{result.ProfileName}'{(activate ? " (now active)" : string.Empty)}.");
    return ManagerExitCodes.Success;
}

static int RunProfileExport(string[] tail)
{
    string? profileName = null;
    string? outPath = null;
    string? id = null;
    string? displayName = null;
    string? version = null;
    string? storePath = null;
    var i = 0;
    var ok = true;
    while (i < tail.Length)
    {
        switch (tail[i])
        {
            case "--out" when i + 1 < tail.Length: outPath = tail[i + 1]; i += 2; break;
            case "--id" when i + 1 < tail.Length: id = tail[i + 1]; i += 2; break;
            case "--name" when i + 1 < tail.Length: displayName = tail[i + 1]; i += 2; break;
            case "--version" when i + 1 < tail.Length: version = tail[i + 1]; i += 2; break;
            case "--store" when i + 1 < tail.Length: storePath = tail[i + 1]; i += 2; break;
            default:
                if (profileName is null && !tail[i].StartsWith('-')) { profileName = tail[i]; i++; }
                else { ok = false; }
                break;
        }

        if (!ok) break;
    }

    if (!ok || string.IsNullOrWhiteSpace(outPath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile export [<name>] --out <file.collection.yaml> [--id <id>] [--name <display-name>] [--version <v>] [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileExportService().Export(
        layout,
        profileName,
        outPath!,
        new ProfileExportOptions { Id = id, Name = displayName, Version = version });
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Exported profile '{result.ProfileName}' to '{result.OutputPath}' (collection '{result.CollectionId}', {result.ModCount} mod(s)).");
    return ManagerExitCodes.Success;
}

static int RunProfileDelete(string[] tail)
{
    if (!TryParseNamedWithStore(tail, out var profileName, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager profile delete <name> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileLifecycleService().Delete(layout, profileName!);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Deleted profile '{result.ProfileName}'.");
    return ManagerExitCodes.Success;
}

static int RunProfileShow(string[] tail)
{
    string? profileName = null;
    string? storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length)
        {
            storePath = tail[i + 1];
            i += 2;
        }
        else if (profileName is null && !tail[i].StartsWith("-"))
        {
            profileName = tail[i];
            i++;
        }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager profile show [<name>] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new ProfileLifecycleService().Show(layout, profileName);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Profile is null)
    {
        return ManagerExitCodes.Error;
    }

    var profile = result.Profile;
    Console.WriteLine($"Profile:       {profile.Name}");
    Console.WriteLine($"  version:     {profile.ProfileVersion}");
    Console.WriteLine($"  file:        {layout.ProfileFile(profile.Name)}");
    if (!string.IsNullOrEmpty(profile.Collection))
    {
        Console.WriteLine($"  collection:  {profile.Collection}");
    }

    Console.WriteLine($"  enabled:     {profile.EnabledMods.Count}");
    Console.WriteLine();

    if (profile.LoadOrder.Count == 0)
    {
        Console.WriteLine("  (no enabled mods)");
        return ManagerExitCodes.Success;
    }

    var index = 1;
    foreach (var modId in profile.LoadOrder)
    {
        var enabled = profile.EnabledMods.FirstOrDefault(mod =>
            string.Equals(mod.Id, modId, StringComparison.Ordinal));
        var version = enabled?.Version ?? "(missing version)";
        Console.WriteLine($"  {index,2}. {modId}@{version}");
        index++;
    }

    return ManagerExitCodes.Success;
}

static int RunOutdated(string[] tail)
{
    string? storePath = null;
    string? jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager outdated [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var result = new UpdateDetectionService(http).Check(layout);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
    }

    Console.WriteLine($"pagonia-manager outdated — store {layout.Root}");
    Console.WriteLine();

    if (result.Updates.Count == 0)
    {
        Console.WriteLine($"All {result.CheckedCount} checkable mod(s) are up to date.");
    }
    else
    {
        Console.WriteLine($"{result.Updates.Count} mod update(s) available:");
        foreach (var update in result.Updates)
        {
            var gdb = string.IsNullOrWhiteSpace(update.GameDatabaseVersion) ? string.Empty : $" (gameDatabaseVersion {update.GameDatabaseVersion})";
            Console.WriteLine($"  {update.Id}: {update.InstalledVersion} -> {update.AvailableVersion}{gdb}");
        }
    }

    if (result.SkippedLocalCount > 0)
    {
        Console.WriteLine($"  ({result.SkippedLocalCount} local/non-remote mod(s) skipped — nothing to check against.)");
    }

    // Same-version content drift — the source re-published an identical version with changed content.
    if (result.ContentDrifts.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"{result.ContentDrifts.Count} mod(s) changed content at the same version (re-install to refresh):");
        foreach (var drift in result.ContentDrifts)
        {
            Console.WriteLine($"  {drift.Id}: {drift.Version} (content changed)");
        }
    }

    // Collections half — same shape, only shown when the store has any (checked or skipped),
    // so a mod-only store reads exactly as before.
    if (result.CheckedCollectionCount > 0 || result.SkippedLocalCollectionCount > 0)
    {
        Console.WriteLine();
        if (result.CollectionUpdates.Count == 0)
        {
            Console.WriteLine($"All {result.CheckedCollectionCount} checkable collection(s) are up to date.");
        }
        else
        {
            Console.WriteLine($"{result.CollectionUpdates.Count} collection update(s) available:");
            foreach (var update in result.CollectionUpdates)
            {
                var gdb = string.IsNullOrWhiteSpace(update.GameDatabaseVersion) ? string.Empty : $" (gameDatabaseVersion {update.GameDatabaseVersion})";
                Console.WriteLine($"  {update.Id}: {update.InstalledVersion} -> {update.AvailableVersion}{gdb}");
            }
        }

        if (result.SkippedLocalCollectionCount > 0)
        {
            Console.WriteLine($"  ({result.SkippedLocalCollectionCount} local/non-remote collection(s) skipped — nothing to check against.)");
        }
    }

    // Updates are info-level; only the per-item check failures (warnings) need surfacing here.
    PrintDiagnostics(result.Diagnostics.Where(d => d.Severity != ManagerDiagnosticSeverity.Info).ToList());
    return ManagerExitCodes.Success;
}

static int RunUpdate(string[] tail)
{
    string? modId = null;
    string? storePath = null;
    string? profileName = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (!tail[i].StartsWith("--", StringComparison.Ordinal) && modId is null) { modId = tail[i]; i += 1; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager update <mod-id> [--profile <name>] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(modId))
    {
        Console.Error.WriteLine("Usage: pagonia-manager update <mod-id> [--profile <name>] [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    var state = new StoreStateReader().Read(layout);
    var profile = string.IsNullOrWhiteSpace(profileName)
        ? (string.IsNullOrWhiteSpace(state.ActiveProfile) ? StoreLayoutConstants.DefaultProfileName : state.ActiveProfile!)
        : profileName;

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var result = new ModUpdateService(http, state.AllowInsecureSources).Update(layout, modId, profile);
    PrintDiagnostics(result.Diagnostics);

    return result.Outcome == ModUpdateOutcome.Failed ? ManagerExitCodes.Error : ManagerExitCodes.Success;
}

static int RunCollectionUpdate(string[] tail)
{
    string? collectionId = null;
    string? storePath = null;
    var reseedTweaks = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--reseed-tweaks") { reseedTweaks = true; i += 1; }
        else if (!tail[i].StartsWith("--", StringComparison.Ordinal) && collectionId is null) { collectionId = tail[i]; i += 1; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager collection update <collection-id> [--reseed-tweaks] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(collectionId))
    {
        Console.Error.WriteLine("Usage: pagonia-manager collection update <collection-id> [--reseed-tweaks] [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    // Scripted mode can't prompt per conflict: default is the non-destructive Merge (carry
    // genuine overrides forward); --reseed-tweaks opts into the full curator reseed.
    var policy = reseedTweaks ? CollectionTweakPolicy.Reseed : CollectionTweakPolicy.Merge;

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var result = new CollectionUpdateService(http).Update(layout, collectionId, policy);
    PrintDiagnostics(result.Diagnostics);

    return result.Outcome == CollectionUpdateOutcome.Failed ? ManagerExitCodes.Error : ManagerExitCodes.Success;
}

static int RunDoctor(string[] tail)
{
    string? storePath = null;
    string? gamePath = null;
    var checkUpdates = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--game" && i + 1 < tail.Length) { gamePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--check-updates") { checkUpdates = true; i += 1; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager doctor [--store <path>] [--game <game-root>] [--check-updates]");
            return ManagerExitCodes.Usage;
        }
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    var resolvedGame = GameRootResolver.Resolve(layout, gamePath);

    // doctor is offline by default; --check-updates opts into the one network check
    // (read-only update detection). The fetcher lives only for the duration of Run.
    using var updateFetcher = checkUpdates
        ? new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)")
        : null;
    var report = new DoctorService().Run(layout, resolvedGame.HasPath ? resolvedGame.Path : null, updateFetcher);

    Console.WriteLine($"pagonia-manager doctor — store {layout.Root}");
    if (resolvedGame.HasPath)
    {
        Console.WriteLine($"  game root: {resolvedGame.Path} ({resolvedGame.Source})");
    }
    Console.WriteLine();

    foreach (var check in report.Checks)
    {
        var marker = check.Status switch
        {
            DoctorStatus.Ok => "OK  ",
            DoctorStatus.Warning => "WARN",
            DoctorStatus.Error => "FAIL",
            _ => "SKIP",
        };
        Console.WriteLine($"[{marker}] {check.Name}: {check.Summary}");
        PrintDiagnostics(check.Diagnostics);
    }

    Console.WriteLine();
    var errors = report.Checks.Count(c => c.Status == DoctorStatus.Error);
    var warnings = report.Checks.Count(c => c.Status == DoctorStatus.Warning);
    Console.WriteLine($"Summary: {report.Checks.Count} checks, {errors} error(s), {warnings} warning(s).");

    return report.HasErrors ? ManagerExitCodes.Error : ManagerExitCodes.Success;
}

static int RunPlan(string[] tail)
{
    string? gameRoot = null;
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    string? markdownPath = null;
    var overrides = new Dictionary<string, OwnershipState>(StringComparer.OrdinalIgnoreCase);
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--out" && i + 1 < tail.Length) { markdownPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--assume-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.Owned)) return ManagerExitCodes.Usage; i += 2; }
        else if (tail[i] == "--assume-not-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.NotOwned)) return ManagerExitCodes.Usage; i += 2; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager plan --game <path> [--profile <name>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <path>] [--json <out>] [--out <markdown>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(gameRoot))
    {
        Console.Error.WriteLine("Usage: pagonia-manager plan --game <path> [--profile <name>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <path>] [--json <out>] [--out <markdown>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    // Read the install's real version off the exe (null for an extracted layout)
    // so the plan can compare it against each mod's declared gameDatabaseVersion.
    var installVersion = GameVersionReader.TryRead(gameRoot, out var detectedVersion, out _) ? detectedVersion : null;
    // Resolve the install's expansion ownership (present/owned/effective) from the
    // real game root so the plan's ownership gate fires; --assume-* overrides ride along.
    var expansions = ExpansionOwnershipService.ResolveForInstall(layout, gameRoot, overrides);
    var result = new PlanProfileService().Plan(layout, gameRoot, profileName, installVersion, expansions);
    PrintDiagnostics(result.ManagerDiagnostics);

    if (result.PatcherPlan is not null)
    {
        foreach (var diagnostic in result.PatcherPlan.Diagnostics
                     .Concat(result.PatcherPlan.ModPlans.SelectMany(mp => mp.Diagnostics)))
        {
            var marker = diagnostic.Severity switch
            {
                PagoniaLand.Patcher.PatchDiagnosticSeverity.Error => "ERROR",
                PagoniaLand.Patcher.PatchDiagnosticSeverity.Warning => "WARN",
                _ => "INFO",
            };
            var line = $"  [{marker}] [{diagnostic.Code}] {diagnostic.Message}";
            if (!string.IsNullOrEmpty(diagnostic.Path))
            {
                line += $" (at {diagnostic.Path})";
            }
            if (diagnostic.Severity == PagoniaLand.Patcher.PatchDiagnosticSeverity.Error)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }

    var reporter = new ManagerPlanReporter();
    if (!string.IsNullOrWhiteSpace(jsonPath) || !string.IsNullOrWhiteSpace(markdownPath))
    {
        reporter.WriteReports(result, markdownPath, jsonPath);
    }

    Console.WriteLine();
    Console.WriteLine($"Profile:    {result.ProfileName ?? "(none)"}");
    Console.WriteLine($"Game root:  {result.GameRoot}");
    if (result.PatcherPlan is not null)
    {
        Console.WriteLine($"Mods:       {result.PatcherPlan.ModPlans.Count}");
        Console.WriteLine($"Writes:     {result.PatcherPlan.Writes.Count}");
        Console.WriteLine($"Conflicts:  {result.PatcherPlan.Conflicts.Count + result.PatcherPlan.EntryConflicts.Count}");
    }
    Console.WriteLine($"Result:     {(result.Success ? "OK" : "Blocked")}");
    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        Console.WriteLine($"JSON:       {jsonPath}");
    }
    if (!string.IsNullOrWhiteSpace(markdownPath))
    {
        Console.WriteLine($"Markdown:   {markdownPath}");
    }

    return result.Success ? ManagerExitCodes.Success
        : (result.PatcherPlan?.Conflicts.Count > 0 || result.PatcherPlan?.EntryConflicts.Count > 0)
            ? ManagerExitCodes.Conflict
            : ManagerExitCodes.Error;
}

static int RunDeploy(string[] tail)
{
    string? gameRoot = null;
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    var acceptWarnings = false;
    var dryRun = false;
    var force = false;
    var overrides = new Dictionary<string, OwnershipState>(StringComparer.OrdinalIgnoreCase);
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--profile" && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--accept-warnings") { acceptWarnings = true; i++; }
        else if (tail[i] == "--dry-run") { dryRun = true; i++; }
        else if (tail[i] == "--force") { force = true; i++; }
        else if (tail[i] == "--assume-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.Owned)) return ManagerExitCodes.Usage; i += 2; }
        else if (tail[i] == "--assume-not-owned" && i + 1 < tail.Length) { if (!TryAddOverride(overrides, tail[i + 1], OwnershipState.NotOwned)) return ManagerExitCodes.Usage; i += 2; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager deploy --game <path> [--profile <name>] [--accept-warnings] [--force] [--dry-run] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(gameRoot))
    {
        Console.Error.WriteLine("Usage: pagonia-manager deploy --game <path> [--profile <name>] [--accept-warnings] [--force] [--dry-run] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new DeployService().Deploy(layout, gameRoot, profileName, acceptWarnings, dryRun, acceptDrift: force, assumeOwnership: overrides);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
    }

    Console.WriteLine();
    Console.WriteLine($"Profile:     {result.ProfileName ?? "(none)"}");
    if (!string.IsNullOrEmpty(result.GameFingerprint))
    {
        Console.WriteLine($"Fingerprint: {result.GameFingerprint}");
    }
    if (!string.IsNullOrEmpty(result.Timestamp))
    {
        Console.WriteLine($"Timestamp:   {result.Timestamp}");
    }
    // Live-install deploys repack the canonical paks instead of writing loose XMLs,
    // so report RebuiltPakCount when present; otherwise fall back to the extracted-
    // layout file counts. Pattern B (overlay-pak) deploys put .pak files into
    // AddedFileCount in either mode.
    if (result.RebuiltPakCount > 0)
    {
        Console.WriteLine($"Paks:        {result.RebuiltPakCount} rebuilt + {result.AddedFileCount} added (overlay)");
    }
    else
    {
        Console.WriteLine($"Files:       {result.ModifiedFileCount} modified + {result.AddedFileCount} added");
    }
    if (!string.IsNullOrEmpty(result.ManifestPath))
    {
        Console.WriteLine($"Manifest:    {result.ManifestPath}");
        Console.WriteLine($"Backup:      {result.BackupDirectory}");
    }
    Console.WriteLine($"Outcome:     {result.Outcome}");

    return result.Outcome switch
    {
        DeployOutcome.Completed => ManagerExitCodes.Success,
        DeployOutcome.DryRun => ManagerExitCodes.Success,
        _ => ManagerExitCodes.Error,
    };
}

static int RunRollback(string[] tail)
{
    string? gameRoot = null;
    string? storePath = null;
    string? jsonPath = null;
    var force = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--force") { force = true; i++; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager rollback --game <path> [--force] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(gameRoot))
    {
        Console.Error.WriteLine("Usage: pagonia-manager rollback --game <path> [--force] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new RollbackService().Rollback(layout, gameRoot, acceptDrift: force);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
    }

    Console.WriteLine();
    Console.WriteLine($"Fingerprint:   {result.GameFingerprint ?? "(unknown)"}");
    Console.WriteLine($"Outcome:       {result.Outcome}");
    if (result.Outcome == RollbackOutcome.Reverted)
    {
        Console.WriteLine($"Reverted:      {result.RevertedTimestamp} (profile '{result.RevertedProfile}')");
        Console.WriteLine($"Files restored: {result.RestoredFileCount}");
    }

    return result.Outcome switch
    {
        RollbackOutcome.Reverted => ManagerExitCodes.Success,
        RollbackOutcome.NothingToRollback => ManagerExitCodes.Success,
        _ => ManagerExitCodes.Error,
    };
}

static int RunDeployStatus(string[] tail)
{
    if (!TryParseGameWithStoreAndJson(tail, out var gameRoot, out var storePath, out var jsonPath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager deploy-status --game <path> [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new DeployStatusService().List(layout, gameRoot!);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result, gameRoot!));
    }

    if (result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Game root:    {Path.GetFullPath(gameRoot!)}");
    Console.WriteLine($"Game version: {(result.GameProductVersion is { } v ? $"v{v}" : "(unknown)")}");
    Console.WriteLine($"Fingerprint:  {result.GameFingerprint}");
    if (!result.HasDeploys)
    {
        Console.WriteLine("Status:       no prior deploys");
        return ManagerExitCodes.Success;
    }

    var latest = result.Deploys[0];
    Console.WriteLine($"Status:       last deploy '{latest.Timestamp}' (profile '{latest.Profile}', {latest.ModCount} mods, {latest.FileCount} files)");
    return ManagerExitCodes.Success;
}

static int RunDeployList(string[] tail)
{
    if (!TryParseGameWithStoreAndJson(tail, out var gameRoot, out var storePath, out var jsonPath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager deploy-list --game <path> [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new DeployStatusService().List(layout, gameRoot!);
    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result, gameRoot!));
    }

    if (result.Diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error))
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Game root:    {Path.GetFullPath(gameRoot!)}");
    Console.WriteLine($"Fingerprint:  {result.GameFingerprint}");
    Console.WriteLine($"Deploys:      {result.Deploys.Count}");
    Console.WriteLine();

    if (result.Deploys.Count == 0)
    {
        Console.WriteLine("  (none)");
        return ManagerExitCodes.Success;
    }

    var index = 1;
    foreach (var deploy in result.Deploys)
    {
        var marker = index == 1 ? "*" : " ";
        Console.WriteLine($"  {marker} {deploy.Timestamp} — profile '{deploy.Profile}', {deploy.ModCount} mods, {deploy.FileCount} files");
        index++;
    }

    return ManagerExitCodes.Success;
}

static int RunDeploysClean(string[] tail)
{
    int? keep = null;
    string? gameRoot = null;
    string? storePath = null;
    var dryRun = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--keep" && i + 1 < tail.Length && int.TryParse(tail[i + 1], out var parsedKeep) && parsedKeep >= 0)
        {
            keep = parsedKeep;
            i += 2;
        }
        else if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--dry-run") { dryRun = true; i++; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager deploys clean --keep <N> [--game <path>] [--dry-run] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (keep is null)
    {
        Console.Error.WriteLine("Usage: pagonia-manager deploys clean --keep <N> [--game <path>] [--dry-run] [--store <path>]");
        Console.Error.WriteLine("       --keep is required (use 0 to prune all but the newest deploy per fingerprint; the newest is always kept as the rollback anchor)");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var result = new DeployCleanService().Clean(layout, keep.Value, gameRoot, dryRun);
    PrintDiagnostics(result.Diagnostics);

    Console.WriteLine();
    Console.WriteLine($"Store root:  {layout.Root}");
    if (!string.IsNullOrWhiteSpace(gameRoot))
    {
        Console.WriteLine($"Game root:   {Path.GetFullPath(gameRoot)}");
    }
    Console.WriteLine($"Keep:        {keep.Value}");
    Console.WriteLine($"Mode:        {(result.DryRun ? "dry-run (no changes written)" : "removing")}");
    Console.WriteLine($"Removed:     {result.RemovedCount}");
    Console.WriteLine($"Kept:        {result.KeptCount}");
    if (result.RefusedCount > 0)
    {
        Console.WriteLine($"Refused:     {result.RefusedCount} (current state.yaml.lastDeploy protected)");
    }

    return ManagerExitCodes.Success;
}

static int RunDeploysListOrphans(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager deploys list-orphans [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var orphans = new OrphanedDeployFinder().FindAll(layout);

    Console.WriteLine($"Store root:  {layout.Root}");
    Console.WriteLine($"Orphans:     {orphans.Count}");
    Console.WriteLine();

    if (orphans.Count == 0)
    {
        Console.WriteLine("  (no orphaned deploys — every deploy directory still matches a live install fingerprint)");
        return ManagerExitCodes.Success;
    }

    foreach (var orphan in orphans)
    {
        var reasonText = orphan.Reason switch
        {
            OrphanReason.GameRootGone => "recorded gameRoot no longer exists on disk",
            OrphanReason.GameUpdated => "recorded gameRoot exists but its fingerprint has changed (likely a Pioneers of Pagonia update)",
            _ => "unknown",
        };
        Console.WriteLine($"  fingerprint:  {orphan.Fingerprint}");
        Console.WriteLine($"    gameRoot:   {orphan.RecordedGameRoot}");
        Console.WriteLine($"    deploys:    {orphan.TotalDeployCount}");
        Console.WriteLine($"    latest:     {orphan.LatestTimestamp} (profile '{orphan.LatestProfile}', {orphan.LatestModCount} mods, {orphan.LatestFileCount} files)");
        Console.WriteLine($"    stale:      {reasonText}");
        Console.WriteLine();
    }

    return ManagerExitCodes.Success;
}

static bool TryParseGameWithStoreAndJson(string[] tail, out string? gameRoot, out string? storePath, out string? jsonPath)
{
    gameRoot = null;
    storePath = null;
    jsonPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--game" && i + 1 < tail.Length) { gameRoot = tail[i + 1]; i += 2; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else { return false; }
    }
    return !string.IsNullOrWhiteSpace(gameRoot);
}

static int RunSchemaValidate(string[] tail)
{
    string? kind = null;
    string? reportPath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--kind" && i + 1 < tail.Length) { kind = tail[i + 1]; i += 2; }
        else if (tail[i] == "--report" && i + 1 < tail.Length) { reportPath = tail[i + 1]; i += 2; }
        else
        {
            Console.Error.WriteLine($"Usage: pagonia-manager schema-validate --kind <{string.Join('|', ManagerReportKinds.All)}> --report <path>");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(reportPath))
    {
        Console.Error.WriteLine($"Usage: pagonia-manager schema-validate --kind <{string.Join('|', ManagerReportKinds.All)}> --report <path>");
        return ManagerExitCodes.Usage;
    }

    var diagnostics = new ManagerSchemaValidator().ValidateReport(kind, reportPath);
    PrintDiagnostics(diagnostics);

    return diagnostics.Any(d => d.Severity == ManagerDiagnosticSeverity.Error)
        ? ManagerExitCodes.Error
        : ManagerExitCodes.Success;
}

static int RunCollectionInstall(string[] tail)
{
    string? from = null;
    string? modsRoot = null;
    string? profileName = null;
    string? storePath = null;
    string? jsonPath = null;
    bool activate = false;
    bool overwrite = false;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--from" && i + 1 < tail.Length) { from = tail[i + 1]; i += 2; }
        else if (tail[i] == "--mods-root" && i + 1 < tail.Length) { modsRoot = tail[i + 1]; i += 2; }
        // --as-profile is the canonical name; --profile is a legacy alias.
        // Both override the auto-derived profile name; either flag works.
        else if ((tail[i] == "--profile" || tail[i] == "--as-profile") && i + 1 < tail.Length) { profileName = tail[i + 1]; i += 2; }
        else if (tail[i] == "--activate") { activate = true; i += 1; }
        else if (tail[i] == "--overwrite") { overwrite = true; i += 1; }
        else if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (tail[i] == "--json" && i + 1 < tail.Length) { jsonPath = tail[i + 1]; i += 2; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager collection install --from <file|gh:owner/repo[#ref]/id> [--mods-root <path>] [--as-profile <name>] [--activate] [--overwrite] [--store <path>] [--json <out>]");
            return ManagerExitCodes.Usage;
        }
    }

    if (string.IsNullOrWhiteSpace(from))
    {
        Console.Error.WriteLine("Usage: pagonia-manager collection install --from <file|gh:owner/repo[#ref]/id> [--mods-root <path>] [--as-profile <name>] [--activate] [--overwrite] [--store <path>] [--json <out>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised. Run 'pagonia-manager store init' first.");
        return ManagerExitCodes.Error;
    }

    // Detect a remote (gh:/https://github.com/...) source. When matched, we
    // fetch the entire collection + every mod it references into a temp dir
    // and feed THAT to the existing CollectionInstallService. The remote
    // ModSources map gets threaded through CollectionInstallOptions so the
    // lockfile records each mod's pinned commit SHA.
    Dictionary<string, string>? remoteModSources = null;
    string? remoteCollectionSource = null;
    string? remoteTempDir = null;
    string installCollectionPath = from;
    string? installModsRoot = modsRoot;

    if (RemoteSourceParser.TryParse(from, out var parsed))
    {
        if (parsed is GitHubSource gh)
        {
            using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
            var fetch = new RemoteFetcher(http).FetchCollection(gh);
            PrintDiagnostics(fetch.Diagnostics);

            if (!fetch.Success || fetch.TempDirectory is null || fetch.CollectionFilePath is null || fetch.ModsRoot is null)
            {
                return ManagerExitCodes.Error;
            }

            remoteTempDir = fetch.TempDirectory;
            installCollectionPath = fetch.CollectionFilePath;
            installModsRoot = fetch.ModsRoot;
            remoteModSources = new Dictionary<string, string>(fetch.ModSources, StringComparer.Ordinal);
            remoteCollectionSource = fetch.ResolvedCollectionSource;
        }
        else if (parsed is ModIoSource)
        {
            // mod.io's collection model is server-curated + maintained inside
            // mod.io itself. Doesn't map onto our portable *.collection.yaml
            // (which is a YAML file with cross-repo refs + version pinning).
            // Refuse cleanly with an info diagnostic that points users at the
            // GitHub-repo collection path instead, which IS portable.
            PrintDiagnostics(new[]
            {
                new ManagerDiagnostic(
                    ManagerDiagnosticSeverity.Info,
                    ManagerDiagnosticCodes.ModIoCollectionsUnsupported,
                    "mod.io collections are server-curated and not portable as *.collection.yaml. " +
                    "Use 'collection install --from gh:<owner>/<repo>/<collection-id>' for portable collections, " +
                    "or install individual mod.io mods with 'install --from modio:<game>/<mod-id>'."),
            });
            return ManagerExitCodes.Error;
        }
        else if (parsed is DirectUrlSource)
        {
            // Direct-URL ZIP collections aren't a thing — a single ZIP file
            // is one mod, not a multi-mod collection bundle. Surface the same
            // hint as the mod.io case.
            Console.Error.WriteLine(
                "Direct-URL ZIP sources install a single mod, not a collection. " +
                "Use 'collection install --from gh:<owner>/<repo>/<collection-id>' for remote collections.");
            return ManagerExitCodes.Error;
        }
    }

    if (string.IsNullOrWhiteSpace(installModsRoot))
    {
        // Local install path with no --mods-root provided is a usage error;
        // remote install supplies it from the fetched temp tree, so this
        // check sits AFTER the gh: dispatch.
        Console.Error.WriteLine("Usage: pagonia-manager collection install --from <file|gh:owner/repo[#ref]/id> [--mods-root <path>] [--as-profile <name>] [--activate] [--overwrite] [--store <path>] [--json <out>]");
        if (remoteTempDir is not null) { try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, true); } } catch { } }
        return ManagerExitCodes.Usage;
    }

    CollectionInstallResult result;
    try
    {
        var options = new CollectionInstallOptions
        {
            ProfileNameOverride = profileName,
            Activate = activate,
            Overwrite = overwrite,
            RemoteModSources = remoteModSources,
            RemoteCollectionSource = remoteCollectionSource,
        };
        result = new CollectionInstallService().InstallWithOptions(layout, installCollectionPath, installModsRoot, options);
    }
    finally
    {
        if (remoteTempDir is not null)
        {
            try { if (Directory.Exists(remoteTempDir)) { Directory.Delete(remoteTempDir, true); } }
            catch { /* best-effort cleanup */ }
        }
    }

    PrintDiagnostics(result.Diagnostics);

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        ManagerReports.WriteJson(jsonPath, ManagerReports.ToJson(result));
    }

    switch (result.Outcome)
    {
        case CollectionInstallOutcome.Installed:
            Console.WriteLine();
            Console.WriteLine($"Installed collection '{result.CollectionId}@{result.CollectionVersion}'");
            Console.WriteLine($"  manifest: {result.ManifestPath}");
            Console.WriteLine($"  lockfile: {result.LockfilePath}");
            Console.WriteLine($"  mods:");
            foreach (var (id, version) in result.InstalledMods)
            {
                Console.WriteLine($"    - {id}@{version}");
            }
            Console.WriteLine($"  profile '{result.ProfileName}' created and pinned to this collection.");
            if (result.ProfileActivated)
            {
                Console.WriteLine($"  profile '{result.ProfileName}' is now ACTIVE — next plan / deploy targets it.");
            }
            else
            {
                Console.WriteLine($"  Hint: 'pagonia-manager profile use {result.ProfileName}' to activate.");
            }
            return ManagerExitCodes.Success;
        case CollectionInstallOutcome.AlreadyInstalled:
            Console.WriteLine($"Already installed: {result.CollectionId}@{result.CollectionVersion}");
            return ManagerExitCodes.Success;
        default:
            return ManagerExitCodes.Error;
    }
}

static int RunCollectionList(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager collection list [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var resolution = StoreRootResolver.Resolve(storePath);
    var layout = new StoreLayout(resolution.Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var collections = new CollectionLister().List(layout);
    Console.WriteLine($"Store root:    {layout.Root}");
    Console.WriteLine($"  source:      {DescribeSource(resolution.Source)}");
    Console.WriteLine($"  collections: {collections.Count}");
    Console.WriteLine();

    if (collections.Count == 0)
    {
        Console.WriteLine("  (none)");
        return ManagerExitCodes.Success;
    }

    foreach (var c in collections)
    {
        Console.WriteLine($"  {c.Id}@{c.Version}");
        if (!string.IsNullOrEmpty(c.Name))
        {
            Console.WriteLine($"      name:        {c.Name}");
        }
        Console.WriteLine($"      gdb version: {c.GameDatabaseVersion ?? "(unknown)"}");
        Console.WriteLine($"      mods:        {c.ResolvedModCount}");
        Console.WriteLine($"      installed:   {c.GeneratedAt ?? "(unknown)"}");
    }

    return ManagerExitCodes.Success;
}

static int RunCollectionShow(string[] tail)
{
    if (!TryParseNamedWithStore(tail, out var collectionId, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager collection show <id> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var match = new CollectionLister().List(layout)
        .FirstOrDefault(c => string.Equals(c.Id, collectionId, StringComparison.Ordinal));
    if (match is null)
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.CollectionNotInstalled}] Collection '{collectionId}' is not installed in this store.");
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Collection:    {match.Id}@{match.Version}");
    if (!string.IsNullOrEmpty(match.Name))
    {
        Console.WriteLine($"  name:        {match.Name}");
    }
    if (!string.IsNullOrEmpty(match.Author))
    {
        Console.WriteLine($"  author:      {match.Author}");
    }
    Console.WriteLine($"  gdb version: {match.GameDatabaseVersion ?? "(unknown)"}");
    if (!string.IsNullOrEmpty(match.Description))
    {
        Console.WriteLine($"  description: {match.Description}");
    }
    Console.WriteLine($"  mods:        {match.ResolvedModCount}");
    Console.WriteLine($"  installed:   {match.GeneratedAt ?? "(unknown)"}");
    Console.WriteLine($"  manifest:    {match.ManifestPath}");
    if (!string.IsNullOrEmpty(match.LockfilePath))
    {
        Console.WriteLine($"  lockfile:    {match.LockfilePath}");
    }

    return ManagerExitCodes.Success;
}

static int RunCollectionUninstall(string[] tail)
{
    if (!TryParseNamedWithStore(tail, out var collectionId, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager collection uninstall <id> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine(
            $"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var result = new CollectionUninstaller().Uninstall(layout, collectionId!);
    PrintDiagnostics(result.Diagnostics);

    if (result.Outcome != CollectionUninstallOutcome.Removed)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Removed collection '{result.CollectionId}'.");
    Console.WriteLine($"  manifest dir: {(result.ManifestDirectoryRemoved ? "removed" : "absent")}");
    Console.WriteLine($"  lockfile:     {(result.LockfileRemoved ? "removed" : "absent")}");
    Console.WriteLine("  note: installed mods and the linked profile are kept; remove them with 'uninstall <mod-id>' / 'profile delete <name>' if no longer needed.");
    return ManagerExitCodes.Success;
}

// ---- Catalog verbs (catalog list / add / remove / browse / show) -----------

static int RunCatalogList(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog list [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine($"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var subscriptions = new CatalogSubscriptionService().List(layout);
    if (subscriptions.Count == 0)
    {
        Console.WriteLine("(no catalog subscriptions)");
        Console.WriteLine("Add one with: pagonia-manager catalog add gh:<owner>/<repo>  -- or 'file:./path/catalog.yaml' for a local one.");
        return ManagerExitCodes.Success;
    }

    Console.WriteLine($"Subscribed catalogs ({subscriptions.Count}):");
    foreach (var src in subscriptions)
    {
        Console.WriteLine($"  - {src.Canonical}");
    }
    return ManagerExitCodes.Success;
}

static int RunCatalogAdd(string[] tail)
{
    if (tail.Length < 1)
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog add <gh:owner/repo[#ref][/path]|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var spec = tail[0];
    var rest = tail[1..];
    if (!TryParseStoreFlag(rest, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog add <gh:owner/repo[#ref][/path]|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine($"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var result = new CatalogSubscriptionService().Add(layout, spec);
    PrintDiagnostics(result.Diagnostics);
    return result.Success ? ManagerExitCodes.Success : ManagerExitCodes.Error;
}

static int RunCatalogRemove(string[] tail)
{
    if (tail.Length < 1)
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog remove <gh:owner/repo[#ref][/path]|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var spec = tail[0];
    var rest = tail[1..];
    if (!TryParseStoreFlag(rest, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog remove <gh:owner/repo[#ref][/path]|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine($"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var result = new CatalogSubscriptionService().Remove(layout, spec);
    PrintDiagnostics(result.Diagnostics);
    return result.Success ? ManagerExitCodes.Success : ManagerExitCodes.Error;
}

static int RunCatalogBrowse(string[] tail)
{
    if (!TryParseStoreFlag(tail, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog browse [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine($"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var state = new StoreStateReader().Read(layout);
    var subscriptions = new CatalogSubscriptionService().List(layout);
    if (subscriptions.Count == 0)
    {
        Console.WriteLine("(no catalog subscriptions — add one with 'pagonia-manager catalog add gh:<owner>/<repo>')");
        return ManagerExitCodes.Success;
    }

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var aggregator = new CatalogAggregator(new CachingCatalogFetcher(http, layout, state.CatalogCacheStalenessHours, state.AllowInsecureCatalogSources));
    var result = aggregator.Aggregate(subscriptions, state.CatalogMaxDepth);
    PrintDiagnostics(result.Diagnostics);

    if (result.Repos.Count == 0)
    {
        Console.WriteLine("(no repos found across subscribed catalogs)");
        return ManagerExitCodes.Success;
    }

    Console.WriteLine($"Aggregated repos ({result.Repos.Count} unique across {result.VisitedSources.Count} fetched catalog(s)):");
    foreach (var repo in result.Repos)
    {
        var vouches = repo.VouchedBy.Count > 1 ? $" [vouched by {repo.VouchedBy.Count} catalogs]" : "";
        // Show the install-spec base form (owner/repo[:indexPath]) so the printed
        // string is what `install --from gh:<this>/<mod-id>` expects.
        var repoSpec = repo.IndexPath.Length > 0 ? $"{repo.Owner}/{repo.Repo}:{repo.IndexPath}" : $"{repo.Owner}/{repo.Repo}";
        Console.WriteLine($"  {repoSpec}{vouches}");
        if (!string.IsNullOrWhiteSpace(repo.Summary))
        {
            Console.WriteLine($"    {repo.Summary}");
        }
    }
    return ManagerExitCodes.Success;
}

static int RunCatalogShow(string[] tail)
{
    if (tail.Length < 1)
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog show <gh:owner/repo[#ref][/path]|https://...|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }

    var spec = tail[0];
    var rest = tail[1..];
    if (!TryParseStoreFlag(rest, out var storePath))
    {
        Console.Error.WriteLine("Usage: pagonia-manager catalog show <gh:owner/repo[#ref][/path]|https://...|file:path> [--store <path>]");
        return ManagerExitCodes.Usage;
    }
    // catalog show benefits from the cache too — if you're drilling down
    // into a catalog you already browsed within the freshness window, the
    // YAML is already on disk.
    if (!CatalogSourceParser.TryParse(spec, out var source))
    {
        Console.Error.WriteLine($"'{spec}' is not a recognised catalog source. Expected 'gh:owner/repo[#ref][/path]', 'https://host/path/catalog.yaml', or 'file:path'.");
        return ManagerExitCodes.Usage;
    }

    var showLayout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    var showState = new StoreStateReader().Exists(showLayout) ? new StoreStateReader().Read(showLayout) : new StoreState();

    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var fetch = new CachingCatalogFetcher(http, showLayout, showState.CatalogCacheStalenessHours, showState.AllowInsecureCatalogSources).Fetch(source);
    PrintDiagnostics(fetch.Diagnostics);

    if (!fetch.Success || fetch.Catalog is null)
    {
        return ManagerExitCodes.Error;
    }

    Console.WriteLine($"Catalog: {fetch.Source.Canonical}");
    if (!string.IsNullOrWhiteSpace(fetch.Catalog.CatalogMeta?.Name))
    {
        Console.WriteLine($"  name: {fetch.Catalog.CatalogMeta.Name}");
    }
    if (!string.IsNullOrWhiteSpace(fetch.Catalog.CatalogMeta?.Maintainer))
    {
        Console.WriteLine($"  maintainer: {fetch.Catalog.CatalogMeta.Maintainer}");
    }
    if (!string.IsNullOrWhiteSpace(fetch.Catalog.CatalogMeta?.Description))
    {
        Console.WriteLine($"  description: {fetch.Catalog.CatalogMeta.Description}");
    }
    if (fetch.Catalog.Repos.Count > 0)
    {
        Console.WriteLine($"  repos ({fetch.Catalog.Repos.Count}):");
        foreach (var r in fetch.Catalog.Repos)
        {
            var repoSpec = r.IndexPath.Length > 0 ? $"{r.Owner}/{r.Repo}:{r.IndexPath}" : $"{r.Owner}/{r.Repo}";
            Console.WriteLine($"    - {repoSpec}{(string.IsNullOrWhiteSpace(r.Summary) ? "" : $"  — {r.Summary}")}");
        }
    }
    if (fetch.Catalog.Catalogs.Count > 0)
    {
        Console.WriteLine($"  federated catalogs ({fetch.Catalog.Catalogs.Count}):");
        foreach (var c in fetch.Catalog.Catalogs)
        {
            Console.WriteLine($"    - {c.Source}{(string.IsNullOrWhiteSpace(c.Summary) ? "" : $"  — {c.Summary}")}");
        }
    }
    return ManagerExitCodes.Success;
}

static int RunCatalogRefresh(string[] tail)
{
    // Two forms:
    //   catalog refresh                — refresh every subscribed catalog
    //   catalog refresh <source>       — refresh just the named one
    // Both force a fresh fetch via the cache wrapper, which writes the
    // result back to disk so subsequent reads hit cache.
    string? specifiedSource = null;
    string? storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length) { storePath = tail[i + 1]; i += 2; }
        else if (specifiedSource is null && !tail[i].StartsWith("-")) { specifiedSource = tail[i]; i++; }
        else
        {
            Console.Error.WriteLine("Usage: pagonia-manager catalog refresh [<gh:owner/repo[#ref][/path]|https://...|file:path>] [--store <path>]");
            return ManagerExitCodes.Usage;
        }
    }

    var layout = new StoreLayout(StoreRootResolver.Resolve(storePath).Root);
    if (!new StoreStateReader().Exists(layout))
    {
        Console.Error.WriteLine($"[{ManagerDiagnosticCodes.StoreNotInitialised}] Store at '{layout.Root}' is not initialised.");
        return ManagerExitCodes.Error;
    }

    var state = new StoreStateReader().Read(layout);
    using var http = new HttpRemoteContentFetcher($"pagonia-manager/{ManagerInfo.Version} (+https://github.com/pagonia-land/Pagonia-Land)");
    var fetcher = new CachingCatalogFetcher(http, layout, state.CatalogCacheStalenessHours, state.AllowInsecureCatalogSources);

    IReadOnlyList<CatalogSource> targets;
    if (specifiedSource is not null)
    {
        if (!CatalogSourceParser.TryParse(specifiedSource, out var parsed))
        {
            Console.Error.WriteLine($"'{specifiedSource}' is not a recognised catalog source. Expected 'gh:owner/repo[#ref][/path]', 'https://host/path/catalog.yaml', or 'file:path'.");
            return ManagerExitCodes.Usage;
        }
        targets = new[] { parsed };
    }
    else
    {
        targets = new CatalogSubscriptionService().List(layout);
        if (targets.Count == 0)
        {
            Console.WriteLine("(no catalog subscriptions to refresh)");
            return ManagerExitCodes.Success;
        }
    }

    var anyError = false;
    foreach (var target in targets)
    {
        var result = fetcher.Fetch(target, forceRefresh: true);
        PrintDiagnostics(result.Diagnostics);
        if (!result.Success)
        {
            anyError = true;
            Console.Error.WriteLine($"  failed to refresh {target.Canonical}");
        }
    }
    return anyError ? ManagerExitCodes.Error : ManagerExitCodes.Success;
}

static bool TryParseNamedWithStore(string[] tail, out string? name, out string? storePath)
{
    name = null;
    storePath = null;
    var i = 0;
    while (i < tail.Length)
    {
        if (tail[i] == "--store" && i + 1 < tail.Length)
        {
            storePath = tail[i + 1];
            i += 2;
        }
        else if (name is null && !tail[i].StartsWith("-"))
        {
            name = tail[i];
            i++;
        }
        else
        {
            return false;
        }
    }

    return !string.IsNullOrWhiteSpace(name);
}

static bool TryParseStoreFlag(string[] tail, out string? storePath)
{
    storePath = null;
    if (tail.Length == 0)
    {
        return true;
    }

    if (tail is ["--store", var path])
    {
        storePath = path;
        return true;
    }

    return false;
}

static string DescribeSource(StoreRootResolver.ResolutionSource source) => source switch
{
    StoreRootResolver.ResolutionSource.Flag => "--store flag",
    StoreRootResolver.ResolutionSource.EnvironmentVariable => $"{StoreRootResolver.EnvironmentVariableName} env",
    StoreRootResolver.ResolutionSource.PlatformDefault => "platform default",
    _ => "unknown"
};

static void PrintDiagnostics(IReadOnlyList<ManagerDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
    {
        var marker = diagnostic.Severity switch
        {
            ManagerDiagnosticSeverity.Error => "ERROR",
            ManagerDiagnosticSeverity.Warning => "WARN",
            _ => "INFO",
        };

        var message = $"  [{marker}] [{diagnostic.Code}] {diagnostic.Message}";
        if (!string.IsNullOrEmpty(diagnostic.Path))
        {
            message += $" (at {diagnostic.Path})";
        }

        if (diagnostic.Severity == ManagerDiagnosticSeverity.Error)
        {
            Console.Error.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}

static void PrintUsage()
{
    Console.WriteLine($"{ManagerInfo.ProductName} {ManagerInfo.Version}");
    Console.WriteLine($"Usage: {ManagerInfo.CommandName} <command> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  --version, -v                              Print product name and version");
    Console.WriteLine("  --info                                     Print backing core library versions");
    Console.WriteLine("  store init [--store <path>]                Create the mod store layout");
    Console.WriteLine("  store info [--store <path>]                Print resolved root, version, counts");
    Console.WriteLine("  install --from <folder|zip|gh:owner/repo[#ref]/mod-id|https://.../mod.zip|modio:<game>/<mod-id>[#<ver>]> [--store <p>]");
    Console.WriteLine("                                             Validate and install a mod into the store");
    Console.WriteLine("  uninstall <mod-id> [--version <v>] [--store <p>]");
    Console.WriteLine("                                             Remove a mod version (or the only one)");
    Console.WriteLine("  list [--store <path>]                      Show installed mods");
    Console.WriteLine("  enable <mod-id> [--version <v>] [--store <p>]");
    Console.WriteLine("                                             Enable a mod in the active profile");
    Console.WriteLine("  disable <mod-id> [--store <p>]             Disable a mod in the active profile (keeps it installed)");
    Console.WriteLine("  move <mod-id> (--position <n>|--before <id>|--after <id>) [--store <p>]");
    Console.WriteLine("                                             Reorder a mod in the active profile's load order");
    Console.WriteLine("  status [--store <path>]                    Show active profile + enabled mods in load order");
    Console.WriteLine("  doctor [--store <path>] [--game <path>]    Health roll-up: store, profile, cross-mod conflicts, orphans, storage, expansions");
    Console.WriteLine("  plan --game <path> [--profile <n>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>] [--out <md>]");
    Console.WriteLine("                                             Dry-run plan for the active (or named) profile");
    Console.WriteLine("  deploy --game <path> [--profile <n>] [--accept-warnings] [--force] [--dry-run] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>]");
    Console.WriteLine("                                             Apply the active (or named) profile to the game install (--force overwrites out-of-band changes)");
    Console.WriteLine("  rollback --game <path> [--force] [--store <p>]  Undo the last deploy for this game install (--force restores over out-of-band changes)");
    Console.WriteLine("  deploy-status --game <path> [--store <p>]  Show last deploy timestamp + profile for the game");
    Console.WriteLine("  deploy-list --game <path> [--store <p>]    List all retained deploys for the game");
    Console.WriteLine("  schema-validate --kind <k> --report <path>  Validate a JSON report against its schema");
    Console.WriteLine("                                             kinds: install, uninstall, deploy, rollback,");
    Console.WriteLine("                                                    collectionInstall, status, deployStatus,");
    Console.WriteLine("                                                    tweakList, tweakSet, tweakReset,");
    Console.WriteLine("                                                    expansionsList, expansionsSet, updates");
    Console.WriteLine();
    Console.WriteLine("  --json <out> is supported on: install, uninstall, deploy, rollback,");
    Console.WriteLine("    collection install, status, deploy-status, deploy-list, plan,");
    Console.WriteLine("    tweak list, tweak set, tweak reset, expansions list, expansions set");
    Console.WriteLine("  profile create <name> [--store <p>]        Create a new empty profile");
    Console.WriteLine("  profile list [--store <p>]                 List all profiles (* marks active)");
    Console.WriteLine("  profile use <name> [--store <p>]           Switch the active profile");
    Console.WriteLine("  profile copy <source> <target> [--activate] [--store <p>]   Duplicate a profile");
    Console.WriteLine("  profile export [<name>] --out <file.collection.yaml> [--id <id>] [--name <n>] [--version <v>] [--store <p>]");
    Console.WriteLine("                                             Export a profile as a shareable collection");
    Console.WriteLine("  profile delete <name> [--store <p>]        Delete a non-default, non-active profile");
    Console.WriteLine("  profile show [<name>] [--store <p>]        Show a profile (defaults to active)");
    Console.WriteLine();
    Console.WriteLine("  tweak list <mod-id> [--profile <n>] [--store <p>] [--json <out>]");
    Console.WriteLine("                                             Show a mod's tweaks + current values + origin");
    Console.WriteLine("  tweak set <mod-id> <tweak-id> <value> [--profile <n>] [--store <p>] [--json <out>]");
    Console.WriteLine("                                             Store a per-profile tweak override (validated)");
    Console.WriteLine("  tweak reset <mod-id> [<tweak-id>] [--profile <n>] [--store <p>] [--json <out>]");
    Console.WriteLine("                                             Drop one override, or all of a mod's overrides");
    Console.WriteLine("  collection install --from <file|gh:owner/repo[#ref]/id> [--mods-root <p>] [--as-profile <n>] [--activate] [--overwrite] [--store <p>]");
    Console.WriteLine("                                             Resolve + install a collection (local or remote)");
    Console.WriteLine("  collection list [--store <p>]              List installed collections");
    Console.WriteLine("  collection show <id> [--store <p>]         Show a collection's details");
    Console.WriteLine("  collection uninstall <id> [--store <p>]    Remove a collection's manifest + lockfile");
    Console.WriteLine();
    Console.WriteLine("  expansions list [--game <p>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>]");
    Console.WriteLine("                                             Show each expansion's Present/Owned/Effective state");
    Console.WriteLine("  expansions set <decorations1|dlc1> <owned|not-owned|unknown> [--game <p>] [--store <p>] [--json <out>]");
    Console.WriteLine("                                             Declare whether you own a DLC expansion (per install)");
    Console.WriteLine();
    Console.WriteLine("  catalog list [--store <p>]                 List subscribed catalogs");
    Console.WriteLine("  catalog add <gh:owner/repo|file:path> [--store <p>]      Subscribe to a catalog");
    Console.WriteLine("  catalog remove <gh:owner/repo|file:path> [--store <p>]   Unsubscribe from a catalog");
    Console.WriteLine("  catalog browse [--store <p>]               Show every repo across subscribed catalogs (federated, dedup'd)");
    Console.WriteLine("  catalog show <gh:owner/repo|file:path> [--store <p>]     Show one catalog's repos + federation refs");
    Console.WriteLine("  catalog refresh [<gh:owner/repo|file:path>] [--store <p>] Force re-fetch (all subs, or one named source)");
    Console.WriteLine();
    Console.WriteLine("  deploys list-orphans [--store <p>]         List deploys whose game install moved or updated");
    Console.WriteLine("  deploys clean --keep <N> [--game <p>] [--dry-run] [--store <p>]");
    Console.WriteLine("                                             Trim deploy backups to N most recent per fingerprint");
    Console.WriteLine();
    Console.WriteLine($"Store root resolution: --store flag > {StoreRootResolver.EnvironmentVariableName} env > platform default");
}
