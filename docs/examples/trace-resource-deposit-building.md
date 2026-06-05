# Trace A Resource From Deposit To Building

This example is read-only. It shows how to trace a resource from world availability to production and consumption.

The goal is to answer:

```text
Where does this resource come from?
Which building or recipe produces it?
Which systems consume it?
```

## Example Resource

Use `Grape` from `dlc1` because it connects several useful systems:

- deposit data
- farm/gather output data
- production recipe data
- shrine offering consumption
- DLC package references

## Step 1: Start With Resource Flow

Regenerate the catalog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Open:

```text
generated/catalog/resource-flow.md
generated/catalog/resource-usage.md
generated/catalog/filters/production/resources-produced.md
generated/catalog/filters/production/resources-consumed.md
```

Search for:

```text
Grape
```

In the current catalog, `Grape` is produced by deposits and the Fruit Farm, and consumed by winery and sanctuary-related recipes.

## Step 2: Find The Resource Entity

Search:

```powershell
rg --no-ignore "Grape|Grape Description|icon_com_grapes" .\game-gdb\dlc1\gdb\resources.gd.xml
```

Read:

```text
ResourceDescription
Name
Description
Icon
ResourceCategory
CarryType
Tags
```

This confirms what the resource is, not where it comes from.

## Step 3: Trace Deposits

Search the DLC deposit file:

```powershell
rg --no-ignore "Grape|Grapevine|HarvestResources|DepositResourceType" .\game-gdb\dlc1\gdb\deposits.gd.xml
```

Then inspect:

```text
generated/catalog/deposits.md
generated/catalog/deposit-resource-types.md
generated/catalog/filters/world/deposits-with-harvest-resources.md
generated/catalog/filters/packages/dlc1/deposits.md
```

Useful fields:

| Field | Meaning |
| --- | --- |
| `DepositResourceType` | Type/category of deposit |
| `HarvestResources` | Resources gathered from the deposit |
| `Prefab` | Visual object used for the deposit |
| `AllowedSediments` | Placement/terrain hints if present |
| `HasGrowing` / `HasRegrowing` | Growth behavior hints |

## Step 4: Trace Production

Search recipes:

```powershell
rg --no-ignore "WineRecipe|wine|Grape" .\game-gdb\dlc1\gdb\productionrecipes.gd.xml
```

Then inspect:

```text
generated/catalog/recipes.md
generated/catalog/building-production.md
generated/catalog/building-dependency-matrix.md
generated/catalog/production-chains.md
```

The current catalog shows:

```text
Grape -> WineRecipe -> Wine
```

and links `WineRecipe` to the `Winery`.

## Step 5: Trace Consumption

Open:

```text
generated/catalog/resource-usage.md
generated/catalog/resource-flow.md
```

Search for `Grape` and note each `UsageType`.

Typical usage contexts include:

- recipe input
- shrine recipe input
- treasure target or special system references, if present

## Step 6: Draw The Mental Model

For this resource, think:

```text
ResourceDescription: Grape
  -> Deposit / farm output
  -> Winery production recipe
  -> Wine
  -> shrine/offering consumption
```

This is exactly the kind of chain the generated catalogs are meant to make visible.

## What Not To Edit Yet

For a first trace, do not change:

- the resource GUID
- the deposit resource type GUID
- recipe step order
- `HarvestResources` references
- building production links
- shrine recipe references

Trace first. Edit later, one small value at a time.

## Validation After A Real Edit

If you later edit any part of this chain, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then re-check the same catalog rows to confirm the chain still resolves.
