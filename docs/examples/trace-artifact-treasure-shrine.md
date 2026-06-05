# Trace An Artifact Through Treasure And Shrine Systems

This example is read-only. It teaches how to follow an artifact from a resource definition into treasure hunter reward data and nearby shrine-style systems.

## Goal

Trace one artifact and understand whether it is:

- a normal artifact resource
- a treasure hunter target
- a combat-boost artifact
- referenced by objectives, notifications, or shrine-related systems

## Step 1: Generate The Local Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then open:

```text
generated/catalog/systems/artifacts.md
generated/catalog/systems/treasure-hunter-targets.md
generated/catalog/systems/artifact-treasure-shrine-graph.md
```

These files are local and ignored by Git.

## Step 2: Pick An Artifact

A good first target is:

```text
Artifact 1 Witchwood Harp
```

Find it in:

```text
game-gdb/core/gdb/resources.gd.xml
```

Record:

```text
Entity name:
GUID:
ResourceCategory:
Name key:
Icon:
Mesh:
CarryType:
Treasure tier:
CombatBoost:
```

## Step 3: Check Whether It Is A Treasure Hunter Target

Search the artifact GUID:

```powershell
rg --no-ignore "ARTIFACT-GUID-HERE" .\game-gdb\core\gdb\treasurehunterrecipes.gd.xml
```

Or use the generated target catalog:

```text
generated/catalog/systems/treasure-hunter-targets.md
```

If the artifact appears there, record the treasure hunter recipe/category name.

## Step 4: Check Whether It Has Combat Boost Data

In `generated/catalog/systems/artifacts.md`, check the `CombatBoost` and `CombatWeight` columns.

If they are empty, the artifact is probably a normal artifact resource.

If they are filled, also search for the artifact GUID in objectives and campaign files:

```powershell
rg --no-ignore "ARTIFACT-GUID-HERE" .\game-gdb
```

Combat-boost artifacts can be tied to scenario balance or objective thresholds.

## Step 5: Check Objectives And Notifications

Search for the artifact name and GUID:

```powershell
rg --no-ignore "Artifact 1 Witchwood Harp|ARTIFACT-GUID-HERE" .\game-gdb
```

Useful files to inspect:

```text
game-gdb/core/gdb/objectives.gd.xml
game-gdb/core/gdb/notifications.gd.xml
game-gdb/core/gdb/campaign map *.gd.xml
```

Record any objective that checks ownership, delivery, production, or combat boost totals.

## Step 6: Compare Shrine Systems

Artifacts and shrines are separate systems, but both can be part of scenario rewards, objectives, and high-value map progression.

Open:

```text
generated/catalog/systems/shrine-buildings.md
generated/catalog/systems/shrine-abilities.md
generated/catalog/systems/shrine-recipes.md
```

Look for the same campaign or DLC file names. If an artifact and a shrine are both defined in the same map file, treat edits as scenario-sensitive.

## Step 7: Draw A Trace Note

Use this template:

```text
Artifact:
GUID:
Package:
Source file:
Resource category:
Treasure hunter recipe:
Combat boost:
Objectives:
Notifications:
Scenario/map references:
Potential risk:
```

## What Not To Edit Yet

Avoid changing:

- artifact GUIDs
- `ResourceCategory`
- combat boost values
- treasure hunter target lists
- objective requirements
- shrine ability references

These fields can connect to several systems at once.

## What You Learned

Artifacts are resource entities, but their meaning depends on references from treasure hunting, objectives, notifications, campaign files, and combat boost data. A safe modding change starts by tracing those references before touching values.
