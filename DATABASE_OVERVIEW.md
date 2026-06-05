# Game Database Overview

Snapshot: `1.3.1-11826+193733` (1.3.1 hotfix, 2026-06-02). Based on 59 `*.gd.xml` files across `core`, `decorations1`, `dlc1`, and `tools`.

This page is the high-level map of how the database is organised. For empirical oddities the structured docs don't cover, see [Quirks And Anomalies](docs/quirks-and-anomalies.md). For mod-distribution mechanics (Pattern A / B / C, cross-pak entity merging primitives), see [Mod Distribution Patterns](docs/mod-distribution.md).

## Summary

The database is a component-based entity system. Every file uses the same XML wrapper:

```xml
<EntityGroup>
  <Entities>...</Entities>
  <Groups>...</Groups>
</EntityGroup>
```

The actual meaning of an entity is defined by its `Values`: a building is not a building because of the file name, but because the entity contains components such as `Building`, `Buildable`, `AspectBuildup`, `AspectProduction`, `TerrainBlocking`, `IdleLocators`, and so on. GUIDs are the primary foreign keys. Names are human-readable, but references almost always use GUIDs.

## Scope

| Package | Entities | Role |
| --- | ---: | --- |
| `core` | 4,147 | Base game: resources, units, buildings, recipes, campaign, NPCs, terrain, UI, audio |
| `decorations1` | 19 | Small expansion with decorative buildables |
| `dlc1` | 517 | Meadowsong expansion: new units, resources, buildings, NPCs, objectives, and scenario map |
| `tools` | 26 | Editor/Magmaview data for terrain, vegetation, and brushes |
| Total | 4,709 | All GUIDs are unique |

## Key Analysis Numbers

These numbers come from a structural scan of all XML files in the extracted game database:

| Metric | Count | Notes |
| --- | ---: | --- |
| XML files | 59 | All files use the `*.gd.xml` pattern |
| Total entities | 4,709 | Every entity has a unique GUID |
| Unique GUIDs | 4,709 | No duplicate entity GUIDs were found |
| GUID-like references | 31,761 | Text values matching the GUID format |
| Resolved references | 24,420 | References that point to an entity in this XML set |
| Null GUID references | 7,311 | Explicit empty/default references using `00000000-0000-0000-0000-000000000000` |
| Other unresolved references | 30 | 12 engine-magic GUIDs (Unit + CustomFaction wildcards) plus 18 transient `NoMVP.` orphans the 1.3.1 hotfix introduced — see [Quirks And Anomalies](docs/quirks-and-anomalies.md) |

Package-level entity distribution:

| Package | Entity count |
| --- | ---: |
| `core` | 4,147 |
| `decorations1` | 19 |
| `dlc1` | 517 |
| `tools` | 26 |

The extracted XML set is largely self-contained: 77% of references resolve to a defined entity, and almost all remaining references (7,311 of 7,341 unresolved) are explicit null GUIDs marking intentionally-empty optional fields.

## File Roles

### Core Simulation

| File | Entities | Contents |
| --- | ---: | --- |
| `game-gdb/core/gdb/resources.gd.xml` | 129 | Goods/resources with category, name, icon, mesh, carry type, UI visibility |
| `game-gdb/core/gdb/resourcecategories.gd.xml` | 51 | Categories for resources and UI grouping |
| `game-gdb/core/gdb/buildings.gd.xml` | 134 | Buildables, production buildings, storage, residences, military, special buildings |
| `game-gdb/core/gdb/buildingcategories.gd.xml` | 15 | Construction menu categories |
| `game-gdb/core/gdb/units.gd.xml` | 142 | Settlers, workers, military, tags, traits, recruitment costs |
| `game-gdb/core/gdb/productionrecipes.gd.xml` | 137 | Production workflows as step lists with inputs, work, wait, outputs |
| `game-gdb/core/gdb/unitattachments.gd.xml` | 267 | Tools, weapons, visual attachments, active attachment sets |
| `game-gdb/core/gdb/deposits.gd.xml` | 223 | Resource deposits, plants, underground/terrain deposits |
| `game-gdb/core/gdb/terrainprops.gd.xml` | 778 | Props and prop tags for world generation/vegetation |
| `game-gdb/core/gdb/sediments.gd.xml` | 101 | Ground types, tags, textures, vegetation groups |

### Progression, UI, and Metadata

