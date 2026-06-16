# Game Database Changelog

This file tracks local observations from extracted GameDatabase XML updates.

The extracted XML files under `game-gdb/` are not committed to this repository. This changelog therefore records derived local observations, validation counts, and patch-note-driven review points.

**Current baseline:** `1.4.0-11944+194631` — 1.4.0 "Pagonia Editor Update" (QoL 8) Beta Update #1, Steam Beta branch, 2026-06-16.

## 1.4.0-11944+194631 - 2026-06-16 Pagonia Editor Update (Beta Update #1)

"Beta Update #1" for the 1.4.0 "Pagonia Editor Update" (QoL 8) on the Steam **Beta** branch. GameDatabase-side it is a **near no-op**: the structural diff against the previous beta baseline (`1.4.0-11893+194274`) shows zero added/removed/changed entities and zero reference deltas — every validation count holds (4,715 entities, 31,763 GUID-like references, 24,422 resolved, 7,311 null, 30 other-unresolved, 76.89% resolution rate; `InheritanceMode` `Template` 18 / `Replace` 14 / `Incremental` 19, `<InheritedIndex>` markers 4,447). Only **three `dlc1` XML files** changed, all value/schema edits *inside existing entities*. Cross-referenced against [`game-paks/patch_notes.txt`](game-paks/README.md).

### Validation Delta (1.4.0-11893 → 1.4.0-11944)

| Metric | 11893 | 11944 | Delta |
| --- | ---: | ---: | ---: |
| XML files | 60 | 60 | 0 |
| Entity definitions | 4,715 | 4,715 | 0 |
| Unique entity GUIDs | 4,715 | 4,715 | 0 |
| GUID-like references | 31,763 | 31,763 | 0 |
| Resolved references | 24,422 | 24,422 | 0 |
| Null GUID references | 7,311 | 7,311 | 0 |
| Other unresolved references | 30 | 30 | 0 |
| Reference resolution rate | 76.89% | 76.89% | 0 |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

### Changed XML files (3, all `dlc1`)

- **Withered-area spread sampling reworked — modder-relevant schema change.** `dlc1/gdb/npcbases.gd.xml` and `dlc1/maps/meadowsong map database.gd.xml` drop the `<SpreadRadialProbCount>` (70 / 50) and `<SpreadDistanceProbCount>` (12 / 10) fields and replace them with `<SpreadProbSpacing>` (1.5) and `<SpreadProbRandomOffset>` (0.5). This is the data behind the patch-notes fixes *"Fixed a desync in multiplayer caused by withered area spread"* and *"Fixed withered area in some cases not spreading correctly."* The old field names are not documented by name anywhere in this repo, but any external mod that patched them by name will no longer match — re-author against the new fields.
- **Boss heal-charge status icon recolour (cosmetic).** `dlc1/gdb/npcunits.gd.xml`: the `UiNPCBossHealingChargeEffectStatus` `<IconColor>` changed from `(0.937, 0.4086915, 0.9070957)` to `(1, 1, 1)` (white).

`core`, `decorations1`, and `tools` are byte-for-byte unchanged. The complete diff is available locally under `generated/diffs/1.4.0-11893+194274_to_1.4.0-11944+194631/` after running `scripts/diff_versions.ps1`.

## 1.4.0-11893+194274 - 2026-06-10 Pagonia Editor Update (Beta)

The 1.4.0 "Pagonia Editor Update" (QoL 8) landed on the Steam **Beta** branch — its headline is the **modder-facing editor**, not new shipped content. The editor now authors **GameDatabase changes** (map-scoped *and* globally active) and publishes them as ordinary `.pak` mods. On the engine roadmap this is **Wave 1** (per-map GDB editing) plus an early slice of **Wave 2** (globally-active GDB mods) arriving together. Database-side the release is nearly empty: four new entities and one new file. Cross-referenced against [`game-paks/patch_notes.txt`](game-paks/README.md).

### Validation Delta (1.3.2 → 1.4.0)

| Metric | 1.3.2 | 1.4.0 | Delta |
| --- | ---: | ---: | ---: |
| XML files | 59 | 60 | +1 |
| Entity definitions | 4,711 | 4,715 | +4 |
| Unique entity GUIDs | 4,711 | 4,715 | +4 |
| GUID-like references | 31,763 | 31,763 | 0 |
| Resolved references | 24,422 | 24,422 | 0 |
| Null GUID references | 7,311 | 7,311 | 0 |
| Other unresolved references | 30 | 30 | 0 |
| Reference resolution rate | 76.89% | 76.89% | 0 |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

