using PagoniaLand.Patcher;

var reader = new ManifestReader();
var validator = new ManifestValidator();
var advisor = new EntityRelationAdvisor();
var schemaValidator = new SchemaValidator();
var planner = new PatchPlanner();
var applier = new PatchApplier();
var reporter = new PatchPlanReporter();
var applyReporter = new PatchApplyReporter();
var collectionResolver = new CollectionResolver();
var collectionExporter = new CollectionExporter();

if (args is ["--version"] or ["-v"])
{
    Console.WriteLine($"{PatcherInfo.ProductName} {PatcherInfo.Version}");
    return PatcherExitCodes.Success;
}

if (args is ["inspect-mod", "--mod", var modDirectory])
{
    var result = reader.ReadMod(modDirectory);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    var manifest = result.Value.Manifest;
    Console.WriteLine($"Mod: {manifest.Name} ({manifest.Id})");
    Console.WriteLine($"Version: {manifest.Version}");
    Console.WriteLine($"GameDatabase: {manifest.GameDatabaseVersion}");
    Console.WriteLine($"Required packages: {string.Join(", ", manifest.RequiredPackages)}");
    Console.WriteLine($"Patch files: {result.Value.PatchFiles.Count}");
    Console.WriteLine($"Operations: {result.Value.PatchFiles.Sum(file => file.PatchFile.Operations.Count)}");

    if (manifest.Tweaks.Count > 0)
    {
        Console.WriteLine($"Tweaks: {manifest.Tweaks.Count}");
        foreach (var tweak in manifest.Tweaks)
        {
            var range = tweak.Type is "number" or "integer" && (tweak.Min is not null || tweak.Max is not null)
                ? $", range [{tweak.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-∞"}, {tweak.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "+∞"}]"
                : string.Empty;
            var values = tweak.Type == "enum" && tweak.Values.Count > 0
                ? $", values: {string.Join("/", tweak.Values.Select(v => v.Value))}"
                : string.Empty;
            Console.WriteLine($"  - {tweak.Id} ({tweak.Type}) = {tweak.Default}{range}{values} — {tweak.Label}");
        }
    }

    return PatcherExitCodes.Success;
}

