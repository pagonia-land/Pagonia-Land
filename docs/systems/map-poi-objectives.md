# 🗺️ Map, POI, And Objective Systems

Maps are where many database systems become concrete gameplay. A resource, building, artifact, shrine, or objective can exist globally, but a campaign map decides where it appears, when it becomes visible, what objective references it, and which rewards or triggers fire.

This page focuses on map-specific systems:

- points of interest
- objective flow
- treasure areas
- shrine and sanctuary map content
- campaign/map-specific artifacts and combat boosts

## Main Files

| File | Role |
| --- | --- |
| `game-gdb/core/gdb/pointsofinterest.gd.xml` | Global point-of-interest templates and treasure area entities |
| `game-gdb/core/gdb/objectives.gd.xml` | Shared objective definitions and objective components |
| `game-gdb/core/gdb/campaign map *.gd.xml` | Core campaign map-specific objectives, POIs, NPCs, and triggers |
| `game-gdb/dlc1/maps/meadowsong map database.gd.xml` | DLC Meadowsong map-specific POIs, objectives, sanctuary content, infection data, and combat boosts |
| `game-gdb/core/gdb/notifications.gd.xml` | Shared notification data used by objectives and POIs |
| `game-gdb/dlc1/gdb/notifications.gd.xml` | DLC notification data |
| `game-gdb/core/gdb/resources.gd.xml` | Artifacts, combat-boost artifacts, treasure maps, and special resources |
| `game-gdb/core/gdb/treasurehunterrecipes.gd.xml` | Treasure hunter target categories |

## Current Local Snapshot

Current generated system catalogs report:

| Catalog | Count |
| --- | ---: |
| Treasure areas | 2 |
| Shrine buildings | 6 |
| Shrine abilities | 13 |
| Shrine recipes | 35 |
| Artifacts and artifact-like resources | 33 |
| Treasure hunter recipes | 9 |
| Treasure hunter targets | 65 |

> **Note on "Shrine buildings".** This is the generator's shrine classification.
> Direct `<AspectShrine>` matches in the local XML count 5 placeable buildings
> (4 in `core`, 1 in `dlc1`); the extra entry reflects an abstract/template
> shrine entity the generator also counts.

Direct text checks in the current XML also show:

- many `AspectPointOfInterest` entries in `game-gdb/core/gdb/pointsofinterest.gd.xml`
- additional `AspectPointOfInterest` entries in `game-gdb/dlc1/maps/meadowsong map database.gd.xml`
- `AspectTreasureArea` entities in `game-gdb/core/gdb/pointsofinterest.gd.xml`
- DLC map-specific Sanctuary and POI entities in `game-gdb/dlc1/maps/meadowsong map database.gd.xml`

Regenerate the exact current numbers with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## Mental Model

Think in four layers:

```text
Global definitions
-> map-specific entities
-> objective and trigger flow
-> player-visible state
```

Examples:

```text
Resource/artifact definition
-> treasure hunter target or map-specific reward
-> objective or POI references it
-> player discovers, supplies, claims, or completes it
```

```text
Shrine building and abilities
-> map-specific shrine/sanctuary entity
-> objective unlocks or requires it
-> player uses abilities or supplies offerings
```

## Points Of Interest

POIs commonly use `AspectPointOfInterest`.

A POI can contain or reference:

- unknown and known descriptions
- active building description
- objective completion triggers
- required construction or supply behavior
- icon/building display data
- map-specific variants

Useful search:

```powershell
rg --no-ignore "AspectPointOfInterest|ObjectiveCompletionTriggers|ActiveBuildingDescription" .\game-gdb
```

POIs are often objective anchors. Changing one POI can affect objective flow, map pacing, notifications, and rewards.

## Objective Flow

Objectives are not isolated checkboxes. They are small pieces of a map state machine.

Common links:

```text
Objective
-> requirement component
-> POI/building/resource/unit/NPC target
-> notifications
-> reward timing
-> completion triggers
-> unlocks or follow-up objectives
```

Useful search:

```powershell
rg --no-ignore "ObjectiveNotifications|ObjectiveCompletionTriggers|RewardTime|UnlockConditions" .\game-gdb
```

Map objective edits are high risk because a broken trigger can block scenario progress even when the XML still validates.

