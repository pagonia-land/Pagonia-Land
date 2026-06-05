# Game Database Changelog

This file tracks local observations from extracted GameDatabase XML updates.

The extracted XML files under `game-gdb/` are not committed to this repository. This changelog therefore records derived local observations, validation counts, and patch-note-driven review points.

**Current baseline:** `1.3.1-11826+193733` — 1.3.1 hotfix, 2026-06-02.

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
- **Pre-release placeholders retired.** Entities prefixed `NoMVP.` (EE's "not part of MVP" marker for unshipped content) and a handful of `LocaParkplatz` placeholder rows were cleaned up between the development phase and the public release. Most visible example: `NoMVP.Beer` → `Beer` (the Brewery + Beer feature went live). The `NoMVP.` convention is documented in [docs/quirks-and-anomalies.md → EE's internal "NoMVP." prefix](docs/quirks-and-anomalies.md#ees-internal-nomvp-prefix-on-pre-release-entities).
- **Component vocabulary grew from 281 to 313 types** (`<Aspect*>` / `<Vis*>` / `<Effect*>` element names) — 33 added, 1 retired (`UiGlobalUnitTraits` → singular `UiGlobalUnitTrait`). Of the 33 additions, 26 are dlc1-exclusive (animal husbandry, the Withering / infection system, Sanctuary, DLC-aware progression) and 7 are also picked up by core entities — notably `UnitRaidParameters`, which got bulk-added to 57 existing campaign NPCs in a single rollout. Full table in [docs/quirks-and-anomalies.md → Meadowsong added 33 new component types](docs/quirks-and-anomalies.md#meadowsong-added-33-new-component-types-and-removed-1-typo).

The complete diff (added/removed/changed entities, reference deltas, changed XML files) is available locally under `generated/diffs/1.2.2-11216+189567_to_1.3.0-11768+193445/` after running `scripts/diff_versions.ps1`.

