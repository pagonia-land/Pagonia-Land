# Trace A DLC Production Addition

This example is read-only. It shows how DLC content can extend the base game database.

The current generated catalog shows several DLC production additions, including:

- `Bakery DLC1 extension` — extends the base Bakery via `InheritanceMode="Incremental"`
- `Brewery DLC1 extension` — same pattern
- `Provisioner DLC1 extension` — same pattern
- `Winery` — brand-new building, no inheritance attribute

> The `-DLC1 extension` entities use Meadowsong's **Incremental inheritance**: a new entity with its own GUID, declaring `InheritanceMode="Incremental"` + `InheritedGuid="<core entity GUID>"`, contributes additional list items (recipes, costs, locators) on top of the inherited entity's lists. List items reference preserved positions via `<InheritedIndex>N</InheritedIndex>` markers. See [DLC Patch And Override Model → Patch Behaviour Patterns](../dlc-patch-override-model.md#patch-behaviour-patterns) for the full list of inheritance primitives.

## Goal

Understand how a DLC package adds production data while still using the combined effective database.

## Step 1: Open The DLC Package Filter

Regenerate the catalog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Open:

```text
generated/catalog/filters/packages/dlc1/building-production.md
generated/catalog/filters/packages/dlc1/production-graph.md
```

This shows DLC building-production links without mixing them into the full core list.

## Step 2: Pick A DLC Building

`Winery` is a good first example because it is a DLC building with its own production recipes.

Search:

```powershell
rg --no-ignore "Winery|WineRecipe|AppleJuiceRecipe" .\game-gdb\dlc1
```

Look for:

```text
Building
Buildable
AspectBuildup
AspectProduction
Recipes
```

## Step 3: Trace Recipe GUIDs

When you find recipe GUIDs in the DLC building, search them:

```powershell
rg --no-ignore "RECIPE-GUID-HERE" .\game-gdb
```

Important question:

- Is the recipe defined in `dlc1`?
- Does it reference core resources?
- Does it reference DLC resources?
- Does it use a core worker?

This is the practical difference between source package and effective database.

## Step 4: Compare With Core Buildings

Compare Winery with a core production building such as Brewery:

```powershell
rg --no-ignore "Brewery|BeerRecipe|ElderberryJuiceRecipe" .\game-gdb\core\gdb\buildings.gd.xml .\game-gdb\core\gdb\productionrecipes.gd.xml
```

Look for similar structure:

- construction costs
- worker employment
- recipes list
- optimal work step time
- input/output resources

## Step 5: Understand The Pattern

DLC can add new entities that still plug into core systems.

The pattern often looks like:

```text
dlc1 building -> dlc1 recipe -> dlc1 and/or core resources
dlc1 building -> core worker/category/template references
dlc1 objective/map -> dlc1 and/or core entities
```

## Safe Editing Advice

For DLC production edits:

- edit only DLC files when targeting DLC-only content
- trace every referenced GUID before changing it
- avoid changing core entities unless you intend to affect base game and DLC
- test with the DLC enabled

## What You Learned

Package folders are physical source locations. The game database behaves more like a combined effective database where packages can reference each other.

This is why the package-filtered catalog is helpful: it shows where an entity comes from without hiding cross-package dependencies.
