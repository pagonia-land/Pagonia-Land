# Trace A Map Objective

This example is read-only. It teaches how to inspect objective flow without editing it.

Objectives are high-risk edit targets, so tracing comes before modification.

## Goal

Trace one objective from a map file to its notifications, triggers, and referenced entities.

## Step 1: Pick A Map File

Campaign and DLC map objectives can appear in files such as:

```text
game-gdb/core/gdb/campaign map 1 - tutorial.gd.xml
game-gdb/core/gdb/campaign map 7 - final showdown.gd.xml
game-gdb/dlc1/maps/meadowsong map database.gd.xml
```

For a first trace, pick an early campaign or DLC map objective and do not edit it.

## Step 2: Search For Objective Components

Use:

```powershell
rg --no-ignore "Objective|ObjectiveNotifications|ObjectiveCompletionTriggers|RewardTime" ".\game-gdb\core\gdb\campaign map 1 - tutorial.gd.xml"
```

For DLC:

```powershell
rg --no-ignore "Objective|ObjectiveNotifications|ObjectiveCompletionTriggers|RewardTime" ".\game-gdb\dlc1\maps\meadowsong map database.gd.xml"
```

## Step 3: Identify The Objective Entity

Copy the objective entity GUID and name.

Then search the whole database:

```powershell
rg --no-ignore "OBJECTIVE-GUID-HERE" .\game-gdb
```

This tells you whether other map logic, notifications, unlock conditions, or rewards point at it.

## Step 4: Trace Notifications

Within the objective, find notification GUIDs or notification blocks.

Search each GUID:

```powershell
rg --no-ignore "NOTIFICATION-GUID-HERE" .\game-gdb
```

Then inspect:

```text
game-gdb/core/gdb/notifications.gd.xml
game-gdb/dlc1/gdb/notifications.gd.xml
```

Objective logic can technically work without clear notifications, but player experience becomes confusing.

## Step 5: Trace Requirements

Depending on the objective type, inspect fields such as:

- required resource or commodity
- required amount
- point of interest target
- unit target
- building target
- faction/NPC target
- unlock condition

Search every non-null GUID referenced in the objective.

## Step 6: Trace Rewards And Triggers

Look for:

```text
RewardTime
ObjectiveCompletionTriggers
OnObjectiveStarted
OnObjectiveCompleted
OnObjectiveSkipped
OnObjectiveFailed
```

These fields describe when follow-up actions happen.

Do not edit them in a first pass.

## Step 7: Draw A Small Trace Note

Write something like:

```text
Objective:
File:
GUID:
Type/component:
Requires:
Notifications:
Rewards:
Triggers:
Other references:
```

This becomes very useful later if you want to safely change requirements or document campaign flow.

## What Not To Edit Yet

Avoid:

- objective GUIDs
- trigger hooks
- reward timing
- map-specific objective chains
- required target GUIDs
- notification references

## What You Learned

Objectives are not isolated tasks. They are small pieces of a larger map state machine.

A safe objective mod starts with a complete trace of requirements, notifications, triggers, and rewards.