| File | Entities | Contents |
| --- | ---: | --- |
| `game-gdb/core/gdb/techtree.gd.xml` | 158 | Tech tier groups, unlock objectives, tech tree links |
| `game-gdb/core/gdb/objectives.gd.xml` | 260 | General objective types and mission/victory logic |
| `game-gdb/core/gdb/notifications.gd.xml` | 139 | Notifications, filters, explorer discover events |
| `game-gdb/core/gdb/globals.gd.xml` | 129 | Global parameters, UI hints, traits, global settings |
| `game-gdb/core/gdb/factionparameters.gd.xml` | 48 | Faction and diplomacy parameters |
| `game-gdb/core/gdb/uiunitscategories.gd.xml` | 20 | HUD/statistics categories |
| `game-gdb/core/gdb/audio.gd.xml` | 36 | Discover/audio events |
| `game-gdb/core/gdb/credits.gd.xml` | 33 | Credits categories and entries |
| `game-gdb/core/gdb/dlcs.gd.xml` | 4 | DLC/unlock metadata |

### Maps and Campaign

The campaign maps are databases in their own right. They contain local entities for objectives, dialogues, points of interest, factions, NPC camps, difficulty settings, landing parties, and sometimes local buildings or units.

| File | Entities |
| --- | ---: |
| `game-gdb/core/gdb/campaign map 1 - tutorial.gd.xml` | 111 |
| `game-gdb/core/gdb/campaign map 2 - bandits.gd.xml` | 140 |
| `game-gdb/core/gdb/campaign map 3 - scavs.gd.xml` | 144 |
| `game-gdb/core/gdb/campaign map 4 - ghosts.gd.xml` | 138 |
| `game-gdb/core/gdb/campaign map 5 - werewolves.gd.xml` | 113 |
| `game-gdb/core/gdb/campaign map 6 - malthorn.gd.xml` | 83 |
| `game-gdb/core/gdb/campaign map 7 - final showdown.gd.xml` | 147 |
| `game-gdb/dlc1/maps/meadowsong map database.gd.xml` | 177 |

### NPCs and Encounters

| File | Entities | Contents |
| --- | ---: | --- |
| `game-gdb/core/gdb/npcbases.gd.xml` | 68 | NPC bases, factions, subfactions, camps, base tags |
| `game-gdb/core/gdb/npcunits.gd.xml` | 97 | Enemy/NPC units, raid parameters, encounter parameters, bosses |
| `game-gdb/dlc1/gdb/npcbases.gd.xml` | 17 | Withered/infected camps and DLC factions |
| `game-gdb/dlc1/gdb/npcunits.gd.xml` | 60 | Animals, infected animals, witches, boss and summon logic |

## Entity Model

An entity contains at least:

- `Name`: editor/human-readable name
- `Guid`: stable ID and reference target
- optional `IsAbstract="true"`: template/base entity, often not directly playable
- `Children`: nested entities; effectively an editor/inheritance structure
- `Values`: component block with concrete data

Example structure from `game-gdb/core/gdb/buildings.gd.xml`:

```xml
<Entity Name="Buildable" Guid="..." IsAbstract="true">
  <Children>
    <Entity Name="Construction Camp" Guid="...">
      <Values>
        <AspectBuildup>...</AspectBuildup>
        <Building>...</Building>
        <TerrainBlocking />
        <Buildable>...</Buildable>
      </Values>
    </Entity>
  </Children>
</Entity>
```

This shows the central design idea: entities are carriers for multiple components. `Aspect...` components describe simulation behavior, `Vis...` components describe visualization, `Ui...` components describe presentation, and `Objective...` components describe objective/quest logic.

## Reference System

The scan finds 31,761 GUID-like text values:

| Reference class | Count |
| --- | ---: |
| Resolvable to an entity in this XML set | 24,420 |
| Null GUID `00000000-0000-0000-0000-000000000000` | 7,311 |
| Other unresolved references | 30 |

