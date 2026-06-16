# Pagonia Land Patcher CLI

This page documents the command line contract for `pagonia-patcher`.

The same patcher should work in three ways:

- directly from PowerShell
- as a subprocess called by any mod manager
- as a library through `PagoniaLand.Patcher.Core`

The CLI project is only responsible for arguments, console output, files, and exit codes. GameDatabase patch logic belongs in `PagoniaLand.Patcher.Core`.

## Exit Codes

| Code | Name | Meaning |
| ---: | --- | --- |
| `0` | `Success` | The command completed successfully. |
| `1` | `Error` | The command failed because input files, manifests, XML targets, validation, or apply logic produced errors. |
| `2` | `Conflict` | A valid plan was built, but enabled mods contain a blocking conflict. |
| `64` | `Usage` | CLI arguments are missing or invalid. |

These values are also exposed by `PatcherExitCodes` in `PagoniaLand.Patcher.Core`.

## Commands

Show the patcher version:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- --version
```

Inspect a mod manifest and its patch files (also lists any declared `tweaks:` with their type, default, and range / enum values):

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-mod --mod .\fixtures\mods\tweakable-sawmill
```

Validate a mod manifest and patch file layout:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- validate-mod --mod .\fixtures\mods\cheaper-sawmill
```

`validate-mod` runs the patcher's own internal validation (`ManifestValidator`) — the rules the patcher enforces when planning. For mods that declare `tweaks:`, it also runs a lint pass that warns (without failing) about likely authoring mistakes the schema can't catch: a tweak declared but never referenced by a `{{ tweaks.<id> }}` placeholder, a boolean ternary used on a non-boolean tweak, or a numeric tweak whose `min` exceeds its `max`.

For mods that ship their own GameDatabase overlay (`*.gd.xml` files declared under `entries:`), `validate-mod` also runs the **conflict-minimising authoring advisor**. It encodes the engine maintainers' guidance — prefer the additive, stackable inheritance modes (`Incremental` / `Template`) over the destructive, last-loaded-wins ones (`Replace` / `Unload`) so a mod co-exists cleanly with others. Findings are advisory (`validate-mod` still exits `0`):

- **`usesDestructiveInheritanceMode`** (info) — names each `Replace` / `Unload` entity and reminds you that the last-loaded mod wins; if the change is purely additive, use `Incremental` / `Template`.
- **`unloadsReferencedEntity`** (warning) — an `Unload` whose target GUID is still referenced elsewhere in the same overlay; the reference will dangle after the unload.
- **`inheritanceConflictRisk`** (info) — a per-mod summary score (low / medium / high) based on how many entities the mod `Replace`s or `Unload`s.

Pass an optional `--game-root <unpacked-game-gdb>` to turn on the **base-aware** checks, which compare the overlay against the shipped database:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- validate-mod --mod .\fixtures\mods\overlay-replace-additive --game-root .\fixtures\game-gdb-mini
```

- **`unloadsReferencedEntity`** widens to warn when the unload target is still referenced anywhere in core/dlc (not just in your own overlay) — the dangling-reference case the engine maintainers flagged.
- **`replaceCouldBeIncremental`** (warning) — your `Replace` keeps every part of the inherited entity verbatim and only adds to it, so it could be an `Incremental` that stacks with other mods instead of a wholesale replace that the last-loaded mod wins. The diff is conservative: any modified or removed value means a genuine rewrite and is not flagged.

