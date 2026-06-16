# 🔗 Patcher And Mod Manager Architecture

How the three Pagonia Land tools fit together, what each one owns, and what integrators can rely on.

The ecosystem ships as **three Native AOT command-line binaries** plus a shared set of **JSON Schemas + diagnostic codes** as the integration contract:

```text
pagonia-manager        stateful — mod store, profiles, plan / deploy / rollback
    ↳ links Patcher.Core   library, also shipped as pagonia-patcher CLI
    ↳ links Paker.Core     library, also shipped as pagonia-paker CLI

pagonia-patcher        stateless — validate, plan, apply
pagonia-paker          stateless — list, unpack, pack, patch

JSON schemas + diagnostic codes form the integration contract:
    schemas/mod-patches/   patch DSL — mod.yaml, patches, collections, lockfiles
    schemas/patcher/       patcher report shapes (plan, apply)
    schemas/paker/         paker report shapes (list, unpack, pack, patch, classify, gd.bin)
    schemas/manager/       manager report shapes (install, deploy, rollback, status, ...)
```

A modder typically runs **just `pagonia-manager`**. The patcher and paker are exposed as separate CLIs because they're useful on their own (writing mods, repacking paks, CI checks) and because the manager is built on top of their library cores — keeping the patching engine separate from the mod manager keeps the ecosystem open for future GUIs, IDE plugins, or alternative managers.

## Roles

| Tool | What it does | What it does NOT do |
|---|---|---|
| **`pagonia-patcher`** | Validates `mod.yaml` + patch files; resolves XML targets; detects conflicts; builds dry-run plans; applies patches to an output folder. Stateless — every call is a fresh invocation. | No store, no profiles, no deploy backups, no `.pak` archive handling. |
| **`pagonia-paker`** | Lists / unpacks / packs / patches / classifies `.pak` archives; builds `.gd.bin` indexes; scaffolds Pattern B overlay paks. Stateless — operates on paths the caller supplies. | No mod awareness, no game-install knowledge, no patch validation. |
| **`pagonia-manager`** | Owns the stateful lifecycle the other two deliberately don't: mod store (versioned), named profiles, plan/deploy/rollback against a real game install, deploy history with SHA-256 round-trip, Pattern B overlay-pak deploy, schema-validated JSON reports. Interactive Spectre.Console shell on `mod-args` + TTY; scripted CLI on any explicit arg. | No GameDatabase parsing or `.pak` writing of its own — calls Patcher.Core / Paker.Core libraries directly. |
| **This repository's docs/schemas** | Documents the GameDatabase, defines schemas, provides examples and worked walkthroughs, ships diagnostic-code conventions. | Doesn't ship downloads or run anything itself. |

## Tool Composition

The manager imports `PagoniaLand.Patcher.Core` and `PagoniaLand.Paker.Core` as NuGet-style library references — they're built from the same solutions as the patcher and paker CLIs, but the manager links them directly rather than spawning subprocesses. This avoids JSON-report round-trips, lets the manager surface patcher/paker diagnostics inline (via `ManagerDiagnostic.From(patcherDiagnostic)`), and keeps Native AOT publishing simple.

Every project follows the same layout:

```text
tools/<tool>/
├── README.md, CLI.md              ─ user-facing docs
├── PagoniaLand.<Tool>.slnx        ─ solution
├── src/
│   ├── PagoniaLand.<Tool>.Core/   ─ library (the actual engine)
│   └── PagoniaLand.<Tool>.Cli/    ─ thin CLI wrapper
└── tests/
    └── PagoniaLand.<Tool>.Tests/
```

The important boundary is `*.Core`. A future GUI sits on top of these libraries the same way the CLI and the manager do today.

## Integration Contracts

Two stable contracts third-party tools (mod managers, IDE plugins, web validators, future GUIs) consume:

### JSON Schemas

Every state-changing command emits a schema-validated JSON report via `--json <path>`. The schemas live under [`schemas/`](../schemas/) and are embedded into each tool's binary (so `schema-validate` works offline):

- [`schemas/mod-patches/`](../schemas/mod-patches/) — the patch DSL: `mod.yaml`, patch files, collections, collection lockfiles
- [`schemas/patcher/`](../schemas/patcher/) — patcher report shapes (`plan`, `apply`)
- [`schemas/paker/`](../schemas/paker/) — paker report shapes (`list`, `unpack`, `pack`, `patch`, `classify`, `gdbin-info`)
- [`schemas/manager/`](../schemas/manager/) — manager report shapes (`install`, `uninstall`, `deploy`, `rollback`, `collectionInstall`, `status`, `deployStatus`)

