using PagoniaLand.Patcher;

var root = FindRepositoryRoot();
var patcherRoot = Path.Combine(root, "tools", "pagonia-patcher");
var reader = new ManifestReader();
var validator = new ManifestValidator();
var planner = new PatchPlanner();
var applier = new PatchApplier();
var reporter = new PatchPlanReporter();
var applyReporter = new PatchApplyReporter();
var collectionResolver = new CollectionResolver();
var collectionExporter = new CollectionExporter();
var schemaValidator = new SchemaValidator();
var advisor = new EntityRelationAdvisor();

// JsonSchema.Net auto-registers a schema by its $id on FromFile and refuses to
// register the same $id twice in a process. Cache by path so multiple tests can
// validate against the same schema file without "Overwriting registered schemas".
var schemaCache = new Dictionary<string, Json.Schema.JsonSchema>();

var tests = new (string Name, Func<bool> Run)[]
{
    ("product name is stable", () => PatcherInfo.ProductName == "Pagonia Land Patcher"),
    ("command name is stable", () => PatcherInfo.CommandName == "pagonia-patcher"),
    ("version is present", () => !string.IsNullOrWhiteSpace(PatcherInfo.Version)),
    ("exit codes are stable", ExitCodesAreStable),
    ("fixture mod can be read", FixtureModCanBeRead),
    ("fixture patch file can be read", FixturePatchFileCanBeRead),
    ("collection example can be read", CollectionExampleCanBeRead),
    ("collection lock example can be read", CollectionLockExampleCanBeRead),
    ("invalid yaml reports readable diagnostic", InvalidYamlReportsDiagnostic),
    ("valid fixture mod passes validation", ValidFixtureModPassesValidation),
    ("broken fixture mod fails validation", BrokenFixtureModFailsValidation),
    ("valid fixture mod produces patch plan", ValidFixtureModProducesPatchPlan),
    ("missing target fixture fails planning", MissingTargetFixtureFailsPlanning),
    ("expected value mismatch fixture fails planning", ExpectedValueMismatchFixtureFailsPlanning),
    ("conflicting fixture mods fail combined planning", ConflictingFixtureModsFailCombinedPlanning),
    ("valid fixture mod applies to output folder", ValidFixtureModAppliesToOutputFolder),
    ("patch plan reports can be written", PatchPlanReportsCanBeWritten),
    ("local collection can be resolved", LocalCollectionCanBeResolved),
    ("collection lockfile can be written", CollectionLockfileCanBeWritten),
    ("lockfile: writer emits v0.1 with empty source/resolvedAt for local resolves", LockfileWriterEmitsCurrentForLocalResolve),
    ("lockfile: reader accepts a minimal v0.1 lockfile", LockfileReaderAcceptsV01),
    ("lockfile: reader rejects unknown future version with structured diagnostic", LockfileReaderRejectsUnknownVersion),
    ("lockfile: schema-validate passes v0.1 with per-mod source + resolvedAt", LockfileSchemaValidateAcceptsRemoteFields),
    ("direct mod set can be exported as collection", DirectModSetCanBeExportedAsCollection),
    ("collection can be planned", CollectionCanBePlanned),
    ("multiple collections can be resolved", MultipleCollectionsCanBeResolved),
    ("multiple collections reject version conflicts", MultipleCollectionsRejectVersionConflicts),
    ("multiple collections reject GameDatabase version conflicts", MultipleCollectionsRejectGameDatabaseVersionConflicts),
    ("local collection can be applied to output folder", LocalCollectionCanBeApplied),
    ("multiple collections can be applied to output folder", MultipleCollectionsCanBeApplied),
    ("mod manifest carries metadata fields", ModManifestCarriesMetadataFields),
    ("mod manifest parses entries replace/add/delete", ModManifestParsesEntries),
    ("mod manifest with only entries (no patches) parses", ModManifestEntriesOnly),
    ("entry operations: missing source reports diagnostic", EntryOperationsMissingSourceDiagnostic),
    ("entry operations: apply replace/add/delete materialises sandbox/out", EntryOperationsApplyRoundTrip),
    ("entry operations: two mods replacing same path conflict", EntryOperationsConflict),
    ("entry operations: two mods deleting same path do not conflict", EntryOperationsTwoDeletesAreIdempotent),
    ("pak scaffold: mod with pak + added gd.xml writes manifest/files/.gd.bin/memory", PakScaffoldWritesAllFourFiles),
    ("pak scaffold: mod with pak but no gd.xml skips files.json and .gd.bin", PakScaffoldSkipsFilesAndGdBinWhenNoXml),
    ("pak scaffold: pak.name with slash reports scaffoldNameInvalid", PakScaffoldRejectsNameWithSlash),
    ("pak scaffold: empty pak.dependencies defaults to [core]", PakScaffoldDefaultsDependenciesToCore),
    ("collection manifest carries safety and metadata fields", CollectionManifestCarriesSafetyAndMetadataFields),
    ("safety states parse from true false and unknown", SafetyStatesParseFromTrueFalseAndUnknown),
    ("invalid safety value reports a readable diagnostic", InvalidSafetyValueReportsDiagnostic),
    ("apply reports can be written for direct mods", ApplyReportsCanBeWrittenForDirectMods),
    ("apply reports can be written for a collection", ApplyReportsCanBeWrittenForCollection),
    ("lockfile can be planned and applied", LockfileCanBePlannedAndApplied),
    ("lockfile mod missing produces a readable diagnostic", LockfileModMissingReportsDiagnostic),
    ("lockfile archive hash mismatch produces a readable diagnostic", LockfileHashMismatchReportsDiagnostic),
    ("replaceAttribute fixture plans and applies cleanly", ReplaceAttributePlansAndApplies),
    ("replaceNode fixture plans and applies cleanly", ReplaceNodePlansAndApplies),
    ("replaceAttribute and replaceValue on same node do not conflict", ReplaceAttributeAndReplaceValueDoNotConflict),
    ("two replaceAttribute writes on the same attribute conflict", TwoReplaceAttributeWritesConflict),
    ("addListItem fixture plans and applies cleanly", AddListItemPlansAndApplies),
    ("removeListItem fixture plans and applies cleanly", RemoveListItemPlansAndApplies),
    ("addListItem missing target list item reports diagnostic", RemoveListItemMissingItemReportsDiagnostic),
    ("two addListItem with different items do not conflict", TwoAddListItemsWithDifferentItemsDoNotConflict),
    ("addListItem and removeListItem targeting the same item conflict", AddAndRemoveTargetingSameItemConflict),
    ("addEntity fixture plans and applies cleanly", AddEntityPlansAndApplies),
    ("removeEntity round trip plans and applies cleanly", RemoveEntityPlansAndApplies),
    ("mergeComponent fixture plans and applies cleanly", MergeComponentPlansAndApplies),
    ("addEntity with duplicate GUID reports diagnostic", AddEntityDuplicateGuidReportsDiagnostic),
    ("two addEntity writes for same GUID conflict", TwoAddEntityWritesConflict),
    ("schema-validate passes for cheaper-sawmill fixture", SchemaValidateAcceptsCheaperSawmill),
    ("schema-validate fails broken-manifest with id pattern violation", SchemaValidateRejectsBrokenManifest),
    ("schema-validate passes the sanctuary-add-custom-ability example (pak + entries + patches)", SchemaValidateAcceptsSanctuaryExample),
    ("schema-validate handles boolean yaml values without type coercion", SchemaValidateHandlesBooleanScalars),
    ("schema-validate passes for the bundled collection example", SchemaValidateAcceptsCollectionExample),
    ("schema-validate passes for the example-mods repo index", SchemaValidateAcceptsRepoIndexExample),
    ("schema-validate rejects repo index with bad mod id pattern", SchemaValidateRejectsRepoIndexBadModId),
    ("schema-validate rejects repo index with unknown property on mod entry", SchemaValidateRejectsRepoIndexUnknownProperty),
    ("schema-validate rejects repo index with unknown indexFormatVersion", SchemaValidateRejectsRepoIndexUnknownVersion),
    ("relativePath: repo index rejects traversal in mod path (..)", SchemaValidateRejectsRepoIndexTraversal),
    ("relativePath: repo index rejects leading slash in mod path", SchemaValidateRejectsRepoIndexLeadingSlash),
    ("relativePath: repo index rejects drive letter in mod path", SchemaValidateRejectsRepoIndexDriveLetter),
    ("relativePath: repo index rejects backslash separator in mod path", SchemaValidateRejectsRepoIndexBackslash),
    ("relativePath: collection rejects traversal in previewImages", SchemaValidateRejectsCollectionTraversal),
    ("relativePath: collection rejects drive letter in previewImages", SchemaValidateRejectsCollectionDriveLetter),
    ("schema-validate passes for the example catalog (top-level + nested federation reference)", SchemaValidateAcceptsCatalogExample),
    ("schema-validate passes for the example sub-catalog (leaf, federation target)", SchemaValidateAcceptsCatalogSubExample),
    ("schema-validate rejects catalog with unknown property on repo entry", SchemaValidateRejectsCatalogUnknownProperty),
    ("schema-validate rejects catalog with unknown catalogFormatVersion", SchemaValidateRejectsCatalogUnknownVersion),
    ("schema-validate rejects catalog repo entry with invalid owner chars", SchemaValidateRejectsCatalogBadOwner),
    ("schema roundtrip: patch-plan-report", SchemaRoundtripPatchPlanReport),
    ("schema roundtrip: patch-apply-report", SchemaRoundtripPatchApplyReport),
    ("schema roundtrip: patch-plan-report carries arithmetic ops (multiplyValue)", SchemaRoundtripPatchPlanReportArithmetic),
    ("schema roundtrip: patch-apply-report carries arithmetic ops (multiplyValue)", SchemaRoundtripPatchApplyReportArithmetic),
    ("tweaks: tweakable fixture parses number/boolean/enum declarations", TweakableFixtureParsesTweaks),
    ("tweaks: tweakable fixture passes validation", TweakableFixturePassesValidation),
    ("tweaks: default outside min..max fails validation", TweakDefaultOutOfRangeFailsValidation),
    ("tweaks: a fractional integer default fails validation", IntegerTweakFractionalDefaultFailsValidation),
    ("apply: output overlapping the source game root is refused before any wipe", ApplyRefusesOutputOverlappingSource),
    ("plan: a malformed target path (empty segment / no predicate element) fails with targetPathMalformed", MalformedPathFailsWithSpecificDiagnostic),
    ("tweaks: duplicate id via alias collision fails validation", TweakDuplicateIdFailsValidation),
    ("tweaks: schema-validate passes the tweakable fixture", SchemaValidateAcceptsTweakableFixture),
    ("tweaks: schema-validate rejects a malformed tweak block", SchemaValidateRejectsMalformedTweaks),
    ("templating: resolver substitutes a literal number value", TweakResolverSubstitutesLiteral),
    ("templating: resolver evaluates the boolean ternary form", TweakResolverEvaluatesTernary),
    ("templating: resolver substitutes an enum value", TweakResolverSubstitutesEnum),
    ("templating: resolver throws on an undeclared tweak reference", TweakResolverThrowsOnUndeclared),
    ("templating: resolver throws on malformed placeholder syntax", TweakResolverThrowsOnMalformed),
    ("templating: --tweak override parses; malformed flag reports diagnostic", TweakOverridesParse),
    ("templating: templated fixture plan resolves the default into the write", TemplatedFixturePlanResolvesDefault),
    ("templating: --tweak override changes the resolved write", TemplatedFixtureOverrideChangesWrite),
    ("templating: out-of-range external override warns but still resolves", TweakOutOfRangeOverrideWarnsButResolves),
    ("templating: undeclared placeholder fails planning with tweakUndeclared", UndeclaredPlaceholderFailsPlanning),
    ("templating: end-to-end plan to apply produces the tweaked XML", TemplatedFixtureAppliesTweakedXml),
    ("arithmetic: multiplyValue fixture plans and applies the scaled value", MultiplyValueFixturePlansAndApplies),
    ("arithmetic: multiplyValue clamps a low result up to clampMin", MultiplyValueClampsLowResult),
    ("tweaks: an undeclared placeholder in clampMin is detected (tweakUndeclared), not a 'not numeric' failure", ClampMinPlaceholderDetectsUndeclared),
    ("tweaks: an out-of-set enum / non-boolean CLI override warns tweakValueInvalid", CliTweakInvalidEnumOrBoolWarns),
    ("arithmetic: parser rejects NaN/Infinity, Format guards overflow, clamp bounds round", ArithmeticOpsGuardParseFormatClamp),
    ("arithmetic: addValue adds a delta to the vanilla value", AddValuePlansAndApplies),
    ("arithmetic: ceil rounding rounds a fractional result up", ArithmeticCeilRoundingRoundsUp),
    ("arithmetic: a non-numeric operand fails planning", ArithmeticNonNumericOperandFailsPlanning),
    ("arithmetic: expectedOldValue drift fails planning", ArithmeticExpectedOldValueMismatchFailsPlanning),
    ("arithmetic: schema-validate passes the multiplyValue fixture", SchemaValidateAcceptsMultiplyFixture),
    ("arithmetic: multiplyValue and replaceValue on same target conflict", MultiplyAndReplaceSameTargetConflict),
    ("arithmetic: clampMin greater than clampMax warns", LintWarnsOnClampMinGreaterThanMax),
    ("arithmetic: shared Compute rounds, clamps, and adds identically to apply", ArithmeticPatchOpsComputeBehaves),
    ("arithmetic: usage scanner reports the multiplier wiring for a tweak", TweakUsageScannerReportsMultiplier),
    ("predicate: whitespace around '=' resolves the same as no spaces", PredicateWithWhitespaceResolves),
    ("templating: plan JSON report carries resolvedTweaks", PlanReportCarriesResolvedTweaks),
    ("collection tweaks: selection precedence is lockfile > cli > collection", TweakSelectionPrecedence),
    ("collection tweaks: a curator override resolves and applies", CollectionTweakOverrideResolvesAndApplies),
    ("collection tweaks: resolve writes a lockfile pinning effective tweak values", ResolveWritesLockfileWithTweaks),
    ("collection tweaks: a lockfile pin is followed back into the plan", LockfilePinFollowedIntoPlan),
    ("collection tweaks: a lockfile value under a legacy alias is followed forward", LockfileAliasFollowedForward),
    ("collection tweaks: apply JSON report carries resolvedTweaks", ApplyReportCarriesResolvedTweaks),
    ("tweak lint: a declared-but-unused tweak warns (no error)", LintWarnsOnUnusedTweak),
    ("tweak lint: a referenced tweak is not flagged as unused", LintAllowsReferencedTweak),
    ("tweak lint: a ternary on a non-boolean tweak warns", LintWarnsOnTernaryOnNonBoolean),
    ("tweak lint: min greater than max warns", LintWarnsOnMinGreaterThanMax),
    ("apply: a cancelled token aborts Apply before writing the staging tree", ApplyHonoursCancellationToken),
    ("patchSets: an optional set is skipped when its package is absent", OptionalPatchSetSkippedWhenPackageAbsent),
    ("patchSets: a set is applied when its required package is present", PatchSetAppliedWhenPackagePresent),
    ("advisor: a Replace overlay is flagged Info, never Error", AdvisorFlagsReplaceAsInfo),
    ("advisor: unloading a still-referenced entity warns", AdvisorWarnsOnUnloadOfReferencedEntity),
    ("advisor: an additive Incremental overlay is silent", AdvisorIsSilentOnAdditiveIncremental),
    ("advisor: shipped dlc1 produces no warnings (calibration)", AdvisorIsQuietOnShippedDlc1),
    ("advisor base-aware: unload of a base-referenced entity warns with --game-root", AdvisorBaseAwareWarnsUnloadReferencedInBaseGame),
    ("advisor base-aware: that same unload is clean base-free (no game-root)", AdvisorBaseFreeSilentOnBaseOnlyReference),
    ("advisor base-aware: a purely additive Replace is flagged as Incremental-able", AdvisorBaseAwareFlagsAdditiveReplace),
    ("advisor base-aware: a modifying Replace is not flagged", AdvisorBaseAwareSilentOnModifyingReplace),
    ("advisor base-aware: shipped dlc1 stays warning-free against the full game-gdb (calibration)", AdvisorBaseAwareQuietOnShippedDlc1),
};

