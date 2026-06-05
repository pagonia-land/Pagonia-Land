# 💰 Treasure Hunting

Treasure hunting is a cross-system feature. It uses a special building aspect, target resource categories, treasure area data, notifications, and resource definitions.

It is not the same as normal production. A treasure hunter recipe describes possible target resources rather than a deterministic production chain.

For map-specific treasure areas, POI links, objectives, and artifact flow, see [Map, POI, And Objective Systems](map-poi-objectives.md).

## Main Files

| File | Role |
| --- | --- |
| `game-gdb/core/gdb/buildings.gd.xml` | `Treasure Hunter` building and `AspectTreasureHunter` |
| `game-gdb/core/gdb/treasurehunterrecipes.gd.xml` | Treasure hunter reward categories |
| `game-gdb/core/gdb/resources.gd.xml` | Treasure maps, artifacts, valuables, and combat-boost artifacts |
| `game-gdb/core/gdb/pointsofinterest.gd.xml` | Treasure area and point-of-interest style data |
| `game-gdb/core/gdb/campaign map *.gd.xml` | Campaign-specific treasure sites and objectives |
| `game-gdb/core/gdb/notifications.gd.xml` | Treasure discovery and warning notifications |

## Building Aspect

The `Treasure Hunter` building contains `AspectTreasureHunter`.

Important fields:

| Field | Meaning |
| --- | --- |
| `Employment` | Unit, amount, and work locator used by the treasure hunter work process. |
| `RationResource` | Resource consumed while searching. |
| `Range` | Search range. |
| `SearchPointInWorldMarkers` | Marker visuals for current or old search points. |
| `SearchPointVisual` / `SearchPointRangeVisual` | Visual feedback for selected search areas. |
| `WorkActions` | Animation and attachment data for work, success, failure, and treasure-map study. |
| `SpawnTreasureAnimationEventName` | Animation event that appears to spawn treasure output. |
| `StudyLocator` | Locator for studying treasure maps. |

## Treasure Hunter Recipes

Treasure hunter recipes are `TreasureHunterRecipe` entities. They define UI/category behavior and target resources.

Important fields:

| Field | Meaning |
| --- | --- |
| `Icon` | UI icon for the category. |
| `Name` | Localization key for the category name. |
| `SortOrder` | UI order. |
| `DefaultEnabled` | Whether the category starts enabled. |
| `AllowSubSelection` | Whether individual target resources can be selected. |
| `TargetResources` | Candidate resources for the category. |

Generated catalogs:

- `generated/catalog/systems/treasure-hunter-recipes.md`
- `generated/catalog/systems/treasure-hunter-targets.md`

## Treasure Areas

Treasure areas use `AspectTreasureArea`. They appear as map or point-of-interest style entities and can be hidden or discovered depending on scenario logic.

Generated catalog:

- `generated/catalog/systems/treasure-areas.md`

## Modding Notes

Safer first edits:

- inspect reward categories and target lists
- compare treasure hunter categories by target count
- document which resources are artifacts, maps, valuables, or combat-boost artifacts

Riskier edits:

- remove target resources without checking objectives
- change `AspectTreasureHunter` animations or locators
- change search range without testing pathing and UI feedback
- edit treasure areas inside campaign map files without tracing objective dependencies

## Suggested Trace

```text
Treasure Hunter
-> AspectTreasureHunter
-> treasure hunter recipe category (e.g. TreasureHunterArtifacts)
-> TargetResources
-> Artifact resource
-> Objectives / notifications / combat boost data
```

Use the generated Mermaid graph as a visual starting point:

```text
generated/catalog/systems/artifact-treasure-shrine-graph.md
```
