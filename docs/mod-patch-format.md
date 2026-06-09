# 📄 Declarative Mod Patch Format

This page describes the v0.1 format for representing Pioneers of Pagonia GameDatabase mods as small, reviewable patches.

The goal is not to replace the XML documentation. The goal is to make future mods easier to read, validate, compare, and combine.

> **Which mod pattern does this format target?** This page describes the declarative mod format used to build **Pattern A (patched canonical pak)** and the XML side of **Pattern B (side-by-side overlay pak)**. For a comparison of all three Pioneers of Pagonia mod patterns — patching shipped paks, side-by-side overlays, and user maps from the editor — see [Mod Distribution Patterns](mod-distribution.md).

## Core Idea

A mod should describe intent:

```text
Find this known entity and field.
Confirm the old value is still what the mod expects.
Apply this small change.
Warn if another mod or game update changed the same target first.
```

In short:

```text
A mod is a declared, validated patch against a known GameDatabase snapshot.
```

This is inspired by patch-based modding workflows such as Anno modding, where mods describe targeted XML operations instead of shipping full replacement database files. For Pagonia, the format should be stricter and more beginner-friendly by default.

Useful Anno references for comparison:

- [Anno Modloader Modinfo Reference](https://jakobharder.github.io/anno-mod-loader/modinfo/)
- [Anno Modloader Loading Order](https://jakobharder.github.io/anno-mod-loader/loading-order/)
- [Anno Modding Guide: Structure Of A Mod](https://anno-mods.github.io/modding-guide/getting-started/structure-of-a-mod.html)

The most important lessons to carry over are metadata, dependencies, optional dependencies, load order, incompatibilities, and a clear folder structure. The main improvement for Pagonia should be stricter target validation with expected old values.

## Why Patch Descriptions?

Directly editing full XML files is hard to share safely:

- it is difficult to see what changed
- it is easy to accidentally include unrelated game data
- multiple mods can overwrite each other
- game updates can silently invalidate edits
- beginners often change GUIDs or structure when they only meant to change one amount

A patch description keeps the mod small:

```yaml
operation: replaceValue
target:
  file: core/gdb/buildings.gd.xml
  entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
  entityName: Sawmill
  component: AspectBuildup
  path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
expectedOldValue: "4"
value: "3"
```

The exact syntax will evolve, but the principle should stay: every patch declares its target and its expectation.

v0.1 schemas for this format live in [schemas/mod-patches](../schemas/mod-patches/README.md).

## Suggested Mod Layout

A future mod package could look like this:

```text
mods/
  cheaper-sawmill/
    mod.yaml
    patches/
      buildings.yaml
    README.md
```

`mod.yaml` describes the mod:

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.cheaper-sawmill
name: Cheaper Sawmill
version: 0.1.0
author: TheLavaBlock
gameDatabaseVersion: "1.3.2-11873+194094"
description: Lowers one Sawmill construction cost for local testing.
requiredPackages:
  - core
optionalPackages: []
requiresNewGame: false
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown
loadAfter: []
loadBefore: []
incompatibleWith: []
patches:
  - patches/buildings.yaml
```

Patch files describe operations:

```yaml
operations:
  - id: cheaper-sawmill-softwood
    operation: replaceValue
    risk: low
    reason: Lower early Sawmill construction cost for testing.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
```

## Required Metadata

| Field | Purpose |
| --- | --- |
| `patchFormatVersion` | Version of this patch metadata format, so future tools can handle format changes |
| `id` | Stable unique mod id, preferably reverse-domain style |
| `name` | Human-readable mod name |
| `version` | Mod version |
| `author` | Author or team |
| `gameDatabaseVersion` | Exact program/database version the mod was authored against, such as `1.3.2-11873+194094` |
| `description` | Short description |
| `requiredPackages` | Packages that must be present, such as `core`, `dlc1`, `decorations1`, or `tools` |
| `optionalPackages` | Packages that enable optional patch sets when present |
| `requiresNewGame` | Whether the mod should only be used with a new save |
| `safeToRemove` | Whether the mod can likely be removed from an existing save |
| `multiplayerSafe` | Whether the mod is expected to work in multiplayer |
| `campaignSafe` | Whether the mod is expected to work in campaign mode |
| `loadAfter` / `loadBefore` | Optional ordering hints |
| `incompatibleWith` | Known incompatible mod ids |
| `patches` | Patch files included in the mod |

## Game Database Version

Use the exact program version that belongs to the extracted GameDatabase XML set:

```yaml
gameDatabaseVersion: "1.3.2-11873+194094"
```

Prefer this exact version over descriptive names such as "Meadowsong release". Human labels can still be used in notes, but validators and patch files need stable, unambiguous identifiers.

This keeps mod metadata short and readable. A separate technical checksum can still be added later by tooling if the community needs stricter verification.

## Package And DLC Dependencies

Pagonia data is split into package folders:

```text
core
decorations1
dlc1
tools
```

Patch metadata should declare which packages are required:

```yaml
requiredPackages:
  - core
  - dlc1
```

If a mod has optional DLC support, put that support in a separate patch set:

```yaml
requiredPackages:
  - core
optionalPackages:
  - dlc1

patchSets:
  - id: base
    optional: false
    requiresPackages:
      - core
    patches:
      - patches/core.yaml

  - id: dlc1-support
    optional: true
    requiresPackages:
      - dlc1
    patches:
      - patches/dlc1.yaml
```

The validator should skip optional patch sets when their package is missing. It should block required patch sets when their package is missing.

This is especially important because `dlc1` can add new resources, buildings, recipes, objectives, map data, and cross-package references. A core-only player should not accidentally apply DLC-targeted patches.

> **At deploy time, "missing" splits in two.** Because Envision ships every pak (`core`, `decorations1`, `dlc1`, `tools`) to *every* player, "the pak is present on disk" does not mean "the player owns the expansion". `pagonia-manager` separates **Present** (on disk) from **Owned** (declared) from **Effective** (active in solo play), so it can give your players an honest message instead of a silent no-op: a required package that's genuinely *absent* errors, while a *present-but-not-owned* one only warns and still deploys (the bytes a co-op participant needs to match an owning host). As a mod author you only declare `requiredPackages` / `optionalPackages` here; the ownership handling is the manager's. See [Present vs Owned vs Effective](package-layering.md#present-vs-owned-vs-effective) and the [manager's expansion gate](../tools/pagonia-manager/CLI.md#expansions-ownership).

## Save And Mode Safety

Safety metadata should be explicit even when the answer is unknown:

```yaml
requiresNewGame: false
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown
```

Suggested values:

| Field | Values |
| --- | --- |
| `requiresNewGame` | `true`, `false`, `unknown` |
| `safeToRemove` | `true`, `false`, `unknown` |
| `multiplayerSafe` | `true`, `false`, `unknown` |
| `campaignSafe` | `true`, `false`, `unknown` |

Examples:

- changing one display icon may be `requiresNewGame: false`
- adding a new entity may be `requiresNewGame: unknown`
- changing objectives or tech tree data should usually be `campaignSafe: unknown` until tested
- removing entities should usually be `safeToRemove: false` or `unknown`

## Target Model

Targets should be entity-oriented first and XPath-like second.

Good target information:

| Field | Why It Helps |
| --- | --- |
| `file` | Limits the search scope and improves error messages |
| `entityGuid` | Stable identity of the target entity |
| `entityName` | Human-readable safety check |
| `component` | Limits the patch to one component |
| `path` | Identifies the field or list item inside the component |

The `entityGuid` should be the primary identity. The `entityName` is a helpful cross-check and error message, but names can be duplicated or changed.

The schema permits a `target` that carries only `entityName` (no `entityGuid`), but this is **discouraged** — names are not guaranteed unique or stable across versions. Treat "require `entityGuid`" as the rule for any patch you intend to share (see [Mod Conflicts → Recommended first rule set](mod-conflicts.md)); the `entityName`-only form is a convenience for quick local experiments only.

## Patch Operations

Start with a small operation set. More operations can be added later after real modding tests.

| Operation | Meaning | Beginner Risk |
| --- | --- | --- |
| `replaceValue` | Replace the text value of an existing field | Low |
| `multiplyValue` | Scale an existing numeric value by a factor | Low |
| `addValue` | Add a delta to an existing numeric value | Low |
| `replaceAttribute` | Replace an existing XML attribute value | Medium |
| `addListItem` | Add one item to an existing list | Medium |
| `mergeComponent` | Add missing fields to an existing component | Medium |
| `removeListItem` | Remove one matching item from a list | High |
| `replaceNode` | Replace a whole XML node | High |
| `addEntity` | Add a new entity with a new GUID | High |
| `removeEntity` | Remove an entity | Very high |

The **Beginner Risk** column above is descriptive guidance. It is *not* the
operation's `risk:` metadata field — that field takes the machine-readable
values `low | medium | high | very-high` (note the hyphen in `very-high`).

`addEntity` is **advanced and not yet fully specified** for general use: the
container contract (where a brand-new entity is inserted, and what `target`
should carry when there is no existing entity to point at) is still being
finalised, and there is no worked example yet. Until then, prefer the existing
in-place operations.

For first public experiments, prefer `replaceValue` only.

### The `xml:` field (for `addListItem`, `mergeComponent`, `replaceNode`, `addEntity`)

The operations that insert or replace whole nodes carry the new content in an
`xml:` field — a raw XML fragment. The patcher **normalises whitespace** inside
`xml:` blocks before insertion, so your indentation there is for readability
only; it does not have to match the surrounding file. (The same normalisation
applies to `expectedOldXml`, which is why a value-level cross-check tolerates
reformatting.)

`addListItem` appends the fragment as a new item under the `path` list:

```yaml
- id: add-fast-cleanse-ability-to-sanctuary
  operation: addListItem
  risk: medium
  reason: Surface the custom Fast Cleanse ability in the Sanctuary spell bar.
  target:
    file: dlc1/gdb/buildings.gd.xml
    entityGuid: 482eddcc-ae28-49b0-8e97-807ab1a69db6
    entityName: Sanctuary
    component: AspectShrine
    path: Abilities
  xml: |
    <Item>
      <Content>
        <Ability>e1a91b00-0001-4000-8000-000000000001</Ability>
      </Content>
    </Item>
```

`replaceNode` swaps the single node selected by `target.path` for the `xml:`
fragment; `mergeComponent` merges the fragment's child elements into the
component named by `target.component`. In every case the fragment is a plain
XML element with no namespace declarations — match the shape the file already
uses for that list or node.

### Arithmetic operations (`multiplyValue`, `addValue`)

`replaceValue` sets a value to a fixed number. The arithmetic operations instead
compute the new value **relative to the existing one**, which is what a literal
substitution cannot express — "all costs ×1.5" or "+2 to a build cost":

- `multiplyValue` writes `expectedOldValue × factor`;
- `addValue` writes `expectedOldValue + delta` (a negative `delta` subtracts).

```yaml
- id: scale-sawmill-softwood-cost
  operation: multiplyValue
  risk: low
  reason: Scale the Sawmill softwood cost with the shared cost multiplier.
  target:
    file: core/gdb/buildings.gd.xml
    entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
    entityName: Sawmill
    component: AspectBuildup
    path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
  expectedOldValue: "4"
  factor: "{{ tweaks.cost-multiplier }}"   # literal or a tweak placeholder
  rounding: round                          # round | floor | ceil (default round)
  clampMin: "1"                            # optional bounds on the result
```

Key points:

- The math runs at **plan time** from the declared `expectedOldValue` and the
  operand — the existing-value cross-check (the same drift guard `replaceValue`
  uses) still has to match the file, so the result never depends on another mod's
  earlier write. `expectedOldValue` is therefore **required**.
- The operand (`factor` / `delta`) may be a literal or a `{{ tweaks.<id> }}`
  placeholder, so **one shared multiplier tweak referenced by many ops** is one
  knob that scales every target relative to its own vanilla value.
- Game-database values are integers, so the result is **rounded** (`round` /
  `floor` / `ceil`, default `round`) and optionally **clamped** to `clampMin` /
  `clampMax` after rounding. A clamp that fires is reported in the plan.
- The target must be a single leaf value (same rule as `replaceValue`), and two
  ops writing the same target still conflict — a `multiplyValue` and a
  `replaceValue` on one field collide just like two `replaceValue`s.

## Binary Pak Entries

XML patches live in `patches/*.yaml` and target the game-database XML files. Non-XML pak entries — icons, textures, meshes, prefabs, materials, audio events, etc. — are handled separately by an `entries:` block in `mod.yaml` plus a sibling `entries/` folder that ships the raw bytes.

```yaml
entries:
  replace:
    - path: core/gui/icons/buildings/icon_sawmill.image
      source: entries/icon_sawmill.image
  add:
    - path: core/gdb/my-new-rule.bin
      source: entries/my-new-rule.bin
  delete:
    - core/sounds/obsolete-cue.audio
```

The three lists are all optional; a real mod uses whichever it needs.

| Operation | What it does | Path constraint |
| --- | --- | --- |
| `entries.replace` | Substitute an existing pak entry with the bytes from `source`. The entry keeps its original `compressed` flag. | `path` must match an entry name in the base pak you'll patch against. |
| `entries.add` | Append a new pak entry. Compression flag is chosen by extension (`.xml`/`.yaml`/`.yml`/`.txt`/`.json` → gzip; everything else stored verbatim). | `path` must NOT already be in the base pak. |
| `entries.delete` | Omit a pak entry from the resulting pak. | `path` must match an entry name in the base pak. |

`source` is a path relative to the mod folder; it must not be rooted and must not contain `..`. `path` is a pak entry name; the first segment is always a pak namespace (`core`, `decorations1`, `dlc1`, `tools`) and `..` traversal is rejected at the schema level.

Conflicts surface in the plan as `entryConflict` diagnostics. Two mods touching the same `path` is an error UNLESS every touching operation is a `delete` (two mods removing the same entry agree). The same path cannot appear twice across `replace`/`add`/`delete` within a single mod either.

Apply produces, in addition to the patched XML files:

- one file under `sandbox/out/<pak>/<path>` for every `replace` and `add` operation
- one line in `sandbox/out/.entries-deleted.txt` per `delete`

[`scripts/sandbox-pack.ps1 -BasePak <base.pak>`](../scripts/sandbox-pack.ps1) consumes that layout: every file under `sandbox/out/` is forwarded to `pagonia-paker patch` as a positional argument (it auto-classifies into Replace vs Add by comparing against the base pak's entry list), and every line in `.entries-deleted.txt` becomes a `--delete <path>` flag. The pure-asset side and the XML side both land in the same `.pak` file from one `sandbox-pack` invocation.

When `entries.add` ships a new `*.gd.xml` under a module namespace already in the base pak, `pagonia-paker patch` also updates that module's `<m>/<m>.gd.bin` index automatically so the engine actually loads the new file. No extra mod-side declaration needed; the auto-register surfaces as a `pakPatchGdbinUpdated` diagnostic and a `GdbinUpdates` entry in the patch report. Pass `--no-gdbin-register` to `pagonia-paker patch` to opt out.

See [`sandbox/examples/add-extra-asset/`](../sandbox/examples/add-extra-asset/) for a worked template that ships only entry operations and no XML patches.

## Standalone Overlay Paks (`pak:` block)

The `entries:` workflow above produces a patched copy of an existing base pak — Pattern A in [docs/mod-distribution.md](mod-distribution.md). When the mod is meant to ship as a **standalone overlay pak** (Pattern B) instead — a brand-new module that lives alongside `core.pak` and overlays its files at load time — declare a `pak:` block in `mod.yaml`:

```yaml
pak:
  name: pagonia-overlay-example         # required — becomes the module folder + .gd.bin filename
  summary: One-line description shown in mod browsers
  author: TheLavaBlock
  image: pagonia-overlay-example/images/preview.image
  dependencies:                          # optional — defaults to ["core"] when omitted
    - core
```

When `pak:` is present, the patcher's apply step writes the four-file module skeleton the engine needs to recognise the folder as a loadable module:

| File | Always written? | Contents |
| --- | :---: | --- |
| `<name>/manifest.json` | yes | `{ Name, Summary, Author, Image, Dependencies }` mirroring shipped paks. |
| `<name>/memory.bin` | yes | 28 zero bytes (shipped paks carry per-module allocation stats here; a fresh scaffold uses zeros and trusts the engine to repopulate). |
| `<name>/files.json` | only when `*.gd.xml` is present under `<name>/` | `{ "Files": [{ "Key": "GameDatabase", "Paths": ["<name>/<name>.gd.bin"] }] }`. Asset-only overlays (System.pak-style) skip this file. |
| `<name>/<name>.gd.bin` | only when `*.gd.xml` is present under `<name>/` | Index listing every `*.gd.xml` the module ships, in ordinal order. `byte[3]` tracks `entries.Count - 1` per the [`.gd.bin` format](../tools/pagonia-paker/CLI.md). |

The scaffold runs AFTER patches and entry operations are applied, so any new `*.gd.xml` from `entries.add` is picked up automatically — no need to list it twice.

`pak.name` must be a single path segment (no slashes, no `..`); validation surfaces `scaffoldNameInvalid` if the value contains separators. A missing or empty `pak.name` produces `scaffoldNameMissing` and fails the apply.

See [`sandbox/examples/standalone-overlay/`](../sandbox/examples/standalone-overlay/) for a worked example. After `sandbox-apply.ps1`, run `sandbox-pack.ps1` **without** `-BasePak` to bundle the result into an engine-loadable `.pak`.

## Tweaks (`tweaks:` block)

> New to tweaks? The [Mod Tweaks guide](mod-tweaks.md) maps the whole feature end-to-end (author → curator → player). This section is the authoring reference it points to.

A mod author can mark specific values as **user-adjustable** — a multiplier, a threshold, an on/off flag — so a player picks a value in the mod manager without editing the mod files. The `tweaks:` block in `mod.yaml` *declares* those parameters; it is the contract a manager reads to build its adjustment UI and the set of names patch operations may later reference.

```yaml
tweaks:
  - id: softwood-cost          # required — ^[a-z0-9][a-z0-9-]*$, max 40 chars; stable, see below
    type: integer              # required — number | integer | boolean | enum
    label: Softwood trunk cost # required — short human-readable name (max 80 chars)
    description: How many softwood trunks the Sawmill costs to build.  # optional (max 500)
    default: 3                 # required — must match `type`
    min: 1                     # number / integer only (optional)
    max: 8                     # number / integer only (optional)
    step: 1                    # number / integer only (optional)

  - id: free-upkeep
    type: boolean
    label: Remove upkeep
    default: false

  - id: difficulty
    type: enum
    label: Difficulty preset
    default: standard
    values:                    # enum only — required, the allowed { value, label } options
      - { value: mild,     label: Mild }
      - { value: standard, label: Standard }
      - { value: hardcore, label: Hardcore }
    aliases:                   # optional — legacy ids this tweak used to carry
      - difficulty-level
```

Field rules enforced by `validate-mod` (and, where a single field can express
it, by `schema-validate` / IDE JSON-Schema validation):

| Field | Applies to | Rule |
| --- | --- | --- |
| `id` | all | Unique within the mod, **including across `aliases`**. Pattern `^[a-z0-9][a-z0-9-]*$`, max 40 chars. |
| `type` | all | One of `number`, `integer`, `boolean`, `enum`. |
| `label` | all | Required, max 80 chars. |
| `default` | all | Required, and its literal must match `type` (a number for `number`/`integer`, `true`/`false` for `boolean`, one of `values` for `enum`). The literal↔type match itself is enforced by `schema-validate` / IDE JSON-Schema; `validate-mod` adds the semantic checks on top — `default` within `min..max`, and `default` ∈ `values`. |
| `min` / `max` / `step` | number, integer | Optional bounds. The `default` must fall inside `min..max`. |
| `values` | enum | Required array of `{ value, label }` options. |
| `aliases` | all | Optional legacy ids, each satisfying the `id` pattern. |

A few of these are **cross-field** rules — most notably "`default` must fall
inside `min..max`" and "`enum` default is one of the `values`". A plain JSON
Schema cannot express a literal-vs-sibling-bounds check, so your IDE's
schema validation alone will *not* catch an out-of-range default. `validate-mod`
does. Run it before publishing; don't rely on the IDE squiggles for these.

**Stable ids matter.** A mod manager stores a user's chosen values keyed by tweak `id`. If you rename a tweak between versions, the stored choice would silently fall back to the default. To rename safely, keep the new `id` and list the old one under `aliases:` — the manager maps the stored value forward. (This is the lesson the [iMYA tweak feature](https://github.com/anno-mods/iModYourAnno/wiki/Setting-up-your-Mod-for-tweaking) learned the hard way.)

Tag a mod that exposes tweaks with the [`tweakable`](mod-tags.md) tag so catalog browsers can filter for mods they can adjust.

### Using tweaks in patch operations

A patch operation references a tweak with a `{{ tweaks.<id> }}` placeholder in any of its value-carrying fields — `value`, `factor`, `delta`, `clampMin`, `clampMax`, `expectedOldValue`, `xml`, `expectedOldXml` (the arithmetic operands and clamp bounds included, so one shared tweak can drive a multiplier and its floor alike). `rounding` is the exception: it's a fixed enum (`round`/`floor`/`ceil`), not a placeholder field. The patcher substitutes the chosen (or default) value **at plan time**, so the plan report shows the concrete values and `apply` just consumes the resolved plan. Two forms exist:

```yaml
operations:
  # Literal substitution — the tweak's value replaces the placeholder verbatim.
  - id: sawmill-softwood-cost
    operation: replaceValue
    target: { file: core/gdb/buildings.gd.xml, entityGuid: "…", entityName: Sawmill, component: AspectBuildup, path: "Costs/Item/Content/Amount" }
    expectedOldValue: "4"
    value: "{{ tweaks.softwood-cost }}"

  # Boolean ternary — picks one of two literal strings based on a boolean tweak.
  - id: sawmill-upkeep-mode
    operation: replaceValue
    target: { … }
    expectedOldValue: Normal
    value: "{{ tweaks.free-upkeep ? 'NoUpkeep' : 'Normal' }}"
```

That is the **entire** grammar — literal substitution plus one boolean ternary. There is deliberately no arithmetic, no nested expressions, and no eval surface (it keeps the patcher AOT-clean and the contract small). If a tweak needs to scale a vanilla value (`vanilla × factor`), that would arrive as a dedicated patch op, not as expression syntax.

Resolution rules:

- A placeholder that names a tweak the mod didn't declare fails planning with `tweakUndeclared`. Malformed placeholder syntax fails with `tweakSyntaxError`.
- Each substitution emits an info `tweakValueResolved` diagnostic naming the value's origin (`default` or an external override).
- The ternary treats the tweak value `true` (case-insensitive) as the first branch and anything else as the second.

A CLI consumer (or the manager) supplies overrides; absent overrides resolve to the declared defaults. From the CLI:

```powershell
pagonia-patcher plan --game <game-root> --mods <mod-dir> --tweak <mod-id>:<tweak-id>=<value>
```

An externally-supplied number outside a tweak's declared `min..max` produces a `tweakValueOutOfRange` warning but is still applied — the bound is the author's recommendation, not a hard gate. The plan report records every tweak's effective value under a per-mod `resolvedTweaks` array (`tweakId`, `sourceValue`, `resolvedValue`, `origin`).

## Expected Old Values

Every destructive or changing operation should include an expectation:

```yaml
expectedOldValue: "4"
value: "3"
```

This gives a future validator three important checks:

1. The target still exists.
2. The game database still has the value the mod was written against.
3. No previous mod already changed the value unexpectedly.

If the old value does not match, the patch should not be applied automatically.

## Safer List Items

List patches select a list item with a `[predicate]` step. Only **match-by-value**
predicates of the form `[child/path='value']` are supported. Positional indexes
like `[1]` are **not supported** — the resolver does not interpret them, so a path
using one fails to resolve (`targetPathMissing`). They are also fragile by nature:
list order is not guaranteed stable across game versions.

Not supported (positional index — will not resolve):

```yaml
path: Costs/Item[1]/Content/Amount
```

Better:

```yaml
path: Costs/Item[Content/Resource='RESOURCE-GUID-HERE']/Content/Amount
```

Best for human-readable patches:

```yaml
path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
resourceName: Softwood Trunk
resourceGuid: c22b4997-5563-44ab-8aa0-04a7b2c826be
```

The validator can resolve the name through `generated/entities.json` and still apply by GUID.

## Validation Flow

The Pagonia Land Patcher already implements this flow:

```text
1. Read base GameDatabase snapshot.
2. Read all enabled mods.
3. Validate mod metadata.
4. Check required packages and optional patch sets.
5. Compare gameDatabaseVersion.
6. Resolve every patch target.
7. Check expected old values.
8. Build a combined write plan.
9. Detect conflicts.
10. Show a dry-run report.
11. Apply only if there are no blocking errors.
12. Run database validation again.
```

No patch should be applied silently.

## Dry-Run Output

A good dry-run report might say:

```text
Mod: Cheaper Sawmill 0.1.0
Operations: 1
Safe: 1
Warnings: 0
Conflicts: 0

Will change:
game-gdb/core/gdb/buildings.gd.xml
  Sawmill / AspectBuildup / Costs / Softwood Trunk / Amount
  4 -> 3
```

For a conflict:

```text
Conflict:
Two mods write the same target:
  Sawmill / AspectBuildup / Costs / Softwood Trunk / Amount

Mod A expects 4 and writes 3.
Mod B expects 4 and writes 2.

Result:
Manual decision required.
```

## Relationship To This Repository

This repository can support patch-style mods by providing:

- entity and reference indexes
- generated catalogs for human lookup
- validation scripts
- worked examples
- snapshot and diff workflows
- v0.1 patch schemas
- the Pagonia Land Patcher as the reference validator

The first goal is documentation and shared vocabulary. Tooling can come after the format has survived feedback from real modding experiments.

For the larger tool direction, see [Patcher And Mod Manager Architecture](mod-manager-architecture.md).