var failed = 0;

foreach (var test in tests)
{
    try
    {
        if (test.Run())
        {
            Console.WriteLine($"PASS {test.Name}");
            continue;
        }

        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");
    }
}

if (failed == 0)
{
    Console.WriteLine($"All {tests.Length} tests passed.");
    return 0;
}

Console.Error.WriteLine($"{failed} of {tests.Length} test(s) failed.");
return 1;

bool FixtureModCanBeRead()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var result = reader.ReadMod(modPath);
    return result.Success
        && result.Value?.Manifest.Id == "pagonia-land.fixture.cheaper-sawmill"
        && result.Value.PatchFiles.Count == 1;
}

bool ExitCodesAreStable()
    => PatcherExitCodes.Success == 0
        && PatcherExitCodes.Error == 1
        && PatcherExitCodes.Conflict == 2
        && PatcherExitCodes.Usage == 64;

bool FixturePatchFileCanBeRead()
{
    var patchPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill", "patches", "buildings.yaml");
    var result = reader.ReadPatchFile(patchPath);
    return result.Success
        && result.Value?.Operations.Count == 1
        && result.Value.Operations[0].Target.EntityName == "Sawmill";
}

bool CollectionExampleCanBeRead()
{
    var collectionPath = Path.Combine(root, "docs", "examples", "collections", "beginner-qol.collection.yaml");
    var result = reader.ReadCollectionManifest(collectionPath);
    return result.Success
        && result.Value?.Id == "pagonia-land.collections.beginner-qol"
        && result.Value.Mods.Count == 2;
}

bool CollectionLockExampleCanBeRead()
{
    var lockPath = Path.Combine(root, "docs", "examples", "collections", "beginner-qol.collection-lock.yaml");
    var result = reader.ReadCollectionLock(lockPath);
    return result.Success
        && result.Value?.CollectionId == "pagonia-land.collections.beginner-qol"
        && result.Value.Mods.Count == 2;
}

bool InvalidYamlReportsDiagnostic()
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-invalid-{Guid.NewGuid():N}.yaml");
    File.WriteAllText(tempPath, "id: [broken");

    try
    {
        var result = reader.ReadPatchFile(tempPath);
        return !result.Success
            && result.Diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error
                && diagnostic.Message.Contains("YAML", StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        File.Delete(tempPath);
    }
}

bool ValidFixtureModPassesValidation()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    var diagnostics = validator.ValidateMod(result.Value);
    return diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}

bool BrokenFixtureModFailsValidation()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "broken-manifest");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    var diagnostics = validator.ValidateMod(result.Value);
    return diagnostics.Any(diagnostic => diagnostic.Code == "invalidId")
        && diagnostics.Any(diagnostic => diagnostic.Code == "invalidGameDatabaseVersion")
        && diagnostics.Any(diagnostic => diagnostic.Code == "unknownPackage")
        && diagnostics.Any(diagnostic => diagnostic.Code == "duplicateOperationId");
}

bool TweakableFixtureParsesTweaks()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var result = reader.ReadModManifest(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var tweaks = result.Value.Tweaks;
    if (tweaks.Count != 3) { return false; }

    var cost = tweaks.FirstOrDefault(t => t.Id == "softwood-cost");
    var upkeep = tweaks.FirstOrDefault(t => t.Id == "free-upkeep");
    var difficulty = tweaks.FirstOrDefault(t => t.Id == "difficulty");

    return cost is { Type: "integer", Default: "3", Min: 1, Max: 8 }
        && upkeep is { Type: "boolean", Default: "false" }
        && difficulty is { Type: "enum", Default: "standard" }
        && difficulty.Values.Count == 3
        && difficulty.Values[1] is { Value: "standard", Label: "Standard" }
        && difficulty.Aliases.SequenceEqual(["difficulty-level"]);
}

bool TweakableFixturePassesValidation()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var diagnostics = validator.ValidateMod(result.Value);
    return diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error);
}

bool IntegerTweakFractionalDefaultFailsValidation()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-int-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.tweak-int
name: Tweak Integer Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: An integer tweak whose default is fractional.
requiredPackages:
  - core
entries:
  add:
    - path: core/textures/placeholder.bc.texture
      source: entries/placeholder.bc.texture
tweaks:
  - id: cost
    type: integer
    label: Cost
    default: 3.5
    min: 1
    max: 8
""");
    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }
        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.TweakDefaultNotInteger);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool MalformedPathFailsWithSpecificDiagnostic()
{
    // A doubled '/' (empty segment) and a predicate with no element name must each fail planning
    // with the specific targetPathMalformed, not the generic targetPathMissing.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var guid = "c732cb26-7487-4a7b-b1ba-b65e094f9bac";

    bool Fails(string path)
    {
        var tempRoot = WriteTempArithmeticMod("badpath", $"""
operations:
  - id: bad-path
    operation: replaceValue
    risk: low
    reason: malformed-path test
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: {guid}
      entityName: Sawmill
      component: AspectBuildup
      path: "{path}"
    expectedOldValue: "4"
    value: "9"
""");
        try
        {
            var read = reader.ReadMod(tempRoot);
            if (!read.Success || read.Value is null) { return false; }
            var plan = planner.Plan(gameRoot, [read.Value]);
            return !plan.Success
                && plan.ModPlans.SelectMany(p => p.Diagnostics).Any(d => d.Code == DiagnosticCodes.TargetPathMalformed);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
        }
    }

    return Fails("Costs//Item")
        && Fails("[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount");
}

bool ApplyRefusesOutputOverlappingSource()
{
    // Apply wipes the output before writing, so output == source (or one nested in the other)
    // must be refused up front — otherwise the wipe destroys the source. Use a temp dir as both
    // source and output with a sentinel file; the refusal must leave the sentinel intact.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-overlap-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    try
    {
        var sentinel = Path.Combine(tempRoot, "sentinel.txt");
        File.WriteAllText(sentinel, "keep me");

        var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
        var read = reader.ReadMod(Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill"));
        if (!read.Success || read.Value is null) { return false; }
        var plan = planner.Plan(gameRoot, [read.Value]);
        if (!plan.Success) { return false; }

        var diagnostics = applier.Apply(tempRoot, tempRoot, plan);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.ApplyOutputOverlapsSource
                && d.Severity == PatchDiagnosticSeverity.Error)
            && File.Exists(sentinel);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool TweakDefaultOutOfRangeFailsValidation()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-range-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.tweak-range
name: Tweak Range Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: A numeric tweak whose default falls outside the declared range.
requiredPackages:
  - core
entries:
  add:
    - path: core/textures/placeholder.bc.texture
      source: entries/placeholder.bc.texture
tweaks:
  - id: cost
    type: integer
    label: Cost
    default: 99
    min: 1
    max: 8
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.TweakDefaultOutOfRange);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool TweakDuplicateIdFailsValidation()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-dup-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.tweak-dup
name: Tweak Dup Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: A second tweak's alias collides with the first tweak's id.
requiredPackages:
  - core
entries:
  add:
    - path: core/textures/placeholder.bc.texture
      source: entries/placeholder.bc.texture
tweaks:
  - id: cost
    type: integer
    label: Cost
    default: 3
  - id: speed
    type: number
    label: Speed
    default: 1.0
    aliases:
      - cost
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.DuplicateTweakId);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool SchemaValidateAcceptsTweakableFixture()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var diagnostics = schemaValidator.ValidateMod(modPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool SchemaValidateRejectsMalformedTweaks()
{
    // An enum tweak missing its required `values` array, with an id that breaks the pattern.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-malformed-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.tweak-malformed
name: Tweak Malformed Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Enum tweak missing values plus an id that violates the pattern.
requiredPackages:
  - core
entries:
  add:
    - path: core/textures/placeholder.bc.texture
      source: entries/placeholder.bc.texture
tweaks:
  - id: Bad_Id
    type: enum
    label: Broken
    default: x
""");

    try
    {
        var diagnostics = schemaValidator.ValidateMod(tempRoot);
        var errors = diagnostics.Where(d => d.Severity == PatchDiagnosticSeverity.Error).ToList();
        return errors.Count > 0
            && errors.All(d => d.Code == DiagnosticCodes.SchemaValidationFailed)
            && errors.Any(d => d.Message.Contains("/tweaks/0", StringComparison.Ordinal));
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool TweakResolverSubstitutesLiteral()
{
    var resolver = new TweakResolver();
    var resolutions = new List<TweakPlaceholderResolution>();
    var result = resolver.Resolve("{{ tweaks.cost }}", new Dictionary<string, string> { ["cost"] = "3" }, resolutions);
    return result == "3" && resolutions.Count == 1 && resolutions[0].TweakId == "cost" && resolutions[0].ResolvedText == "3";
}

bool TweakResolverEvaluatesTernary()
{
    var resolver = new TweakResolver();
    var on = resolver.Resolve("{{ tweaks.flag ? 'NoUpkeep' : 'Normal' }}", new Dictionary<string, string> { ["flag"] = "true" }, new());
    var off = resolver.Resolve("{{ tweaks.flag ? 'NoUpkeep' : 'Normal' }}", new Dictionary<string, string> { ["flag"] = "false" }, new());
    return on == "NoUpkeep" && off == "Normal";
}

bool TweakResolverSubstitutesEnum()
{
    var resolver = new TweakResolver();
    var result = resolver.Resolve("Difficulty_{{ tweaks.difficulty }}", new Dictionary<string, string> { ["difficulty"] = "hardcore" }, new());
    return result == "Difficulty_hardcore";
}

bool TweakResolverThrowsOnUndeclared()
{
    var resolver = new TweakResolver();
    try
    {
        resolver.Resolve("{{ tweaks.missing }}", new Dictionary<string, string>(), new());
        return false;
    }
    catch (TweakResolutionError error)
    {
        return error.Kind == TweakResolutionErrorKind.UndeclaredTweak && error.TweakId == "missing";
    }
}

bool TweakResolverThrowsOnMalformed()
{
    var resolver = new TweakResolver();
    try
    {
        resolver.Resolve("{{ tweaks.flag ? 'a' }}", new Dictionary<string, string> { ["flag"] = "true" }, new());
        return false;
    }
    catch (TweakResolutionError error)
    {
        return error.Kind == TweakResolutionErrorKind.MalformedSyntax;
    }
}

bool TweakOverridesParse()
{
    var (good, goodDiag) = TweakOverrides.Parse(["mod.a:cost=5", "mod.a:flag=true"]);
    var (_, badDiag) = TweakOverrides.Parse(["nonsense"]);
    return goodDiag.Count == 0
        && good.ForMod("mod.a")?["cost"] == "5"
        && good.ForMod("mod.a")?["flag"] == "true"
        && good.ForMod("mod.b") is null
        && badDiag.Any(d => d.Code == DiagnosticCodes.TweakOverrideMalformed);
}

bool TemplatedFixturePlanResolvesDefault()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var plan = planner.Plan(gameRoot, result.Value);
    return plan.Success
        && plan.Writes.Count == 1
        && plan.Writes[0].NewValue == "2"
        && plan.ResolvedTweaks.Any(t => t.TweakId == "softwood-cost" && t.ResolvedValue == "2" && t.Origin == "default")
        && plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.TweakValueResolved);
}

bool TemplatedFixtureOverrideChangesWrite()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var (overrides, _) = TweakOverrides.Parse(["pagonia-land.fixture.templated-sawmill:softwood-cost=5"]);
    var plan = planner.Plan(gameRoot, result.Value, TweakSelection.ForCli(overrides));
    return plan.Success
        && plan.Writes.Count == 1
        && plan.Writes[0].NewValue == "5"
        && plan.ResolvedTweaks.Any(t => t.TweakId == "softwood-cost" && t.ResolvedValue == "5" && t.Origin == "external");
}

bool TweakOutOfRangeOverrideWarnsButResolves()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    // 99 is above the declared max of 8 — warns but still substitutes into the write.
    var (overrides, _) = TweakOverrides.Parse(["pagonia-land.fixture.templated-sawmill:softwood-cost=99"]);
    var plan = planner.Plan(gameRoot, result.Value, TweakSelection.ForCli(overrides));
    return plan.Success
        && plan.Writes.Count == 1
        && plan.Writes[0].NewValue == "99"
        && plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.TweakValueOutOfRange && d.Severity == PatchDiagnosticSeverity.Warning);
}

