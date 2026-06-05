# Change A Building Construction Cost

This example shows how to change a construction cost while preserving the existing entity structure.

It is a low-risk first edit when done carefully.

## Goal

Change the amount of an existing construction resource for one building.

Do not change the resource GUID in this example. Only change an amount.

## Step 1: Pick A Building

Use the generated catalog:

```text
generated/catalog/buildings.md
generated/catalog/filters/packages/core/buildings.md
```

For example, the current catalog lists the Sawmill construction costs as:

```text
4 Softwood Trunk; 4 Stone Block
```

## Step 2: Find The Building In XML

Search by name or GUID:

```powershell
rg --no-ignore "Sawmill|c732cb26-7487-4a7b-b1ba-b65e094f9bac" .\game-gdb\core\gdb\buildings.gd.xml
```

Inside the building entity, find:

```text
AspectBuildup
Costs
Resource
Amount
```

The construction cost entries are list items. Preserve the surrounding list structure.

## Step 3: Change Only The Amount

Example style of change:

```xml
<Amount>4</Amount>
```

to:

```xml
<Amount>3</Amount>
```

Keep these unchanged:

- `Resource`
- `PileLocator`
- `Employment`
- `Unit`
- `Guid`
- list wrappers such as `Item` and `Content`

## Step 4: Validate

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then confirm the generated catalog reflects the new cost:

```text
generated/catalog/buildings.md
```

## Step 5: Test In Game

Use a new test save if possible.

Check:

- the building still appears in the build menu
- the displayed construction cost changed
- the building can still be placed
- construction starts
- workers deliver the expected resource
- construction completes

## What Not To Do Yet

Avoid these in a first cost edit:

- deleting a cost item
- changing a resource GUID
- changing build workers
- changing placement or terrain components
- editing the same building in multiple packages at once

## Rollback Notes

Write down:

```text
File:
Building:
Field:
Old amount:
New amount:
Validation result:
In-game result:
```

This small note makes the change easy to undo and easy to explain later.