For a check that compares your `mod.yaml`, patches, collections, or lockfiles against the canonical JSON Schemas under `schemas/mod-patches/`, use `schema-validate`:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- schema-validate --mod .\fixtures\mods\cheaper-sawmill
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- schema-validate --collection .\fixtures\collections\local-beginner.collection.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- schema-validate --lock .\fixtures\out\collection-lock.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- schema-validate --repo-index ..\..\examples\mod-repo-example\index.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- schema-validate --catalog ..\..\examples\mod-catalog-example\catalog.yaml
```

`schema-validate` is the conformance check third-party tools rely on. The schemas under `schemas/mod-patches/` are the public contract — any mod that passes `schema-validate` is guaranteed to parse cleanly in mod managers, IDE plugins, web validators, and any future tooling built against that contract. Run it before publishing a mod (in CI or as a local pre-commit step).

The `--repo-index` variant validates a Git-distribution repo's top-level `index.yaml` against `schemas/mod-patches/repo-index.schema.json`. Mod authors who publish through a Git repo (one mod or many, with or without collections) should wire this into their CI — see [`examples/mod-repo-example/`](../../examples/mod-repo-example/) for a working reference repo with a [`validate.yml`](../../examples/mod-repo-example/.github/workflows/validate.yml) GitHub Action.

### Keeping `index.yaml` in sync with each `mod.yaml` (`index-check` / `index build`)

A repo's `index.yaml` duplicates a curated subset of every mod's `mod.yaml` so the manager can browse a catalog (name, version, safety) *without* fetching each mod folder. `schema-validate` checks the index's *shape*, but not that those copies still match the manifests they mirror — so the copy can silently drift (a `safeToRemove` flipped in `mod.yaml`, a version bumped, a game-database version updated, and the index left stale, advertising the wrong thing to users).

The **mirror contract** — index fields that, *when the entry carries them*, must equal their `mod.yaml` source: `displayName` (↔ manifest `name`), `version`, `gameDatabaseVersion`, and the four `safetyFlags` (↔ the flat `requiresNewGame` / `safeToRemove` / `multiplayerSafe` / `campaignSafe`). The index is a curated *subset*, so an entry may **omit** a field (the catalog just won't surface it — that's a curation choice, not drift); only a present-but-wrong copy is flagged, since that's what misleads a browsing user. The index's `description`, `tags`, and `screenshots` are likewise curated and never compared (the index carries a short catalog blurb; the manifest the full text).

```powershell
# Detect drift (read-only). Errors on a mirror mismatch, an index entry whose
# mod.yaml is missing, a mod folder absent from the index, or an id mismatch.
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- index-check ..\..\official-mods

# Re-sync the index's mirror fields from each mod.yaml, in place (formatting preserved).
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- index build ..\..\official-mods
# CI gate: report what would change, exit non-zero on drift, write nothing.
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- index build ..\..\official-mods --check
```

`index build` only swaps drifted *values* on fields that already exist in both files (so the rewrite is surgical and leaves surrounding formatting untouched); structural gaps it can't resolve by a value swap — a missing entry, an orphaned entry, an absent field — are reported for you to fix by hand rather than reformatting the file. Wire `index-check` (or `index build --check`) into the same CI job as `schema-validate`.

**Scope.** Missing-entry detection assumes the conventional `mods/<dir>/mod.yaml` layout — a mod kept under a different path isn't discovered as "missing from the index". Collection entries (the index's `collections:` list) are **not** cross-checked against their `*.collection.yaml`; keep those in sync by hand. If a re-sync would produce YAML that no longer parses (an unusually-shaped value that needs quoting), `index build` reports `indexMirrorWriteAborted` and writes nothing rather than corrupt the file.

The `--catalog` variant validates a `catalog.yaml` (a curated list of mod-distribution repos plus optional federated references to other catalogs) against `schemas/mod-patches/catalog.schema.json`. The manager subscribes to catalogs to enable cross-repo discovery — see [`examples/mod-catalog-example/`](../../examples/mod-catalog-example/) for a working reference catalog with a nested federation entry.

Build a dry-run plan:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill
```