if (args.Length >= 3 && args[0] == "validate-mod" && args[1] == "--mod")
{
    var validateModDirectory = args[2];
    var result = reader.ReadMod(validateModDirectory);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    var diagnostics = validator.ValidateMod(result.Value).ToList();

    // Conflict-minimising authoring advisor: lint the mod's own
    // overlay *.gd.xml for InheritanceMode usage. Advisory only — Info notices
    // plus an unload-dangling Warning; never blocks validate-mod. An optional
    // --game-root turns on the base-aware checks (unload-vs-whole-DB, and
    // replace-could-be-incremental diffed against the inherited entity).
    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    diagnostics.AddRange(overlay.Diagnostics);

    var validateGameRoot = ReadOptionValue(args, "--game-root");
    ReferenceGdbIndex? reference = null;
    if (validateGameRoot is not null)
    {
        reference = ReferenceGdbIndex.Load(validateGameRoot);
        // A typo'd / wrong --game-root yields an empty index and the base-aware checks
        // silently no-op — the user would read a clean pass as "base-aware coverage".
        // Warn loudly instead so a false sense of coverage can't slip through.
        if (reference.EntityCount == 0)
        {
            diagnostics.Add(new PatchDiagnostic(
                PatchDiagnosticSeverity.Warning,
                DiagnosticCodes.ReferenceGameRootEmpty,
                $"--game-root '{validateGameRoot}' contained no *.gd.xml entities, so the base-aware advisor checks were skipped. Point it at an unpacked game database (e.g. game-gdb)."));
        }
    }
    diagnostics.AddRange(advisor.Advise(overlay, reference));

    PrintDiagnostics(diagnostics);

    return diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["schema-validate", "--mod", var schemaModDirectory])
{
    var diagnostics = schemaValidator.ValidateMod(schemaModDirectory);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["schema-validate", "--collection", var schemaCollectionPath])
{
    var diagnostics = schemaValidator.ValidateCollection(schemaCollectionPath);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["schema-validate", "--lock", var schemaLockPath])
{
    var diagnostics = schemaValidator.ValidateCollectionLock(schemaLockPath);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["schema-validate", "--repo-index", var schemaRepoIndexPath])
{
    var diagnostics = schemaValidator.ValidateRepoIndex(schemaRepoIndexPath);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["schema-validate", "--catalog", var schemaCatalogPath])
{
    var diagnostics = schemaValidator.ValidateCatalog(schemaCatalogPath);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["index-check", var indexCheckRoot])
{
    var diagnostics = new RepoIndexMirror().Check(indexCheckRoot);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

// Accept --check on either side of the path: `index build --check <root>` and the
// equally-natural `index build <root> --check` both run the write-nothing CI gate.
string? indexBuildCheckRoot =
    args is ["index", "build", "--check", var flagFirstRoot] ? flagFirstRoot
    : args is ["index", "build", var flagLastRoot, "--check"] ? flagLastRoot
    : null;
if (indexBuildCheckRoot is not null)
{
    var diagnostics = new RepoIndexMirror().Build(indexBuildCheckRoot, checkOnly: true);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["index", "build", var indexBuildRoot])
{
    var diagnostics = new RepoIndexMirror().Build(indexBuildRoot, checkOnly: false);
    PrintDiagnostics(diagnostics);
    return diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args.Length >= 4 && args[0] == "plan" && args[1] == "--game" && args[3] == "--mods")
{
    var modDirectories = ReadValuesUntilOption(args, 4);
    if (modDirectories.Count == 0)
    {
        Console.Error.WriteLine("Error: 'plan --game <path> --mods' needs at least one mod directory after --mods.");
        return PatcherExitCodes.Usage;
    }
    var markdownPath = ReadOptionValue(args, "--out");
    var jsonPath = ReadOptionValue(args, "--json");
    var (tweakOverrides, tweaksOk) = ReadTweakOverrides(args);
    if (!tweaksOk)
    {
        return PatcherExitCodes.Usage;
    }
    var planResult = BuildPlan(args[2], modDirectories, reader, validator, planner, TweakSelection.ForCli(tweakOverrides));
    PrintPlan(planResult.Plan);
    WriteReports(planResult.Plan, markdownPath, jsonPath, reporter, "directMods");
    return GetPlanExitCode(planResult);
}

if (args.Length >= 6 && args[0] == "plan" && args[1] == "--game")
{
    var planCollectionPaths = ReadOptionValues(args, "--collection");
    var planLockPath = ReadOptionValue(args, "--lock");
    var modsRoot = ReadOptionValue(args, "--mods-root");
    var markdownPath = ReadOptionValue(args, "--out");
    var jsonPath = ReadOptionValue(args, "--json");

    if (modsRoot is null || (planLockPath is null && planCollectionPaths.Count == 0))
    {
        Console.Error.WriteLine("Error: planning requires --collection <collection-yaml> or --lock <collection-lock-yaml>, plus --mods-root <mods-root>.");
        return PatcherExitCodes.Usage;
    }

    if (planLockPath is not null && planCollectionPaths.Count > 0)
    {
        Console.Error.WriteLine("Error: pass either --lock or --collection, not both.");
        return PatcherExitCodes.Usage;
    }

    var (planCliOverrides, planTweaksOk) = ReadTweakOverrides(args);
    if (!planTweaksOk)
    {
        return PatcherExitCodes.Usage;
    }

    List<LoadedMod> resolvedMods;
    string planSource;
    TweakSelection planSelection;

    if (planLockPath is not null)
    {
        var lockResolution = collectionResolver.ResolveFromLock(planLockPath, modsRoot);
        PrintDiagnostics(lockResolution.Diagnostics);

        if (!lockResolution.Success || lockResolution.Value is null)
        {
            return PatcherExitCodes.Error;
        }

        resolvedMods = lockResolution.Value.Mods.ToList();
        planSelection = BuildLockfileTweakSelection(planCliOverrides, lockResolution.Value.Lock);
        planSource = "lockfile";
    }
    else
    {
        var resolution = collectionResolver.ResolveMany(planCollectionPaths, modsRoot);
        PrintDiagnostics(resolution.Diagnostics);

        if (!resolution.Success || resolution.Value is null)
        {
            return PatcherExitCodes.Error;
        }

        resolvedMods = resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList();
        planSelection = BuildCollectionTweakSelection(planCliOverrides, resolution.Value.Mods);
        planSource = planCollectionPaths.Count == 1 ? "collection" : "collections";
    }

    var planResult = BuildLoadedModsPlan(args[2], resolvedMods, validator, planner, planSelection);
    PrintPlan(planResult.Plan);
    WriteReports(planResult.Plan, markdownPath, jsonPath, reporter, planSource);
    return GetPlanExitCode(planResult);
}

if (args.Length >= 6 && args[0] == "apply" && args[1] == "--game" && args[3] == "--mods")
{
    var modDirectories = ReadValuesUntilOption(args, 4);
    var outputGameRoot = ReadOptionValue(args, "--out");
    var applyReportPath = ReadOptionValue(args, "--report");
    var applyJsonPath = ReadOptionValue(args, "--json");

    if (outputGameRoot is null)
    {
        Console.Error.WriteLine("Error: apply requires --out <output-game-root>.");
        return PatcherExitCodes.Usage;
    }

    var (applyTweakOverrides, applyTweaksOk) = ReadTweakOverrides(args);
    if (!applyTweaksOk)
    {
        return PatcherExitCodes.Usage;
    }

    var planResult = BuildPlan(args[2], modDirectories, reader, validator, planner, TweakSelection.ForCli(applyTweakOverrides));
    PrintPlan(planResult.Plan);

    if (GetPlanExitCode(planResult) != PatcherExitCodes.Success || planResult.Plan is null)
    {
        return GetPlanExitCode(planResult);
    }

    var diagnostics = applier.Apply(args[2], outputGameRoot, planResult.Plan);
    PrintDiagnostics(diagnostics);
    WriteApplyReports(planResult.Plan, diagnostics, outputGameRoot, applyReportPath, applyJsonPath, applyReporter, "directMods");

    return diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args.Length >= 6 && args[0] == "apply" && args[1] == "--game")
{
    var applyCollectionPaths = ReadOptionValues(args, "--collection");
    var applyLockPath = ReadOptionValue(args, "--lock");
    var modsRoot = ReadOptionValue(args, "--mods-root");
    var outputGameRoot = ReadOptionValue(args, "--out");
    var applyReportPath = ReadOptionValue(args, "--report");
    var applyJsonPath = ReadOptionValue(args, "--json");

    if (modsRoot is null || outputGameRoot is null || (applyLockPath is null && applyCollectionPaths.Count == 0))
    {
        Console.Error.WriteLine("Error: apply requires --collection <collection-yaml> or --lock <collection-lock-yaml>, plus --mods-root <mods-root> and --out <output-game-root>.");
        return PatcherExitCodes.Usage;
    }

    if (applyLockPath is not null && applyCollectionPaths.Count > 0)
    {
        Console.Error.WriteLine("Error: pass either --lock or --collection, not both.");
        return PatcherExitCodes.Usage;
    }

    var (applyCliOverrides, applyCollectionTweaksOk) = ReadTweakOverrides(args);
    if (!applyCollectionTweaksOk)
    {
        return PatcherExitCodes.Usage;
    }

    List<LoadedMod> resolvedMods;
    string applyPlanSource;
    TweakSelection applySelection;

    if (applyLockPath is not null)
    {
        var lockResolution = collectionResolver.ResolveFromLock(applyLockPath, modsRoot);
        PrintDiagnostics(lockResolution.Diagnostics);

        if (!lockResolution.Success || lockResolution.Value is null)
        {
            return PatcherExitCodes.Error;
        }

        resolvedMods = lockResolution.Value.Mods.ToList();
        applySelection = BuildLockfileTweakSelection(applyCliOverrides, lockResolution.Value.Lock);
        applyPlanSource = "lockfile";
    }
    else
    {
        var resolution = collectionResolver.ResolveMany(applyCollectionPaths, modsRoot);
        PrintDiagnostics(resolution.Diagnostics);

        if (!resolution.Success || resolution.Value is null)
        {
            return PatcherExitCodes.Error;
        }

        resolvedMods = resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList();
        applySelection = BuildCollectionTweakSelection(applyCliOverrides, resolution.Value.Mods);
        applyPlanSource = applyCollectionPaths.Count == 1 ? "collection" : "collections";
    }

    var planResult = BuildLoadedModsPlan(args[2], resolvedMods, validator, planner, applySelection);
    PrintPlan(planResult.Plan);

    if (GetPlanExitCode(planResult) != PatcherExitCodes.Success || planResult.Plan is null)
    {
        return GetPlanExitCode(planResult);
    }

    var diagnostics = applier.Apply(args[2], outputGameRoot, planResult.Plan);
    PrintDiagnostics(diagnostics);
    WriteApplyReports(planResult.Plan, diagnostics, outputGameRoot, applyReportPath, applyJsonPath, applyReporter, applyPlanSource);

    return diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error)
        ? PatcherExitCodes.Error
        : PatcherExitCodes.Success;
}

