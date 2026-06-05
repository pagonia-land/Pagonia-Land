# 📚 Mod Collections

Mod collections are curated sets of mods that can be installed together.

For Pagonia Land, a collection should be a small manifest that references mods, versions, package requirements, and load order. It should not be a new kind of mod and it should not hide how the included mods work.

## How The Pieces Fit Together

Modding Pagonia Land follows one rule: **one patch format, one patcher core, many possible managers.** Everything is a layer on top of a single mod, and each layer answers a different question — from "what does a mod author write?" down to "what bytes land in my game install?".

```text
AUTHOR        mod          one author writes ONE mod: mod.yaml + patches + assets + adjustable tweaks
                │
DISTRIBUTION  catalog      an index: "these mods and collections exist, here is where they live"
                │
CURATION      collection   a shareable recipe: these mods, in this order, with these suggested tweak values
                │
LOCAL STATE   profile      YOUR working setup: which mods are active, their load order, YOUR tweak overrides
                │
REPRODUCTION  lockfile     the frozen result: exact versions + archive hashes + the resolved tweak values
                │
INSTALL       deploy       the paks built from all of the above, written into the real game (with rollback backup)
```

The split between the two tools follows this stack. The **patcher core** is stateless and owns the three formats it can validate and plan from on their own — `mod`, `collection`, `lockfile`. The **manager** owns the stateful layers around them — `catalog` (where mods come from), `profile` (what you have active), and `deploy` (what is written to the game and how to undo it).

### What each layer does

- **Mod** — the smallest unit. Declarative XML patches, optional binary asset entries, an optional overlay pak, and optional `tweaks:` declaring which values a user may adjust (each with a default and a type). This is the floor every other layer builds on. See [the mod patch format](mod-patch-format.md).
- **Catalog** — pure discovery. A repository's `index.yaml` lists the mods and collections it offers and where to fetch them; catalogs can reference other catalogs. A catalog *points at* content, it never contains it. See [mod catalogs](mod-catalogs.md).
- **Collection** — a curated, shareable recipe (see the manifest below). It lists mods with their sources, a load order, and optionally per-mod `tweaks:` as *suggested* values. A collection is the artifact you hand to someone else.
- **Profile** — your local, mutable setup in the manager's store. It records which mods are enabled, their load order, an optional link to the collection it came from, and your own per-mod tweak overrides. A profile references *installed* mods; it is working state, not a distribution format.
- **Lockfile** — the deterministic, frozen result of resolving a collection or profile: exact versions, archive checksums, and the final resolved tweak values. This is what reproduces an install bit-for-bit.
- **Deploy** — not a format but an action: the manager builds the patched paks from the resolved set and writes them into the live game, keeping a backup so `rollback` restores the original byte-for-byte.

### How a tweak value is decided

A single tweak passes through several layers, and they stack into a precedence order. When the same tweak is set in more than one place, the **highest wins**:

1. **lockfile pin** — a value frozen in the lockfile reproduces an exact past install, so it beats everything.
2. **CLI `--tweak` override** — an ad-hoc value for one run.
3. **profile override** — the value you stored for this profile in the manager.
4. **collection-supplied value** — the curator's suggested preset.
5. **mod default** — the value the mod author declared.

So a tweak's life cycle is: the **mod author** sets a default → a **collection curator** may suggest a different value → **you** override it in your profile → the manager **freezes** the final value into the lockfile at resolve/deploy time → the patcher substitutes it into the patch via `{{ tweaks.<id> }}`. Renamed tweak ids are followed forward through each tweak's `aliases:` list, so a mod update never silently drops your stored value.

### Sharing your setup

Because a collection already carries a mod list, a load order, and per-mod tweak values, it *is* the shareable form of a profile. The patcher's [`export-collection`](#exporting-a-mod-set) turns a working mod set into a manifest, and the manager raises that to the profile level — exporting your active profile, tweak overrides included, as a `*.collection.yaml` you can hand to someone else. For a **bit-identical** reproduction (a bug report, a CI run) share the **lockfile** instead; for a **curatable** recipe others can edit and keep updating, share the exported **collection**. There is deliberately no separate "profile file" format to pass around — the collection and the lockfile already cover both ends of that need.

## Why Collections Matter

Collections are useful for a young modding community because they lower the entry barrier:

- beginners can install a tested starter set
- map makers can share a recommended toolkit
- balance testers can use the same mod setup
- DLC users can get a curated DLC-ready set
- contributors can reproduce issues with the same mod list

Good collection examples:

```text
Beginner QoL Pack
DLC1 Balance Test Pack
Visual And UI Pack
Map Author Toolkit
Hard Economy Overhaul
```

## Collection Versus Mod

