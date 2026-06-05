# Trace The Sawmill Production Chain

This example is read-only. It teaches how to trace a production chain from building to recipe to resources.

The Sawmill is a good first trace because it is part of the base economy and has multiple simple recipes.

## Goal

Answer these questions:

- Which building produces sawmill outputs?
- Which recipes are attached to that building?
- Which resources do those recipes consume and produce?
- Where is the production timing stored?

## Step 1: Start With The Generated Catalog

Regenerate the catalog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Open:

```text
generated/catalog/filters/production/buildings-with-recipes.md
generated/catalog/building-dependency-matrix.md
```

Find `Sawmill`. In the current database it appears with these recipes:

```text
FirewoodFromHardwoodRecipe
FirewoodFromSoftwoodRecipe
SoftwoodBoardsRecipe
```

The current catalog also reports `OptimalWorkStep` as:

```text
00:00:21
```

This timing hint comes from the building, not from the recipe itself.

The dependency matrix also summarizes the same building in one row: construction costs, production worker, recipe inputs, recipe outputs, and explicit storage resources.

## Step 2: Find The Building Entity

Search for the building name or GUID:

```powershell
rg --no-ignore "Sawmill|c732cb26-7487-4a7b-b1ba-b65e094f9bac" .\game-gdb\core\gdb\buildings.gd.xml
```

Look for:

```text
AspectProduction
Recipes
Employment
Efficiency
TimeOfOptimalWorkStep
```

This tells you which recipes are attached to the building, which worker is used, and which building-level timing hint is configured.

## Step 3: Find The Recipe Entities

Search the recipe file:

```powershell
rg --no-ignore "FirewoodFromSoftwoodRecipe|SoftwoodBoardsRecipe|FirewoodFromHardwoodRecipe" .\game-gdb\core\gdb\productionrecipes.gd.xml
```

Then inspect the matching `ProductionRecipe` entities.

Look for:

```text
ProductionSteps
Type
InputOutput
Resource
Work
LoopAnimationAmount
```

The important pattern is:

- `Input` steps usually consume resources.
- `Output` steps usually produce resources.
- `Work` steps describe work/animation loop behavior.
- `Wait` or `WaitInterruptable` steps coordinate the state machine.

## Step 4: Resolve Resource GUIDs

Whenever you find a resource GUID, search it:

```powershell
rg --no-ignore "RESOURCE-GUID-HERE" .\game-gdb
```

Or inspect:

```text
generated/entities.json
generated/references.json
generated/catalog/recipes.md
```

The generated catalog already resolves many resource names for you, so it is usually the fastest first stop.

## Step 5: Draw The Mental Model

For this kind of production chain, think:

```text
Building -> AspectProduction -> Recipe GUIDs -> ProductionRecipe -> Input/Output resources
```

For the current Sawmill:

```text
Sawmill -> SoftwoodBoardsRecipe -> resource inputs/outputs
Sawmill -> FirewoodFromSoftwoodRecipe -> resource inputs/outputs
Sawmill -> FirewoodFromHardwoodRecipe -> resource inputs/outputs
```

The generated Mermaid graph shows the same relationship in visual form:

```text
generated/catalog/production-graph.md
generated/catalog/filters/packages/core/production-graph.md
generated/catalog/production-chain-graph.md
```

## What You Learned

You traced a production chain without editing any files.

The key lesson is that production is split:

- buildings connect workers, recipes, and timing hints
- recipes define input/output/work step flow
- resources are separate entities referenced by GUID

This is the foundation for safer production mods.
