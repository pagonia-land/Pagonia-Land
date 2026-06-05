# Trace A Full Production Chain

This example is read-only. It shows how to use the generated chain catalog to trace longer paths such as a raw resource becoming a tool and then becoming a unit requirement.

## Goal

Answer these questions:

- Which raw or gathered resource starts the chain?
- Which recipes transform it?
- Which building performs each recipe, when known?
- Which building, unit, or shrine recipe depends on the final resource?

## Step 1: Regenerate The Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Open:

```text
generated/catalog/production-chains.md
```

For spreadsheet filtering, use:

```text
generated/catalog/production-chains.csv
```

## Step 2: Filter By End Use

Use the focused generated views:

```text
generated/catalog/filters/production/production-chains-to-buildings.md
generated/catalog/filters/production/production-chains-to-units.md
generated/catalog/filters/production/production-chains-to-shrines.md
```

These answer different modding questions:

| End use | Question |
| --- | --- |
| Buildings | What resource chains are needed to construct buildings? |
| Units | What resource chains are needed to recruit units? |
| Shrines | What resource chains feed shrine or sanctuary recipes? |

## Step 3: Search For A Tool Or Resource

Search the local generated chain catalog:

```powershell
rg --no-ignore "Hammer|Toolsmith|Builder" .\generated\catalog\production-chains.md
```

Example chain shape:

```text
Coal -> IronIngotRecipe [Smelting Works] -> Iron Ingot -> HammerIronRecipe [Toolsmith] -> Hammer -> RecruitmentCost: Builder (1)
```

Read this as:

```text
resource -> recipe [building] -> resource -> recipe [building] -> resource -> end use
```

## Step 4: Inspect The Underlying Rows

If a chain looks important, inspect the smaller catalogs behind it:

```text
generated/catalog/resource-flow.md
generated/catalog/resource-usage.md
generated/catalog/building-production.md
generated/catalog/recipes.md
```

The chain catalog is a derived navigation aid. The underlying catalogs show the raw resolved relationships.

## Step 5: Check The XML Before Editing

Search the exact recipe or resource in the extracted XML:

```powershell
rg --no-ignore "HammerIronRecipe|Iron Ingot|Hammer" .\game-gdb
```

Then verify:

- the recipe input and output resources
- the building that references the recipe
- the unit or building that consumes the final resource
- storage and carry-type behavior for the involved resources

## What You Learned

Long production chains are not stored as one object in the database. They emerge from multiple smaller references:

- recipes consume and produce resources
- buildings attach recipes and timing hints
- construction costs consume resources
- recruitment costs consume resources
- shrine recipes consume resources

The generated chain catalog stitches those references together so a modder can see downstream effects before changing a resource or recipe.
