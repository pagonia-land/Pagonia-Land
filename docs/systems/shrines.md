# 🕯️ Shrines And Sanctuary

Shrines combine buildings, abilities, mana resources, and offering recipes. The base game contains iconic shrine-style buildings such as the Light Spire and Monument of Wisdom. The DLC adds Sanctuary-related data with additional abilities and offering recipes.

For map-specific shrine, sanctuary, POI, and objective flow, see [Map, POI, And Objective Systems](map-poi-objectives.md).

## Main Files

| File | Role |
| --- | --- |
| `game-gdb/core/gdb/buildings.gd.xml` | Core shrine buildings and `AspectShrine` |
| `game-gdb/core/gdb/abilities.gd.xml` | Core `ShrineAbility` definitions |
| `game-gdb/core/gdb/productionrecipes.gd.xml` | Core `ShrineRecipe` entries |
| `game-gdb/dlc1/gdb/buildings.gd.xml` | Sanctuary building, mana resource, and offering recipes |
| `game-gdb/dlc1/gdb/abilities.gd.xml` | DLC shrine/sanctuary abilities |
| `game-gdb/dlc1/maps/meadowsong map database.gd.xml` | DLC map-specific sanctuary objectives and POIs |

## Shrine Buildings

Shrine-capable buildings expose `AspectShrine`.

Important fields:

| Field | Meaning |
| --- | --- |
| `Abilities` | GUID references to `ShrineAbility` entities. |
| `Employment` | Worker data used by the shrine. |
| `ManaProduction` | Recipe and mana pile data for producing/holding shrine mana. |
| `ManaPiles` | Mana resources and possible refund resources. |

Generated catalog:

- `generated/catalog/systems/shrine-buildings.md`

## Shrine Abilities

`ShrineAbility` describes the shared UI and usage part of an ability.

Important fields:

| Field | Meaning |
| --- | --- |
| `Name` / `Description` | Localization keys. |
| `Icon` | Ability icon. |
| `Type` | Ability type, such as exploration, detection, spawning, conversion, or deposit manipulation. |
| `UsageType` | Active, targeted, passive, or similar usage mode. |
| `ManaCost` | Mana cost when present. |
| `Cooldown` | Cooldown duration. |
| `TargetRange` | Targeting range for active abilities. |
| `ResourceCosts` | Additional resource costs if used. |

Abilities often include a second component that contains the actual effect, for example `ExploreAreaAbility`, `SpawnUnitsAbility`, `ConvertUnitsAreaAbility`, or `RemoveDepositsAreaAbility`.

Generated catalog:

- `generated/catalog/systems/shrine-abilities.md`

## Shrine Recipes

Shrine recipes use `ShrineRecipe`, which looks similar to production recipes but appears specialized for mana/offering workflows.

Important fields:

| Field | Meaning |
| --- | --- |
| `RecipeIdentifier` | Internal recipe key when present. |
| `ProductionSteps` | Step machine for offerings, piles, waits, work, and input resources. |
| `Input/Resource` | Resource consumed by an offering step. |
| `AssignedWorker` | Worker slot used by a step. |
| `TargetLocator` | Building locator for the offering/pile visual. |

Generated catalog:

- `generated/catalog/systems/shrine-recipes.md`

## Modding Notes

Safer first edits:

- inspect ability mana costs, cooldowns, and ranges
- compare shrine recipe inputs
- trace which building exposes which abilities

Riskier edits:

- remove abilities from shrine buildings without checking objectives
- edit effect-specific components without understanding their radius, target, and visual fields
- change shrine recipe step order
- remove offering resources that are used by DLC objectives or map logic

## Good First Trace

Start with a visible shrine building:

1. Find the building with `AspectShrine`.
2. Resolve every ability GUID under `Abilities`.
3. Open each `ShrineAbility` and inspect its `Type`, `ManaCost`, and effect-specific component.
4. Resolve mana production recipes.
5. Check map objectives that mention shrine ability usage.
