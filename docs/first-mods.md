# 🎓 First Modding Experiments

This page collects small, relatively safe experiments for learning how the game database works.

These examples are written as guidance, not as guaranteed compatible mods for every game version. Always keep backups and validate your local XML files after changes.

> **Which mod pattern is this?** The experiments below all target **Pattern A (patched canonical pak)** — you edit shipped XML and rebuild the original pak. For an overview of the other shapes a Pioneers of Pagonia mod can take (side-by-side overlay paks, user maps from the editor), see [Mod Distribution Patterns](mod-distribution.md).

## Before You Start

Read these first:

- [Local Setup](setup.md)
- [GUID Reference](guid-reference.md)
- [Entity Model](entities.md)

For more detailed walkthroughs, continue with [Worked Examples](examples/README.md) after this page.

Run validation before making changes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Then make one small change at a time.

After each change:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

## Experiment 1: Change A Building Construction Cost

Goal:

Change the amount of an existing construction cost.

Where to look:

```text
game-gdb/core/gdb/buildings.gd.xml
```

Find a building and locate the cost entry. Each cost is a list item, so
`Resource` and `Amount` live under an `Item/Content` wrapper — not directly
under `Costs`:

```xml
<AspectBuildup>
  ...
  <Costs>
    <Item>
      <Content>
        <Resource>resource-guid</Resource>
        <Amount>4</Amount>
        <PileLocator>...</PileLocator>
      </Content>
    </Item>
    ...
  </Costs>
</AspectBuildup>
```

Safer change (change only the `Amount`):

```xml
<Amount>4</Amount>
```

to:

```xml
<Amount>3</Amount>
```

Why this is a good first mod:

- the resource GUID stays unchanged
- the cost entry stays structurally the same
- workers and locators stay unchanged

Avoid at first:

- changing `Resource`
- deleting an `Item`
- changing `PileLocator`

## Experiment 2: Change Recipe Output Amount

Goal:

Make an existing recipe produce more or less of the same output.

Where to look:

```text
game-gdb/core/gdb/productionrecipes.gd.xml
```

Find a `ProductionRecipe`, then find an `Output` step. The output resource
lives under a `Content/InputOutput` wrapper inside the step's `Item` — search
for `InputOutput` to land on the exact element:

```xml
<Item>
  <Content>
    <Type>Output</Type>
    ...
    <InputOutput>
      <Resource>resource-guid</Resource>
    </InputOutput>
  </Content>
</Item>
```

Notice there is **no** `<Amount>` inside `<InputOutput>`. Most
`ProductionRecipe` rows express their input/output *quantities* through step
state, not through a per-resource amount field — so don't add one. The recipes
that *do* carry an explicit, safely-editable amount are the shrine-style
recipes, where the amount sits inside a `WorkplacePile` (no `Resource` child):

```xml
<Wait>
  <WorkplacePile>
    <Amount>10</Amount>
  </WorkplacePile>
</Wait>
```

Safer change:

- search `<Amount>` in `productionrecipes.gd.xml` to find a recipe that
  already has one, then adjust that number
- keep step order unchanged

The dedicated walkthrough [Change A Recipe Resource Amount](examples/change-recipe-resource-amount.md)
covers this end to end.

Why this is useful:

You learn how recipe steps work without disturbing animations, locators, or input conditions.

Avoid at first:

- changing `StartCondition`
- changing `FinishCondition`
- removing `Wait` or `Work` steps
- changing `TargetLocator`

## Experiment 3: Change Build Menu Order

Goal:

Move a building within its construction menu group.

Where to look:

```text
game-gdb/core/gdb/buildings.gd.xml
```

Find:

```xml
<Buildable>
  <Category>category-guid</Category>
  <SortOrder>number</SortOrder>
  <UiBuildingGroup>...</UiBuildingGroup>
</Buildable>
```

Safer change:

- change `SortOrder`

Avoid at first:

- changing `Category`
- changing `CanBuild`
- changing unlock-related components

## Experiment 4: Trace A Resource Through The Economy

Goal:

Understand where a resource is defined and used.

Steps:

1. Search for a resource name:

    ```powershell
    rg --no-ignore "Firewood|Softwood|Wine" .\game-gdb
    ```

2. Find the resource entity and copy its GUID.

3. Search for the GUID:

    ```powershell
    rg --no-ignore "resource-guid-here" .\game-gdb
    ```

4. Note where it appears:

    - recipes
    - building costs
    - storage
    - objectives
    - DLC or campaign files

This experiment is safe because you are only reading and tracing.

## Experiment 5: Compare Two Similar Entities

Goal:

Learn patterns by comparing similar objects.

Good comparisons:

- two resources in the same category
- two production recipes used by the same building
- two houses/residences
- two decorative buildings
- core resource versus DLC resource

Useful command:

```powershell
rg --no-ignore "Entity Name=.*Sawmill|Entity Name=.*Wood" .\game-gdb\core\gdb\buildings.gd.xml
```

Or search by component:

```powershell
rg --no-ignore "AspectProduction|AspectStorage|AspectHome" .\game-gdb\core\gdb\buildings.gd.xml
```

## What To Avoid In First Mods

Avoid these until you are comfortable with references:

- changing existing GUIDs
- deleting entities
- removing components
- editing campaign objective chains
- editing `GridSize`, terrain blocking, or placement rules
- changing recipe state-machine conditions
- replacing visual prefab paths
- changing tags used by objectives

## A Good Testing Habit

Use a tiny changelog for yourself:

```text
Changed:
- game-gdb/core/gdb/buildings.gd.xml
- Building: Example Building
- Field: AspectBuildup/Costs/Amount
- Old value: 3
- New value: 2

Validated:
- XML parses
- no duplicate GUIDs
- unresolved non-null references unchanged
```

This makes it much easier to undo a change or explain it in a pull request.

## Next: Move To The Sandbox

The experiments above edit `game-gdb/` directly. That is the fastest way to *see* how the data fits together, but it is destructive: every edit overwrites an official game file, so undoing a change means re-extracting the pak.

Once you want to keep a change rather than just inspect one, move to the **sandbox**. It lets you write the same edits as small, named mods that apply *on top of* `game-gdb/` without ever touching it — so the original stays clean, several mods can be bundled into a collection, and the result can be packed into a `.pak` the game loads. Your in-progress mods stay git-ignored until you choose to publish them.

→ [Sandbox](../sandbox/README.md) walks through copying a template, applying it with `scripts/sandbox-apply.ps1`, and packing the output into a `.pak`.