| Concept | Meaning |
| --- | --- |
| Mod | Contains `mod.yaml` and patch files that change the GameDatabase |
| Mod set | Current local list of enabled mods, usually created from direct user selection |
| Collection | References multiple mods and describes how to install them together |
| Lockfile | Records the exact resolved mod versions and archive checksums |
| Profile | User-local enabled/disabled state for one play setup |

A collection should usually reference mods instead of bundling other authors' mods. This keeps ownership clearer and makes updates easier.

## Implicit Mod Sets

Even when the user does not choose a collection, the patcher or mod manager should internally treat the selected mods as a temporary mod set.

Example direct CLI input:

```powershell
pagonia-patcher plan --game .\game --mods .\mods\a .\mods\b
```

Internally, this can be normalized to:

```yaml
id: local.implicit
name: Local Mod Set
implicit: true
mods:
  - id: mod.a
    enabled: true
  - id: mod.b
    enabled: true
loadOrder:
  - mod.a
  - mod.b
```

This keeps the patching pipeline simple:

```text
direct mod list or collection -> resolved mod set -> validate -> plan -> detect conflicts -> report -> apply
```

The UI does not need to call this a collection. For users, "current mod set" is clearer. When they save or share it, it becomes a real collection.

## Exporting A Mod Set

The patcher already provides this command:

```powershell
pagonia-patcher export-collection --mods .\mods\a .\mods\b --out .\my-collection.collection.yaml --id my.collection --name "My Collection"
```

For a mod manager, the user-facing action could be:

```text
Save current enabled mods as collection
```

This would let users:

- test a local setup first
- save a working setup later
- share the setup as a curated collection
- reproduce a bug report with the same mod list
- turn an informal mod list into documentation

### Exporting A Profile

`export-collection` works on a loose `--mods` list; the manager extends the same idea to the profile level. An active profile already holds the mod list, the load order, *and* the user's per-mod tweak overrides, so it exports straight into a collection — folding each mod's stored overrides into the collection's `mods[].tweaks` and recovering each mod's `source:` from its install provenance.

```powershell
pagonia-manager profile export [<name>] --out .\my-setup.collection.yaml --id my.setup --name "My Setup"
```

This is the natural answer to "share my exact setup": the result is an ordinary, schema-valid `*.collection.yaml` anyone can install with `collection install`. A mod with no recoverable remote source is exported as `source: "local"` with a warning, since it won't fetch on another machine without a matching `--mods-root`. For a bit-identical reproduction, share the [lockfile](#lockfiles) instead.

For a purely **local** snapshot — back up a working profile, or branch one to try a small change without disturbing the original — the manager also duplicates a profile in place (`profile copy <source> <target>`), no export round trip needed.

## Basic Collection Manifest

Example:

```yaml
collectionFormatVersion: "0.1"
id: pagonia-land.collections.beginner-qol
name: Beginner QoL Pack
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.3.1-11826+193733"
description: Small safe quality-of-life patch set for new mod users.
conflictPolicy: strict

mods:
  - id: pagonia-land.example.cheaper-sawmill
    version: "0.1.0"
    source: "https://example.invalid/cheaper-sawmill-0.1.0.zip"
    required: true
    enabled: true

  - id: pagonia-land.example.apple-icon-test
    version: "0.1.0"
    source: "https://example.invalid/apple-icon-test-0.1.0.zip"
    required: false
    enabled: false
    requiresPackages:
      - dlc1

loadOrder:
  - pagonia-land.example.cheaper-sawmill
  - pagonia-land.example.apple-icon-test
```

The collection tells a mod manager what to fetch and enable. The patcher should still validate the individual mods and build the real patch plan.

## Conflict Policy

Collections should declare how strict they intend to be:

| Policy | Meaning |
| --- | --- |
| `strict` | Any blocking conflict stops installation |
| `warn` | Conflicts are shown, user decides |
| `curated` | Maintainer has reviewed known warnings and load order |
| `experimental` | Test setup, expect breakage |

Even with `curated`, the patcher should not silently apply broken targets or mismatched expected values.

## Tweak Overrides

> Part of the broader tweaks feature — see the [Mod Tweaks guide](mod-tweaks.md) for the author → curator → player overview. This section covers the collection (curator) layer.