Each tool ships a `schema-validate` subcommand that is the **reference implementation** of its own contract:

```powershell
pagonia-patcher schema-validate --mod path\to\mod
pagonia-manager schema-validate --kind deploy --report path\to\deploy.json
```

Anything that disagrees with `schema-validate` is the report's bug, not the schema's.

### Diagnostic Codes

Every diagnostic the tools emit carries a stable `code` field (e.g. `manager.deployBlockedByErrors`, `patcher.duplicateWriteTarget`). Codes are documented in each tool's `CLI.md` and form the supported integration surface — match against `code`, not against the human-readable `message`, because message wording is allowed to change between releases.

| Tool | Diagnostic table |
|---|---|
| Manager | [`tools/pagonia-manager/CLI.md`](../tools/pagonia-manager/CLI.md) |
| Patcher | [`tools/pagonia-patcher/CLI.md`](../tools/pagonia-patcher/CLI.md) |
| Paker | [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md) |

## Mod Archive Layout

A downloadable mod ships as a `.zip` (or distributed as a folder) with a predictable shape:

```text
cheaper-sawmill-0.1.0.zip
  mod.yaml                ─ root-level manifest (required)
  README.md               ─ optional
  patches/
    buildings.yaml        ─ patch files referenced from mod.yaml
  entries/                ─ Pattern B: loose files to seed the overlay
  preview/
    image.png             ─ optional preview asset
```

Rules:

- `mod.yaml` must be at the archive root
- patch paths in `mod.yaml` are relative to the archive root
- extracted game XML files never ship in a mod archive
- generated patch output never ships either

See [Declarative Mod Patch Format](mod-patch-format.md) for the `mod.yaml` schema and supported patch operations.

## Collections And Lockfiles

A collection is a curated list of mods. Manager handles the lifecycle:

```text
collection.yaml       (curated list of mod refs)
    │
    ▼   `pagonia-manager collection install <yaml>`
    ▼
Resolver picks each mod from --mods-root (local match)
    │
    ▼
Manifest snapshot     <store>/collections/<id>/<id>.collection.yaml
Lockfile              <store>/collections/locks/<id>.lock.yaml
Per-mod install       <store>/mods/<id>/<version>/
Profile               <store>/profiles/<id>.profile.yaml
```