## Treasure Areas

Treasure areas use `AspectTreasureArea`.

Current generated catalog:

```text
generated/catalog/systems/treasure-areas.md
```

Treasure areas appear as map or POI-style entities. They connect to treasure hunter logic, discovery state, and sometimes objectives or notifications.

Trace pattern:

```text
Treasure area
-> AspectTreasureArea
-> Treasure Hunter building/search behavior
-> treasure hunter recipe target list
-> resource/artifact target
-> objective or notification
```

Useful search:

```powershell
rg --no-ignore "AspectTreasureArea|TreasureArea|TreasureHunterRecipe" .\game-gdb
```

## Shrines And Sanctuary On Maps

Shrines are global buildings and ability systems, but maps can add local shrine-like content or local objective flow around them.

Current generated catalogs:

```text
generated/catalog/systems/shrine-buildings.md
generated/catalog/systems/shrine-abilities.md
generated/catalog/systems/shrine-recipes.md
```

The DLC Meadowsong map contains map-specific Sanctuary/POI data. It references the broader DLC sanctuary system and ties it into map objectives, infection systems, abilities, and local story flow.

Trace pattern:

```text
Map-specific sanctuary or shrine entity
-> AspectPointOfInterest and/or AspectShrine
-> abilities
-> shrine recipes / mana resources / offerings
-> objectives and notifications
```

Useful search:

```powershell
rg --no-ignore "Sanctuary|AspectShrine|ShrineAbility|ShrineRecipe|AspectPointOfInterest" ".\game-gdb\dlc1\maps\meadowsong map database.gd.xml"
```

## Map-Specific Artifacts And Combat Boosts

Artifacts and combat-boost artifacts are defined as resources, but maps can decide how they matter.

Relevant generated catalogs:

```text
generated/catalog/systems/artifacts.md
generated/catalog/systems/treasure-hunter-targets.md
generated/catalog/systems/artifact-treasure-shrine-graph.md
```

Map files can reference combat boost tags or values in encounter, reward, NPC, or objective data. In the current DLC Meadowsong map, combat boost artifact references appear in map-specific data.

Useful search:

```powershell
rg --no-ignore "Artifact|CombatBoostArtifact|CombatBoostArtifactTag|TreasureHunter" ".\game-gdb\dlc1\maps\meadowsong map database.gd.xml"
```

Treat artifact edits as cross-system edits. A resource may be a treasure target, a combat boost, an objective reward, and a map pacing tool at the same time.

## Safe Trace Workflow

For any map/POI/objective system:

1. Start from the map file or POI entity.
2. Record entity name, GUID, package, and file.
3. List components under `Values`.
4. Search every non-null GUID in `.\game`.
5. Resolve references in `generated/entities.json` or the catalog browser.
6. Check notifications and objective completion triggers.
7. Check generated system catalogs for treasure, shrine, and artifact links.
8. Validate after any edit.
9. Test from a fresh start of that map.

Useful local files:

```text
generated/entities.json
generated/references.json
generated/catalog/systems/
generated/catalog/search-index.json
tools/catalog-browser/index.html
```

Or use the [live catalog browser](https://pagonia-land.github.io/Pagonia-Land/catalog/) and load your local `search-index.json` through its file picker.

## Safer Modding Ideas

Safer first experiments:

- document a POI/objective chain without editing it
- change a small objective amount in a copied test map
- adjust display text keys in a copied POI
- compare two POI templates
- trace which artifacts are map-specific versus globally available

Riskier changes:

- remove objective completion triggers
- change POI target GUIDs
- edit reward timing
- delete or replace map-specific shrine entities
- change combat boost artifact tags or values without checking encounters
- alter treasure areas without testing treasure hunter behavior

## What Still Needs Research

Open questions:

- exact runtime relationship between map entity placement and database entity definitions
- which POI fields control discovery versus completion
- whether treasure areas can be safely added to arbitrary maps
- how map-specific artifacts are awarded or spawned in all cases
- which shrine/sanctuary fields are mandatory for map flow
- how much objective trigger behavior is data-only versus hardcoded

For now, treat map/POI/objective edits as high-impact scenario changes. Trace first, edit only small pieces, and test the full map flow from the beginning.