When a mod exposes user-adjustable [tweaks](mod-patch-format.md#tweaks-tweaks-block), a collection can set its own values for them via an optional `tweaks:` map on the mod's entry. This is the headline collection use case: ship two presets — say a "Hardcore" and an "Easy" collection — that pull in the *same* mods with *different* tweak values, no forking required.

```yaml
mods:
  - id: pagonia-land.example.tweakable-economy
    version: "0.1.0"
    source: "local"
    required: true
    enabled: true
    tweaks:
      build-cost-multiplier: "1.5"   # curator dials this mod up for the "Hardcore" preset
      free-upkeep: false
```

Keys are the mod's declared tweak ids; each value should match that tweak's declared type. A tweak the collection doesn't mention keeps the mod's declared default. The value flows into the patch plan exactly like a `--tweak` override, so the plan report's per-mod `resolvedTweaks` shows it with origin `collection`. A collection-supplied number outside the tweak's declared range produces a `tweakValueOutOfRange` warning but is still applied.

### In `pagonia-manager`

`pagonia-manager collection install` **seeds** these curator values into the new profile's `enabledMods[].tweaks` map (only the values the curator explicitly set; tweaks the curator left alone fall back to the mod default). `pagonia-manager tweak list <mod-id>` then shows those values with origin `collection-default` until the user edits one with `tweak set`, at which point it becomes `profile-override`. A reinstall behaves as follows:

- **without `--overwrite`** — an already-installed collection is a no-op, so any overrides the user made with `tweak set` survive.
- **with `--overwrite`** — the profile's tweak map is **reseeded** from the collection, discarding the user's overrides; the manager surfaces a `manager.tweakOverridesResetByReinstall` info diagnostic before doing so.

**Precedence** when the same tweak is set in more than one place, highest wins:

1. **lockfile pin** — a value recorded in the lockfile (see below) reproduces an exact past install, so it beats everything.
2. **`--tweak` CLI override** — an ad-hoc override for one run.
3. **profile override** — the value you stored for this profile with `tweak set`.
4. **collection-supplied value** — the curator's preset.
5. **mod default** — the floor.

## Lockfiles

A collection manifest describes intent. A lockfile describes what was actually installed.

Example:

```yaml
collectionLockVersion: "0.1"
collectionId: pagonia-land.collections.beginner-qol
collectionVersion: 0.1.0
gameDatabaseVersion: "1.3.1-11826+193733"

mods:
  - id: pagonia-land.example.cheaper-sawmill
    version: "0.1.0"
    resolvedSource: "https://example.invalid/cheaper-sawmill-0.1.0.zip"
    archiveSha256: "0000000000000000000000000000000000000000000000000000000000000000"
    enabled: true
    tweaks:
      softwood-cost: "3"
```

This is where archive hashes are useful. They verify downloaded mod archives, not the local GameDatabase.

The lockfile (`collectionLockVersion: "0.1"`) carries optional per-mod fields: `source` + `resolvedAt` (the remote commit pin) and a `tweaks` map recording the effective tweak values resolved when the lockfile was generated (curator overrides folded over each mod's defaults). Pinning them means a re-apply months later reproduces the exact same substitution — even if the mod author later ships a different default.

Lockfiles help with:

- reproducing bug reports
- reinstalling the same setup
- detecting changed downloads
- showing exactly which mod versions were active
- sharing a stable test setup

## Profiles

Profiles are local user setups. They are not part of the public collection itself.

Example profile names:

```text
vanilla-plus
dlc1-testing
economy-overhaul
map-authoring
```

A profile can point to a collection and then store local choices:

```yaml
profileVersion: 0.1
name: dlc1-testing
collection: pagonia-land.collections.beginner-qol
enabledMods:
  - pagonia-land.example.cheaper-sawmill
  - pagonia-land.example.apple-icon-test
disabledMods: []
```

This lets one player keep several mod setups without changing the collection definition.

## Mod Manager Behavior

`pagonia-manager`'s `collection install` walks the layers below; a future GUI on top of `PagoniaLand.Manager.Core` would follow the same sequence:

1. Read the collection manifest.
2. Check `gameDatabaseVersion` against the active install (warn on mismatch).
3. Locate each referenced mod under `--mods-root` (local lookup only at present; URL `source:` fields currently surface a `manager.collectionRemoteSourceUnsupported` warning and require a matching local mod).
4. Validate every mod manifest via `PagoniaLand.Patcher.Core.ManifestValidator`.
5. Resolve dependencies and package requirements via the patcher's `CollectionResolver`.
6. Build the final load order.
7. Build a dry-run patch plan through `PlanProfileService` (which wraps the patcher's `PatchPlanner`).
8. Write a lockfile snapshot into `<store>/collections/locks/<id>.lock.yaml`.
9. Install per mod, create the profile, enable the resolved mods, and seed each mod's curator-supplied `tweaks:` into the profile's `enabledMods[].tweaks`. Deploy is a separate command; the collection install never touches the game install.

The patcher stays unaware of where a mod came from — it receives local mod folders from the manager (or from a hand-supplied `--mods` list when used standalone) and produces a validation / patch plan.

## Redistribution And Permissions

Collections should be careful with redistribution:

- reference other mods by source URL when possible
- do not reupload another author's mod without permission
- keep collection metadata separate from bundled content
- include license information when known
- show missing or unavailable mods clearly

This keeps collections useful without taking ownership away from individual mod authors.

## Useful Collection Metadata

Optional fields a mod manager (today's `pagonia-manager` ignores them, a future GUI on top of `Manager.Core` would surface them) can read off a collection manifest:

```yaml
homepage: "https://example.invalid"
repository: "https://example.invalid/repo"
updateUrl: "https://example.invalid/collection.yaml"
license: "CC-BY-4.0"
category: beginner
tags:
  - qol
  - safe-edits
previewImages:
  - preview/collection.png
```

Collections may also carry user-facing safety notes:

```yaml
requiresNewGame: false
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown
```

The collection value should be the most conservative value from included mods unless the maintainer has tested the full setup.

## Current Patcher Commands For Collections

The patcher already supports the full collection pipeline from local manifests, including multi-collection input:

```powershell
pagonia-patcher inspect-collection --collection .\my-collection.collection.yaml
pagonia-patcher resolve-collection --collection .\my-collection.collection.yaml --mods-root .\mods --lock .\out\collection-lock.yaml
pagonia-patcher plan --game .\game --collection .\my-collection.collection.yaml --mods-root .\mods
pagonia-patcher plan --game .\game --collection .\a.collection.yaml --collection .\b.collection.yaml --mods-root .\mods
pagonia-patcher apply --game .\game --collection .\my-collection.collection.yaml --mods-root .\mods --out .\out
pagonia-patcher apply --game .\game --collection .\a.collection.yaml --collection .\b.collection.yaml --mods-root .\mods --out .\out
```

When more than one `--collection` is supplied, the patcher loads the same mod once when versions match and reports an error when versions differ between collections. The same conflict rules that block direct `--mods` planning also block collection planning and apply.

The full command contract is documented in [the Patcher CLI page](../tools/pagonia-patcher/CLI.md).

## What Collections Should Not Do

Avoid:

- hiding conflicts from users
- overriding individual mod metadata without saying so
- bundling many mods into one giant opaque patch
- mixing downloaded mods and generated patched XML files
- claiming compatibility without a matching dry-run and test result
- forcing DLC-only mods into a core-only setup

Collections should make modding easier, not less inspectable.

## First Good Collection Types

For Pagonia Land, useful first collections would be:

| Collection | Purpose |
| --- | --- |
| Beginner QoL Pack | Safe small changes that demonstrate the patch system |
| DLC1 Test Pack | Optional DLC-aware patches and examples |
| Visual/UI Pack | Low-risk icon, text, and presentation changes |
| Economy Experiment Pack | Clearly marked balance experiments |
| Mod Author Toolkit | Example mods, schemas, and test patches for new authors |

Start with documentation-only examples. Real downloadable collections should wait until the patcher can validate and dry-run them.

See [Collection Examples](examples/collections/README.md) for small example manifests.

## Publishing Collections Via Git Repos

Collections are first-class portable artifacts — they ship as plain `*.collection.yaml` files alongside mods. A Git repository (typically GitHub) is the recommended distribution channel: one repo can host any combination of mods and collections, indexed by a top-level `index.yaml` catalog.

The repo layout, the `index.yaml` schema (validated via `pagonia-patcher schema-validate --repo-index`), and the per-mod `source:` field used for **cross-repo collection references** are documented in:

- [`schemas/mod-patches/repo-index.schema.json`](../schemas/mod-patches/repo-index.schema.json) — repo-index schema (`indexFormatVersion: "0.1"`)
- [`docs/mod-distribution.md`](mod-distribution.md#distribution-channels) — distribution-channel overview + source-type table, intended audience: mod authors
- [`examples/mod-repo-example/`](../examples/mod-repo-example/) — working reference repo (2 mods + 1 collection + CI validation)

A collection from any GitHub repo can be installed and activated as a profile with one command:

```powershell
pagonia-manager collection install --from gh:<owner>/<repo>/<collection-id> --as-profile <name> --activate
```

The fetch covers the `*.collection.yaml` plus every mod it references — including cross-repo refs declared via the per-mod `source: gh:` field. The lockfile written to `<store>/collections/locks/<id>.lock.yaml` carries `collectionLockVersion: "0.1"` with each mod's resolved commit SHA + ISO-8601 timestamp in the per-mod `source` + `resolvedAt` fields, so a re-install months later reproduces byte-identical content even after the source branches move on the remotes.

The local-file path (`pagonia-manager collection install --from <file> --mods-root <dir>`) continues to work unchanged for collections shipped via download / sneakernet / a local clone.