if (args is ["inspect-collection", "--collection", var collectionPath])
{
    var result = reader.ReadCollectionManifest(collectionPath);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    var collection = result.Value;
    Console.WriteLine($"Collection: {collection.Name} ({collection.Id})");
    Console.WriteLine($"Version: {collection.Version}");
    Console.WriteLine($"GameDatabase: {collection.GameDatabaseVersion}");
    Console.WriteLine($"Mods: {collection.Mods.Count}");
    Console.WriteLine($"Load order entries: {collection.LoadOrder.Count}");
    return PatcherExitCodes.Success;
}

if (args.Length >= 6 && args[0] == "resolve-collection")
{
    var collection = ReadOptionValue(args, "--collection");
    var modsRoot = ReadOptionValue(args, "--mods-root");
    var outputLockPath = ReadOptionValue(args, "--lock");

    if (collection is null || modsRoot is null || outputLockPath is null)
    {
        Console.Error.WriteLine("Error: resolve-collection requires --collection <collection-yaml> --mods-root <mods-root> --lock <lock-yaml>.");
        return PatcherExitCodes.Usage;
    }

    var result = collectionResolver.Resolve(collection, modsRoot);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    collectionResolver.WriteLockFile(result.Value.Lock, outputLockPath);
    Console.WriteLine($"Resolved collection: {result.Value.Collection.Name}");
    Console.WriteLine($"Enabled mods: {result.Value.Mods.Count}");
    Console.WriteLine($"Wrote lockfile: {outputLockPath}");
    return PatcherExitCodes.Success;
}

