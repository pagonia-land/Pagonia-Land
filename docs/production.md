# ⚙️ Production Recipes

Production in the game database is represented by `ProductionRecipe` entities, mainly in `game-gdb/core/gdb/productionrecipes.gd.xml` and `game-gdb/dlc1/gdb/productionrecipes.gd.xml`.

Recipes are one of the best systems to study first because they connect resources, buildings, workers, animations, locators, UI, and visual attachments.

## Core Idea

A production recipe is not just a list of inputs and outputs. It is a small step machine.

Typical recipe structure:

```xml
<Entity Name="FirewoodFromSoftwoodRecipe" Guid="...">
  <Values>
    <ProductionRecipe>
      <RecipeIdentifier>firewoodsoftwood</RecipeIdentifier>
      <DefaultState>InfiniteDisabled</DefaultState>
      <ProductionSteps>
        <Item>
          <Content>
            <Type>Input</Type>
            ...
          </Content>
        </Item>
        <Item>
          <Content>
            <Type>Work</Type>
            ...
          </Content>
        </Item>
        <Item>
          <Content>
            <Type>Output</Type>
            ...
          </Content>
        </Item>
      </ProductionSteps>
    </ProductionRecipe>
  </Values>
</Entity>
```

## Important Fields

| Field | Meaning |
| --- | --- |
| `RecipeIdentifier` | Readable/internal recipe key. Useful for search and UI/debugging. |
| `DefaultState` | Initial recipe state, for example enabled/disabled/infinite behavior. |
| `ProductionSteps` | Ordered list of step entries. This is the heart of the recipe. |
| `Type` | Step type, such as `Input`, `Work`, `Wait`, `WaitInterruptable`, or `Output`. |
| `Resource` | GUID reference to a resource entity. Used for inputs and outputs. |
| `Amount` | Quantity consumed or produced when present. |
| `AssignedWorker` | Worker slot used by the step. |
| `TargetLocator` | Named locator in the building prefab where the action happens. |
| `Animation` | Animation used for the step. |
| `UnitAttachments` | GUID reference to visual/tool attachments used during the step. |
| `StartCondition`, `FinishCondition`, `OnStart`, `OnFinished` | Step-state control logic. |

## Common Step Types

| Step type | Role |
| --- | --- |
| `Input` | Consumes or reserves an input resource and often plays pickup/drop animations. |
| `Work` | Main production action, usually with looped animation and work duration/amount. |
| `Wait` | Waits for a condition before continuing. |
| `WaitInterruptable` | Wait state that can likely be interrupted by other simulation needs. |
| `Output` | Produces a resource and places it at an output locator/pile. |

The recipe step list also drives presentation. Locators, animations, worker assignment, and attachments are all embedded in the same workflow.

## Resource Links

Recipe resources are GUID references. To understand a recipe:

1. Find the `ProductionRecipe` entity.
2. Collect every `Resource` GUID used in its steps.
3. Resolve those GUIDs in the locally generated `generated/entities.json`.
4. Check whether the resources are inputs, outputs, or intermediate display references.

The locally generated `generated/references.json` file is useful for tracing every recipe-to-resource link. See [../generated/README.md](../generated/README.md) for how to generate it.

## Building Links

Recipes are normally used by buildings through production-related components, especially `AspectProduction` and related fields in building entities.

The usual chain is:

```text
Building entity
-> AspectProduction
-> Recipe GUID
-> ProductionRecipe entity
-> Resource GUIDs
-> ResourceDescription entities
```

This makes production a cross-file system:

- buildings live mostly in `buildings.gd.xml`
- recipes live in `productionrecipes.gd.xml`
- resources live in `resources.gd.xml`
- units and attachments may be referenced through worker and visual fields

## Production Chains

Individual recipe rows explain one transformation. Production chains explain how transformations connect.

The generated catalog can derive chain paths such as:

```text
raw or gathered resource
-> production recipe
-> produced resource
-> later production recipe
-> building construction cost, unit recruitment cost, or shrine recipe input
```

Open these local files after running `scripts\generate_catalog.ps1`:

```text
generated/catalog/production-chains.md
generated/catalog/filters/production/production-chains-to-buildings.md
generated/catalog/filters/production/production-chains-to-units.md
generated/catalog/filters/production/production-chains-to-shrines.md
generated/catalog/production-chain-graph.md
```

The chain rows are especially useful when changing a resource, recipe, or tool because they show downstream consequences. For example, a tool resource may be the output of one or more recipes and then be used as a recruitment cost for several units.

Treat these chains as database-derived navigation paths. They do not calculate final throughput, logistics capacity, priority, or availability rules.

## Worker and Visual Links

Recipes often reference:

- `AssignedWorker`
- `TargetLocator`
- `Animation`
- `UnitAttachments`

These fields make the production logic match the building prefab and unit visuals. Changing recipe flow without understanding these fields can cause visual or behavioral mismatch even if the resource math is valid.

## DLC Production

`game-gdb/dlc1/gdb/productionrecipes.gd.xml` follows the same pattern as core. It adds recipes such as:

- Apple Cake
- Apple Juice
- CleansingPotion
- Egg
- Raisin Cake
- Wine

These recipes connect DLC resources and DLC buildings to the same core production model.

## Modding Notes

Likely safer recipe edits:

- change output/input amounts
- enable or disable recipe defaults
- adjust recipe availability through building or unlock data

Riskier edits:

- remove steps from the middle of a recipe
- change `StartCondition`/`FinishCondition` names without tracing the whole recipe
- change locators or animations without checking the building prefab
- replace resource GUIDs without verifying category, carry type, and storage behavior
- delete a recipe that buildings or objectives still reference

Before editing a recipe, search for its GUID in the local `generated/references.json` file to see which buildings, objectives, or scenario files depend on it.

## Good First Trace

A useful learning exercise is `FirewoodFromSoftwoodRecipe` in `game-gdb/core/gdb/productionrecipes.gd.xml`:

1. Find the recipe entity by name.
2. Resolve its input and output `Resource` GUIDs.
3. Find buildings that reference the recipe.
4. Check which workers and attachments are used in the production steps.
5. Compare it with another recipe from the same building.

This single trace touches the main systems a modder needs to understand.