bool UndeclaredPlaceholderFailsPlanning()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-undeclared-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.undeclared-tweak
name: Undeclared Tweak
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: References a tweak placeholder without declaring it.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
    File.WriteAllText(Path.Combine(tempRoot, "patches", "buildings.yaml"), """
operations:
  - id: undeclared-op
    operation: replaceValue
    risk: low
    reason: References an undeclared tweak.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.ghost }}"
""");

    try
    {
        var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, result.Value);
        return !plan.Success && plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.TweakUndeclared);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool PredicateWithWhitespaceResolves()
{
    // Regression: a naturally-formatted predicate with spaces around '='
    // (`Content/Resource = '...'`) must resolve the same as the no-space form.
    // Before the trim fix, the leading space left the opening quote in place
    // so the value comparison never matched and the target didn't resolve.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-predicate-ws-{Guid.NewGuid():N}");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-predicate-ws-out-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.predicate-whitespace
name: Predicate Whitespace
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Predicate formatted with spaces around the equals sign.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
    File.WriteAllText(Path.Combine(tempRoot, "patches", "buildings.yaml"), """
operations:
  - id: spaced-predicate-op
    operation: replaceValue
    risk: low
    reason: Predicate with whitespace around '='.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource = 'c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "7"
""");

    try
    {
        var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var (overrides, _) = TweakOverrides.Parse([]);
        var plan = planner.Plan(gameRoot, [result.Value], TweakSelection.ForCli(overrides));
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputXml = File.ReadAllText(Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml"));

        // The spaced predicate resolved + the value changed (no ExpectedOldValue
        // mismatch). A failed resolution would leave Amount at 4.
        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>7</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool TemplatedFixtureAppliesTweakedXml()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-tweak-apply-{Guid.NewGuid():N}");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    try
    {
        var (overrides, _) = TweakOverrides.Parse(["pagonia-land.fixture.templated-sawmill:softwood-cost=6"]);
        var plan = planner.Plan(gameRoot, [result.Value], TweakSelection.ForCli(overrides));
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputXml = File.ReadAllText(Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml"));
        var sourceXml = File.ReadAllText(Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml"));

        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>6</Amount>", StringComparison.Ordinal)
            && sourceXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool ArithmeticOpsGuardParseFormatClamp()
{
    // NaN / ±Infinity are rejected by the shared parser (so they surface as
    // "not numeric" rather than computing garbage); ordinary + exponent forms still parse.
    if (ArithmeticPatchOps.TryParse("NaN", out _)) { return false; }
    if (ArithmeticPatchOps.TryParse("Infinity", out _)) { return false; }
    if (ArithmeticPatchOps.TryParse("-Infinity", out _)) { return false; }
    if (!ArithmeticPatchOps.TryParse("1E3", out var exp) || exp != 1000) { return false; }
    if (!ArithmeticPatchOps.TryParse("4", out var four) || four != 4) { return false; }

    // a whole value beyond long range formats as a plain integer (no overflow, no exponent).
    var big = ArithmeticPatchOps.Format(9.3e18);
    if (big.Contains('E') || big.Contains('.')) { return false; }
    if (ArithmeticPatchOps.Format(6.0) != "6") { return false; }

    // a fractional clamp bound is rounded with the same policy, so the clamped result
    // stays integral — 4 * 0.1 = 0.4 rounds to 0, clampMin "1.5" rounds to 2, result "2".
    var clampedResult = ArithmeticPatchOps.Compute(PatchOperationTypes.MultiplyValue, 4, 0.1, "round", 1.5, null, out var didClamp);
    return clampedResult == "2" && didClamp;
}

bool ClampMinPlaceholderDetectsUndeclared()
{
    // A {{ tweaks.* }} in clampMin used to be copied through verbatim (never resolved, never
    // flagged), failing later with a confusing "not numeric". It must now be treated as a
    // placeholder: an undeclared id reports tweakUndeclared up front.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var tempRoot = WriteTempArithmeticMod("clampph", """
operations:
  - id: clamp-op
    operation: multiplyValue
    risk: low
    reason: Scale the Sawmill cost with a tweak-driven floor.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    factor: "0.1"
    clampMin: "{{ tweaks.nope }}"
""");
    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [result.Value]);
        return !plan.Success
            && plan.ModPlans.SelectMany(p => p.Diagnostics)
                .Any(d => d.Code == DiagnosticCodes.TweakUndeclared);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool CliTweakInvalidEnumOrBoolWarns()
{
    // A direct patcher --tweak (unlike the manager) reaches the planner unchecked. An out-of-set
    // enum value and a non-boolean must each surface a tweakValueInvalid warning.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var (overrides, _) = TweakOverrides.Parse([
        "pagonia-land.fixture.tweakable-sawmill:difficulty=banana",
        "pagonia-land.fixture.tweakable-sawmill:free-upkeep=maybe"]);
    var plan = planner.Plan(gameRoot, [result.Value], TweakSelection.ForCli(overrides));

    return plan.ModPlans.SelectMany(p => p.Diagnostics)
        .Count(d => d.Code == DiagnosticCodes.TweakValueInvalid) == 2;
}

string WriteTempArithmeticMod(string idSuffix, string operationsYaml)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-arith-{idSuffix}-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: pagonia-land.test.arith-{idSuffix}
name: Arithmetic Test {idSuffix}
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Inline arithmetic-op fixture for {idSuffix}.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
    File.WriteAllText(Path.Combine(tempRoot, "patches", "buildings.yaml"), operationsYaml);
    return tempRoot;
}

bool MultiplyValueFixturePlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-multiply-apply-{Guid.NewGuid():N}");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    try
    {
        // Default cost-multiplier is 1.5: 4 * 1.5 = 6, rounded, above clampMin 1.
        var plan = planner.Plan(gameRoot, [result.Value]);
        if (!plan.Success
            || plan.Writes.Single() is not { OldValue: "4", NewValue: "6", OperationType: "multiplyValue" })
        {
            return false;
        }

        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputXml = File.ReadAllText(Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml"));
        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>6</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool MultiplyValueClampsLowResult()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    // 4 * 0.1 = 0.4, rounds to 0, then clampMin 1 floors it back up to 1.
    var (overrides, _) = TweakOverrides.Parse(["pagonia-land.fixture.multiply-sawmill:cost-multiplier=0.1"]);
    var plan = planner.Plan(gameRoot, [result.Value], TweakSelection.ForCli(overrides));
    return plan.Success
        && plan.Writes.Single().NewValue == "1"
        && plan.ModPlans.Single().Diagnostics.Any(d => d.Code == DiagnosticCodes.ArithmeticResultClamped);
}

bool AddValuePlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-apply-{Guid.NewGuid():N}");
    var tempRoot = WriteTempArithmeticMod("addvalue", """
operations:
  - id: add-op
    operation: addValue
    risk: low
    reason: Add two softwood to the Sawmill cost.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    delta: "2"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [result.Value]);
        if (!plan.Success || plan.Writes.Single() is not { OldValue: "4", NewValue: "6", OperationType: "addValue" })
        {
            return false;
        }

        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputXml = File.ReadAllText(Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml"));
        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>6</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool ArithmeticCeilRoundingRoundsUp()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var tempRoot = WriteTempArithmeticMod("ceil", """
operations:
  - id: ceil-op
    operation: multiplyValue
    risk: low
    reason: 4 * 1.6 = 6.4, ceil rounds to 7.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    factor: "1.6"
    rounding: ceil
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [result.Value]);
        return plan.Success && plan.Writes.Single().NewValue == "7";
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool ArithmeticNonNumericOperandFailsPlanning()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var tempRoot = WriteTempArithmeticMod("nonnumeric", """
operations:
  - id: bad-factor-op
    operation: multiplyValue
    risk: low
    reason: A factor that is not a number must fail planning.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    factor: "abc"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [result.Value]);
        return !plan.Success
            && plan.ModPlans.Single().Diagnostics.Any(d => d.Code == DiagnosticCodes.ArithmeticOperandNotNumeric);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool ArithmeticExpectedOldValueMismatchFailsPlanning()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var tempRoot = WriteTempArithmeticMod("drift", """
operations:
  - id: drift-op
    operation: multiplyValue
    risk: low
    reason: expectedOldValue 9 does not match the vanilla 4.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "9"
    factor: "2"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [result.Value]);
        return !plan.Success
            && plan.ModPlans.Single().Diagnostics.Any(d => d.Code == DiagnosticCodes.ExpectedOldValueMismatch);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool SchemaValidateAcceptsMultiplyFixture()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var diagnostics = schemaValidator.ValidateMod(modPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool MultiplyAndReplaceSameTargetConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var multiply = reader.ReadMod(Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill"));
    var replace = reader.ReadMod(Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill"));
    if (!multiply.Success || multiply.Value is null || !replace.Success || replace.Value is null) { return false; }

    // Both write the same Sawmill softwood Amount: a multiplyValue and a replaceValue collide as a
    // single-target conflict regardless of the differing computed values.
    var plan = planner.Plan(gameRoot, [multiply.Value, replace.Value]);
    return !plan.Success
        && plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.DuplicateWriteTarget);
}

bool LintWarnsOnClampMinGreaterThanMax()
{
    var tempRoot = WriteTempArithmeticMod("clamp-order", """
operations:
  - id: clamp-op
    operation: multiplyValue
    risk: low
    reason: clampMin above clampMax should warn.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    factor: "2"
    clampMin: "10"
    clampMax: "2"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.ClampMinGreaterThanMax
            && d.Severity == PatchDiagnosticSeverity.Warning);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool ArithmeticPatchOpsComputeBehaves()
{
    var multiply = ArithmeticPatchOps.Compute(PatchOperationTypes.MultiplyValue, 4, 2.5, "round", null, null, out var mClamped);
    var add = ArithmeticPatchOps.Compute(PatchOperationTypes.AddValue, 4, 2, null, null, null, out _);
    var ceil = ArithmeticPatchOps.Compute(PatchOperationTypes.MultiplyValue, 4, 1.6, "ceil", null, null, out _);
    var floor = ArithmeticPatchOps.Compute(PatchOperationTypes.MultiplyValue, 4, 1.6, "floor", null, null, out _);
    var clampedLow = ArithmeticPatchOps.Compute(PatchOperationTypes.MultiplyValue, 4, 0.1, "round", 1, null, out var cClamped);

    return multiply == "10" && !mClamped
        && add == "6"
        && ceil == "7"
        && floor == "6"
        && clampedLow == "1" && cClamped
        && ArithmeticPatchOps.IsArithmetic(PatchOperationTypes.MultiplyValue)
        && !ArithmeticPatchOps.IsArithmetic(PatchOperationTypes.ReplaceValue);
}

bool TweakUsageScannerReportsMultiplier()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var usages = TweakUsageScanner.Scan(result.Value);
    var usage = usages.SingleOrDefault(u => u.TweakId == "cost-multiplier");

    return usage is
    {
        OperationType: "multiplyValue",
        OperandField: "factor",
        ExpectedOldValue: "4",
    }
    && !string.IsNullOrWhiteSpace(usage.Reason);
}

bool OptionalPatchSetSkippedWhenPackageAbsent()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patchset-skip-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), PatchSetTests.ModYaml);
    File.WriteAllText(Path.Combine(tempRoot, "patches", "sawmill.yaml"), PatchSetTests.SawmillYaml);

    try
    {
        // game-gdb-mini has no dlc1/ directory, so the optional set's package is absent.
        var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, result.Value);
        return plan.Success
            && plan.Writes.Count == 0
            && plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.PatchSetSkipped);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool PatchSetAppliedWhenPackagePresent()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patchset-apply-{Guid.NewGuid():N}");
    var tempGame = Path.Combine(Path.GetTempPath(), $"pagonia-patchset-game-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    Directory.CreateDirectory(Path.Combine(tempGame, "core", "gdb"));
    Directory.CreateDirectory(Path.Combine(tempGame, "dlc1")); // package present → set applies
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), PatchSetTests.ModYaml);
    File.WriteAllText(Path.Combine(tempRoot, "patches", "sawmill.yaml"), PatchSetTests.SawmillYaml);
    File.Copy(
        Path.Combine(patcherRoot, "fixtures", "game-gdb-mini", "core", "gdb", "buildings.gd.xml"),
        Path.Combine(tempGame, "core", "gdb", "buildings.gd.xml"));

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var plan = planner.Plan(tempGame, result.Value);
        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationId == "sawmill-cost"
            && !plan.Diagnostics.Any(d => d.Code == DiagnosticCodes.PatchSetSkipped);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
        if (Directory.Exists(tempGame)) { Directory.Delete(tempGame, recursive: true); }
    }
}

bool ApplyHonoursCancellationToken()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-apply-cancel-{Guid.NewGuid():N}");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    try
    {
        var (overrides, _) = TweakOverrides.Parse(["pagonia-land.fixture.templated-sawmill:softwood-cost=6"]);
        var plan = planner.Plan(gameRoot, [result.Value], TweakSelection.ForCli(overrides));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            applier.Apply(gameRoot, outputRoot, plan, cts.Token);
            return false; // must have thrown
        }
        catch (OperationCanceledException)
        {
            // Cancelled at the top of Apply, before CopyGameRoot ran — so no
            // staging tree was materialised.
            return !Directory.Exists(outputRoot);
        }
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool PlanReportCarriesResolvedTweaks()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var plan = planner.Plan(gameRoot, [result.Value]);
    var json = reporter.ToJson(plan);
    return json.Contains("\"ResolvedTweaks\"", StringComparison.Ordinal)
        && json.Contains("\"TweakId\": \"softwood-cost\"", StringComparison.Ordinal)
        && json.Contains("\"Origin\": \"default\"", StringComparison.Ordinal);
}

bool TweakSelectionPrecedence()
{
    const string modId = "mod.x";
    var lockfile = new Dictionary<string, string> { ["cost"] = "1" };
    var cli = new Dictionary<string, string> { ["cost"] = "2" };
    var collection = new Dictionary<string, string> { ["cost"] = "3", ["other"] = "9" };
    var (cliOverrides, _) = TweakOverrides.Parse(["mod.x:cost=2"]);

    var full = TweakSelection.ForCli(cliOverrides)
        .WithCollectionValues(modId, collection)
        .WithLockfileValues(modId, lockfile);
    var collectionOnly = TweakSelection.Create().WithCollectionValues(modId, collection);
    var collectionPlusCli = TweakSelection.ForCli(cliOverrides).WithCollectionValues(modId, collection);

    return full.Resolve(modId, "cost") is { Value: "1", Origin: "lockfile" }
        && collectionPlusCli.Resolve(modId, "cost") is { Value: "2", Origin: "external" }
        && collectionOnly.Resolve(modId, "cost") is { Value: "3", Origin: "collection" }
        && collectionOnly.Resolve(modId, "other") is { Value: "9", Origin: "collection" }
        && full.Resolve(modId, "missing") is null;
}

bool CollectionTweakOverrideResolvesAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-collection-tweak-{Guid.NewGuid():N}");
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-tweaked.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");

    var resolution = collectionResolver.ResolveMany([collectionPath], modsRoot);
    if (!resolution.Success || resolution.Value is null) { return false; }

    var selection = TweakSelection.Create();
    foreach (var mod in resolution.Value.Mods)
    {
        selection.WithCollectionValues(mod.LoadedMod.Manifest.Id, mod.CollectionMod.Tweaks);
    }

    try
    {
        var plan = planner.Plan(gameRoot, resolution.Value.Mods.Select(m => m.LoadedMod).ToList(), selection);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputXml = File.ReadAllText(Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml"));

        return plan.Success
            && plan.Writes.Single().NewValue == "5"
            && plan.ModPlans.Single().ResolvedTweaks.Any(t => t.TweakId == "softwood-cost" && t.Origin == "collection")
            && diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>5</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool ResolveWritesLockfileWithTweaks()
{
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-tweaked.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var result = collectionResolver.Resolve(collectionPath, modsRoot);
    if (!result.Success || result.Value is null) { return false; }

    var locked = result.Value.Lock.Mods.SingleOrDefault(m => m.Id == "pagonia-land.fixture.templated-sawmill");
    return result.Value.Lock.CollectionLockVersion == CollectionLockVersions.Current
        && locked?.Tweaks is not null
        && locked.Tweaks["softwood-cost"] == "5";
}

bool LockfilePinFollowedIntoPlan()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-tweaked.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lock-pin-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(outputRoot, "collection-lock.yaml");

    var resolve = collectionResolver.Resolve(collectionPath, modsRoot);
    if (!resolve.Success || resolve.Value is null) { return false; }

    try
    {
        Directory.CreateDirectory(outputRoot);
        collectionResolver.WriteLockFile(resolve.Value.Lock, lockPath);

        var lockResolution = collectionResolver.ResolveFromLock(lockPath, modsRoot);
        if (!lockResolution.Success || lockResolution.Value is null) { return false; }

        var selection = TweakSelection.Create();
        foreach (var lockedMod in lockResolution.Value.Lock.Mods)
        {
            selection.WithLockfileValues(lockedMod.Id, lockedMod.Tweaks);
        }

        var plan = planner.Plan(gameRoot, lockResolution.Value.Mods.ToList(), selection);
        return plan.Success
            && plan.Writes.Single().NewValue == "5"
            && plan.ModPlans.Single().ResolvedTweaks.Any(t => t.TweakId == "softwood-cost" && t.Origin == "lockfile")
            && plan.ModPlans.Single().Diagnostics.Any(d => d.Code == DiagnosticCodes.TweakValuePinnedByLockfile);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool LockfileAliasFollowedForward()
{
    // tweakable-sawmill declares an enum tweak `difficulty` carrying the legacy alias
    // `difficulty-level`. A value pinned under the old id must follow forward to the current id.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var selection = TweakSelection.Create().WithLockfileValues(
        "pagonia-land.fixture.tweakable-sawmill",
        new Dictionary<string, string> { ["difficulty-level"] = "hardcore" });

    var plan = planner.Plan(gameRoot, result.Value, selection);
    var difficulty = plan.ResolvedTweaks.FirstOrDefault(t => t.TweakId == "difficulty");
    return difficulty is { ResolvedValue: "hardcore", Origin: "lockfile" };
}

bool ApplyReportCarriesResolvedTweaks()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-apply-tweak-report-{Guid.NewGuid():N}");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    try
    {
        var plan = planner.Plan(gameRoot, [result.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var json = applyReporter.ToJson(plan, diagnostics, outputRoot);
        return json.Contains("\"ResolvedTweaks\"", StringComparison.Ordinal)
            && json.Contains("\"TweakId\": \"softwood-cost\"", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool LintWarnsOnUnusedTweak()
{
    // tweakable-sawmill declares three tweaks but its patch op uses a literal value, so each tweak
    // is a dead declaration. Warnings only — validation still succeeds.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "tweakable-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var diagnostics = validator.ValidateMod(result.Value);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.TweakDeclaredButUnused
            && d.Severity == PatchDiagnosticSeverity.Warning
            && d.Message.Contains("softwood-cost", StringComparison.Ordinal));
}

bool AdvisorFlagsReplaceAsInfo()
{
    // overlay-replace ships one Replace entity and references nothing else: the
    // advisor emits the destructive-mode Info + a risk Info, no Warning/Error.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-replace");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var diagnostics = advisor.Advise(overlay);

    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Warning)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.UsesDestructiveInheritanceMode
            && d.Severity == PatchDiagnosticSeverity.Info)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.InheritanceConflictRisk);
}

bool AdvisorWarnsOnUnloadOfReferencedEntity()
{
    // overlay-unload-dangling Unloads 4444… while another entity still points a
    // Worker at it — the reference would dangle, so the advisor warns.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-unload-dangling");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var diagnostics = advisor.Advise(overlay);

    return diagnostics.Any(d => d.Code == DiagnosticCodes.UnloadsReferencedEntity
        && d.Severity == PatchDiagnosticSeverity.Warning
        && d.Message.Contains("44444444-4444-4444-4444-444444444444", StringComparison.OrdinalIgnoreCase));
}

bool AdvisorIsSilentOnAdditiveIncremental()
{
    // overlay-incremental uses only the additive Incremental mode: no destructive
    // notice, no risk score, no warning — the advisor stays quiet.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-incremental");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var diagnostics = advisor.Advise(overlay);

    return diagnostics.Count == 0;
}

bool AdvisorIsQuietOnShippedDlc1()
{
    // Calibration: run the advisor over the real shipped dlc1 gd.xml. EE authored
    // it deliberately, so the advisor must not cry wolf — Info notices are fine,
    // but zero Warning/Error. game-gdb/ is local-only (gitignored, absent in CI);
    // skip cleanly when it isn't present.
    var dlc1Gdb = Path.Combine(root, "game-gdb", "dlc1", "gdb");
    if (!Directory.Exists(dlc1Gdb)) { return true; }

    var files = Directory.GetFiles(dlc1Gdb, "*.gd.xml", SearchOption.AllDirectories);
    if (files.Length == 0) { return true; }

    var overlay = OverlayGdbReader.ReadFiles(files);
    var diagnostics = advisor.Advise(overlay);

    return overlay.Diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Warning
            && d.Severity != PatchDiagnosticSeverity.Error);
}

bool AdvisorBaseAwareWarnsUnloadReferencedInBaseGame()
{
    // overlay-unload-base-referenced unloads c22b4997…, which the mini game-gdb's
    // Sawmill still lists as a Cost. With --game-root the advisor sees the dangle.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-unload-base-referenced");
    var miniGameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var reference = ReferenceGdbIndex.Load(miniGameRoot);
    var diagnostics = advisor.Advise(overlay, reference);

    return diagnostics.Any(d => d.Code == DiagnosticCodes.UnloadsReferencedEntity
        && d.Severity == PatchDiagnosticSeverity.Warning
        && d.Message.Contains("base game database", StringComparison.OrdinalIgnoreCase));
}

bool AdvisorBaseFreeSilentOnBaseOnlyReference()
{
    // Same overlay, but base-free (no game root): the GUID appears only as the
    // unload pointer, so there is nothing to warn about without the reference DB.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-unload-base-referenced");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var diagnostics = advisor.Advise(overlay);

    return !diagnostics.Any(d => d.Code == DiagnosticCodes.UnloadsReferencedEntity);
}

bool AdvisorBaseAwareFlagsAdditiveReplace()
{
    // overlay-replace-additive keeps the base Sawmill verbatim and only adds a
    // third Cost item, so the advisor should suggest Incremental.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-replace-additive");
    var miniGameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var reference = ReferenceGdbIndex.Load(miniGameRoot);
    var diagnostics = advisor.Advise(overlay, reference);

    return diagnostics.Any(d => d.Code == DiagnosticCodes.ReplaceCouldBeIncremental
        && d.Severity == PatchDiagnosticSeverity.Warning);
}

bool AdvisorBaseAwareSilentOnModifyingReplace()
{
    // overlay-replace-modifying changes the building name: a genuine rewrite, not
    // additive — must NOT be flagged as Incremental-able.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "overlay-replace-modifying");
    var miniGameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var overlay = OverlayGdbReader.ReadFromMod(result.Value);
    var reference = ReferenceGdbIndex.Load(miniGameRoot);
    var diagnostics = advisor.Advise(overlay, reference);

    return !diagnostics.Any(d => d.Code == DiagnosticCodes.ReplaceCouldBeIncremental);
}

bool AdvisorBaseAwareQuietOnShippedDlc1()
{
    // Calibration with full base context: index the whole local game-gdb and run
    // the advisor over dlc1 as the overlay. EE's 14 Replaces genuinely modify
    // their targets, so replaceCouldBeIncremental must not fire, and dlc1 has no
    // Unload — zero Warning/Error. game-gdb/ is local-only; skip when absent.
    var gameGdb = Path.Combine(root, "game-gdb");
    var dlc1Gdb = Path.Combine(gameGdb, "dlc1", "gdb");
    if (!Directory.Exists(dlc1Gdb)) { return true; }

    var files = Directory.GetFiles(dlc1Gdb, "*.gd.xml", SearchOption.AllDirectories);
    if (files.Length == 0) { return true; }

    var overlay = OverlayGdbReader.ReadFiles(files);
    var reference = ReferenceGdbIndex.Load(gameGdb);
    var diagnostics = advisor.Advise(overlay, reference);

    return overlay.Diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Warning
            && d.Severity != PatchDiagnosticSeverity.Error);
}

bool LintAllowsReferencedTweak()
{
    // templated-sawmill references its softwood-cost tweak in a placeholder, so it must not warn.
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "templated-sawmill");
    var result = reader.ReadMod(modPath);
    if (!result.Success || result.Value is null) { return false; }

    var diagnostics = validator.ValidateMod(result.Value);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && !diagnostics.Any(d => d.Code == DiagnosticCodes.TweakDeclaredButUnused);
}

bool LintWarnsOnTernaryOnNonBoolean()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lint-ternary-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.lint-ternary
name: Lint Ternary Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: A boolean ternary placeholder used on a number tweak.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
tweaks:
  - id: cost
    type: integer
    label: Cost
    default: 3
""");
    File.WriteAllText(Path.Combine(tempRoot, "patches", "buildings.yaml"), """
