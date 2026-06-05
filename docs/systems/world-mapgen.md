# 🌍 Terrain Props, Deposits, And MapGen

Terrain props, deposits, and map generation data describe how the world is populated.

For modding, this is the bridge between abstract resources and actual map content:

```text
resource definition
-> deposit resource type
-> deposit entity / terrain deposit
-> mapgen distribution or map-specific placement
-> gatherer, objective, or player discovery
```

## Main XML Areas

Common files:

```text
game-gdb/core/gdb/terrainprops.gd.xml
game-gdb/core/gdb/deposits.gd.xml
game-gdb/dlc1/gdb/deposits.gd.xml
game-gdb/core/gdb/mapgen.gd.xml
game-gdb/core/gdb/sediments.gd.xml
```

`terrainprops.gd.xml` is mostly visual/world prop data. `deposits.gd.xml` connects harvestable objects to resource outputs and terrain placement rules. `mapgen.gd.xml` contains map size, difficulty, landscape, treasure, NPC building, and deposit distribution data.

## Generated Catalogs

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then open:

```text
generated/catalog/terrain-props.md
generated/catalog/deposit-resource-types.md
generated/catalog/deposits.md
generated/catalog/mapgen.md
generated/catalog/filters/world/
generated/catalog/filters/packages/<package>/terrain-props.md
generated/catalog/filters/packages/<package>/deposit-resource-types.md
generated/catalog/filters/packages/<package>/deposits.md
generated/catalog/filters/packages/<package>/mapgen.md
```

## Current Local Snapshot

The latest local catalog generation reports:

| Catalog | Count |
| --- | ---: |
| Terrain props | 737 |
| Terrain props with blocking | 505 |
| Terrain props with sediment rules | 18 |
| Deposit resource types | 45 |
| Deposits | 175 |
| Deposits with harvest resources | 82 |
| Growing or regrowing deposits | 54 |
| Generated deposit types | 7 |
| MapGen rows | 224 |
| MapGen rows with deposits | 8 |

Adding deposit harvest outputs also improves the resource flow catalog. The current resource flow now counts deposit harvest output as a producer source.

## Terrain Props

Terrain props are world objects such as bushes, rocks, plants, visual clutter, and placement objects.

Important generated columns:

| Column | Meaning |
| --- | --- |
| `Prefab` | Prefab path used by the prop |
| `BlockingType` | Explicit blocking hint when present |
| `UsePrefabBlocking` | Whether the prefab provides blocking data |
| `CategoryTags` | Prop category tags |
| `AllowedSediments` | Sediment tags where the prop is allowed |
| `ForbiddenSediments` | Sediment tags where the prop should not appear |
| `SpacingRadius` | Placement spacing hint |
| `MinMaxScale` | Random scale range hint |

Useful filters:

```text
generated/catalog/filters/world/terrain-props-with-blocking.md
generated/catalog/filters/world/terrain-props-with-sediment-rules.md
```

## Deposits

Deposits are harvestable or placeable world resources. They can represent plants, animals, stone, ore, fish, branches, treasure-like map resources, and DLC-specific harvestables.

Important generated columns:

| Column | Meaning |
| --- | --- |
| `DepositResourceType` | Deposit category/type resolved from GUID when present |
| `HarvestResources` | Resources produced when the deposit is harvested |
| `HarvestDeposits` | Follow-up deposits created after harvesting |
| `Prefab` | Visual prefab path |
| `VisibleOnMinimap` | Whether the deposit appears on the minimap |
| `DepositTags` | Deposit classification tags |
| `AllowedSediments` | Terrain/sediment placement hints |
| `HasGrowing` | Uses `GrowingDeposit` |
| `HasRegrowing` | Uses `RegrowingDeposit` |
| `HasFixedAmount` | Uses `FixedAmountDeposit` |
| `HasObstacle` | Uses `ObstacleDeposit` |

Useful filters:

```text
generated/catalog/filters/world/deposits-with-harvest-resources.md
generated/catalog/filters/world/deposits-growing-or-regrowing.md
```

## Deposit Resource Types

Deposit resource types describe the type/category layer above concrete deposit entities.

They can define:

- UI icon
- display name key
- deposit gatherer category
- discovered notification
- explorer discover event
- sub-deposit resource types
- generated deposit chance hints
- forest and sediment weights
- NPC territory amount or characteristic hints

Useful filter:

```text
generated/catalog/filters/world/generated-deposit-types.md
```

## MapGen

The MapGen catalog currently captures entities with components such as:

| Component | Count |
| --- | ---: |
| `MapGenNpcBuildingRule` | 135 |
| `MapGenSedimentClimate` | 17 |
| `UiMapGenParameterGroup` | 12 |
| `MapGenCustomFactionSetup` | 10 |
| `MapGenLandscapeTemplate` | 10 |
| `ForestTag` | 9 |
| `MapGenDepositDistributionGroup` | 7 |
| `MapGenTreasureCategory` | 6 |
| `MapGenBuildingDepositRequirements` | 5 |
| `MapGenDifficultyTemplate` | 5 |
| `MapGenAnimalSpawn` | 4 |
| `MapGenBlockingObstacleCategory` | 2 |
| `MapGenPlayerParameters` | 1 |
| `UiMapGenParameters` | 1 |

Important generated columns:

| Column | Meaning |
| --- | --- |
| `Component` | MapGen-related component type |
| `LocaTag` | UI text key where present |
| `Deposits` | Resolved deposit references |
| `Resources` | Resolved resource/deposit-resource references |
| `Buildings` | Resolved building references |
| `Tags` | Resolved forest, sediment, deposit, or resource tag references |
| `Values` | Compact scalar summary for quick triage |

Useful filter:

```text
generated/catalog/filters/world/mapgen-with-deposits.md
```

## Safer Modding Questions

These catalogs help answer questions such as:

- Which deposits produce a resource?
- Which deposits grow or regrow?
- Which deposits are visible on the minimap?
- Which deposit types are generated by mapgen?
- Which mapgen rows reference tree deposits?
- Which terrain props have blocking?
- Which props or deposits have sediment placement rules?
- Which DLC deposits add new harvestables?

## Modding Risks

Safer edits:

- inspect and document deposit output resources
- reuse existing prefab paths for tests
- compare mapgen rows between snapshots
- change small generated chance values in a local test copy

Riskier edits:

- remove deposit resource types used by mapgen
- remove harvest resources from deposits used by objectives or economy chains
- change terrain sediment rules without testing map placement
- change NPC building rules without checking faction, encounter, and objective data
- change map size or difficulty parameters without testing full map generation

World generation changes should be tested by creating new maps. Existing saves may not reflect generation changes, and broken mapgen data can fail before normal gameplay begins.