Per-package entity counts: `core` 4,147 → 4,149 (+2), `decorations1` 19 (=), `dlc1` 519 → 521 (+2), `tools` 26 (=). The unresolved set (two engine-magic GUIDs + 18 transient `NoMVP.*` orphans) is byte-for-byte the same as 1.3.2, and the `InheritanceMode` tallies hold steady (`Template` 18, `Replace` 14, `Incremental` 19; `<InheritedIndex>` markers 4,447) — none of the four new entities uses an inheritance relation.

### Notable Shape Changes

- **New mod-filter infrastructure.** A new file `core/gdb/modfilters.gd.xml` adds two `ModFilterCategory` entities — `Category` and `Required DLC`. This is the data backing the in-game mod browser's filter categories — the only directly modding-relevant data addition in the release.
- **Two DLC1 withered-content entities.** `notifications.gd.xml` gains *"Alert Hostile Encounter unit infected by infected unit"* (and the pre-existing *"…infected"* notification is renamed to *"…infected by infected grounds"*), and `npcunits.gd.xml` gains `HealingChargeEffectUiStatus` (a `UiEffectStatus`) — backing the patch-notes items "withered units infect player units" and "Withered Hideouts display a status effect when an active heal effect is present". Both new references resolve, so the resolution rate is unchanged.

### Asset-reference extension scheme (catalog heuristic updated)

1.4.0 renamed **every asset-reference path extension**: the pre-1.4.0 double suffix `.<type>.json` became a bare `.<type>` (`foo.prefab.json` → `foo.prefab`, `foo.mesh.json` → `foo.mesh`), `.png` became `.image`, `.pile.json` became `.slicedmesh`, and texture-map references (`DiffuseMap` / `NormalMap` / `ParameterMap` / `VegetationLimiter` / `Map` / `FlowMap`) dropped their extension **entirely** (`…/cliffs_001_c.texture.json` → `…/cliffs_001_c`).

The catalog's asset-detection heuristic ([`generate_catalog.ps1`](scripts/generate_catalog.ps1)) keyed off the old `.json`-suffixed extensions, so on raw 1.4.0 data it first *appeared* to lose 455 asset references (`5,406 → 4,951`) with a wild category reshuffle (`JsonAsset 157 → 0`, `Prefab 995 → 693`, `Texture 336 → 64`, `Mesh 811 → 1,110`). A base-name join of the 1.3.2 and 1.4.0 snapshots proved this was **catalog-tooling staleness, not a content change**: 458 references were silently dropped (their field names weren't in the detector's allowlist once the extension no longer matched) and exactly 302 flipped `Prefab → Mesh` purely from rule order — only the extensions had moved, no asset was removed.

The heuristic was extended to recognise both the old and the new scheme (back-compatible, so cross-version diffs stay consistent). After the fix the corrected 1.4.0 catalog has the **same 5,406 asset-reference rows** as 1.3.2 with **zero uncategorised rows**, and `Prefab` (995) / `Mesh` (811) match 1.3.2 exactly. One intentional improvement falls out of the new scheme: texture maps the old `.texture.json` suffix could only file under the generic `JsonAsset` are now correctly `Texture`, so `Texture` rises `336 → 473` and `JsonAsset` falls `157 → 17`. REFERENCE.md and `docs/systems/visual-audio-assets.md` carry the corrected per-category figures.

The complete diff (added/removed/changed entities, reference deltas, changed XML files) is available locally under `generated/diffs/1.3.2-11873+194094_to_1.4.0-11893+194274/` after running `scripts/diff_versions.ps1`.

## 1.3.3-11931+194526 - 2026-06-15 Hotfix #3 (stable line)

A stable-branch hotfix on the **release** channel, shipped *parallel* to the 1.4.0 "Pagonia Editor Update" beta — its build (`11931`, 2026-06-15) is **newer** than this repo's canonical beta baseline (`11893`, 2026-06-10), so these fixes are not yet in the extracted 1.4.0 beta data and should fold into a later beta build. GameDatabase-side it is a **no-op**: the structural diff against 1.3.2 shows zero added/removed/changed entities and not a single changed XML file — every fix is engine/behaviour only. The repo's canonical baseline therefore stays **1.4.0**; 1.3.3 is captured only as a local snapshot for the diff record. Cross-referenced against [`game-paks/patch_notes.txt`](game-paks/README.md).

### Validation Delta (1.3.2 → 1.3.3)