operations:
  - id: ternary-op
    operation: replaceValue
    risk: low
    reason: Ternary on a number tweak.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.cost ? 'a' : 'b' }}"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.TweakTernaryOnNonBoolean
            && d.Severity == PatchDiagnosticSeverity.Warning);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool LintWarnsOnMinGreaterThanMax()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lint-minmax-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(tempRoot, "patches"));
    File.WriteAllText(Path.Combine(tempRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.lint-minmax
name: Lint MinMax Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: A numeric tweak whose min exceeds its max.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
tweaks:
  - id: cost
    type: integer
    label: Cost
    default: 5
    min: 10
    max: 2
""");
    File.WriteAllText(Path.Combine(tempRoot, "patches", "buildings.yaml"), """
operations:
  - id: cost-op
    operation: replaceValue
    risk: low
    reason: References the cost tweak.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "{{ tweaks.cost }}"
""");

    try
    {
        var result = reader.ReadMod(tempRoot);
        if (!result.Success || result.Value is null) { return false; }

        var diagnostics = validator.ValidateMod(result.Value);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.TweakMinGreaterThanMax
            && d.Severity == PatchDiagnosticSeverity.Warning);
    }
    finally
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, recursive: true); }
    }
}

bool ValidFixtureModProducesPatchPlan()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, result.Value);
    return plan.Success
        && plan.Writes.Count == 1
        && plan.Writes[0].OldValue == "4"
        && plan.Writes[0].NewValue == "3";
}

bool MissingTargetFixtureFailsPlanning()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "missing-target");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, result.Value);
    return !plan.Success
        && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "targetEntityMissing");
}

bool ExpectedValueMismatchFixtureFailsPlanning()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "expected-value-mismatch");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, result.Value);
    return !plan.Success
        && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "expectedOldValueMismatch");
}

bool ConflictingFixtureModsFailCombinedPlanning()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var cheaperModPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var conflictingModPath = Path.Combine(patcherRoot, "fixtures", "mods", "conflicting-sawmill");
    var cheaperResult = reader.ReadMod(cheaperModPath);
    var conflictingResult = reader.ReadMod(conflictingModPath);

    if (!cheaperResult.Success || cheaperResult.Value is null
        || !conflictingResult.Success || conflictingResult.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, [cheaperResult.Value, conflictingResult.Value]);
    return !plan.Success
        && plan.Conflicts.Count == 1
        && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "duplicateWriteTarget");
}

bool ValidFixtureModAppliesToOutputFolder()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-out-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [result.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>3</Amount>", StringComparison.Ordinal)
            && sourceXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool PatchPlanReportsCanBeWritten()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-report-{Guid.NewGuid():N}");
    var markdownPath = Path.Combine(outputRoot, "plan.md");
    var jsonPath = Path.Combine(outputRoot, "plan.json");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var result = reader.ReadMod(modPath);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [result.Value]);
        reporter.WriteReports(plan, markdownPath, jsonPath);

        var markdown = File.ReadAllText(markdownPath);
        var json = File.ReadAllText(jsonPath);

        return markdown.Contains("# Pagonia Land Patch Plan", StringComparison.Ordinal)
            && markdown.Contains("Source: directMods", StringComparison.Ordinal)
            && json.Contains("\"PlanSource\": \"directMods\"", StringComparison.Ordinal)
            && markdown.Contains("Writes: 1", StringComparison.Ordinal)
            && json.Contains("\"WriteCount\": 1", StringComparison.Ordinal)
            && json.Contains("cheaper-sawmill-softwood-cost", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool LocalCollectionCanBeResolved()
{
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var result = collectionResolver.Resolve(collectionPath, modsRoot);

    return result.Success
        && result.Value?.Mods.Count == 1
        && result.Value.Lock.Mods.Count == 1
        && result.Value.Diagnostics.Any(diagnostic => diagnostic.Code == "collectionModSkipped");
}

bool CollectionLockfileCanBeWritten()
{
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-lock-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(outputRoot, "collection-lock.yaml");
    var result = collectionResolver.Resolve(collectionPath, modsRoot);

    if (!result.Success || result.Value is null)
    {
        return false;
    }

    try
    {
        collectionResolver.WriteLockFile(result.Value.Lock, lockPath);
        var lockText = File.ReadAllText(lockPath);
        return lockText.Contains("collectionId: pagonia-land.fixture.collections.local-beginner", StringComparison.Ordinal)
            && lockText.Contains("id: pagonia-land.fixture.cheaper-sawmill", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool LockfileWriterEmitsCurrentForLocalResolve()
{
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-lock-v02-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(outputRoot, "collection-lock.yaml");
    var result = collectionResolver.Resolve(collectionPath, modsRoot);
    if (!result.Success || result.Value is null) { return false; }

    try
    {
        collectionResolver.WriteLockFile(result.Value.Lock, lockPath);
        var lockText = File.ReadAllText(lockPath);
        // New writes pin the current schema version (0.1). Local resolves leave
        // source / resolvedAt empty — those fields exist only to record remote
        // origins; locking them at "" stays in-schema and round-trips fine.
        return lockText.Contains("collectionLockVersion: 0.1", StringComparison.Ordinal)
            && result.Value.Lock.CollectionLockVersion == CollectionLockVersions.Current
            && result.Value.Lock.Mods.All(m => m.Source == string.Empty && m.ResolvedAt == string.Empty);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool LockfileReaderAcceptsV01()
{
    // A minimal v0.1 lockfile with no source/resolvedAt/tweaks per mod. The
    // reader must accept it (those fields are optional). Skip the archive-hash
    // field so the hash-mismatch path isn't exercised — we're testing the
    // version gate, not SHA verification.
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var lockYaml = """
        collectionLockVersion: "0.1"
        collectionId: pagonia-land.fixture.collections.local-beginner
        collectionVersion: 0.1.0
        gameDatabaseVersion: "1.3.0-11694+192849"
        generatedAt: "2026-05-01T00:00:00Z"
        mods:
          - id: pagonia-land.fixture.cheaper-sawmill
            version: 0.1.0
            enabled: true
        """;
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-lock-v01-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(outputRoot, "collection-lock.yaml");
    try
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(lockPath, lockYaml);
        var resolved = collectionResolver.ResolveFromLock(lockPath, modsRoot);
        return resolved.Success
            && resolved.Value is { Mods.Count: 1 }
            && resolved.Diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error);
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool LockfileReaderRejectsUnknownVersion()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    // A future v0.4 lockfile must be refused with a structured diagnostic so
    // users on older managers get a clear "upgrade your tooling" signal
    // instead of a silent under-validated install.
    var lockYaml = """
        collectionLockVersion: "0.4"
        collectionId: pagonia-land.fixture.collections.future
        collectionVersion: 0.1.0
        gameDatabaseVersion: "1.3.0-11694+192849"
        generatedAt: "2026-12-01T00:00:00Z"
        mods:
          - id: pagonia-land.fixture.cheaper-sawmill
            version: 0.1.0
            enabled: true
        """;
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-lock-future-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(outputRoot, "collection-lock.yaml");
    try
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(lockPath, lockYaml);
        var resolved = collectionResolver.ResolveFromLock(lockPath, modsRoot);
        return !resolved.Success
            && resolved.Diagnostics.Any(d => d.Code == DiagnosticCodes.LockfileVersionUnsupported
                && d.Severity == PatchDiagnosticSeverity.Error
                && d.Message.Contains("0.4", StringComparison.Ordinal));
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool LockfileSchemaValidateAcceptsRemoteFields()
{
    // Bake a v0.1 lockfile by hand with the optional source + resolvedAt fields
    // populated, then run it through the public JSON Schema. Verifies
    // collection-lock.schema.json accepts the remote-provenance shape; without
    // this, downstream tooling (mod managers, IDE plugins) would silently
    // reject conformant lockfiles.
    var lockYaml = """
        collectionLockVersion: "0.1"
        collectionId: pagonia-land.example.beginner-qol
        collectionVersion: 0.1.0
        gameDatabaseVersion: "1.3.0-11694+192849"
        generatedAt: "2026-06-01T00:00:00Z"
        mods:
          - id: pagonia-land.example.cheaper-sawmill
            version: 0.1.0
            resolvedSource: "<store>/mods/pagonia-land.example.cheaper-sawmill/0.1.0"
            # Note: all-digit SHA is wrapped in quotes so YAML doesn't type-infer
            # it as an integer (which then fails the schema's string-type check).
            archiveSha256: "0000000000000000000000000000000000000000000000000000000000000000"
            enabled: true
            source: "gh:pagonia-land/example-mods#a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0/pagonia-land.example.cheaper-sawmill"
            resolvedAt: "2026-06-01T12:00:00Z"
        """;
    var tempPath = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-lock-schema-{Guid.NewGuid():N}.yaml");
    try
    {
        File.WriteAllText(tempPath, lockYaml);
        var diagnostics = schemaValidator.ValidateCollectionLock(tempPath);
        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
            && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
    }
    finally
    {
        if (File.Exists(tempPath)) { File.Delete(tempPath); }
    }
}


bool DirectModSetCanBeExportedAsCollection()
{
    var cheaperModPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var conflictingModPath = Path.Combine(patcherRoot, "fixtures", "mods", "conflicting-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-export-{Guid.NewGuid():N}");
    var collectionPath = Path.Combine(outputRoot, "exported.collection.yaml");
    var options = new CollectionExportOptions(
        "pagonia-land.fixture.collections.exported",
        "Exported Fixture Collection",
        "0.1.0",
        "Pagonia Land",
        null,
        "Exported from direct mod arguments.",
        "strict");

    try
    {
        var result = collectionExporter.Export([cheaperModPath, conflictingModPath], options);

        if (!result.Success || result.Value is null)
        {
            return false;
        }

        collectionExporter.WriteCollection(result.Value, collectionPath);
        var readResult = reader.ReadCollectionManifest(collectionPath);

        return readResult.Success
            && readResult.Value?.Id == "pagonia-land.fixture.collections.exported"
            && readResult.Value.Mods.Count == 2
            && readResult.Value.LoadOrder.SequenceEqual([
                "pagonia-land.fixture.cheaper-sawmill",
                "pagonia-land.fixture.conflicting-sawmill",
            ]);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool CollectionCanBePlanned()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var resolution = collectionResolver.Resolve(collectionPath, modsRoot);

    if (!resolution.Success || resolution.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList());

    return plan.Success
        && plan.ModPlans.Count == 1
        && plan.Writes.Count == 1
        && plan.Writes[0].OperationId == "cheaper-sawmill-softwood-cost";
}

bool MultipleCollectionsCanBeResolved()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var beginnerCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var duplicateCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-duplicate.collection.yaml");
    var resolution = collectionResolver.ResolveMany([beginnerCollectionPath, duplicateCollectionPath], modsRoot);

    return resolution.Success
        && resolution.Value?.Collections.Count == 2
        && resolution.Value.Mods.Count == 1
        && resolution.Value.Mods[0].LoadedMod.Manifest.Id == "pagonia-land.fixture.cheaper-sawmill"
        && resolution.Value.Diagnostics.Any(diagnostic => diagnostic.Code == "collectionModDuplicateSkipped");
}

bool MultipleCollectionsRejectVersionConflicts()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var beginnerCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-version-conflict-{Guid.NewGuid():N}");
    var conflictingCollectionPath = Path.Combine(outputRoot, "version-conflict.collection.yaml");

    try
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(conflictingCollectionPath, """
collectionFormatVersion: 0.1
id: pagonia-land.fixture.collections.version-conflict
name: Fixture Version Conflict Collection
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Fixture collection that requests a different version of an already loaded mod.
conflictPolicy: strict
mods:
  - id: pagonia-land.fixture.cheaper-sawmill
    version: "9.9.9"
    source: "local"
    required: true
    enabled: true
loadOrder:
  - pagonia-land.fixture.cheaper-sawmill
""");

        var resolution = collectionResolver.ResolveMany([beginnerCollectionPath, conflictingCollectionPath], modsRoot);
        return !resolution.Success
            && resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "collectionModVersionConflict");
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool MultipleCollectionsRejectGameDatabaseVersionConflicts()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var beginnerCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-gdb-conflict-{Guid.NewGuid():N}");
    var conflictingCollectionPath = Path.Combine(outputRoot, "gdb-conflict.collection.yaml");

    try
    {
        Directory.CreateDirectory(outputRoot);
        File.WriteAllText(conflictingCollectionPath, """
collectionFormatVersion: 0.1
id: pagonia-land.fixture.collections.gdb-conflict
name: Fixture GameDatabase Conflict Collection
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-99999+999999"
description: Fixture collection that targets another GameDatabase version.
conflictPolicy: strict
mods:
  - id: pagonia-land.fixture.cheaper-sawmill
    version: "0.1.0"
    source: "local"
    required: true
    enabled: true
loadOrder:
  - pagonia-land.fixture.cheaper-sawmill
""");

        var resolution = collectionResolver.ResolveMany([beginnerCollectionPath, conflictingCollectionPath], modsRoot);
        return !resolution.Success
            && resolution.Diagnostics.Any(diagnostic => diagnostic.Code == "collectionGameDatabaseVersionConflict");
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool LocalCollectionCanBeApplied()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-collection-apply-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var resolution = collectionResolver.ResolveMany([collectionPath], modsRoot);

    if (!resolution.Success || resolution.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList());
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Amount>3</Amount>", StringComparison.Ordinal)
            && sourceXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool MultipleCollectionsCanBeApplied()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var beginnerCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var duplicateCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-duplicate.collection.yaml");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-patcher-collections-apply-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var resolution = collectionResolver.ResolveMany([beginnerCollectionPath, duplicateCollectionPath], modsRoot);

    if (!resolution.Success || resolution.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList());
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && plan.Writes.Count == 1
            && resolution.Value.Diagnostics.Any(diagnostic => diagnostic.Code == "collectionModDuplicateSkipped")
            && outputXml.Contains("<Amount>3</Amount>", StringComparison.Ordinal)
            && sourceXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool ModManifestParsesEntries()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-mod-entries-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var modYamlPath = Path.Combine(tempRoot, "mod.yaml");
    File.WriteAllText(modYamlPath, """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.entries-test
name: Entries Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11727+193140"
description: Inline mod manifest with binary entry operations.
requiredPackages:
  - dlc1
patches:
  - patches/none.yaml
entries:
  replace:
    - path: dlc1/gui/icons/buildings/icon_sawmill.image
      source: entries/icon_sawmill.image
  add:
    - path: dlc1/textures/my_new_wall.bc.texture
      source: entries/my_new_wall.bc.texture
  delete:
    - dlc1/sounds/annoying.audio
""");

    try
    {
        var result = reader.ReadModManifest(tempRoot);
        if (!result.Success || result.Value is null) return false;

        var entries = result.Value.Entries;
        return entries is not null
            && entries.Replace.Count == 1
            && entries.Replace[0].Path == "dlc1/gui/icons/buildings/icon_sawmill.image"
            && entries.Replace[0].Source == "entries/icon_sawmill.image"
            && entries.Add.Count == 1
            && entries.Add[0].Path == "dlc1/textures/my_new_wall.bc.texture"
            && entries.Delete.Count == 1
            && entries.Delete[0] == "dlc1/sounds/annoying.audio";
    }
    finally
    {
        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
    }
}

bool ModManifestEntriesOnly()
{
    // A pure-asset mod has neither `patches` nor `patchSets`, only `entries`.
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-mod-entries-only-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var modYamlPath = Path.Combine(tempRoot, "mod.yaml");
    File.WriteAllText(modYamlPath, """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.entries-only
name: Pure Asset Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11727+193140"
description: Manifest with only binary entry operations and no XML patches.
requiredPackages:
  - core
entries:
  replace:
    - path: core/gui/icons/buildings/icon_house.image
      source: entries/icon_house.image
""");

    try
    {
        var result = reader.ReadModManifest(tempRoot);
        return result.Success
            && result.Value?.Patches.Count == 0
            && result.Value?.PatchSets.Count == 0
            && result.Value?.Entries?.Replace.Count == 1;
    }
    finally
    {
        if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
    }
}

bool ModManifestCarriesMetadataFields()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-mod-metadata-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var modYamlPath = Path.Combine(tempRoot, "mod.yaml");
    File.WriteAllText(modYamlPath, """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.metadata-test
name: Metadata Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Inline mod manifest for metadata tests.
requiredPackages:
  - core
patches:
  - patches/none.yaml
homepage: "https://example.invalid/home"
repository: "https://example.invalid/repo"
downloadUrl: "https://example.invalid/download"
updateUrl: "https://example.invalid/update"
license: MIT
category: balance
tags:
  - safe
  - small
previewImages:
  - preview/a.png
  - preview/b.png
""");

    try
    {
        var result = reader.ReadModManifest(tempRoot);

        return result.Success
            && result.Value?.Homepage == "https://example.invalid/home"
            && result.Value.Repository == "https://example.invalid/repo"
            && result.Value.DownloadUrl == "https://example.invalid/download"
            && result.Value.UpdateUrl == "https://example.invalid/update"
            && result.Value.License == "MIT"
            && result.Value.Category == "balance"
            && result.Value.Tags.SequenceEqual(["safe", "small"])
            && result.Value.PreviewImages.SequenceEqual(["preview/a.png", "preview/b.png"]);
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

bool CollectionManifestCarriesSafetyAndMetadataFields()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-collection-metadata-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var collectionPath = Path.Combine(tempRoot, "collection.yaml");
    File.WriteAllText(collectionPath, """
collectionFormatVersion: "0.1"
id: pagonia-land.fixture.collections.metadata-test
name: Metadata Test Collection
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Inline collection manifest for metadata tests.
conflictPolicy: strict
mods:
  - id: pagonia-land.fixture.cheaper-sawmill
    version: "0.1.0"
    source: "local"
    required: true
    enabled: true
    notes: "Tiny softwood cost reduction."
loadOrder:
  - pagonia-land.fixture.cheaper-sawmill
requiresNewGame: false
safeToRemove: true
multiplayerSafe: unknown
campaignSafe: false
homepage: "https://example.invalid/c-home"
repository: "https://example.invalid/c-repo"
updateUrl: "https://example.invalid/c-update"
license: CC-BY-4.0
category: qol
tags:
  - beginner
  - qol
previewImages:
  - preview/c.png
""");

    try
    {
        var result = reader.ReadCollectionManifest(collectionPath);

        return result.Success
            && result.Value?.RequiresNewGame == SafetyState.No
            && result.Value.SafeToRemove == SafetyState.Yes
            && result.Value.MultiplayerSafe == SafetyState.Unknown
            && result.Value.CampaignSafe == SafetyState.No
            && result.Value.Homepage == "https://example.invalid/c-home"
            && result.Value.Repository == "https://example.invalid/c-repo"
            && result.Value.UpdateUrl == "https://example.invalid/c-update"
            && result.Value.License == "CC-BY-4.0"
            && result.Value.Category == "qol"
            && result.Value.Tags.SequenceEqual(["beginner", "qol"])
            && result.Value.PreviewImages.SequenceEqual(["preview/c.png"])
            && result.Value.Mods.Count == 1
            && result.Value.Mods[0].Notes == "Tiny softwood cost reduction.";
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

bool SafetyStatesParseFromTrueFalseAndUnknown()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-safety-states-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var modYamlPath = Path.Combine(tempRoot, "mod.yaml");
    File.WriteAllText(modYamlPath, """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.safety-states
name: Safety States Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Inline mod manifest for safety-state tests.
requiredPackages:
  - core
patches:
  - patches/none.yaml
requiresNewGame: true
safeToRemove: false
multiplayerSafe: unknown
""");

    try
    {
        var result = reader.ReadModManifest(tempRoot);

        return result.Success
            && result.Value?.RequiresNewGame == SafetyState.Yes
            && result.Value.SafeToRemove == SafetyState.No
            && result.Value.MultiplayerSafe == SafetyState.Unknown
            && result.Value.CampaignSafe is null;
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

bool InvalidSafetyValueReportsDiagnostic()
{
    var tempRoot = Path.Combine(Path.GetTempPath(), $"pagonia-safety-invalid-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempRoot);
    var modYamlPath = Path.Combine(tempRoot, "mod.yaml");
    File.WriteAllText(modYamlPath, """
patchFormatVersion: "0.1"
id: pagonia-land.fixture.safety-invalid
name: Safety Invalid Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Inline mod manifest with an invalid safety value.
requiredPackages:
  - core
patches:
  - patches/none.yaml
requiresNewGame: maybe
""");

    try
    {
        var result = reader.ReadModManifest(tempRoot);

        return !result.Success
            && result.Diagnostics.Any(diagnostic => diagnostic.Severity == PatchDiagnosticSeverity.Error
                && diagnostic.Message.Contains("maybe", StringComparison.OrdinalIgnoreCase));
    }
    finally
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}

bool ApplyReportsCanBeWrittenForDirectMods()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-apply-report-direct-{Guid.NewGuid():N}");
    var outputGameRoot = Path.Combine(outputRoot, "out");
    var markdownPath = Path.Combine(outputRoot, "apply.md");
    var jsonPath = Path.Combine(outputRoot, "apply.json");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputGameRoot, plan);
        applyReporter.WriteReports(plan, diagnostics, outputGameRoot, markdownPath, jsonPath, "directMods");

        var markdown = File.ReadAllText(markdownPath);
        var json = File.ReadAllText(jsonPath);

        return markdown.Contains("# Pagonia Land Patch Apply Report", StringComparison.Ordinal)
            && markdown.Contains("Source: directMods", StringComparison.Ordinal)
            && markdown.Contains("Result: OK", StringComparison.Ordinal)
            && markdown.Contains("Applied writes: 1", StringComparison.Ordinal)
            && markdown.Contains("Failed writes: 0", StringComparison.Ordinal)
            && json.Contains("\"PlanSource\": \"directMods\"", StringComparison.Ordinal)
            && json.Contains("\"Success\": true", StringComparison.Ordinal)
            && json.Contains("\"PlanWriteCount\": 1", StringComparison.Ordinal)
            && json.Contains("\"AppliedWriteCount\": 1", StringComparison.Ordinal)
            && json.Contains("\"FailedWriteCount\": 0", StringComparison.Ordinal)
            && json.Contains("cheaper-sawmill-softwood-cost", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool ApplyReportsCanBeWrittenForCollection()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var beginnerCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var duplicateCollectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-duplicate.collection.yaml");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-apply-report-collection-{Guid.NewGuid():N}");
    var outputGameRoot = Path.Combine(outputRoot, "out");
    var markdownPath = Path.Combine(outputRoot, "apply.md");
    var jsonPath = Path.Combine(outputRoot, "apply.json");
    var resolution = collectionResolver.ResolveMany([beginnerCollectionPath, duplicateCollectionPath], modsRoot);

    if (!resolution.Success || resolution.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, resolution.Value.Mods.Select(mod => mod.LoadedMod).ToList());
        var diagnostics = applier.Apply(gameRoot, outputGameRoot, plan);
        applyReporter.WriteReports(plan, diagnostics, outputGameRoot, markdownPath, jsonPath, "collections");

        var markdown = File.ReadAllText(markdownPath);
        var json = File.ReadAllText(jsonPath);

        return markdown.Contains("Source: collections", StringComparison.Ordinal)
            && markdown.Contains("Applied writes: 1", StringComparison.Ordinal)
            && json.Contains("\"PlanSource\": \"collections\"", StringComparison.Ordinal)
            && json.Contains("\"AppliedWriteCount\": 1", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool LockfileCanBePlannedAndApplied()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var collectionPath = Path.Combine(patcherRoot, "fixtures", "collections", "local-beginner.collection.yaml");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lock-plan-apply-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(workRoot, "collection-lock.yaml");
    var outputGameRoot = Path.Combine(workRoot, "out");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");

    try
    {
        Directory.CreateDirectory(workRoot);

        var resolution = collectionResolver.Resolve(collectionPath, modsRoot);
        if (!resolution.Success || resolution.Value is null) { return false; }
        collectionResolver.WriteLockFile(resolution.Value.Lock, lockPath);

        var lockResolution = collectionResolver.ResolveFromLock(lockPath, modsRoot);
        if (!lockResolution.Success || lockResolution.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, lockResolution.Value.Mods);
        var diagnostics = applier.Apply(gameRoot, outputGameRoot, plan);
        var outputBuildingFile = Path.Combine(outputGameRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return lockResolution.Value.Mods.Count == 1
            && plan.Success
            && plan.Writes.Count == 1
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && lockResolution.Value.Diagnostics.Any(diagnostic => diagnostic.Code == "lockfileModResolved")
            && outputXml.Contains("<Amount>3</Amount>", StringComparison.Ordinal)
            && sourceXml.Contains("<Amount>4</Amount>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool LockfileModMissingReportsDiagnostic()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lock-missing-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(workRoot, "collection-lock.yaml");

    try
    {
        Directory.CreateDirectory(workRoot);
        File.WriteAllText(lockPath, """
collectionLockVersion: 0.1
collectionId: pagonia-land.fixture.collections.lock-missing
collectionVersion: 0.1.0
gameDatabaseVersion: "1.3.0-11694+192849"
generatedAt: "2026-05-21T00:00:00Z"
mods:
  - id: pagonia-land.fixture.does-not-exist
    version: "0.1.0"
    resolvedSource: "local"
    archiveSha256: ""
    enabled: true
""");

        var lockResolution = collectionResolver.ResolveFromLock(lockPath, modsRoot);

        return !lockResolution.Success
            && lockResolution.Diagnostics.Any(diagnostic => diagnostic.Code == "lockfileModMissing");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool LockfileHashMismatchReportsDiagnostic()
{
    var modsRoot = Path.Combine(patcherRoot, "fixtures", "mods");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-lock-mismatch-{Guid.NewGuid():N}");
    var lockPath = Path.Combine(workRoot, "collection-lock.yaml");

    try
    {
        Directory.CreateDirectory(workRoot);
        File.WriteAllText(lockPath, """
collectionLockVersion: 0.1
collectionId: pagonia-land.fixture.collections.lock-hash-mismatch
collectionVersion: 0.1.0
gameDatabaseVersion: "1.3.0-11694+192849"
generatedAt: "2026-05-21T00:00:00Z"
mods:
  - id: pagonia-land.fixture.cheaper-sawmill
    version: "0.1.0"
    resolvedSource: "local"
    archiveSha256: "0000000000000000000000000000000000000000000000000000000000000000"
    enabled: true
""");

        var lockResolution = collectionResolver.ResolveFromLock(lockPath, modsRoot);

        return !lockResolution.Success
            && lockResolution.Diagnostics.Any(diagnostic => diagnostic.Code == "lockfileArchiveHashMismatch");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool ReplaceAttributePlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "replace-attribute-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-replace-attribute-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "replaceAttribute"
            && plan.Writes[0].Attribute == "Variant"
            && plan.Writes[0].OldValue == "default"
            && plan.Writes[0].NewValue == "alternative"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("Variant=\"alternative\"", StringComparison.Ordinal)
            && sourceXml.Contains("Variant=\"default\"", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool ReplaceNodePlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "replace-node-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-replace-node-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "replaceNode"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("decal_sawmill_002.png", StringComparison.Ordinal)
            && outputXml.Contains("<Opacity>0.8</Opacity>", StringComparison.Ordinal)
            && sourceXml.Contains("decal_sawmill_001.png", StringComparison.Ordinal)
            && !sourceXml.Contains("Opacity", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool ReplaceAttributeAndReplaceValueDoNotConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var cheaperModPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var attributeModPath = Path.Combine(patcherRoot, "fixtures", "mods", "replace-attribute-sawmill");
    var cheaperResult = reader.ReadMod(cheaperModPath);
    var attributeResult = reader.ReadMod(attributeModPath);

    if (!cheaperResult.Success || cheaperResult.Value is null
        || !attributeResult.Success || attributeResult.Value is null)
    {
        return false;
    }

    var plan = planner.Plan(gameRoot, [cheaperResult.Value, attributeResult.Value]);
    return plan.Success
        && plan.Writes.Count == 2
        && plan.Conflicts.Count == 0;
}

bool TwoReplaceAttributeWritesConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-attr-conflict-{Guid.NewGuid():N}");
    var clonedModPath = Path.Combine(workRoot, "replace-attribute-sawmill-other");
    var attributeModPath = Path.Combine(patcherRoot, "fixtures", "mods", "replace-attribute-sawmill");

    try
    {
        Directory.CreateDirectory(Path.Combine(clonedModPath, "patches"));
        File.WriteAllText(Path.Combine(clonedModPath, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.replace-attribute-sawmill-other
name: Fixture Replace Attribute Sawmill Other
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Conflicting fixture mod that also writes Sawmill mesh Variant.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(clonedModPath, "patches", "buildings.yaml"), """
operations:
  - id: replace-attribute-sawmill-mesh-variant-other
    operation: replaceAttribute
    risk: low
    reason: Conflicting fixture write on the same attribute.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: Building
      path: Mesh
    attribute: Variant
    expectedOldValue: "default"
    value: "thirdvariant"
""");

        var originalResult = reader.ReadMod(attributeModPath);
        var clonedResult = reader.ReadMod(clonedModPath);

        if (!originalResult.Success || originalResult.Value is null
            || !clonedResult.Success || clonedResult.Value is null)
        {
            return false;
        }

        var plan = planner.Plan(gameRoot, [originalResult.Value, clonedResult.Value]);
        return !plan.Success
            && plan.Conflicts.Count == 1
            && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "duplicateWriteTarget");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool AddListItemPlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "add-list-item-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-list-item-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        var sourceItemCount = System.Text.RegularExpressions.Regex.Matches(sourceXml, "<Item>").Count;
        var outputItemCount = System.Text.RegularExpressions.Regex.Matches(outputXml, "<Item>").Count;

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "addListItem"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("11111111-2222-3333-4444-555555555555", StringComparison.Ordinal)
            && !sourceXml.Contains("11111111-2222-3333-4444-555555555555", StringComparison.Ordinal)
            && outputItemCount == sourceItemCount + 1;
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool RemoveListItemPlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "remove-list-item-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-remove-list-item-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "removeListItem"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && !outputXml.Contains("d8dd765a-ac73-49cc-a9b9-f6102f6f8e07", StringComparison.Ordinal)
            && sourceXml.Contains("d8dd765a-ac73-49cc-a9b9-f6102f6f8e07", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool RemoveListItemMissingItemReportsDiagnostic()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-remove-missing-item-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(workRoot, "remove-missing-item-sawmill");

    try
    {
        Directory.CreateDirectory(Path.Combine(modRoot, "patches"));
        File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.remove-missing-item-sawmill
name: Fixture Remove Missing List Item Sawmill
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Fixture mod that removes a list item that does not exist.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(modRoot, "patches", "buildings.yaml"), """
operations:
  - id: remove-missing-item-sawmill
    operation: removeListItem
    risk: low
    reason: Conflict fixture targeting a non-existent list item.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs
    expectedOldXml: |
      <Item><Content><Resource>00000000-0000-0000-0000-000000000000</Resource><Amount>99</Amount></Content></Item>
""");

        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [modResult.Value]);
        return !plan.Success
            && plan.Diagnostics.Concat(plan.ModPlans.SelectMany(modPlan => modPlan.Diagnostics))
                .Any(diagnostic => diagnostic.Code == "targetListItemMissing");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool TwoAddListItemsWithDifferentItemsDoNotConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-list-different-{Guid.NewGuid():N}");
    var secondModRoot = Path.Combine(workRoot, "add-list-item-sawmill-other");
    var firstModPath = Path.Combine(patcherRoot, "fixtures", "mods", "add-list-item-sawmill");

    try
    {
        Directory.CreateDirectory(Path.Combine(secondModRoot, "patches"));
        File.WriteAllText(Path.Combine(secondModRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.add-list-item-sawmill-other
name: Fixture Add Different List Item Sawmill
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Adds a different fourth Sawmill cost item.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(secondModRoot, "patches", "buildings.yaml"), """
operations:
  - id: add-list-item-sawmill-other
    operation: addListItem
    risk: low
    reason: Conflict fixture adding a different item.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs
    xml: |
      <Item><Content><Resource>22222222-3333-4444-5555-666666666666</Resource><Amount>1</Amount></Content></Item>
""");

        var first = reader.ReadMod(firstModPath);
        var second = reader.ReadMod(secondModRoot);
        if (!first.Success || first.Value is null || !second.Success || second.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [first.Value, second.Value]);
        return plan.Success
            && plan.Writes.Count == 2
            && plan.Conflicts.Count == 0;
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool AddAndRemoveTargetingSameItemConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-remove-same-{Guid.NewGuid():N}");
    var addRoot = Path.Combine(workRoot, "add-stone-block");
    var removeModPath = Path.Combine(patcherRoot, "fixtures", "mods", "remove-list-item-sawmill");

    try
    {
        Directory.CreateDirectory(Path.Combine(addRoot, "patches"));
        File.WriteAllText(Path.Combine(addRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.add-stone-block-sawmill
name: Fixture Add Stone Block Sawmill
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Adds a list item identical to what another mod is removing.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(addRoot, "patches", "buildings.yaml"), """
operations:
  - id: add-stone-block-sawmill
    operation: addListItem
    risk: low
    reason: Conflict fixture adding the same item another mod removes.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs
    xml: |
      <Item><Content><Resource>d8dd765a-ac73-49cc-a9b9-f6102f6f8e07</Resource><Amount>4</Amount></Content></Item>
""");

        var add = reader.ReadMod(addRoot);
        var remove = reader.ReadMod(removeModPath);
        if (!add.Success || add.Value is null || !remove.Success || remove.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [add.Value, remove.Value]);
        return !plan.Success
            && plan.Conflicts.Count == 1
            && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "duplicateWriteTarget");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool AddEntityPlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "add-entity-extra-building");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-entity-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "addEntity"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("FixtureLumberCamp", StringComparison.Ordinal)
            && outputXml.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", StringComparison.Ordinal)
            && !sourceXml.Contains("FixtureLumberCamp", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool RemoveEntityPlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-remove-entity-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(workRoot, "remove-sawmill");
    var outputRoot = Path.Combine(workRoot, "out");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var sourceDoc = System.Xml.Linq.XDocument.Load(sourceBuildingFile);
    var sawmillEntity = sourceDoc.Descendants("Entity")
        .First(element => string.Equals((string?)element.Attribute("Guid"), "c732cb26-7487-4a7b-b1ba-b65e094f9bac", StringComparison.OrdinalIgnoreCase));
    var sawmillXml = sawmillEntity.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

    try
    {
        Directory.CreateDirectory(Path.Combine(modRoot, "patches"));
        File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.remove-sawmill
name: Fixture Remove Sawmill
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Removes the Sawmill entity for round-trip testing.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(modRoot, "patches", "buildings.yaml"),
$"""
operations:
  - id: remove-sawmill-entity
    operation: removeEntity
    risk: high
    reason: Round-trip removeEntity test.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
    expectedOldXml: |
      {sawmillXml}
""");

        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXmlText = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "removeEntity"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && !outputXml.Contains("c732cb26-7487-4a7b-b1ba-b65e094f9bac", StringComparison.Ordinal)
            && sourceXmlText.Contains("c732cb26-7487-4a7b-b1ba-b65e094f9bac", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool MergeComponentPlansAndApplies()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "merge-component-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-merge-component-{Guid.NewGuid():N}");
    var sourceBuildingFile = Path.Combine(gameRoot, "core", "gdb", "buildings.gd.xml");
    var modResult = reader.ReadMod(modPath);

    if (!modResult.Success || modResult.Value is null)
    {
        return false;
    }

    try
    {
        var plan = planner.Plan(gameRoot, [modResult.Value]);
        var diagnostics = applier.Apply(gameRoot, outputRoot, plan);
        var outputBuildingFile = Path.Combine(outputRoot, "core", "gdb", "buildings.gd.xml");
        var outputXml = File.ReadAllText(outputBuildingFile);
        var sourceXml = File.ReadAllText(sourceBuildingFile);

        return plan.Success
            && plan.Writes.Count == 1
            && plan.Writes[0].OperationType == "mergeComponent"
            && diagnostics.All(diagnostic => diagnostic.Severity != PatchDiagnosticSeverity.Error)
            && outputXml.Contains("<Description>A wooden plank workshop.</Description>", StringComparison.Ordinal)
            && outputXml.Contains("<Name>Sawmill</Name>", StringComparison.Ordinal)
            && !sourceXml.Contains("<Description>", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool AddEntityDuplicateGuidReportsDiagnostic()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-add-entity-dup-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(workRoot, "add-duplicate-sawmill");

    try
    {
        Directory.CreateDirectory(Path.Combine(modRoot, "patches"));
        File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.add-duplicate-sawmill
name: Fixture Add Duplicate Sawmill
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Tries to add an entity with the same GUID as the existing Sawmill.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(modRoot, "patches", "buildings.yaml"), """
operations:
  - id: add-duplicate-sawmill-entity
    operation: addEntity
    risk: very-high
    reason: Conflict fixture using a duplicate entity GUID.
    target:
      file: core/gdb/buildings.gd.xml
      entityName: Buildings
    xml: |
      <Entity Name="DuplicateSawmill" Guid="c732cb26-7487-4a7b-b1ba-b65e094f9bac"><Children /><Values><Building><Name>DuplicateSawmill</Name></Building></Values></Entity>
""");

        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [modResult.Value]);
        return !plan.Success
            && plan.Diagnostics.Concat(plan.ModPlans.SelectMany(modPlan => modPlan.Diagnostics))
                .Any(diagnostic => diagnostic.Code == "targetEntityAlreadyExists");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool TwoAddEntityWritesConflict()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-two-add-entity-{Guid.NewGuid():N}");
    var secondModRoot = Path.Combine(workRoot, "add-entity-other");
    var firstModPath = Path.Combine(patcherRoot, "fixtures", "mods", "add-entity-extra-building");

    try
    {
        Directory.CreateDirectory(Path.Combine(secondModRoot, "patches"));
        File.WriteAllText(Path.Combine(secondModRoot, "mod.yaml"), """
patchFormatVersion: 0.1
id: pagonia-land.fixture.add-entity-other
name: Fixture Add Same Entity Other
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11694+192849"
description: Adds a second entity with the same GUID as another mod.
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
""");
        File.WriteAllText(Path.Combine(secondModRoot, "patches", "buildings.yaml"), """
operations:
  - id: add-entity-other
    operation: addEntity
    risk: very-high
    reason: "Conflict fixture: same GUID as a sibling addEntity."
    target:
      file: core/gdb/buildings.gd.xml
      entityName: Buildings
    xml: |
      <Entity Name="LumberCampVariant" Guid="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"><Children /><Values><Building><Name>LumberCampVariant</Name></Building></Values></Entity>
""");

        var first = reader.ReadMod(firstModPath);
        var second = reader.ReadMod(secondModRoot);
        if (!first.Success || first.Value is null || !second.Success || second.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [first.Value, second.Value]);
        return !plan.Success
            && plan.Conflicts.Count == 1
            && plan.Diagnostics.Any(diagnostic => diagnostic.Code == "duplicateWriteTarget");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool SchemaValidateAcceptsCheaperSawmill()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var diagnostics = schemaValidator.ValidateMod(modPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool SchemaValidateRejectsBrokenManifest()
{
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "broken-manifest");
    var diagnostics = schemaValidator.ValidateMod(modPath);
    var errors = diagnostics.Where(d => d.Severity == PatchDiagnosticSeverity.Error).ToList();
    return errors.Count > 0
        && errors.All(d => d.Code == DiagnosticCodes.SchemaValidationFailed)
        && errors.Any(d => d.Message.Contains("/id", StringComparison.Ordinal));
}

bool SchemaValidateAcceptsSanctuaryExample()
{
    var modPath = Path.Combine(root, "sandbox", "examples", "sanctuary-add-custom-ability");
    var diagnostics = schemaValidator.ValidateMod(modPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error);
}

bool SchemaValidateHandlesBooleanScalars()
{
    // Regression test for the YAML-to-JSON conversion: untyped Deserialize<object?> would turn
    // plain `false` into the string "false" and trip the boolean branch of safetyValue's oneOf.
    // The custom YamlStream walker should produce a real JSON boolean.
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-schema-bool-{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(Path.Combine(workRoot, "patches"));
        File.WriteAllText(Path.Combine(workRoot, "mod.yaml"), """
patchFormatVersion: "0.1"
id: pagonia-land.test.bool-scalar
name: Bool Scalar Test
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11768+193445"
description: Regression test for boolean YAML scalars surviving JSON conversion intact.
requiredPackages:
  - core
requiresNewGame: false
safeToRemove: true
multiplayerSafe: false
campaignSafe: true
patches:
  - patches/noop.yaml
""");
        File.WriteAllText(Path.Combine(workRoot, "patches", "noop.yaml"), """
operations:
  - id: noop-replace
    operation: replaceValue
    target:
      file: core/gdb/buildings.gd.xml
      entityName: Placeholder
    expectedOldValue: "0"
    value: "1"
""");

        var diagnostics = schemaValidator.ValidateMod(workRoot);
        return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error);
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool SchemaValidateAcceptsCollectionExample()
{
    var collectionPath = Path.Combine(root, "docs", "examples", "collections", "beginner-qol.collection.yaml");
    var diagnostics = schemaValidator.ValidateCollection(collectionPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error);
}

bool SchemaValidateAcceptsRepoIndexExample()
{
    var indexPath = Path.Combine(root, "examples", "mod-repo-example", "index.yaml");
    var diagnostics = schemaValidator.ValidateRepoIndex(indexPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool SchemaValidateRejectsRepoIndexBadModId()
{
    // Mod id 'BadID-Uppercase' violates the id pattern ^[a-z0-9][a-z0-9._-]*[a-z0-9]$.
    var yaml = """
        indexFormatVersion: "0.1"
        repo:
          name: Bad-Id Repo
        mods:
          - id: BadID-Uppercase
            path: mods/bad
        """;
    return RunRepoIndexValidation(yaml, expectError: true);
}

bool SchemaValidateRejectsRepoIndexUnknownProperty()
{
    // Mod entries are closed objects — additionalProperties: false. A typo
    // like 'descriptionn' (extra n) gets caught instead of being silently
    // ignored, so authors find rename mistakes before the manager fetches.
    var yaml = """
        indexFormatVersion: "0.1"
        repo:
          name: Typo Repo
        mods:
          - id: pagonia-land.example.with-typo
            path: mods/typo
            descriptionn: This field name has a typo and should be rejected.
        """;
    return RunRepoIndexValidation(yaml, expectError: true);
}

bool SchemaValidateRejectsRepoIndexUnknownVersion()
{
    // Only "0.1" is currently accepted. A future bump (e.g. "0.2") deliberately
    // breaks old validators so authors get a clear "upgrade your tooling" signal.
    var yaml = """
        indexFormatVersion: "0.99"
        repo:
          name: Future Repo
        """;
    return RunRepoIndexValidation(yaml, expectError: true);
}

bool RunRepoIndexValidation(string yaml, bool expectError)
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"pagonia-repo-index-{Guid.NewGuid():N}.yaml");
    try
    {
        File.WriteAllText(tempPath, yaml);
        var diagnostics = schemaValidator.ValidateRepoIndex(tempPath);
        var hasError = diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error);
        return hasError == expectError;
    }
    finally
    {
        if (File.Exists(tempPath)) { File.Delete(tempPath); }
    }
}

bool RunCollectionValidation(string yaml, bool expectError)
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"pagonia-collection-{Guid.NewGuid():N}.yaml");
    try
    {
        File.WriteAllText(tempPath, yaml);
        var diagnostics = schemaValidator.ValidateCollection(tempPath);
        var hasError = diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error);
        return hasError == expectError;
    }
    finally
    {
        if (File.Exists(tempPath)) { File.Delete(tempPath); }
    }
}

// relativePath safety — manager-side remote fetchers must not be tricked into
// reaching outside the repo. The schema is the first defence; these tests pin
// it. (Manager-side fetchers will still defensively normalise paths regardless.)
bool SchemaValidateRejectsRepoIndexTraversal() => RunRepoIndexValidation("""
    indexFormatVersion: "0.1"
    repo:
      name: T
    mods:
      - id: pagonia-land.example.escape
        path: ../escape
    """, expectError: true);

bool SchemaValidateRejectsRepoIndexLeadingSlash() => RunRepoIndexValidation("""
    indexFormatVersion: "0.1"
    repo:
      name: T
    mods:
      - id: pagonia-land.example.absolute
        path: /etc/passwd
    """, expectError: true);

bool SchemaValidateRejectsRepoIndexDriveLetter() => RunRepoIndexValidation("""
    indexFormatVersion: "0.1"
    repo:
      name: T
    mods:
      - id: pagonia-land.example.drive
        path: "C:/Users/foo"
    """, expectError: true);

bool SchemaValidateRejectsRepoIndexBackslash() => RunRepoIndexValidation("""
    indexFormatVersion: "0.1"
    repo:
      name: T
    mods:
      - id: pagonia-land.example.backslash
        path: "mods\\sub"
    """, expectError: true);

bool SchemaValidateRejectsCollectionTraversal() => RunCollectionValidation("""
    collectionFormatVersion: 0.1
    id: pagonia-land.example.bad-preview
    name: Bad Preview
    version: 0.1.0
    author: Test
    gameDatabaseVersion: "1.3.0-11694+192849"
    description: Traversal preview path.
    mods:
      - id: pagonia-land.example.mod
        version: "0.1.0"
    previewImages:
      - "../escape.png"
    """, expectError: true);

bool SchemaValidateRejectsCollectionDriveLetter() => RunCollectionValidation("""
    collectionFormatVersion: 0.1
    id: pagonia-land.example.bad-preview-drive
    name: Bad Preview Drive
    version: 0.1.0
    author: Test
    gameDatabaseVersion: "1.3.0-11694+192849"
    description: Drive-letter preview path.
    mods:
      - id: pagonia-land.example.mod
        version: "0.1.0"
    previewImages:
      - "C:/path/preview.png"
    """, expectError: true);

bool SchemaValidateAcceptsCatalogExample()
{
    var catalogPath = Path.Combine(root, "examples", "mod-catalog-example", "catalog.yaml");
    var diagnostics = schemaValidator.ValidateCatalog(catalogPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool SchemaValidateAcceptsCatalogSubExample()
{
    var catalogPath = Path.Combine(root, "examples", "mod-catalog-example", "catalogs", "sub-catalog.yaml");
    var diagnostics = schemaValidator.ValidateCatalog(catalogPath);
    return diagnostics.All(d => d.Severity != PatchDiagnosticSeverity.Error)
        && diagnostics.Any(d => d.Code == DiagnosticCodes.SchemaValidationOk);
}

bool SchemaValidateRejectsCatalogUnknownProperty()
{
    // Repo entries are closed objects — additionalProperties: false. A typo
    // like 'summery' (missing -a-) gets caught at validate time so catalog
    // authors see the issue before the manager ever loads the file.
    var yaml = """
        catalogFormatVersion: "0.1"
        catalog:
          name: Typo Catalog
        repos:
          - owner: someone
            repo: their-repo
            summery: typo in property name
        """;
    return RunCatalogValidation(yaml, expectError: true);
}

bool SchemaValidateRejectsCatalogUnknownVersion()
{
    // Only "0.1" is currently accepted. A future bump (e.g. "0.2") deliberately
    // breaks old validators so authors get a clear "upgrade your tooling" signal.
    var yaml = """
        catalogFormatVersion: "0.99"
        catalog:
          name: Future Catalog
        """;
    return RunCatalogValidation(yaml, expectError: true);
}

bool SchemaValidateRejectsCatalogBadOwner()
{
    // GitHub owner names: alphanumerics + . _ -. Anything else (e.g. a slash
    // mid-name from a copy-paste mistake) must fail the regex check.
    var yaml = """
        catalogFormatVersion: "0.1"
        catalog:
          name: Bad Owner
        repos:
          - owner: "bad/owner"
            repo: somerepo
        """;
    return RunCatalogValidation(yaml, expectError: true);
}

bool RunCatalogValidation(string yaml, bool expectError)
{
    var tempPath = Path.Combine(Path.GetTempPath(), $"pagonia-catalog-{Guid.NewGuid():N}.yaml");
    try
    {
        File.WriteAllText(tempPath, yaml);
        var diagnostics = schemaValidator.ValidateCatalog(tempPath);
        var hasError = diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error);
        return hasError == expectError;
    }
    finally
    {
        if (File.Exists(tempPath)) { File.Delete(tempPath); }
    }
}

bool SchemaRoundtripPatchPlanReport()
{
    // Produce a real plan via the existing fixture pipeline, write the JSON report to disk, then
    // validate the actual file content against the public schema. Catches drift between the
    // PatchPlanReporter shape and patch-plan-report.schema.json.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-schema-plan-{Guid.NewGuid():N}");
    var jsonPath = Path.Combine(outputRoot, "plan.json");

    try
    {
        var read = reader.ReadMod(modPath);
        if (!read.Success || read.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [read.Value]);
        reporter.WriteReports(plan, markdownPath: null, jsonPath: jsonPath);

        var json = File.ReadAllText(jsonPath);
        return ValidateAgainstSchema(json, "patch-plan-report.schema.json");
    }
    finally
    {
        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}

bool SchemaRoundtripPatchApplyReport()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "cheaper-sawmill");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-schema-apply-{Guid.NewGuid():N}");
    var outputGameRoot = Path.Combine(workRoot, "game");
    var jsonPath = Path.Combine(workRoot, "apply.json");

    try
    {
        var read = reader.ReadMod(modPath);
        if (!read.Success || read.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [read.Value]);
        var diagnostics = applier.Apply(gameRoot, outputGameRoot, plan);
        applyReporter.WriteReports(plan, diagnostics, outputGameRoot, markdownPath: null, jsonPath: jsonPath, planSource: "directMods");

        var json = File.ReadAllText(jsonPath);
        return ValidateAgainstSchema(json, "patch-apply-report.schema.json");
    }
    finally
    {
        if (Directory.Exists(workRoot))
        {
            Directory.Delete(workRoot, recursive: true);
        }
    }
}

bool SchemaRoundtripPatchPlanReportArithmetic()
{
    // The arithmetic ops (multiplyValue/addValue) must be in the report's
    // OperationType enum. Plan the multiply-sawmill fixture and validate the
    // emitted plan report against the public schema — guards the regression
    // where an arithmetic-op report failed its own schema.
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var outputRoot = Path.Combine(Path.GetTempPath(), $"pagonia-schema-plan-arith-{Guid.NewGuid():N}");
    var jsonPath = Path.Combine(outputRoot, "plan.json");

    try
    {
        var read = reader.ReadMod(modPath);
        if (!read.Success || read.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [read.Value]);
        if (plan.Writes.Single() is not { OperationType: "multiplyValue" }) { return false; }

        reporter.WriteReports(plan, markdownPath: null, jsonPath: jsonPath);
        return ValidateAgainstSchema(File.ReadAllText(jsonPath), "patch-plan-report.schema.json");
    }
    finally
    {
        if (Directory.Exists(outputRoot)) { Directory.Delete(outputRoot, recursive: true); }
    }
}

bool SchemaRoundtripPatchApplyReportArithmetic()
{
    var gameRoot = Path.Combine(patcherRoot, "fixtures", "game-gdb-mini");
    var modPath = Path.Combine(patcherRoot, "fixtures", "mods", "multiply-sawmill");
    var workRoot = Path.Combine(Path.GetTempPath(), $"pagonia-schema-apply-arith-{Guid.NewGuid():N}");
    var outputGameRoot = Path.Combine(workRoot, "game");
    var jsonPath = Path.Combine(workRoot, "apply.json");

    try
    {
        var read = reader.ReadMod(modPath);
        if (!read.Success || read.Value is null) { return false; }

        var plan = planner.Plan(gameRoot, [read.Value]);
        var diagnostics = applier.Apply(gameRoot, outputGameRoot, plan);
        applyReporter.WriteReports(plan, diagnostics, outputGameRoot, markdownPath: null, jsonPath: jsonPath, planSource: "directMods");
        return ValidateAgainstSchema(File.ReadAllText(jsonPath), "patch-apply-report.schema.json");
    }
    finally
    {
        if (Directory.Exists(workRoot)) { Directory.Delete(workRoot, recursive: true); }
    }
}

bool ValidateAgainstSchema(string json, string schemaFileName)
{
    var schemaPath = Path.Combine(root, "schemas", "patcher", schemaFileName);
    if (!File.Exists(schemaPath))
    {
        Console.Error.WriteLine($"Schema file not found: {schemaPath}");
        return false;
    }

    if (!schemaCache.TryGetValue(schemaPath, out var schema))
    {
        schema = Json.Schema.JsonSchema.FromFile(schemaPath);
        schemaCache[schemaPath] = schema;
    }
    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var results = schema.Evaluate(doc.RootElement, new Json.Schema.EvaluationOptions { OutputFormat = Json.Schema.OutputFormat.Hierarchical });

    if (results.IsValid)
    {
        return true;
    }

    DumpSchemaErrors(results, depth: 0);
    return false;
}

void DumpSchemaErrors(Json.Schema.EvaluationResults result, int depth)
{
    if (result.IsValid) { return; }
    var indent = new string(' ', depth * 2);
    if (result.Errors is { Count: > 0 })
    {
        foreach (var (keyword, message) in result.Errors)
        {
            var location = result.InstanceLocation.ToString();
            var locationHint = string.IsNullOrEmpty(location) ? "(root)" : location;
            Console.Error.WriteLine($"{indent}schema error at {locationHint}: {message} [{keyword}]");
        }
    }
    foreach (var child in result.Details ?? [])
    {
        DumpSchemaErrors(child, depth + 1);
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
        // The repo root is identified by both the README.md and the schemas/ folder being
        // present; README alone is too common (every tool subfolder has one). schemas/ is
        // committed to the public repo, unlike some internal-only folders.
        if (File.Exists(Path.Combine(directory.FullName, "README.md"))
            && Directory.Exists(Path.Combine(directory.FullName, "schemas")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not find repository root.");
}

// --- Entry-operation test helpers ---------------------------------------------------------------

string WriteEntryMod(string modRoot, string modId, string manifestExtra)
{
    Directory.CreateDirectory(modRoot);
    File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Entry-Op Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11727+193140"
description: Fixture mod for entry-operation tests.
requiredPackages:
  - dlc1
{manifestExtra}
""");
    return modRoot;
}

