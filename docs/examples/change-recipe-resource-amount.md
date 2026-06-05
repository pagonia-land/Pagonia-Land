# Change A Recipe Resource Amount

This example explains how to look for editable resource amounts inside recipe-like data.

Important limitation: many normal `ProductionRecipe` rows in the current database express input and output resources through step state, not through an obvious `<Amount>` field. Do not invent an amount field if the recipe does not already use one.

## Goal

Change an existing amount value only where the XML already has an amount field.

For this example, use shrine recipe-style data because it contains explicit amount values in the current database.

## Step 1: Start With The Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Inspect:

```text
generated/catalog/systems/shrine-recipes.md
generated/catalog/resource-usage.md
generated/catalog/filters/production/resources-consumed.md
```

These files help answer:

- which recipe-like rows consume resources
- which resources are involved
- whether the current generator can summarize the amount

## Step 2: Search For Existing Amount Fields

Search recipe XML files:

```powershell
rg --no-ignore "<Amount>" .\game-gdb\core\gdb\productionrecipes.gd.xml .\game-gdb\dlc1\gdb\productionrecipes.gd.xml
```

If a target recipe has no existing `<Amount>` field near the relevant resource or workplace pile, treat the amount as not safely editable yet.

## Step 3: Inspect The Recipe Context

Search for a shrine recipe with an existing amount:

```powershell
rg --no-ignore "LightSpireFireHuge|<Amount>10</Amount>" .\game-gdb\core\gdb\productionrecipes.gd.xml
```

The current data contains amount values inside shrine recipe step structures such as:

```text
ShrineRecipe
ProductionSteps
Wait
WorkplacePile
Amount
```

## Step 4: Change Only The Existing Amount

Example style of change:

```xml
<Amount>10</Amount>
```

to:

```xml
<Amount>8</Amount>
```

Keep the surrounding structure unchanged.

## Step 5: Do Not Change These Yet

Avoid changing:

- the recipe GUID
- resource GUIDs
- inherited indexes
- step order
- `Type`
- `StartCondition`
- `OnStart`
- `OnFinished`
- worker or locator fields

Those fields affect the recipe state machine and are much riskier than a numeric amount edit.

## Step 6: Validate

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/systems/shrine-recipes.md
generated/catalog/resource-usage.md
```

## Step 7: Test In Game

Use a controlled test save.

Check:

- the recipe still appears where expected
- the UI amount changed if the amount is player-facing
- workers still perform the recipe
- the offering or recipe can complete
- no save instability appears after using the changed recipe

## What You Learned

The safe pattern is:

```text
Find an existing amount -> change only that value -> validate -> regenerate -> test
```

Do not assume every recipe output has an amount field. In this database, many production recipes are step machines where resource flow is expressed by input/output steps and engine behavior rather than simple per-resource amount values.
