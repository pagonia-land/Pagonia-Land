# Trace A Tech Tree Unlock

This example is read-only. It shows how to trace a tech tree group from its unlock conditions to the objective entities that satisfy those conditions.

Tech tree data is not just a static tree. In the current XML set, tech tree groups point to objective-style requirements.

## Goal

Trace the Copper tier group:

```text
TechtreeGroupTier2Copper
-> UnlockConditions
-> primary objective: UnlockObjectiveT2ProduceCopper
-> alternative objective: UnlockObjectiveT2OwnCopper
-> unlocked notification: TechTreeUnlockedT2Copper
```

## Step 1: Search The Tech Tree Group

```powershell
rg --no-ignore "TechtreeGroupTier2Copper|UnlockObjectiveT2 Copper Tooltip" .\game-gdb\core\gdb\techtree.gd.xml
```

The current XML shows a tier group with:

```text
Tier Two
Title Tier2Title: Copper
ToolTip UnlockObjectiveT2 Copper Tooltip
```

It also lists buildings and units that belong to the group.

## Step 2: Read Unlock Conditions

In the current XML, the Copper group has:

```text
PrimaryObjective c289ba96-2892-4a6f-8db5-af6a4535dac0
AlternativeObjective 2f8f0692-83e6-4e78-b229-a44d3c6a6431
OnUnlockedNotification 45fa45ca-b705-4b01-955e-450210ba67fa
```

Resolve these GUIDs:

```powershell
rg --no-ignore "c289ba96-2892-4a6f-8db5-af6a4535dac0|2f8f0692-83e6-4e78-b229-a44d3c6a6431|45fa45ca-b705-4b01-955e-450210ba67fa" .\game-gdb\core\gdb\techtree.gd.xml
```

Current resolution:

| GUID | Entity |
| --- | --- |
| `c289ba96-2892-4a6f-8db5-af6a4535dac0` | `UnlockObjectiveT2ProduceCopper` |
| `2f8f0692-83e6-4e78-b229-a44d3c6a6431` | `UnlockObjectiveT2OwnCopper` |
| `45fa45ca-b705-4b01-955e-450210ba67fa` | `TechTreeUnlockedT2Copper` |

## Step 3: Inspect The Primary Objective

`UnlockObjectiveT2ProduceCopper` contains:

```text
GeneralObjective
Type TechtreeUnlock
ObjectiveProduceCommoditiesOfType
AmountMin 1
AmountMiddle 1
AmountMax 1
Resource bc7a40d1-84aa-4f05-a2ce-606514f629d1
```

This tells you that the primary unlock path is objective-driven. The player must produce a specific resource.

Resolve the resource:

```powershell
rg --no-ignore "bc7a40d1-84aa-4f05-a2ce-606514f629d1" .\generated\entities.json
```

In the current local extraction, this resolves to `Copper Ore`.

## Step 4: Inspect The Alternative Objective

`UnlockObjectiveT2OwnCopper` contains:

```text
GeneralObjective
Type TechtreeUnlock
ObjectiveOwnCommoditiesWithTag
AmountMin 1
AllowedTags
```

This means the group can be unlocked through an alternative condition based on owning commodities with a tag.

## Step 5: Check The Notification

Search the unlocked notification:

```powershell
rg --no-ignore "TechTreeUnlockedT2Copper|45fa45ca-b705-4b01-955e-450210ba67fa" .\game-gdb
```

Notifications are player-facing. If the unlock condition changes but the notification does not, the game may still function but the player can receive confusing feedback.

## What To Learn

A tech tree group can connect:

- display keys
- buildings
- units
- unlock conditions
- objective entities
- alternative objective paths
- notification entities

This is why tech tree edits are higher risk than changing a simple amount value.

## Modding Notes

Safer experiments:

- trace tech tree groups without editing
- change only display/localization keys for local testing
- compare two adjacent tier groups

Riskier experiments:

- changing `PrimaryObjective`
- removing `AlternativeObjectives`
- changing objective amounts without checking availability of the required resource
- changing `OnUnlockedNotification` without checking visible text
- moving buildings or units between tier groups without checking `NeedsUnlock` and objective rewards

After any tech tree edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then test in a new save from before the unlock would happen.