if (args.Length >= 8 && args[0] == "export-collection" && args[1] == "--mods")
{
    var modDirectories = ReadValuesUntilOption(args, 2);
    var outputPath = ReadOptionValue(args, "--out");
    var id = ReadOptionValue(args, "--id");
    var name = ReadOptionValue(args, "--name");

    if (outputPath is null || id is null || name is null)
    {
        Console.Error.WriteLine("Error: export-collection requires --mods <mod-directory> --out <collection-yaml> --id <id> --name <name>.");
        return PatcherExitCodes.Usage;
    }

    var options = new CollectionExportOptions(
        id,
        name,
        ReadOptionValue(args, "--version") ?? "0.1.0",
        ReadOptionValue(args, "--author") ?? "Pagonia Land",
        ReadOptionValue(args, "--game-database-version"),
        ReadOptionValue(args, "--description") ?? "Exported local mod set.",
        ReadOptionValue(args, "--conflict-policy") ?? "strict");

    var result = collectionExporter.Export(modDirectories, options);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    collectionExporter.WriteCollection(result.Value, outputPath);
    Console.WriteLine($"Wrote collection: {outputPath}");
    Console.WriteLine($"Mods: {result.Value.Mods.Count}");
    return PatcherExitCodes.Success;
}

if (args is ["inspect-lock", "--lock", var lockPath])
{
    var result = reader.ReadCollectionLock(lockPath);
    PrintDiagnostics(result.Diagnostics);

    if (!result.Success || result.Value is null)
    {
        return PatcherExitCodes.Error;
    }

    var collectionLock = result.Value;
    Console.WriteLine($"Collection lock: {collectionLock.CollectionId}");
    Console.WriteLine($"Collection version: {collectionLock.CollectionVersion}");
    Console.WriteLine($"GameDatabase: {collectionLock.GameDatabaseVersion}");
    Console.WriteLine($"Locked mods: {collectionLock.Mods.Count}");
    return PatcherExitCodes.Success;
}

PrintUsage();
return PatcherExitCodes.Usage;

