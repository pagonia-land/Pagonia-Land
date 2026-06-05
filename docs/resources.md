# 🪵 Resources

Resources represent goods, materials, food, tools, weapons, treasure, DLC products, and other carryable or countable things in the economy.

Most base game resources are defined in:

```text
game-gdb/core/gdb/resources.gd.xml
```

DLC resources are defined in:

```text
game-gdb/dlc1/gdb/resources.gd.xml
```

## Main Component

Resource entities usually contain a `ResourceDescription` component.

Common fields:

| Field | Meaning |
| --- | --- |
| `ResourceCategory` | GUID reference to a resource category |
| `Name` | Display/localization key or readable name |
| `NamePlural` | Plural display/localization key |
| `Icon` | UI icon path |
| `Mesh` | 3D mesh or asset path |
| `PileMeshByPosition` | Visual pile variants |
| `CarryType` | How a carrier visually transports the resource |
| `UiDisplay` | Whether/how the resource appears in UI |
| `Tags` | Flexible grouping for objectives, recipes, storage, or other logic |
| `NotificationBySeverity` | Notification links for shortage/importance states |

## Resource Categories

Categories are separate entities, mainly in:

```text
game-gdb/core/gdb/resourcecategories.gd.xml
```

A resource points to a category through `ResourceCategory`.

Categories are useful for UI grouping and broad logic. Do not replace a category GUID blindly; it may affect menus, storage, objectives, or production filters.

## Where Resources Are Used

Resources are referenced by many systems:

| System | Typical use |
| --- | --- |
| Building buildup | Construction costs |
| Production recipes | Inputs and outputs |
| Deposits | Gathered resource type |
| Storage | Stored or accepted resource types |
| Objectives | Required amounts or produced goods |
| Trade | Trade requests, supply objectives, market behavior |
| UI | Icons, categories, top bar entries, notifications |

This is why resource edits should start with reference tracing.

## Generated Resource Catalogs

Run the local catalog generator:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/resources.md
generated/catalog/resource-flow.md
generated/catalog/resource-usage.md
generated/catalog/filters/production/resources-produced.md
generated/catalog/filters/production/resources-consumed.md
generated/catalog/filters/production/resources-stored.md
```

The most useful first file is `resource-flow.md`. It answers:

- where a resource is produced or gathered
- where it is consumed by recipes or shrine recipes
- which buildings use it as a construction cost
- which units use it as a recruitment cost
- which buildings explicitly reference it in storage/pile data
- whether it appears as a treasure hunter target

Use `resource-usage.md` when you need the detailed row-by-row source context.

The resource flow catalog is generated from explicit database references. Generic storage behavior that is not tied to a specific resource list is not expanded into every possible resource.

## Common Safe Edits

Usually safer:

- changing `SortOrder`
- adjusting `UiDisplay`, if you understand the UI consequence
- changing `Icon` to another valid icon path
- changing display/localization-style text for local experiments
- adding tags only after checking similar resources

Be careful with:

- `ResourceCategory`
- `CarryType`
- `Mesh`
- pile mesh configuration
- tags used by objectives or production filters

Avoid unless you know the full reference chain:

- changing a resource GUID
- deleting a resource
- replacing a resource GUID in recipes with a resource from a very different category

## Example: Trace A Resource

To understand a resource, search for its name first:

```powershell
rg --no-ignore "Firewood|Softwood|Apple|Wine" .\game-gdb
```

Then search for the entity's GUID:

```powershell
rg --no-ignore "resource-guid-here" .\game-gdb
```

You are looking for:

- definition in `resources.gd.xml`
- production recipe inputs/outputs
- building construction costs
- storage or trade references
- objective requirements

## Example: Changing A Construction Cost Resource

Suppose a building cost points to:

```xml
<Resource>...</Resource>
<Amount>3</Amount>
```

Changing `Amount` is usually much safer than changing `Resource`.

If you change `Resource`, verify that:

- the new GUID points to a real resource entity
- carriers can transport it
- the target building can accept/display it
- the economy can produce or gather it before the building is needed

## DLC Resources

DLC resources follow the same structure as core resources. For example, `dlc1` adds resources such as apples, grapes, wine, eggs, cleansing plants, and cleansing potions.

DLC resources often reference core categories or are used by DLC recipes, DLC buildings, and DLC objectives. This is a good example of the additive package model: DLC files extend the system rather than replacing core files wholesale.

## Practical Advice

For first mods, prefer changing amounts where resources are used, rather than changing the resource entities themselves.

Good first experiments:

- lower or raise a building construction resource amount
- increase a recipe output amount
- adjust resource sort order

Save resource identity changes for later. They can affect many systems at once.