Of the 30 unresolved-but-non-null references, 12 are two stable engine wildcards: a `<Unit>` wildcard (`ac941c5f-…`) used 7 times and a `<CustomFaction>` wildcard (`d860e55d-…`) used 5 times — engine-recognised placeholder tokens consumed outside the XML dataset, stable across versions. The other 18 are transient: the 1.3.1 hotfix deleted six `NoMVP.` placeholder resources but left four of them still referenced by recipes. See [Quirks And Anomalies → The 12 dangling references](docs/quirks-and-anomalies.md#the-12-dangling-unit-and-faction-references) and [Validation Baseline](VALIDATION_BASELINE.md#1-unresolved-non-null-guid-references-30) for the full evidence.

Most common reference fields:

| Field | Count | Typical meaning |
| --- | ---: | --- |
| `Resource` | 2,079 | Inputs, outputs, costs, storage, rewards |
| `Unit` | 998 | Workers, recruitment, objectives, NPCs |
| `Category` | 823 | Construction/resource/UI categories |
| `Building` | 578 | Objective, POI, unlock, or production reference |
| `SedimentTag` | 573 | Placement and terrain rules |
| `DepositType` | 539 | Resource/plant/terrain deposits |
| `Notification` | 412 | UI messages and objective feedback |
| `Tag` | 405 | Flexible grouping for objectives, filters, logic |
| `Recipe` | 250 | Production or shrine recipes |
| `UnitAttachments` | 353 | Visual/tool-related attachments |

Cross-package references:

| From | To | Count | Meaning |
| --- | --- | ---: | --- |
| `core` | `core` | 21,287 | The base game is largely self-contained |
| `dlc1` | `dlc1` | 1,859 | Meadowsong-internal new systems |
| `dlc1` | `core` | 1,235 | Meadowsong builds heavily on core categories, units, UI, and base mechanics |
| `decorations1` | `core` | 18 | Decoration pack uses core construction categories/materials |
| `tools` | `tools` | 21 | Editor data is isolated |

Dependency direction is strictly one-way: **no `core` reference ever targets `dlc1`, `decorations1`, or `tools`** (empirically verified against all 31,761 references). This means a mod can extend core without core needing to know the mod exists, and removing a DLC can never leave dangling references in core — the architectural foundation under [Mod Distribution Patterns](docs/mod-distribution.md) Pattern B.

## Important Data Flows

### Building

A buildable building usually combines:

1. `Building`: name, description, mesh/prefab, icon, size, sounds, tags.
2. `Buildable`: construction category, sorting, UI group.
3. `AspectBuildup`: construction costs and builders.
4. `TerrainBlocking`/`AspectTerrainShaping`: placement and terrain logic.
5. Optional `AspectProduction`, `AspectStorage`, `AspectHome`, `AspectGatherer`, `AspectRecruitmentPlace`, `AspectShrine`, and so on.

This means a building is a UI object, world object, placement object, and simulation node at the same time.

### Production

`productionrecipes.gd.xml` models production as an explicit state/step machine:

- `ProductionRecipe` contains `RecipeIdentifier`, state, and `ProductionSteps`.
- Steps have `Type` values such as `Input`, `Work`, `Wait`, `WaitInterruptable`, `Output`.
- Resources are referenced through `Resource` GUIDs.
- Animations, locators, and attachments connect gameplay to visualization.

Example: `FirewoodFromSoftwoodRecipe` takes softwood as input, runs work/wait steps with animations, and produces firewood as output.

### Resources

Resources mostly consist of `ResourceDescription`:

- category GUID (`ResourceCategory`)
- localizable names (`Name`, `NamePlural`)
- icon and mesh/pile meshes
- carry/display rules (`CarryType`, `UiDisplay`)
- optional tags, notifications, and audio events

They are referenced by construction costs, recipes, deposits, objectives, trade, and storage.

### Units

Units are component-based as well:

- `Unit`: name, icon/mesh/basic data
- `AspectWorker`, `AspectTransporter`, `AspectRecruitable`, `AspectHomeResident`
- `RecruitmentCost`
- `UnitAnimations`, `UnitAttachmentSetOverwrites`
- `TaggedUnit`, `UnitTag`, `UnitClassTag`, `TerritoryDefender`
- encounter/raid parameters for combat and NPC behavior

Worker and building logic are connected through `Unit` references: buildings define employment, recipes define animations/attachments, and objectives reference unit types or tags.

### Objectives and Campaigns

Objectives are small, composable condition and reward entities:

- `GeneralObjective` acts as the visible/organizational container.
- Specialized components include `ObjectiveOwnCommodityAmount`, `ObjectiveUnitsOfType`, `ObjectiveDiscoverPointsOfInterest`, `ObjectiveReachFactionStanding`, `ObjectiveSupplyPointsOfInterest`.
- `ObjectiveNotifications`, `ObjectiveRewards`, and `ObjectiveStartRewards`.
- Campaign maps define local objective chains, dialogues, POIs, and factions.

The campaign files are therefore not just map data, but scenario-specific mini databases.

### Tech Tree and Unlocks

`game-gdb/core/gdb/techtree.gd.xml` and DLC globals define tier groups and unlock objectives. `NeedsUnlock` appears in DLC/decoration content and connects new entities to unlock conditions. The DLC therefore does not only extend lists; it plugs into progression, UI, and campaign logic.

### Cross-Pak Entity Merging (Meadowsong-Era)

Meadowsong introduced declarative entity-relation primitives that let one pak modify another without byte-patching the shipped XML. Three are visible in shipped content:

| Primitive | Uses in 1.3.1 | Meaning |
| --- | ---: | --- |
| `InheritanceMode="Template"` | 18 | New entity inherits structure from another, then overrides selected fields |
| `InheritanceMode="Replace"` | 14 | Replaces an existing entity by GUID with the new shape |
| `InheritanceMode="Incremental"` | 17 | Merges into the target entity at list-marker positions (`<InheritedIndex>N</InheritedIndex>`) |

Encounter-level "unload" is expressed through a pre-existing pattern (`<ReplaceSelf><ReplaceWithEntity>00000000-…</ReplaceWithEntity></ReplaceSelf>`, 17 uses) rather than a fourth `InheritanceMode` value. The full evidence and tooling implications are documented in [Mod Distribution Patterns → Confirmed primitives](docs/mod-distribution.md#confirmed-primitives). Pre-Meadowsong (`1.2.2`), the same machinery shipped with the engine but only one entity used it — see [Quirks And Anomalies → The single 1.2.2 InheritanceMode use](docs/quirks-and-anomalies.md#the-single-122-inheritancemodetemplate-use).

### DLC Structure

`dlc1` follows the same file types as `core`, but selectively:

- new resources: Apples, Grapes, Wine, Egg, CleansingPlant, CleansingPotion, Porcini, and others
- new production: Wine, Apple Juice, Apple Cake, Raisin Cake, Egg, CleansingPotion
- new buildings: Animal Farm, Fruit Farm, Winery, Sanctuary, Feeding Stations, infected residences
- new deposits: Apple Trees, Grapevines, Medical/Cleansing Plants, Infected Area Obstacles
- new NPC/encounter logic: Withered, infected animals, witches, boss-specific encounters
- its own map database: `meadowsong map database.gd.xml`

The DLC uses core GUIDs for categories, UI, base systems, and existing resources/units. Functionally, it acts as an overlay on top of the core system.

### decorations1

`game-gdb/decorations1/gdb/decorations.gd.xml` is a very small overlay with 19 entities. The entities are normal `Buildable`/`Building`/`AspectDecoration` combinations with `NeedsUnlock`. This expansion does not introduce a new mechanic; it adds new buildable decorative variants.

### tools

`game-gdb/tools/gdb/magmaview.gd.xml` contains editor/visualization data:

- `EditorBrushes`
- `TerrainSediment`
- `TaggedSediment`
- `VisTerrain`, `VisTexture`, `VisVegetation`, `VisVegetationGroup`
- `VisRoad`, `VisFluid`, `VisTerritory`

It effectively references only itself and is largely separate from game content.

## Implementation Patterns

1. **Composition instead of monolithic types**  
   Entities are assembled from many components. An entity can be a building, objective, POI, NPC base, or visual object depending on its `Values`.

2. **GUIDs as hard links**  
   Almost all relationships are GUID-based. File boundaries are organizational, not strict technical boundaries.

3. **Groups as editor/authoring structure**  
   `Groups` organize entities by topic, tier, dialogue area, objective chain, and so on. Runtime logic appears to depend more on components and GUIDs.

4. **Abstract base entities and children**  
   `IsAbstract="true"` and nested `Children` form templates or categories. Many concrete entities appear to inherit from abstract editor-side/serialized bases.

5. **Assets as paths, logic as GUIDs**  
   Meshes, icons, prefabs, and audio events are path/string values. Gameplay relationships are GUIDs.

6. **Scenarios as local overlays**  
   Campaign maps contain their own entities for objectives, dialogues, POIs, NPCs, and special rules. This lets a map use core mechanics while adding or overriding local scenario behavior.

7. **DLC as additive packages**  
   `dlc1` and `decorations1` define new entities and reference core. There is little evidence of destructive overwriting; the structure looks additive.

## Practical Reading Order

If you want to understand or mod the database, read it in this order:

1. `resources.gd.xml` and `resourcecategories.gd.xml`
2. `units.gd.xml` and `unitattachments.gd.xml`
3. `buildings.gd.xml` and `buildingcategories.gd.xml`
4. `productionrecipes.gd.xml`
5. `deposits.gd.xml`, `sediments.gd.xml`, `terrainprops.gd.xml`
6. `objectives.gd.xml`, `notifications.gd.xml`, `techtree.gd.xml`
7. Campaign maps and DLC files as examples of concrete scenario/expansion usage

## Notable Observations

- The 7,311 null GUIDs are an intentional pattern, not missing data — fields that exist but are deliberately empty (default attachments, optional unlock gates, blank narration speakers). A validator that flags every null GUID will generate thousands of false positives.
- Some value types use readable localisation names directly as element names (e.g. `IconicBuilding Name ...`, campaign-specific objective names). This is an editor-export artifact rather than runtime-meaningful naming.
- Editor-export typos exist in group names and value types (`AvergaePleasureMealQuality`, `AbiltiesVfx`, `Natural Prodcuts`). Runtime logic depends on GUIDs, so these are cosmetic — but they confirm "labels are not safe to target by name; always target by GUID".
- Campaign maps sometimes carry local copies or special variants of global entities. When editing a global entity, check whether maps define local counterparts that should change in sync.
- More observations — pre-release `NoMVP.` placeholder prefix, the structural outlier `tools.pak`, the `memory.bin` shape, the bulk addition of `UnitRaidParameters` to all campaign NPCs in 1.3.0 — live in [Quirks And Anomalies](docs/quirks-and-anomalies.md).