bool EntryOperationsMissingSourceDiagnostic()
{
    var modRoot = Path.Combine(Path.GetTempPath(), $"pagonia-entries-missing-{Guid.NewGuid():N}");
    WriteEntryMod(modRoot, "pagonia.fixture.entries.missing-source", """
entries:
  replace:
    - path: dlc1/gui/icons/x.image
      source: entries/does-not-exist.image
""");

    try
    {
        var read = reader.ReadMod(modRoot);
        if (!read.Success || read.Value is null) return false;

        var plan = planner.Plan(modRoot, read.Value);
        return !plan.Success
            && plan.Diagnostics.Any(d => d.Code == "entrySourceMissing")
            && plan.EntryWrites.Count == 0;
    }
    finally
    {
        if (Directory.Exists(modRoot)) Directory.Delete(modRoot, recursive: true);
    }
}

bool EntryOperationsApplyRoundTrip()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-entries-apply-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(tempDir, "mod");
    Directory.CreateDirectory(Path.Combine(modRoot, "entries"));

    // Source files for replace + add.
    var replacePayload = new byte[] { 1, 2, 3, 4 };
    var addPayload = new byte[] { 9, 9, 9 };
    File.WriteAllBytes(Path.Combine(modRoot, "entries", "icon_new.image"), replacePayload);
    File.WriteAllBytes(Path.Combine(modRoot, "entries", "new_texture.bc.texture"), addPayload);

    WriteEntryMod(modRoot, "pagonia.fixture.entries.apply", """
entries:
  replace:
    - path: dlc1/gui/icons/icon_new.image
      source: entries/icon_new.image
  add:
    - path: dlc1/textures/new_texture.bc.texture
      source: entries/new_texture.bc.texture
  delete:
    - dlc1/sounds/annoying.audio
""");

    // Minimal source-game root so CopyGameRoot has something to copy.
    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(Path.Combine(sourceGameRoot, "core", "gdb"));
    File.WriteAllText(Path.Combine(sourceGameRoot, "core", "gdb", "noop.gd.xml"), "<EntityGroup />");

    var outputRoot = Path.Combine(tempDir, "out");

    try
    {
        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) return false;

        var plan = planner.Plan(sourceGameRoot, [modResult.Value]);
        if (!plan.Success) return false;
        if (plan.EntryWrites.Count != 3) return false;

        var diagnostics = applier.Apply(sourceGameRoot, outputRoot, plan);
        var failed = diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error);
        if (failed) return false;

        var iconBytes = File.ReadAllBytes(Path.Combine(outputRoot, "dlc1", "gui", "icons", "icon_new.image"));
        var textureBytes = File.ReadAllBytes(Path.Combine(outputRoot, "dlc1", "textures", "new_texture.bc.texture"));
        var deletions = File.ReadAllLines(Path.Combine(outputRoot, ".entries-deleted.txt"));

        return iconBytes.SequenceEqual(replacePayload)
            && textureBytes.SequenceEqual(addPayload)
            && deletions.SequenceEqual(new[] { "dlc1/sounds/annoying.audio" });
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