| Metric | 1.3.2 | 1.3.3 | Delta |
| --- | ---: | ---: | ---: |
| XML files | 59 | 59 | 0 |
| Entity definitions | 4,711 | 4,711 | 0 |
| Unique entity GUIDs | 4,711 | 4,711 | 0 |
| GUID-like references | 31,763 | 31,763 | 0 |
| Resolved references | 24,422 | 24,422 | 0 |
| Null GUID references | 7,311 | 7,311 | 0 |
| Other unresolved references | 30 | 30 | 0 |
| Reference resolution rate | 76.89% | 76.89% | 0 |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

### Modder-relevant fixes (no DB change)

Two Core Game "Free Beer" bugfixes touch community-content handling even though they move no database counts (the kind of runtime/loader fix tracked in [docs/mod-distribution.md](docs/mod-distribution.md)):

- *Enemy raids not starting correctly on older community maps* (and some campaign maps) — affects older user/mod maps.
- *Rare crash when browsing community maps/packages* — affects the in-game community/mod browser.

The DLC1 "Meadowsong" fixes (witch raid scheduling, withered-area-spread multiplayer desync, sick-bay newborn bug) are gameplay-only.

## 1.3.2-11873+194094 - 2026-06-09 Hotfix

A tiny maintenance release. The DLC1 "Meadowsong" side ships its second hotfix (Fruit Farm work fix, withered-unit notification fixes, Cat Tree / Dog House minimap + highlight fixes, DLC key-binding/achievement fixes), while the base game "Free Beer" update refines the same-building-type highlight feature and adds UI/multiplayer crash fixes. One "Free Beer" bugfix is directly relevant to map modders — *mod maps whose package name is not entirely lowercase now load* (a runtime loader fix, not a GameDatabase change; it explains why every sampled user-map pak uses a lowercase module folder — see [docs/mod-distribution.md → user-map paks](docs/mod-distribution.md)). Only six XML files changed and the database shape barely moved — a maintenance release, not a content drop. Cross-referenced against [`game-paks/patch_notes.txt`](game-paks/README.md).

### Validation Delta (1.3.1 → 1.3.2)

| Metric | 1.3.1 | 1.3.2 | Delta |
| --- | ---: | ---: | ---: |
| XML files | 59 | 59 | 0 |
| Entity definitions | 4,709 | 4,711 | +2 |
| Unique entity GUIDs | 4,709 | 4,711 | +2 |
| GUID-like references | 31,761 | 31,763 | +2 |
| Resolved references | 24,420 | 24,422 | +2 |
| Null GUID references | 7,311 | 7,311 | 0 |
| Other unresolved references | 30 | 30 | 0 |
| Reference resolution rate | 76.89% | 76.89% | 0 |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

Per-package entity counts: `core` 4,147 (=), `decorations1` 19 (=), `dlc1` 517 → 519 (+2), `tools` 26 (=). The two stable engine-magic GUIDs and the 18 transient `NoMVP.*` orphans are unchanged — the unresolved set is byte-for-byte the same as 1.3.1.

### Notable Shape Changes

- **Two new DLC1 HUD-layout entities.** `OverviewHudMinimum Incremental` and `OverviewHudmixed Incremental` were added to `dlc1/gdb/globals.gd.xml`, both `InheritanceMode="Incremental"` overlays onto the base HUD-layout entities that contribute an `InfectedUnit` row to the overview HUD's default unit list. They nudge the shipped `InheritanceMode="Incremental"` count 17 → 19 and the `<InheritedIndex>` list-merge markers 4,366 → 4,447; `Template` (18) and `Replace` (14) hold steady. Both new references resolve, so the resolution rate is unchanged.
- **Six XML files changed, all small tweaks.** `core/gdb/buildings.gd.xml`, `core/gdb/decorations.gd.xml`, `core/gdb/globals.gd.xml`, `dlc1/gdb/buildings.gd.xml`, `dlc1/gdb/globals.gd.xml`, and `dlc1/maps/meadowsong map database.gd.xml` changed content with **no net entity churn beyond the two additions above** (no entity names added, removed, or renamed in any of them) — value- and attribute-level edits backing the patch-notes bugfixes (Fruit Farm, decorations/minimap behaviour, the building-highlight feature). The diff does not pin each file to a specific patch-note line, so the per-file mapping is left unstated.

The complete diff (added/removed/changed entities, reference deltas, changed XML files) is available locally under `generated/diffs/1.3.1-11826+193733_to_1.3.2-11873+194094/` after running `scripts/diff_versions.ps1`.

