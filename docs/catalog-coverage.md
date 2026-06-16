# Catalog Coverage

This page is a sanity check for the current repository state — what the
generated catalogs already cover and how healthy the underlying data is.

The repository already contains a strong first layer:

- structural entity and reference indexes
- validation checks for GUIDs and core gameplay references
- generated catalogs for buildings, recipes, resources, resource flow, production chains, unit equipment, objective flow, notifications, narration, visual/audio asset references, tools/editor data, terrain props, deposits, mapgen, artifacts, treasure hunting, shrines, and localization keys
- documentation for package loading, DLC overlays, objectives, map/POI systems, artifacts, units, buildings, production, and safe modding workflows

The remaining work is not "missing everything". It is more specific: many XML components are visible in the generic entity/reference indexes, but they are not yet modeled as dedicated modding catalogs.

## Current Health Check

Latest local validation (against `1.4.0-11944+194631`, regenerated 2026-06-16):

| Metric | Count |
| --- | ---: |
| XML files | 60 |
| Entity definitions | 4,715 |
| Unique entity GUIDs | 4,715 |
| GUID-like references | 31,763 |
| Resolved references | 24,422 |
| Null GUID references | 7,311 |
| Other unresolved references | 30 |
| Validation warnings | 2 |
| Validation errors | 0 |

Known warnings:

- 30 unresolved non-null GUID references — 12 are two engine-special "magic GUIDs" (a wildcard Unit token and a wildcard CustomFaction token) the engine resolves outside the XML dataset; the other 18 are dangling references the 1.3.1 hotfix introduced by deleting six `NoMVP.*` placeholder resources while leaving four of them referenced by recipes. See [docs/quirks-and-anomalies.md → The 12 dangling references](quirks-and-anomalies.md#the-12-dangling-unit-and-faction-references) and [VALIDATION_BASELINE.md](../VALIDATION_BASELINE.md#1-unresolved-non-null-guid-references-30) for the breakdown. These are **not** validator bugs.
- 43 production recipes have no non-null input/output resources.

Both warnings are already documented as current baseline warnings. They should be watched after updates, but they are not currently blocking the repository.

## Current Generated Catalog Coverage

The dedicated gameplay catalogs currently cover:

| Area | Current Status |
| --- | --- |
| Buildings | Strong catalog coverage |
| Building dependencies | Strong catalog coverage |
| Building production links | Strong catalog coverage |
| Recipes | Strong catalog coverage for `ProductionRecipe` |
| Resources | Strong catalog coverage |
| Resource flow and usage | Strong catalog coverage for known production, storage, construction, recruitment, shrine, and treasure references |
| Units | Good catalog coverage for recruitment and equipment |
| Unit equipment chains | Good generated matrix |
| Objective flow | Good first catalog for requirements, preconditions, rewards, notifications, localization keys, and tech tree rows |
| Tech tree | Good first catalog for tier groups, unlocked buildings and units, primary/alternative objectives, and unlock notifications |
| Unlock gates | Good first catalog for `NeedsUnlock`, especially DLC-gated content |
| Unlock rewards | Good first catalog for objective rewards that unlock buildings, recipes, recruitments, shrine abilities, and tech tree groups |
| Seasonal gates | Good first catalog for `Seasonal` rows and explicit allowed-season values |
| Visual/audio asset references | Good first catalog for icons, prefabs, meshes, textures, audio events, character kits, VFX, and asset-bearing component rows |
| Tools/editor data | Good first catalog for `magmaview.gd.xml`, terrain sediments, vegetation groups, vegetation, textures, global visual rows, and editor brushes |
| Notifications and narration | Good first catalog for message keys, dialog lines, speakers, audio hints, filter categories, and reverse references |
| Terrain props | Good first catalog for prefab paths, blocking, category tags, sediment rules, spacing, and scale hints |
| Deposits | Good first catalog for harvest resources, deposit types, terrain placement hints, growth/regrowth flags, and minimap hints |
| MapGen | Good first catalog for mapgen component rows, deposit references, resources, buildings, tags, and scalar parameter summaries |
| NPC units | Good first catalog for NPC/combat unit rows, encounter flags, raid flags, boss flags, drops, infection hints, and component summaries |
| NPC bases | Good first catalog for NPC bases, camps, dens, infected-area objects, spawn units, mapgen references, custom raids, and wild animal bases |
| Encounter and combat components | Good first component-level catalog for encounter, raid, boss, drop, infection, spawn, and targeting rows |
| Factions | Good first catalog for faction, sub-faction, faction UI, faction color, and override rows |
| Artifacts | Good first catalog |
| Treasure hunting | Good first catalog |
| Shrines and sanctuary | Good first catalog |
| Localization keys | Good first index, with known limitation that `LocaParkplatz` is not a complete translation database |
| Package filters | Good package-level split |
| Search browser | Good local lookup for all indexed catalog types |

The catalog generator now emits roughly 13,078 dedicated rows across the major interpreted catalogs. The remaining entities are still searchable through `generated/entities.json`, `generated/references.json`, and the generic `entity` search index, but they are not yet deeply interpreted.

Unmodeled-by-dedicated-catalog entities by package:

| Package | Entity Count |
| --- | ---: |
| core | 2,706 |
| dlc1 | 310 |
| tools | 0 |

`decorations1` is mostly covered by the building/decorations path already. `tools` is now covered by the tools/editor catalog, although deeper Magmaview semantics are still future work.

## Quality Assessment

The current repository structure is coherent and useful:

- public docs stay in Git
- extracted game XML and generated derived data stay local and ignored
- generated catalogs are reproducible
- validation has a documented baseline
- Windows-first commands are now consistent
- catalog browser can search local generated data

The main risk is overclaiming. Some generated catalogs are precise relationship extracts, while others are navigation aids. Documentation should keep calling this out where appropriate.