The lockfile records exact resolved versions so a re-resolve is reproducible. Fetching single mods and whole collections from remote sources is available today — GitHub repositories, HTTP(S) ZIPs, and the mod.io adapter (Map-only for Pioneers of Pagonia); see [Mod Distribution Patterns](mod-distribution.md#distribution-channels) for the channel status table. One narrow gap remains: a **non-GitHub** `http(s)://` source declared on an individual mod *inside* a collection manifest is not fetched directly — it emits a `manager.collectionRemoteSourceUnsupported` warning and falls back to a same-repo / `--mods-root` lookup.

See [Mod Collections](mod-collections.md) for the collection model details.

## Deploy / Rollback Lifecycle

The manager's deploy pipeline backs up originals first, writes game files, then commits the deploy to history — in that order, so a crash mid-deploy never produces a silent lie about a successful deploy that didn't physically happen:

```text
1. plan                                        abort on errors / conflicts
2. accept-warnings check                       deployBlockedByWarnings unless --accept-warnings
3. PatchApplier into temp staging              full patcher pipeline
4. diff staging vs <game-root>                 modified-files list
5. backup originals -> <store>/deploys/<fp>/<ts>/backup/
6. write modified files -> <game-root>/<rel>   per-file atomic
7. build + copy Pattern B paks -> <game>/mods/<n>.pak
8. write manifest.yaml                          rollback needs this
9. append history.yaml entry                    THE commit point
10. update state.yaml.lastDeploy
```

Rollback undoes the deploy committed at step 9: it restores originals from `backup/`, deletes the Pattern B paks, prunes the history entry, and removes the timestamp directory. A second rollback rolls back the next entry.

The pipeline above is the **extracted-layout** mode (`<game-root>` is a folder with `core/gdb/*.gd.xml` files, same shape as the repo's local `game-gdb/`). The manager also runs against **live game installs** — folders with `pak/*.pak`. On a live install the pipeline extracts canonical paks into a fingerprinted cache at `<store>/cache/extract-v5-<fp>/` (reused across plans / deploys, with the fingerprint based on the pak filename list + `system.json` content so the cache stays warm through deploy/rollback round-trips), **selectively extracts only the paks the active profile actually touches**, runs a **sparse-apply fast-path** for pure Pattern A mods that pipes patched bytes from memory straight into the rebuild (no temp staging tree), groups modified files by their owning pak, rebuilds only the affected paks, and atomically writes them back into `<gameRoot>/pak/`. Backups are whole-pak files instead of loose XMLs, and rollback verifies the backup's SHA-256 against the manifest before overwriting the live pak. Detection happens automatically via `GameLayoutDetector` — both modes share one entry point, same commit-point ordering, same history shape.

`pagonia-manager deploys list-orphans` enumerates deploy directories whose recorded `gameRoot` is gone or has drifted (Steam update); `pagonia-manager deploys clean --keep N` trims older deploys per fingerprint while refusing to delete the entry `state.yaml.lastDeploy` currently references.

Crash safety:

- crash between 5 and 9 → files partially applied, no history entry → `status` reports the previous deploy as latest, user sees the discrepancy
- crash after 9 → deploy is recorded as successful; rollback recovers cleanly
- `DeployHistoryStore.TryRead` surfaces corrupt `history.yaml` as a `manager.deployHistoryUnreadable` diagnostic, never an unhandled exception

See [`tools/pagonia-manager/CLI.md → Deploy and Rollback`](../tools/pagonia-manager/CLI.md#deploy-and-rollback) for the full step-by-step description.

## Conflict Reporting

The patcher detects duplicate write targets across mods and emits structured diagnostics. The console renders them human-readably:

```text
Conflict:
Two mods write the same target.

Target:
game-gdb/core/gdb/buildings.gd.xml
Sawmill / AspectBuildup / Costs / Softwood Trunk / Amount

Mod A:
expected 4 -> writes 3

Mod B:
expected 4 -> writes 2

Result:
Manual decision required.
```

The same conflict appears in the `--json` plan report (validated against [`schemas/patcher/patch-plan-report.schema.json`](../schemas/patcher/patch-plan-report.schema.json)) as a `conflicts[]` entry:

```json
{
  "severity": "error",
  "type": "duplicateWriteTarget",
  "targetKey": "game-gdb/core/gdb/buildings.gd.xml|c732cb26-7487-4a7b-b1ba-b65e094f9bac|AspectBuildup|Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount",
  "mods": [
    "example.cheaper-sawmill-a",
    "example.cheaper-sawmill-b"
  ]
}
```

A mod manager can render this as a side-by-side conflict resolution UI without parsing console text. See [Mod Patch Conflict Model](mod-conflicts.md) for the full conflict taxonomy.

## Integration Patterns

A third-party tool can integrate at three levels:

| Level | How | When to use |
|---|---|---|
| **CLI subprocess** | Spawn `pagonia-manager <command> --json out.json` and parse the JSON | Different language, want process isolation, CI scripts |
| **Schema-only** | Read the JSON schemas, validate input/output without running the tools | IDE plugins, web validators, mod-archive linters |
| **Library** | Reference `PagoniaLand.Patcher.Core` / `Paker.Core` / `Manager.Core` from .NET | Building a GUI on top of the existing engine; matches the manager's own integration pattern |

`schema-validate` is the conformance check in every case — anything that passes `schema-validate` is guaranteed to parse cleanly in any current or future tool built against the same contract.

## Version And Package Checks

Every mod declares the GameDatabase version it was authored against:

```yaml
gameDatabaseVersion: "1.4.0-11944+194631"
```

Package requirements:

```yaml
requiredPackages:
  - core
optionalPackages:
  - dlc1
```

A package has three states the manager reasons about — **Present** (pak on disk), **Owned** (the player is entitled to the expansion; user-declared, default `unknown`), and **Effective** (`Present ∧ Owned` — active for the player in solo play). Envision ships every pak to every player, so "present" is almost always true and says nothing about ownership; see [Package Layering → Present vs Owned vs Effective](package-layering.md#present-vs-owned-vs-effective). The load-bearing rule is **presence blocks deployment, ownership only warns** (a non-owner must still be able to deploy a present pak's bytes to match an owning co-op host).

| Situation | Result |
|---|---|
| Required package **not present** on disk | error (`manager.modExpansionNotPresent`) — no pak to patch |
| Required package present but **not owned** | warning (`manager.modExpansionNotOwned`), **deploy proceeds** — solo-inactive, bytes still land (co-op parity) |
| Required package present but ownership **unknown** | warning (`manager.expansionOwnershipUnknown`) prompting you to declare; advisory, never blocks |
| Optional package **not present** | the matching optional content is skipped (`manager.modOptionalExpansionSkipped`, info) |
| Optional package present but not effective | still deploys, solo-inert (`manager.modOptionalExpansionInactive`, info) |
| GameDatabase version differs between mods | warning if mismatched within a profile (`manager.profileGameVersionMismatch`) |
| Mod's `gameDatabaseVersion` is the **same line** as the live game but a different build | info (`manager.modGameVersionDrift`) — almost always still applies |
| Mod's `gameDatabaseVersion` is a **different line** from the live game (e.g. after a major game update) | warning (`manager.modGameVersionMismatch`), deploy proceeds with `--accept-warnings` |
| A canonical pak changed on disk since the cache was built (e.g. a game update repacked it) | warning (`manager.canonicalPakChangedExternally`) — the cache re-extracts from the new bytes so patches apply against current data |
| Patch target missing in the game XML | error |
| `expected` old value differs from on-disk value | error unless explicitly overridden |

`core` / `tools` are base game + editor data and are always treated as owned; only `decorations1` / `dlc1` carry a declarable ownership state, set per install with `pagonia-manager expansions set`. The `--assume-owned` / `--assume-not-owned` flags on `plan` / `deploy` simulate entitlement transiently without writing the record.

The last three rows are the manager's **update-awareness** surface: after a real game update it re-reads the live `gameDatabaseVersion`, re-extracts any canonical pak whose bytes changed out-of-band, and flags previously-deployed mods built against the old version. The repo-data side of the same update (refreshing the GameDatabase analysis the docs publish) is a separate workflow — see [`UPDATE_PLAYBOOK.md`](../UPDATE_PLAYBOOK.md), whose "end-user side of an update" note points back here.

## Mod Manager Metadata

The patcher's `mod.yaml` schema accepts optional metadata that mod-manager GUIs consume but the patcher itself ignores:

```yaml
homepage: "https://example.invalid"
repository: "https://example.invalid/repo"
downloadUrl: "https://example.invalid/download"
updateUrl: "https://example.invalid/mod.yaml"
license: "CC-BY-4.0"
category: balance
tags:
  - production
  - beginner
previewImages:
  - preview/image.png
```

All optional. A local experimental mod still works without any of them.

## Lessons From Other Modding Ecosystems

The patterns Pagonia Land takes from elsewhere:

| Ecosystem | Pattern |
|---|---|
| Anno modding | Declarative XML operations, metadata, dependencies, optional dependencies, load order |
| Stardew Valley / SMAPI | Clear manifests, stable mod IDs, game-version checks, dependency declarations |
| Bethesda / Vortex-style managers | User-facing conflict visibility, enable/disable state, deployment + rollback |
| BepInEx-style plugins | Explicit plugin identity and dependency metadata |

Patterns intentionally **NOT** adopted in the current architecture:

- runtime plugin loading (data-patching first, code-injection later if ever)
- hidden automatic conflict resolution (manager surfaces conflicts; the user decides)
- full XML replacement as the default mod format (declarative operations only)
- publishing mods that haven't passed `schema-validate`

## Status And Direction

The CLI surfaces of all three tools are complete and shipping. The interactive shell ships with the manager. Live-install pak deploy works end-to-end (extract → patch → repack → byte-identical rollback).

Remote mod sources already ship: single mods + collections from GitHub (with cross-repo references and lockfile pinning), HTTP(S) ZIPs, federated catalogs, and the mod.io adapter — see [Mod Distribution Patterns](mod-distribution.md#distribution-channels) for the channel status table.

Next directions, in order of likelihood:

- Broader mod.io coverage — Pioneers of Pagonia is Map-only on mod.io today, so non-Map types (gameplay / overlay / localization) wait on EE extending the mod.io taxonomy. Direct fetch of a non-GitHub per-mod URL source declared inside a collection is also still pending.
- A localization-only mod path once the Envision Entertainment localization-modding capability ships.
- A GUI binary that wraps `PagoniaLand.Manager.Core` — same library the CLI uses, separate project.

All three tools publish as single-file Native AOT binaries for win-x64 / linux-x64 / osx-x64 / osx-arm64 via the [release workflow](https://github.com/pagonia-land/Pagonia-Land/actions/workflows/release.yml).