Supply tweak values for mods that declare a `tweaks:` block (see [Declarative Mod Patch Format → Tweaks](../../docs/mod-patch-format.md#tweaks-tweaks-block)). The `--tweak` flag is repeatable and takes `<mod-id>:<tweak-id>=<value>`; any tweak left unspecified resolves to its declared default. The same flag works on `apply`.

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\templated-sawmill --tweak pagonia-land.fixture.templated-sawmill:softwood-cost=5
```

Placeholders resolve at plan time, so the plan output and reports show the substituted values plus a per-mod `resolvedTweaks` summary. A value outside a tweak's declared range warns (`tweakValueOutOfRange`) but is still applied; a placeholder naming an undeclared tweak fails the plan (`tweakUndeclared`).

Build a dry-run plan from a local collection:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods
```

Build a dry-run plan from multiple local collections:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --collection .\fixtures\collections\local-duplicate.collection.yaml --mods-root .\fixtures\mods
```

Build a dry-run plan from a collection lockfile:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --lock .\fixtures\out\collection-lock.yaml --mods-root .\fixtures\mods
```

A lockfile is the deterministic alternative to `--collection`: instead of resolving a collection manifest into local mods at plan time, the lockfile directly names mod IDs, versions, and archive content hashes. The patcher verifies that each enabled mod still exists under `--mods-root` and that its content hash matches the lockfile.

Write human-readable and machine-readable plan reports:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out\plan.md --json .\fixtures\out\plan.json
```

Apply a clean plan to a separate output folder:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out
```

Apply a clean plan from one or more local collections:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods --out .\fixtures\out
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --collection .\fixtures\collections\local-duplicate.collection.yaml --mods-root .\fixtures\mods --out .\fixtures\out
```

Write human-readable and machine-readable apply reports alongside the output game folder:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out --report .\fixtures\out\apply.md --json .\fixtures\out\apply.json
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods --out .\fixtures\out --report .\fixtures\out\apply.md --json .\fixtures\out\apply.json
```

`apply --out` is the output game folder. `apply --report` and `apply --json` are optional Markdown and JSON apply reports written alongside it.

Apply a clean plan from a collection lockfile:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --lock .\fixtures\out\collection-lock.yaml --mods-root .\fixtures\mods --out .\fixtures\out
```

The lockfile-driven apply runs the same plan-then-apply pipeline as the collection-driven variant. It is the reproducible variant: as long as the locked mods are still present under `--mods-root` and their content hashes match, the apply produces the same writes that the matching `resolve-collection` produced earlier.

Inspect and resolve a local collection:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-collection --collection .\fixtures\collections\local-beginner.collection.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- resolve-collection --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods --lock .\fixtures\out\collection-lock.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-lock --lock .\fixtures\out\collection-lock.yaml
```

Export a tested direct mod list as a collection:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- export-collection --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill --out .\fixtures\out\exported.collection.yaml --id pagonia-land.local.exported --name "Exported Local Set"
```

## Console Output

Console output is meant for humans. It may contain readable diagnostics, planned writes, conflicts, and paths to written reports.

Tools should use exit codes and JSON reports for automation. Mod managers (including `pagonia-manager`, which links `PagoniaLand.Patcher.Core` directly) should not parse ordinary text output unless it is only showing it to users.

## Patch Operations

The patcher implements all ten operations defined in [patch-file.schema.json](../../schemas/mod-patches/patch-file.schema.json):

| Operation | What it changes | Required fields |
| --- | --- | --- |
| `replaceValue` | The text value of an XML element resolved by `target.path`. | `expectedOldValue`, `value` |
| `multiplyValue` | The text value of `target.path`, set to `expectedOldValue × factor` (rounded, optionally clamped). | `expectedOldValue`, `factor`. Optional `rounding` (`round`/`floor`/`ceil`, default `round`), `clampMin`, `clampMax`. |
| `addValue` | The text value of `target.path`, set to `expectedOldValue + delta` (negative `delta` subtracts; rounded, optionally clamped). | `expectedOldValue`, `delta`. Optional `rounding`, `clampMin`, `clampMax`. |
| `replaceAttribute` | One attribute on the XML element resolved by `target.path`. | `attribute`, `expectedOldValue`, `value` |
| `replaceNode` | The full XML element resolved by `target.path`, replaced with the literal `xml` fragment. | `xml`. `expectedOldXml` is optional but recommended. |
| `addListItem` | Appends an `xml` fragment as a child of the container resolved by `target.path`. | `xml` |
| `removeListItem` | Removes one child of the container resolved by `target.path` whose XML matches `expectedOldXml`. | `expectedOldXml` |
| `addEntity` | Inserts a complete `<Entity>` element into the `<Group Name="...">/<Entities>` named by `target.entityName`. | `xml`. The new entity must carry `Name` and `Guid` attributes, and the GUID must not already exist in the file. |
| `removeEntity` | Removes the entity identified by `target.entityGuid` from its file. | `expectedOldXml` matching the current entity (normalised). |
| `mergeComponent` | Adds children of the `xml` fragment to the existing `<Values>/<target.component>` on the entity, or inserts the full component when it does not exist yet. | `xml`. The root element of the fragment must match `target.component`. |

Conflict detection groups writes by kind:

- single-target operations (`replaceValue`, `multiplyValue`, `addValue`, `replaceAttribute`, `replaceNode`) collide on file, entity GUID, component, path, and attribute — so a `multiplyValue` and a `replaceValue` on one field conflict
- list operations (`addListItem`, `removeListItem`) additionally include the normalised item XML, so two adds with different XML coexist, but two adds with the same XML or an add and a remove of the same item conflict
- entity operations (`addEntity`, `removeEntity`) collide on file and entity GUID, so an add and a remove targeting the same GUID conflict
- `mergeComponent` collides on file, entity GUID, and component name, so two merges into the same component conflict
- different kinds on the same target (for example `replaceValue` and `addListItem`) do not falsely conflict

## JSON Plan Reports

The `plan --json <path>` option writes a stable JSON report containing:

- plan source, such as `directMods`, `collection`, or `collections`
- overall success state
- enabled mods
- planned writes, each annotated with the operation type and (when applicable) the attribute name
- conflicts
- diagnostics

The JSON shape is documented by [patch-plan-report.schema.json](../../schemas/patcher/patch-plan-report.schema.json).

## JSON Apply Reports

The `apply --json <path>` option writes a stable JSON report containing:

- plan source (`directMods`, `collection`, `collections`, or `lockfile`)
- overall apply success state
- output game folder path
- mod count and planned write count from the plan
- applied write count and failed write count derived from the apply diagnostics
- enabled mods
- writes from the plan
- apply diagnostics (`patchApplied`, `applyTargetMissing`, `applyOldValueMismatch`, `applyComplete`)

The JSON shape is documented by [patch-apply-report.schema.json](../../schemas/patcher/patch-apply-report.schema.json).

## Direct Mod Lists And Implicit Mod Sets

When the CLI receives direct `--mods` arguments, the patcher treats that list as an implicit local mod set.

The argument order is the initial load order:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill
```

This should use the same internal pipeline as an explicit collection:

```text
direct --mods list -> resolved mod set -> validation -> plan -> report/apply
collection manifest -> resolved mod set -> validation -> plan -> report/apply
multiple collection manifests -> resolved mod set -> validation -> plan -> report/apply
```

When multiple collections are passed, the CLI order decides the collection order. Inside each collection, its `loadOrder` is still respected. If the same mod and version appears more than once, it is loaded once. If two collections request different versions of the same mod, planning stops with an error.

That design keeps the simple CLI path compatible with future profiles, collections, and mod manager workflows.

## Publishing A Standalone Binary

The patcher publishes as a Native AOT single-file executable so end users do not need a .NET runtime. From the patcher solution folder, run one of:

```powershell
# Windows x64
dotnet publish .\src\PagoniaLand.Patcher.Cli -c Release -r win-x64

# Linux x64
dotnet publish ./src/PagoniaLand.Patcher.Cli -c Release -r linux-x64

# macOS (Intel / Apple Silicon)
dotnet publish ./src/PagoniaLand.Patcher.Cli -c Release -r osx-x64
dotnet publish ./src/PagoniaLand.Patcher.Cli -c Release -r osx-arm64
```

The output ends up in `src/PagoniaLand.Patcher.Cli/bin/Release/net8.0/<rid>/publish/`. On Windows that is `pagonia-patcher.exe`; the binary bundles YamlDotNet + JsonSchema.Net into a single Native AOT image. The binary can be copied somewhere on `PATH` and used directly:

```powershell
.\pagonia-patcher.exe --version
.\pagonia-patcher.exe plan --game .\game --collection .\my.collection.yaml --mods-root .\mods --json .\plan.json
```

AOT cross-compilation needs the target's native toolchain, so producing all platforms from one machine is not currently supported. Build each binary on its own platform.

Implementation notes:

- `PublishAot=true` in the CLI project triggers Native AOT.
- YamlDotNet's reflection-based deserialisation requires the YAML model types to be reachable. The Pagonia Land Patcher roots them via `[DynamicDependency]` attributes on the `ManifestReader` constructor.
- `System.Text.Json` reports use source-generated contexts (`PatchPlanJsonContext`, `PatchApplyJsonContext`), which are AOT-clean by construction.
- `System.Xml.Linq` is fully AOT-compatible.
- `JsonSchema.Net` (used by `SchemaValidator`) loads the canonical schemas from embedded assembly resources rather than the filesystem, so the AOT binary can run `schema-validate` from any working directory without needing the `schemas/` folder to be present.

## Mod Manager Integration

A mod manager can integrate with the patcher in two ways:

- call the CLI and read exit codes plus JSON reports
- reference `PagoniaLand.Patcher.Core` and use the same planning/apply services directly

The patcher should stay independent from GUI-specific concepts. A GUI can decide how to download, enable, sort, group, and display mods, but patch validation and XML planning should remain in the shared core.

Future direction:

- report whether a plan came from direct mods, a profile, or a collection
- plan and apply directly from a collection or lockfile
- keep adding diagnostics without breaking existing JSON fields
