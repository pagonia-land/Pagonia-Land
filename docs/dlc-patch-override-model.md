# 🔀 DLC Patch And Override Model

This page explains the working model for how `core`, `dlc1`, `decorations1`, and `tools` combine into one effective game database.

The short version:

```text
core = base layer
dlc1/decorations1/tools = additional layers
effective database = all enabled package entities resolved by global GUID references
                     plus the InheritanceMode merge primitives shipped with Meadowsong
```

The additive-overlay base model has held since the earliest analyses. **Meadowsong** (shipped as `dlc1` on 2026-05-27 as version `1.3.0-11768+193445`) introduced **engine-level Entity-relation primitives** that layer on top: `Template`, `Replace`, and `Incremental` modes declared via `InheritanceMode="..."` on `<Entity>` elements, with the inherited entity referenced via `InheritedGuid`. The four-primitive picture, including the encounter-level null-GUID "unload" mechanism, is documented in [docs/mod-distribution.md → Cross-Pak Entity Merging (Meadowsong)](mod-distribution.md#cross-pak-entity-merging-meadowsong). This page captures the underlying overlay model and how the new primitives slot into it.

> **No same-GUID override remains.** Even post-Meadowsong, the engine still does not allow two `<Entity>` definitions to share a GUID. Meadowsong's "Replace" semantics work by a *new* entity declaring `InheritanceMode="Replace"` and pointing at the *inherited* entity's GUID via `InheritedGuid` — different mechanism, same intent.

## Current Evidence

Current local scan (`1.3.1-11826+193733`, regenerated 2026-06-02):

| Observation | Count |
| --- | ---: |
| Total entity definitions | 4,709 |
| Duplicate entity GUIDs | 0 |
| `core` entities | 4,147 |
| `dlc1` entities | 517 |
| `decorations1` entities | 19 |
| `tools` entities | 26 |
| GUID-like references (total) | 31,761 |
| `InheritanceMode="Template"` uses | 18 |
| `InheritanceMode="Replace"` uses | 14 |
| `InheritanceMode="Incremental"` uses | 17 |
| `<InheritedIndex>` list-merge markers | 4,366 |

This strongly suggests:

- entity GUIDs are global, not package-local
- package folders are source layers, not isolated databases
- DLC and decoration content adds new entities and either references core systems by GUID or hooks into them via `InheritanceMode` + `InheritedGuid`
- same file names across packages do not mean file replacement

## Patch Behaviour Patterns

The data shows several patch-like patterns, mixing pre-Meadowsong overlay tricks with the Meadowsong-introduced inheritance modes:

| Pattern | Meaning | Mechanism | Where it's used |
| --- | --- | --- | --- |
| **Additive entity** | A package adds new entities with new GUIDs | Plain `<Entity Guid="...">` | All packages, all the time. The bulk of DLC content. |
| **Reference extension** | A new entity references existing core categories, tags, workers, resources, or templates by GUID | Cross-package GUID references | `dlc1 -> core` and `decorations1 -> core` references throughout. |
| **Variant by reference** | A new entity represents a "core building + DLC changes" variant, and scenarios/unlocks point to the variant instead of the core entity | New GUID, no `InheritanceMode` | DLC infected building variants, scenario-specific units. The pre-Meadowsong "safer than overriding" pattern; still valid. |
| **Template inheritance** | A new entity declares an abstract base whose structure other entities clone | `<Entity InheritanceMode="Template" InheritedGuid="...">` | Meadowsong (18 uses). DLC1 `BuildingBase`, abstract `EncounterAbility Template`, etc. |
| **Engine-level Replace** | A new entity wholesale replaces the inherited entity in the merged dataset | `<Entity InheritanceMode="Replace" InheritedGuid="...">` | Meadowsong (14 uses). Campaign-map tech-tree node replacements, NPC base reskins. |
| **Engine-level Extend (Incremental)** | A new entity contributes additional list items to the inherited entity, referencing preserved positions via `<InheritedIndex>` | `<Entity InheritanceMode="Incremental" InheritedGuid="...">` + `<InheritedIndex>N</InheritedIndex>` markers | Meadowsong (17 uses). `Bakery DLC1 extension`, `Brewery DLC1 extension`, etc. |
| **Encounter-level Unload** | A specific encounter slot is replaced with the null GUID to remove it from the merged set | `<ReplaceSelf><ReplaceWithEntity>00000000-...</ReplaceWithEntity></ReplaceSelf>` | Pre-Meadowsong, in core's campaign maps. 17 occurrences in 1.3.0 (13 in 1.2.2). |

What is **not** observed:

| Pattern | Status |
| --- | --- |
| Same-GUID override | No duplicate GUIDs found, before or after Meadowsong. Override semantics use `InheritanceMode="Replace"` instead. |
| File-level replacement | Same file names exist between packages, but entities are merged rather than the file replacing. |
| Entity-level "Unload" attribute | The dev statements name "unload" as one of four primitives, but no distinct `InheritanceMode="Unload"` value ships in 1.3.0. The encounter-level null-GUID-replace pattern above is the closest available mechanism. |

## Same File Name Does Not Mean Override

Several packages contain matching file names:

```text
core/gdb/resources.gd.xml
dlc1/gdb/resources.gd.xml

core/gdb/buildings.gd.xml
dlc1/gdb/buildings.gd.xml

core/gdb/units.gd.xml
dlc1/gdb/units.gd.xml

core/gdb/productionrecipes.gd.xml
dlc1/gdb/productionrecipes.gd.xml
```

The current model is:

```text
core/gdb/resources.gd.xml contributes base resources
dlc1/gdb/resources.gd.xml contributes additional DLC resources
both sets are loaded into the same effective resource registry
```

The same applies to buildings, units, recipes, objectives, notifications, deposits, and map data.

## How DLC Extends Core

`dlc1` appears to extend core by adding new content and linking it into existing systems:

- DLC resources use core resource categories.
- DLC buildings use core building categories, workers, terrain rules, and production patterns.
- DLC recipes use the same `ProductionRecipe` structure as core recipes.
- DLC objectives and notifications reuse core objective/notification systems.
- DLC map data references both core and DLC entities.
- DLC unlock data controls availability rather than requiring direct replacement of core entities.

Conceptually:

```text
Core category / worker / template / resource
        ^
        |
DLC entity with a new GUID
```

This is why deleting or changing a core entity can break DLC content even if the DLC XML file is untouched.

## Variant Instead Of Override

Some DLC content looks like changed core content, but it is represented as a new entity with a new GUID.

This is the safer pattern:

```text
core building
  unchanged original GUID

dlc building variant
  new GUID
  references shared core systems
  adds DLC-specific components or values
```

Then a DLC scenario, objective, unlock, or menu reference can point to the DLC variant.

For modding, this is usually safer than changing a central core entity in place.

## Decorations Package

`decorations1` appears to be a small additive package:

- it contributes decorative buildable entities
- it references core systems such as categories or resources
- it does not introduce a broad gameplay system like `dlc1`
- it does not duplicate core GUIDs in the current scan

Modding lesson:

```text
small content package + new GUIDs + core category references = clean extension pattern
```

## Tools Package

`tools` appears to be editor/Magmaview data rather than normal gameplay content:

- terrain visualization
- sediment visualization
- vegetation and editor brush data
- tool/editor-specific entities

It should be analyzed with the same global-GUID mindset, but modders should not assume that `tools` content is loaded into normal gameplay in the same way as `core` or `dlc1`.

## Effective Database Model

When reasoning about any entity, keep two identities separate:

| Identity | Meaning |
| --- | --- |
| Source package | The folder/file where the entity is defined |
| Effective database identity | The global GUID identity after all enabled packages are combined |

Example:

```text
Source package: dlc1
Entity: DLC building
References: core category, core worker, DLC recipes, DLC resources
Effective behavior: one building in the combined database
```

Most relationships care about effective database identity. File organization only tells you where the source definition lives.

## Modding Implications

Safer:

- add a new entity with a new GUID
- reuse known core categories, workers, tags, resources, and templates
- create a variant instead of editing a heavily referenced core entity
- gate new content through scenario or unlock references
- validate duplicate GUIDs and unresolved references after every change

Riskier:

- editing core categories or templates
- changing a GUID that DLC references
- assuming `dlc1/gdb/resources.gd.xml` replaces `core/gdb/resources.gd.xml`
- adding a same-GUID entity without knowing runtime conflict behavior
- deleting entities that may be referenced by another package

## How To Test This Model

Useful local checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/entities.json
generated/references.json
generated/catalog/filters/package-summary.md
generated/catalog/filters/packages/dlc1/
generated/catalog/filters/packages/decorations1/
generated/catalog/filters/packages/tools/
```

For precise cross-package reference tracing, inspect `generated/references.json` and filter where `sourcePackage` differs from `targetPackage`.

## Open Questions

Resolved or partially resolved by Meadowsong's release + dev statements:

- ~~Whether the engine supports same-GUID override.~~ **No.** Override is expressed via `InheritanceMode="Replace"` + `InheritedGuid`, never via duplicate GUIDs.
- ~~How the runtime handles duplicate GUIDs.~~ **N/A** — duplicates aren't produced; the merge model gives the engine explicit, conflict-free relations to resolve.
- ~~Whether community packages can be added without replacing official files.~~ **Yes**, via Pattern B overlay paks. The bundled paker's scaffold step generates the required `<modname>/manifest.json` + `files.json` + `.gd.bin` + `memory.bin` — see [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md) and [`docs/mod-patch-format.md`](mod-patch-format.md#standalone-overlay-paks-pak-block).
- Which package metadata is required for a fully valid custom package — answered for module paks by [docs/mod-distribution.md → The Pak Loading Model](mod-distribution.md#the-pak-loading-model).

Still open:

- exact package load order (still empirical — the engine's merge resolution between two mods that target the same `InheritedGuid` is the practical case to watch).
- whether `tools` is loaded only for editor/debug contexts.
- whether a dedicated Entity-level `Unload` primitive ships in a later patch, or whether the encounter-level null-GUID-replace remains the only "remove" mechanism.

Until proven otherwise, treat the database as an additive global registry with package overlays plus the Meadowsong inheritance primitives. The combination — additive base + `InheritanceMode` relations + encounter-level unloads — is the full picture as of `1.3.1-11826+193733` (unchanged through the 1.3.1 hotfix — the InheritanceMode and `<InheritedIndex>` counts held steady).