bool EntryOperationsConflict()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-entries-conflict-{Guid.NewGuid():N}");
    var modA = Path.Combine(tempDir, "mod-a");
    var modB = Path.Combine(tempDir, "mod-b");
    Directory.CreateDirectory(Path.Combine(modA, "entries"));
    Directory.CreateDirectory(Path.Combine(modB, "entries"));
    File.WriteAllBytes(Path.Combine(modA, "entries", "icon.image"), new byte[] { 1 });
    File.WriteAllBytes(Path.Combine(modB, "entries", "icon.image"), new byte[] { 2 });

    WriteEntryMod(modA, "pagonia.fixture.entries.conflict-a", """
entries:
  replace:
    - path: dlc1/gui/icons/icon.image
      source: entries/icon.image
""");
    WriteEntryMod(modB, "pagonia.fixture.entries.conflict-b", """
entries:
  replace:
    - path: dlc1/gui/icons/icon.image
      source: entries/icon.image
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(sourceGameRoot);

    try
    {
        var loadedA = reader.ReadMod(modA).Value!;
        var loadedB = reader.ReadMod(modB).Value!;

        var plan = planner.Plan(sourceGameRoot, [loadedA, loadedB]);
        return !plan.Success
            && plan.EntryConflicts.Count == 1
            && plan.EntryConflicts[0].Path == "dlc1/gui/icons/icon.image"
            && plan.Diagnostics.Any(d => d.Code == "entryConflict");
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

bool EntryOperationsTwoDeletesAreIdempotent()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-entries-twodeletes-{Guid.NewGuid():N}");
    var modA = Path.Combine(tempDir, "mod-a");
    var modB = Path.Combine(tempDir, "mod-b");
    WriteEntryMod(modA, "pagonia.fixture.entries.del-a", """
entries:
  delete:
    - dlc1/sounds/x.audio
""");
    WriteEntryMod(modB, "pagonia.fixture.entries.del-b", """
entries:
  delete:
    - dlc1/sounds/x.audio
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(sourceGameRoot);

    try
    {
        var loadedA = reader.ReadMod(modA).Value!;
        var loadedB = reader.ReadMod(modB).Value!;
        var plan = planner.Plan(sourceGameRoot, [loadedA, loadedB]);
        return plan.Success
            && plan.EntryConflicts.Count == 0
            && plan.EntryWrites.Count == 2;
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

string WritePakScaffoldMod(string modRoot, string modId, string pakBlock, string entriesBlock = "")
{
    Directory.CreateDirectory(modRoot);
    // The two blocks come in as C# raw strings that don't carry a trailing
    // newline; concatenating them directly would smush the last line of
    // entriesBlock into the first line of pakBlock. Append "\n" before
    // splicing so each block stays on its own logical YAML lines.
    var entriesYaml = string.IsNullOrEmpty(entriesBlock) ? string.Empty : entriesBlock + "\n";
    var pakYaml = string.IsNullOrEmpty(pakBlock) ? string.Empty : pakBlock + "\n";
    File.WriteAllText(Path.Combine(modRoot, "mod.yaml"), $"""
patchFormatVersion: "0.1"
id: {modId}
name: Scaffold Test Mod
version: "0.1.0"
author: Pagonia Land
gameDatabaseVersion: "1.3.0-11727+193140"
description: Fixture mod for Pattern B scaffold tests.
requiredPackages:
  - core
{entriesYaml}{pakYaml}
""");
    return modRoot;
}

bool PakScaffoldWritesAllFourFiles()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-pak-scaffold-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(tempDir, "mod");
    Directory.CreateDirectory(Path.Combine(modRoot, "entries"));
    // The mod ships a single new *.gd.xml inside its own namespace.
    File.WriteAllText(Path.Combine(modRoot, "entries", "new_buildings.gd.xml"), "<EntityGroup />");

    WritePakScaffoldMod(modRoot, "pagonia.fixture.scaffold.happy",
        pakBlock: """
pak:
  name: my-overlay
  summary: Adds a new building
  author: Modder McModface
  image: my-overlay/images/preview.image
  dependencies:
    - core
""",
        entriesBlock: """
entries:
  add:
    - path: my-overlay/gdb/new_buildings.gd.xml
      source: entries/new_buildings.gd.xml
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(Path.Combine(sourceGameRoot, "core", "gdb"));
    File.WriteAllText(Path.Combine(sourceGameRoot, "core", "gdb", "noop.gd.xml"), "<EntityGroup />");

    var outputRoot = Path.Combine(tempDir, "out");

    try
    {
        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) return false;

        var plan = planner.Plan(sourceGameRoot, [modResult.Value]);
        if (!plan.Success) return false;

        var diagnostics = applier.Apply(sourceGameRoot, outputRoot, plan);
        if (diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)) return false;

        // 1. manifest.json with PascalCase keys + the right values
        var manifestPath = Path.Combine(outputRoot, "my-overlay", "manifest.json");
        if (!File.Exists(manifestPath)) return false;
        var manifestJson = File.ReadAllText(manifestPath);
        if (!manifestJson.Contains("\"Name\": \"my-overlay\"", StringComparison.Ordinal)) return false;
        if (!manifestJson.Contains("\"Summary\": \"Adds a new building\"", StringComparison.Ordinal)) return false;
        if (!manifestJson.Contains("\"Author\": \"Modder McModface\"", StringComparison.Ordinal)) return false;
        if (!manifestJson.Contains("\"Image\": \"my-overlay/images/preview.image\"", StringComparison.Ordinal)) return false;
        if (!manifestJson.Contains("\"core\"", StringComparison.Ordinal)) return false;

        // 2. files.json: GameDatabase key points at the module's .gd.bin
        var filesPath = Path.Combine(outputRoot, "my-overlay", "files.json");
        if (!File.Exists(filesPath)) return false;
        var filesJson = File.ReadAllText(filesPath);
        if (!filesJson.Contains("\"Key\": \"GameDatabase\"", StringComparison.Ordinal)) return false;
        if (!filesJson.Contains("\"my-overlay/my-overlay.gd.bin\"", StringComparison.Ordinal)) return false;

        // 3. <name>.gd.bin: lists the added XML; decode and check
        var gdBinPath = Path.Combine(outputRoot, "my-overlay", "my-overlay.gd.bin");
        if (!File.Exists(gdBinPath)) return false;
        using (var gdBinStream = File.OpenRead(gdBinPath))
        {
            var read = new PagoniaLand.Paker.GdBinReader().Read(gdBinStream);
            if (!read.Success || read.Index is null) return false;
            if (read.Index.Entries.Count != 1) return false;
            if (read.Index.Entries[0] != "my-overlay/gdb/new_buildings.gd.xml") return false;
        }

        // 4. memory.bin: 28 bytes
        var memoryPath = Path.Combine(outputRoot, "my-overlay", "memory.bin");
        var memoryBytes = File.ReadAllBytes(memoryPath);
        if (memoryBytes.Length != 28) return false;

        return diagnostics.Any(d => d.Code == DiagnosticCodes.ScaffoldWritten);
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

bool PakScaffoldSkipsFilesAndGdBinWhenNoXml()
{
    // Asset-only overlay (e.g. an image-only mod): no *.gd.xml means
    // files.json + .gd.bin should be omitted, like System.pak from mod.io.
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-pak-scaffold-noxml-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(tempDir, "mod");
    Directory.CreateDirectory(modRoot);

    WritePakScaffoldMod(modRoot, "pagonia.fixture.scaffold.noxml",
        pakBlock: """
pak:
  name: image-only
  summary: Just an image
  author: Tester
  image: image-only/preview.image
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(sourceGameRoot);

    var outputRoot = Path.Combine(tempDir, "out");

    try
    {
        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) return false;

        var plan = planner.Plan(sourceGameRoot, [modResult.Value]);
        if (!plan.Success) return false;

        var diagnostics = applier.Apply(sourceGameRoot, outputRoot, plan);
        if (diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)) return false;

        var moduleDir = Path.Combine(outputRoot, "image-only");
        return File.Exists(Path.Combine(moduleDir, "manifest.json"))
            && File.Exists(Path.Combine(moduleDir, "memory.bin"))
            && !File.Exists(Path.Combine(moduleDir, "files.json"))
            && !File.Exists(Path.Combine(moduleDir, "image-only.gd.bin"));
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

bool PakScaffoldRejectsNameWithSlash()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-pak-scaffold-badname-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(tempDir, "mod");

    WritePakScaffoldMod(modRoot, "pagonia.fixture.scaffold.badname",
        pakBlock: """
pak:
  name: bad/name
  summary: Invalid scaffold name
  author: Tester
  image: ""
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(sourceGameRoot);
    var outputRoot = Path.Combine(tempDir, "out");

    try
    {
        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) return false;

        var plan = planner.Plan(sourceGameRoot, [modResult.Value]);
        if (!plan.Success) return false;

        var diagnostics = applier.Apply(sourceGameRoot, outputRoot, plan);
        return diagnostics.Any(d => d.Code == DiagnosticCodes.ScaffoldNameInvalid);
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

bool PakScaffoldDefaultsDependenciesToCore()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"pagonia-pak-scaffold-default-deps-{Guid.NewGuid():N}");
    var modRoot = Path.Combine(tempDir, "mod");

    // No `dependencies:` key under pak: — must default to ["core"].
    WritePakScaffoldMod(modRoot, "pagonia.fixture.scaffold.default-deps",
        pakBlock: """
pak:
  name: deps-default
  summary: Defaults dependencies
  author: Tester
  image: ""
""");

    var sourceGameRoot = Path.Combine(tempDir, "source-game");
    Directory.CreateDirectory(sourceGameRoot);
    var outputRoot = Path.Combine(tempDir, "out");

    try
    {
        var modResult = reader.ReadMod(modRoot);
        if (!modResult.Success || modResult.Value is null) return false;

        var plan = planner.Plan(sourceGameRoot, [modResult.Value]);
        if (!plan.Success) return false;

        var diagnostics = applier.Apply(sourceGameRoot, outputRoot, plan);
        if (diagnostics.Any(d => d.Severity == PatchDiagnosticSeverity.Error)) return false;

        var manifestJson = File.ReadAllText(Path.Combine(outputRoot, "deps-default", "manifest.json"));
        return manifestJson.Contains("\"core\"", StringComparison.Ordinal);
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
    }
}

// Fixtures for the patch-set gating tests. A static type keeps the const YAML out
// of the top-level statement flow (which must precede local functions).
static class PatchSetTests
{
    public const string SawmillYaml = """
operations:
  - id: sawmill-cost
    operation: replaceValue
    risk: low
    reason: Lower the Sawmill softwood cost.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
""";

    public const string ModYaml = """
patchFormatVersion: "0.1"
id: pagonia-land.test.patchset
name: PatchSet Test
version: "0.1.0"
author: TheLavaBlock
gameDatabaseVersion: "1.3.0-11768+193445"
description: An optional patch set gated on dlc1.
requiredPackages:
  - core
optionalPackages:
  - dlc1
patchSets:
  - id: dlc1-only
    optional: true
    requiresPackages:
      - dlc1
    patches:
      - patches/sawmill.yaml
""";
}