static void PrintUsage()
{
    Console.WriteLine(PatcherInfo.ProductName);
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  pagonia-patcher --version");
    Console.WriteLine("  pagonia-patcher inspect-mod --mod <mod-directory>");
    Console.WriteLine("  pagonia-patcher validate-mod --mod <mod-directory> [--game-root <game-root>]");
    Console.WriteLine("  pagonia-patcher schema-validate --mod <mod-directory>");
    Console.WriteLine("  pagonia-patcher schema-validate --collection <collection-yaml>");
    Console.WriteLine("  pagonia-patcher schema-validate --lock <collection-lock-yaml>");
    Console.WriteLine("  pagonia-patcher schema-validate --repo-index <repo-index-yaml>");
    Console.WriteLine("  pagonia-patcher schema-validate --catalog <catalog-yaml>");
    Console.WriteLine("  pagonia-patcher index-check <repo-root>");
    Console.WriteLine("  pagonia-patcher index build <repo-root> [--check]");
    Console.WriteLine("  pagonia-patcher plan --game <game-root> --mods <mod-directory> [--tweak <mod-id>:<tweak-id>=<value>] [--out <plan.md>] [--json <plan.json>]");
    Console.WriteLine("  pagonia-patcher plan --game <game-root> --collection <collection-yaml> [--collection <collection-yaml>] --mods-root <mods-root> [--out <plan.md>] [--json <plan.json>]");
    Console.WriteLine("  pagonia-patcher plan --game <game-root> --lock <collection-lock-yaml> --mods-root <mods-root> [--out <plan.md>] [--json <plan.json>]");
    Console.WriteLine("  pagonia-patcher apply --game <game-root> --mods <mod-directory> --out <output-game-root> [--tweak <mod-id>:<tweak-id>=<value>] [--report <apply.md>] [--json <apply.json>]");
    Console.WriteLine("  pagonia-patcher apply --game <game-root> --collection <collection-yaml> [--collection <collection-yaml>] --mods-root <mods-root> --out <output-game-root> [--report <apply.md>] [--json <apply.json>]");
    Console.WriteLine("  pagonia-patcher apply --game <game-root> --lock <collection-lock-yaml> --mods-root <mods-root> --out <output-game-root> [--report <apply.md>] [--json <apply.json>]");
    Console.WriteLine("  pagonia-patcher inspect-collection --collection <collection-yaml>");
    Console.WriteLine("  pagonia-patcher resolve-collection --collection <collection-yaml> --mods-root <mods-root> --lock <lock-yaml>");
    Console.WriteLine("  pagonia-patcher export-collection --mods <mod-directory> --out <collection-yaml> --id <id> --name <name>");
    Console.WriteLine("  pagonia-patcher inspect-lock --lock <collection-lock-yaml>");
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine("Exit codes: 0 success, 1 error, 2 conflict, 64 usage.");
}

static void PrintDiagnostics(IEnumerable<PatchDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
    {
        Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}");
    }
}

static PlanCommandResult BuildPlan(
    string gameRoot,
    IReadOnlyList<string> modDirectories,
    ManifestReader reader,
    ManifestValidator validator,
    PatchPlanner planner,
    TweakSelection? tweakSelection = null)
{
    var mods = new List<LoadedMod>();

    foreach (var modDirectory in modDirectories)
    {
        var result = reader.ReadMod(modDirectory);
        PrintDiagnostics(result.Diagnostics);

        if (!result.Success || result.Value is null)
        {
            return new PlanCommandResult(null, PatcherExitCodes.Error);
        }

        mods.Add(result.Value);
    }

    return BuildLoadedModsPlan(gameRoot, mods, validator, planner, tweakSelection);
}

static PlanCommandResult BuildLoadedModsPlan(
    string gameRoot,
    IReadOnlyList<LoadedMod> mods,
    ManifestValidator validator,
    PatchPlanner planner,
    TweakSelection? tweakSelection = null)
{
    foreach (var mod in mods)
    {
        var validationDiagnostics = validator.ValidateMod(mod);
        PrintDiagnostics(validationDiagnostics);

        if (validationDiagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error))
        {
            return new PlanCommandResult(null, PatcherExitCodes.Error);
        }
    }

    return new PlanCommandResult(planner.Plan(gameRoot, mods, tweakSelection), PatcherExitCodes.Success);
}

static void PrintPlan(CombinedPatchPlan? combinedPlan)
{
    if (combinedPlan is null)
    {
        return;
    }

    foreach (var modPlan in combinedPlan.ModPlans)
    {
        PrintDiagnostics(modPlan.Diagnostics);
    }

    PrintDiagnostics(combinedPlan.Diagnostics);

    foreach (var write in combinedPlan.Writes)
    {
        Console.WriteLine($"Write: {write.EntityName}/{write.Component}/{write.Path}: {write.OldValue} -> {write.NewValue}");
    }

    foreach (var conflict in combinedPlan.Conflicts)
    {
        Console.WriteLine($"Conflict: {conflict.Type}: {conflict.TargetKey}");

        foreach (var write in conflict.Writes)
        {
            Console.WriteLine($"  {write.OperationId}: {write.OldValue} -> {write.NewValue}");
        }
    }
}

