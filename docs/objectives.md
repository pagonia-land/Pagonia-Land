# 🎯 Objectives

Objectives connect database entities to campaign flow, tutorials, notifications, rewards, map-specific logic, and progression. They are powerful, but they are also one of the riskiest modding areas because a broken objective chain can block a map or corrupt mission pacing.

Objective data appears in several places:

```text
game-gdb/core/gdb/objectives.gd.xml
game-gdb/dlc1/gdb/objectives.gd.xml
game-gdb/core/gdb/campaign map *.gd.xml
game-gdb/core/gdb/campaign test map.gd.xml
game-gdb/dlc1/maps/meadowsong map database.gd.xml
game-gdb/core/gdb/campaignglobals.gd.xml
game-gdb/dlc1/gdb/globals.gd.xml
```

(`campaign test map.gd.xml` is an EE internal test/sandbox map that ships in
`core` alongside the seven numbered campaign maps; the `campaign map *` glob
above does not match its different name.)

For the broader map-specific relationship between objectives, POIs, treasure areas, shrines, artifacts, and scenario flow, see [Map, POI, And Objective Systems](systems/map-poi-objectives.md).

Notifications and related UI data often appear in:

```text
game-gdb/core/gdb/notifications.gd.xml
game-gdb/dlc1/gdb/notifications.gd.xml
```

## What Objectives Usually Connect

Objective entities commonly reference or depend on:

- resources and commodities
- buildings
- points of interest
- NPC bases
- units
- notifications
- rewards
- trigger conditions
- objective categories
- map-specific state

This makes objectives less isolated than resources, recipes, or building costs. Always trace references before editing.

## Common Objective-Like Components

The database uses multiple objective components. Examples seen in the current XML set include:

| Component or Field | Likely Role |
| --- | --- |
| `ObjectiveOwnCommodityAmount` | Require the player to own or store a commodity amount |
| `ObjectiveSupplyPointsOfInterest` | Require resources to be supplied to points of interest |
| `ObjectiveNotifications` | Notifications shown during objective state changes |
| `ObjectiveCompletionTriggers` | Trigger hooks for started, completed, skipped, or failed states |
| `GeneralObjectiveCategory` | UI grouping/category for objectives |
| `UnlockConditions` | Links objective progress to unlock behavior |
| `RewardTime` | Defines when a reward is applied |

The exact field meaning should be verified through examples and in-game testing.

## Notifications

Objectives often use notifications to explain what happened or what changed. Notification references may appear inside:

- objective components
- building components
- unit/NPC components
- resources
- map scripts
- reward blocks

When changing an objective, search for its notification GUIDs and related text keys. If a notification is removed or disconnected, the gameplay may still function but become confusing.

Useful command:

```powershell
rg --no-ignore "ObjectiveNotifications|Notification|RewardTime|ObjectiveCompletionTriggers" .\game-gdb
```

## Rewards And Triggers

Rewards can be linked to objective lifecycle events, such as:

- objective started
- objective completed
- objective skipped
- objective failed

Fields such as `RewardTime` and `ObjectiveCompletionTriggers` should be treated with care. They may cause resources, units, buildings, unlocks, or narrative steps to appear at a specific point in the mission flow.

Safer edits:

- change objective display text keys for local experiments
- adjust small commodity amounts in a copied/test map objective
- change notification text keys if you know where localization is handled

Riskier edits:

- remove triggers
- change reward timing
- change objective dependencies
- change map-specific objective chains
- change objective GUIDs

## Map-Specific Objectives

Campaign maps and DLC maps can contain their own objectives and overrides. These are especially risky because they are tied to a particular map setup.

Examples:

```text
game-gdb/core/gdb/campaign map 1 - tutorial.gd.xml
game-gdb/core/gdb/campaign map 7 - final showdown.gd.xml
game-gdb/dlc1/maps/meadowsong map database.gd.xml
```

When editing map objectives:

1. Work on a copy.
2. Change only one objective at a time.
3. Start the map from the beginning after each change.
4. Check whether the objective appears, updates, completes, and rewards correctly.
5. Keep a clean backup of the original extracted XML.

## Tracing An Objective

A practical tracing workflow:

1. Find the objective entity by name or GUID.
2. List its components.
3. Search every GUID referenced inside the objective.
4. Resolve each referenced GUID in `generated/entities.json`.
5. Check map files for additional references to the objective GUID.
6. Check notifications and rewards.
7. Validate the database.

Useful commands:

```powershell
rg --no-ignore "OBJECTIVE-GUID-HERE" .\game-gdb
rg --no-ignore "ObjectiveOwnCommodityAmount|ObjectiveSupplyPointsOfInterest|ObjectiveNotifications" .\game-gdb
```

## Generated Objective Flow Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then open:

```text
generated/catalog/objective-flow.md
generated/catalog/objective-flow.csv
generated/catalog/filters/objectives/with-preconditions.md
generated/catalog/filters/objectives/with-rewards.md
generated/catalog/filters/objectives/with-notifications.md
generated/catalog/filters/objectives/tech-tree.md
```

The current local catalog reports:

| Catalog | Count |
| --- | ---: |
| Objective flow rows | 814 |
| Rows with preconditions | 249 |
| Rows with rewards | 124 |
| Rows with notifications | 228 |
| Tech tree rows | 21 |

The catalog expands objective-like entities into requirement components, precondition objectives, skip/fail objectives, resource references, building references, unit references, POI/encounter references, start rewards, rewards, notifications, completion triggers, and localization keys.

Treat it as a navigation aid. It helps you find the important pieces quickly, but final campaign flow still needs in-game testing.

## Risk Summary

| Edit | Risk |
| --- | --- |
| Change objective display text key | Low to medium |
| Change a required commodity amount | Medium |
| Change notification references | Medium |
| Change reward resources or timing | High |
| Change trigger hooks | High |
| Remove objective components | High |
| Change objective GUIDs | High |

For early modding, objectives are better for reading and tracing than for editing. Once you have a reliable test map workflow, start with small requirement changes.
