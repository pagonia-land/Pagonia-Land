# 🏗️ Buildings

Buildings are one of the most important modding entry points because they connect construction costs, UI menus, worker logic, production, storage, housing, placement, terrain, visuals, and sometimes objectives.

Most base game buildings are defined in:

```text
game-gdb/core/gdb/buildings.gd.xml
```

DLC buildings are defined in:

```text
game-gdb/dlc1/gdb/buildings.gd.xml
```

Decoration buildings are defined in:

```text
game-gdb/decorations1/gdb/decorations.gd.xml
```

## Buildings Are Component Compositions

A building-like entity is usually not a single simple type. It is an entity with multiple components.

Common building components:

| Component | Purpose |
| --- | --- |
| `Building` | Name, description, prefab/mesh, icon, size, sounds, tags |
| `Buildable` | Construction menu category, sort order, UI group, buildability |
| `AspectBuildup` | Construction workers, construction costs, buildup locators |
| `TerrainBlocking` | Occupied terrain/placement blocking |
| `AspectTerrainShaping` | Terrain shaping behavior during placement/construction |
| `IdleLocators` | Worker/visual idle locator setup |
| `AspectProduction` | Production recipes and worker behavior |
| `AspectStorage` | Storage behavior |
| `AspectHome` | Housing/residence behavior |
| `AspectGatherer` | Gathering behavior |
| `AspectRecruitmentPlace` | Unit recruitment behavior |
| `AspectShrine` | Shrine/special ability behavior |
| `VisAudioAmbience` | Ambient audio |
| `VisCycleableBuilding` | Visual cycle variants |
| `UiBuildingStatesOverwrite` | UI state overrides |

The exact component list depends on the building.

## The `Building` Component

This is the visual and descriptive layer.

Typical fields:

- `Name`
- `NamePlural`
- `Description`
- `Mesh` or prefab path
- `Icon`
- `GridSize`
- `PlacementSound`
- `SelectionAudio`
- `ForbiddenSedimentTags`
- `Tags`
- `Notifications`

Safer edits:

- icon path, if the target asset exists
- sort/display text for local experiments
- audio references only if replacing with known valid audio event strings

Riskier edits:

- `GridSize`
- mesh/prefab path
- sediment/terrain restrictions
- tags used by objectives or map generation

## The `Buildable` Component

This controls how the building appears as a buildable object.

Common fields:

| Field | Meaning |
| --- | --- |
| `CanBuild` | Whether it can be built directly |
| `Category` | GUID reference to construction menu category |
| `SortOrder` | Ordering inside UI lists |
| `UiBuildingGroup` | UI grouping label |

Good first edit:

- change `SortOrder` to move a building within its menu group.

Be careful changing `Category`, because it can move the building into a different construction menu or interact with unlock logic.

## The `AspectBuildup` Component

This controls construction.

Important fields:

| Field | Meaning |
| --- | --- |
| `Employment` | Builder unit and worker amount |
| `Costs` | Required construction resources |
| `BuildupLocators` | Visual locators for construction progress |

Construction costs usually look conceptually like:

```xml
<Costs>
  <Item>
    <Content>
      <Resource>resource-guid</Resource>
      <Amount>number</Amount>
      <PileLocator>locator-name</PileLocator>
    </Content>
  </Item>
</Costs>
```

Good first edit:

- change `<Amount>` for an existing cost.

More risky:

- add or remove entire cost items
- change `PileLocator`
- change builder unit GUIDs

## Production Buildings

Production buildings usually include `AspectProduction`.

The normal chain is:

```text
Building entity
-> AspectProduction
-> Recipe GUID
-> ProductionRecipe entity
-> Resource GUID inputs/outputs
```

Related files:

- `game-gdb/core/gdb/buildings.gd.xml`
- `game-gdb/core/gdb/productionrecipes.gd.xml`
- `game-gdb/core/gdb/resources.gd.xml`

When changing production, decide where the change belongs:

| Desired change | Better place to start |
| --- | --- |
| Make a building cheaper to construct | `AspectBuildup` in building |
| Make a recipe produce more | `ProductionRecipe` output amount |
| Make a building offer a different recipe | `AspectProduction` recipe list |
| Change visual work behavior | Recipe locators/animations, high risk |

## Building Dependency Matrix

For quick lookup, regenerate the local catalog and open:

```text
generated/catalog/building-dependency-matrix.md
generated/catalog/building-dependency-matrix.csv
```

This matrix gives one row per building with:

- construction costs and builder requirements
- production workers
- attached recipes and identifiers
- recipe inputs and outputs
- gather outputs
- explicit storage resources
- combined dependency/provided resource summaries

Use it when asking questions like "what does this building need?", "which recipes does it run?", or "which resources does it provide to later chains?"

## Storage And Housing

Some buildings include components such as:

- `AspectStorage`
- `AspectHome`
- `AspectHub`
- `AspectMarketplace`
- `AspectTradeDepot`

These systems often connect to population, logistics, trade, and UI. Treat them as medium/high risk until the reference chain is documented.

## Placement And Terrain

Placement is controlled by multiple fields:

- `GridSize`
- `TerrainBlocking`
- `AspectTerrainShaping`
- `ForbiddenSedimentTags`
- road/path related locators

Changing these may have unexpected effects:

- building cannot be placed
- workers cannot reach locators
- visuals overlap
- terrain shaping behaves oddly

For a first mod, avoid placement changes.

## Decoration Buildings

Decorations are a good place to study simpler buildables. They usually contain fewer simulation components and more visual/buildable decoration behavior.

Look at:

```text
game-gdb/decorations1/gdb/decorations.gd.xml
```

Common decoration pattern:

- `Building`
- `Buildable`
- `AspectBuildup`
- `AspectDecoration`
- `TerrainBlocking`
- visual components
- sometimes `NeedsUnlock`

## Practical Safe Edits

Good first building edits:

- change construction cost amounts
- change `SortOrder`
- compare two buildings in the same category
- trace which recipes a production building uses

Medium-risk edits:

- change worker counts
- change build menu category
- alter recipe availability
- modify storage values

High-risk edits:

- change GUIDs
- remove components
- change prefab/mesh paths
- change grid size and terrain blocking
- edit objective-critical campaign buildings

## Recommended Building Trace

When studying a building:

1. Find the entity by name.
2. Note its GUID.
3. List its `Values` components.
4. Resolve `Buildable/Category`.
5. Inspect `AspectBuildup/Costs`.
6. If it has production, inspect `AspectProduction`.
7. Search for the building GUID across `game-gdb/`.
8. Check whether campaign maps or objectives reference it.

This turns a large XML block into a manageable set of relationships.