static (TweakOverrides Overrides, bool Ok) ReadTweakOverrides(IReadOnlyList<string> args)
{
    var (overrides, diagnostics) = TweakOverrides.Parse(ReadOptionValues(args, "--tweak"));
    PrintDiagnostics(diagnostics);
    return (overrides, diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error));
}

// Fold collection-supplied tweak values from a resolved collection set into a selection, under the
// CLI overrides. The planner applies precedence lockfile > CLI > collection > default.
static TweakSelection BuildCollectionTweakSelection(TweakOverrides cliOverrides, IReadOnlyList<ResolvedCollectionMod> mods)
{
    var selection = TweakSelection.ForCli(cliOverrides);
    foreach (var mod in mods)
    {
        selection.WithCollectionValues(mod.LoadedMod.Manifest.Id, mod.CollectionMod.Tweaks);
    }

    return selection;
}

// Fold lockfile-pinned tweak values into a selection, under the CLI overrides. A lockfile pin
// deliberately wins over a CLI override so a re-apply reproduces the recorded substitution.
static TweakSelection BuildLockfileTweakSelection(TweakOverrides cliOverrides, CollectionLock collectionLock)
{
    var selection = TweakSelection.ForCli(cliOverrides);
    foreach (var lockedMod in collectionLock.Mods)
    {
        selection.WithLockfileValues(lockedMod.Id, lockedMod.Tweaks);
    }

    return selection;
}

static IReadOnlyList<string> ReadValuesUntilOption(IReadOnlyList<string> args, int startIndex)
{
    var values = new List<string>();

    for (var index = startIndex; index < args.Count; index++)
    {
        if (args[index].StartsWith("--", StringComparison.Ordinal))
        {
            break;
        }

        values.Add(args[index]);
    }

    return values;
}

static string? ReadOptionValue(IReadOnlyList<string> args, string option)
{
    for (var index = 0; index < args.Count - 1; index++)
    {
        if (args[index] == option)
        {
            return args[index + 1];
        }
    }

    return null;
}

static IReadOnlyList<string> ReadOptionValues(IReadOnlyList<string> args, string option)
{
    var values = new List<string>();

    for (var index = 0; index < args.Count - 1; index++)
    {
        if (args[index] == option)
        {
            values.Add(args[index + 1]);
        }
    }

    return values;
}

static void WriteReports(
    CombinedPatchPlan? plan,
    string? markdownPath,
    string? jsonPath,
    PatchPlanReporter reporter,
    string planSource)
{
    if (plan is null)
    {
        return;
    }

    reporter.WriteReports(plan, markdownPath, jsonPath, planSource);

    if (!string.IsNullOrWhiteSpace(markdownPath))
    {
        Console.WriteLine($"Wrote Markdown report: {markdownPath}");
    }

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        Console.WriteLine($"Wrote JSON report: {jsonPath}");
    }
}

static void WriteApplyReports(
    CombinedPatchPlan? plan,
    IReadOnlyList<PatchDiagnostic> applyDiagnostics,
    string outputGameRoot,
    string? markdownPath,
    string? jsonPath,
    PatchApplyReporter reporter,
    string planSource)
{
    if (plan is null)
    {
        return;
    }

    reporter.WriteReports(plan, applyDiagnostics, outputGameRoot, markdownPath, jsonPath, planSource);

    if (!string.IsNullOrWhiteSpace(markdownPath))
    {
        Console.WriteLine($"Wrote Markdown apply report: {markdownPath}");
    }

    if (!string.IsNullOrWhiteSpace(jsonPath))
    {
        Console.WriteLine($"Wrote JSON apply report: {jsonPath}");
    }
}

static int GetPlanExitCode(PlanCommandResult planResult)
{
    if (planResult.ExitCode != PatcherExitCodes.Success)
    {
        return planResult.ExitCode;
    }

    if (planResult.Plan is null)
    {
        return PatcherExitCodes.Error;
    }

    if (planResult.Plan.Conflicts.Count > 0)
    {
        return PatcherExitCodes.Conflict;
    }

    return planResult.Plan.Success
        ? PatcherExitCodes.Success
        : PatcherExitCodes.Error;
}

internal sealed record PlanCommandResult(CombinedPatchPlan? Plan, int ExitCode);