### Dead / cut content audit (catalog + adversarial XML verify)

A reachability sweep of the 1.3.2 baseline found **~60 fully-authored but unreachable entities** — cut, superseded, or never-wired-up content that ships with real assets yet no in-game path. They cluster into recognisable abandoned features: a transport/mechanisation chain (Wooden Cogwheel + `NoMVP.*` wheels/handcart/wheelbarrow), an unbuilt copper-tools tier (9 `*CopperRecipe` + the dead Hardwood Boards intermediate), a cut `Stone Mason` building with its dressed/border-stone recipes (the **Stone Block** output survives via the Quarry), a cut elemental-sorceress upgrade line (Mountain/Water II–III units + their gem-staff/wand goods and recipes), and the usual `*Old` / `* Deprecated` superseded revisions. No fully-cut *buildable* building exists — all non-techtree buildings are either `IsAbstract` leftovers or system pseudo-entities. Full grouped tables with GUIDs and decisive evidence in [docs/quirks-and-anomalies.md → Dead / cut content](docs/quirks-and-anomalies.md#dead-cut-content-fully-authored-but-unreachable). Method: `generated/catalog/*.csv` fan-out (`resource-flow.csv`, `building-production.csv`) then per-GUID raw-XML verification.

## 1.3.1-11826+193733 - 2026-06-02 Hotfix

A small post-Meadowsong hotfix: the DLC1 "Meadowsong" side fixes Withered Hideout / Witch behaviour and rebalances custom-map withered areas, while the base game "Free Beer" side ships UI and crash fixes. Only seven XML files changed and the database shape barely moved — this is a maintenance release, not a content drop.

### Validation Delta (1.3.0 Meadowsong → 1.3.1)

| Metric | 1.3.0 Meadowsong | 1.3.1 | Delta |
| --- | ---: | ---: | ---: |
| XML files | 59 | 59 | 0 |
| Entity definitions | 4,714 | 4,709 | -5 |
| Unique entity GUIDs | 4,714 | 4,709 | -5 |
| GUID-like references | 31,776 | 31,761 | -15 |
| Resolved references | 24,451 | 24,420 | -31 |
| Null GUID references | 7,313 | 7,311 | -2 |
| Other unresolved references | 12 | 30 | +18 |
| Reference resolution rate | 76.95% | 76.89% | -0.06 pp |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

Per-package entity counts: `core` 4,153 → 4,147 (-6), `decorations1` 19 (=), `dlc1` 516 → 517 (+1), `tools` 26 (=).

### Notable Shape Changes

- **Six leftover `NoMVP.*` resources removed — four of them left dangling.** The placeholder resources `NoMVP.Handcart`, `NoMVP.Wheelbarrow`, `NoMVP.Wooden Wheel`, `NoMVP.Iron-bound Wheel`, `NoMVP.Oakwood Boards`, and `NoMVP.Oakwood Trunk` were deleted from `core/gdb/resources.gd.xml` (the `NoMVP.` prefix is EE's "not part of MVP" marker — see [docs/quirks-and-anomalies.md → EE's internal "NoMVP." prefix](docs/quirks-and-anomalies.md#ees-internal-nomvp-prefix-on-pre-release-entities)). EE removed the resource *entities* but left four of them still referenced as `<Content>` in `productionrecipes.gd.xml`, which is the entire `+18` jump in unresolved references (Wooden Wheel 6×, Iron-bound Wheel 6×, Handcart 3×, Wheelbarrow 3×). This is a dangling reference EE shipped, not a repo problem — do not point these at real entities.
- **Custom-map withered-area balancing is now data-driven.** The patch-notes line "the initial amount of withered areas is now balanced depending on landing party and map size" shows up as a brand-new `SimulationParametersDlc1` entity in `dlc1/gdb/globals.gd.xml` plus a `DifficultySettingsDlc1` component grafted onto all four `DLC1CM1` campaign difficulty rows in the Meadowsong map database. `core/gdb/landingparties.gd.xml` also grew (+2.9 KB), consistent with the landing-party dependency.
- **Two new DLC1 component types** (313 → 315): `DifficultySettingsDlc1` and `SimulationParametersDlc1`. Both are DLC1-exclusive and relevant only to mods that touch DLC1 difficulty or simulation parameters.

The complete diff (added/removed/changed entities, reference deltas, changed XML files) is available locally under `generated/diffs/1.3.0-11768+193445_to_1.3.1-11826+193733/` after running `scripts/diff_versions.ps1`.

## 1.3.0-11768+193445 - 2026-05-27 Meadowsong Public Release

The Meadowsong DLC + Free Beer base-game update went live publicly. This is the current reference baseline for everything in the repository.

### Validation Delta (1.2.2 → Meadowsong)

The long-range diff between the previous public release (`1.2.2-11216+189567`) and Meadowsong captures the full impact of the DLC development cycle:

| Metric | 1.2.2 | 1.3.0 Meadowsong | Delta |
| --- | ---: | ---: | ---: |
| XML files | 44 | 59 | +15 |
| Entity definitions | 4,099 | 4,714 | +615 |
| Unique entity GUIDs | 4,099 | 4,714 | +615 |
| GUID-like references | 26,162 | 31,776 | +5,614 |
| Resolved references | 21,208 | 24,451 | +3,243 |
| Null GUID references | 4,942 | 7,313 | +2,371 |
| Other unresolved references | 12 | 12 | 0 |
| Reference resolution rate | 81.06% | 76.95% | -4.11 pp |
| Warnings | 2 | 2 | 0 |
| Errors | 0 | 0 | 0 |

Per-package entity counts: `core` 4,068 → 4,153 (+85), `decorations1` 19 (=), `dlc1` 0 → 516 (+516, brand-new package), `tools` 12 → 26 (+14).

The lower resolution rate is driven by intentionally-empty new fields, not by reference rot — see [docs/quirks-and-anomalies.md → Reference resolution rate drops](docs/quirks-and-anomalies.md#reference-resolution-rate-drops-with-each-new-version) for the analysis.

### Notable Shape Changes

- **Cross-pak entity merging machinery in active use.** Meadowsong is the first release that uses the `InheritanceMode` machinery at scale: 18 `Template`, 14 `Replace`, 17 `Incremental`, plus 17 encounter-level null-GUID `<ReplaceSelf>` unloads. (1.2.2 had exactly one `InheritanceMode="Template"` use — the `DecorativeBuilding` test-bed entity.) Empirical breakdown and modder implications in [docs/mod-distribution.md → Cross-Pak Entity Merging](docs/mod-distribution.md#cross-pak-entity-merging-meadowsong).
  - **Correction (EE dev review, 2026-06-06):** the data scan shows **0** `InheritanceMode="Unload"` uses, but EE confirmed the entity-level `Unload` mode **is engine-implemented since Meadowsong** — it's simply unused in shipped content (a scan can't see a zero-use feature). EE also confirmed the multi-mod collision rule: last-loaded `Replace`/`Unload` wins, `Incremental` stacks; and that externally rewriting shipped paks is not their intended path (overlay paks merged at load time are). See [docs/mod-distribution.md → Dev review follow-up](docs/mod-distribution.md#dev-review-follow-up-2026-06-06) and [docs/quirks-and-anomalies.md → `InheritanceMode="Unload"` ships but is unused](docs/quirks-and-anomalies.md#inheritancemodeunload-ships-but-is-unused).
- **Pre-release placeholders retired.** Entities prefixed `NoMVP.` (EE's "not part of MVP" marker for unshipped content) and a handful of `LocaParkplatz` placeholder rows were cleaned up between the development phase and the public release. Most visible example: `NoMVP.Beer` → `Beer` (the Brewery + Beer feature went live). The `NoMVP.` convention is documented in [docs/quirks-and-anomalies.md → EE's internal "NoMVP." prefix](docs/quirks-and-anomalies.md#ees-internal-nomvp-prefix-on-pre-release-entities).
- **Component vocabulary grew from 281 to 313 types** (`<Aspect*>` / `<Vis*>` / `<Effect*>` element names) — 33 added, 1 retired (`UiGlobalUnitTraits` → singular `UiGlobalUnitTrait`). Of the 33 additions, 26 are dlc1-exclusive (animal husbandry, the Withering / infection system, Sanctuary, DLC-aware progression) and 7 are also picked up by core entities — notably `UnitRaidParameters`, which got bulk-added to 57 existing campaign NPCs in a single rollout. Full table in [docs/quirks-and-anomalies.md → Meadowsong added 33 new component types](docs/quirks-and-anomalies.md#meadowsong-added-33-new-component-types-and-removed-1-typo).

The complete diff (added/removed/changed entities, reference deltas, changed XML files) is available locally under `generated/diffs/1.2.2-11216+189567_to_1.3.0-11768+193445/` after running `scripts/diff_versions.ps1`.

