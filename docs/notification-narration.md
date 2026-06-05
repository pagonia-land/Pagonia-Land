# 🔔 Notifications And Narration

Notifications and narration dialogs are the player-facing layer of objectives, map events, warnings, tutorials, and campaign flow.

This system is useful for modding because many gameplay changes still need clear feedback. A changed objective, encounter, resource requirement, or map event can technically work while still confusing the player if its notification or dialog text no longer matches the new behavior.

## Main XML Areas

Common files:

```text
game-gdb/core/gdb/notifications.gd.xml
game-gdb/dlc1/gdb/notifications.gd.xml
game-gdb/core/gdb/campaign map *.gd.xml
game-gdb/dlc1/maps/meadowsong map database.gd.xml
game-gdb/core/gdb/campaignglobals.gd.xml
game-gdb/dlc1/gdb/globals.gd.xml
```

Notification definitions usually use a `Notification` component. Campaign and map dialogs commonly combine a `Notification` wrapper with `UiNarrationDialog`.

## Common Components

| Component | Likely Role |
| --- | --- |
| `Notification` | Message key, icon, lifetime, sound, optional announcement, and category data |
| `NotificationFilterCategory` | Radius and duration grouping/filter behavior for repeated notifications |
| `NotificationParameters` | Global notification timing/history settings |
| `UiNarrationDialog` | Dialog type, default speaker, and ordered narration line keys |
| `UiNarrationDialogCamera` | Camera behavior for a narration dialog |

## Generated Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then open:

```text
generated/catalog/notification-narration.md
generated/catalog/notification-narration.csv
generated/catalog/filters/notifications/notifications.md
generated/catalog/filters/notifications/narration-dialogs.md
generated/catalog/filters/notifications/referenced.md
generated/catalog/filters/notifications/unreferenced.md
generated/catalog/filters/packages/<package>/notification-narration.md
```

The CSV file is best for filtering by package, kind, message key, dialog type, speaker, sound, or reference count.

## Important Columns

| Column | Meaning |
| --- | --- |
| `Kind` | `Notification`, `NotificationWithFilter`, `NarrationDialog`, `NotificationFilterCategory`, or `NotificationParameters` |
| `MessageKey` | Text key or message key used by the notification |
| `DialogType` | Narration dialog type, such as `InfoConfirm`, `InfoTimed`, `Fullscreen`, or `GameEnd` |
| `DialogLines` | Ordered line keys, with resolved speaker names when available |
| `DefaultSpeaker` | Resolved default speaker entity |
| `Flags` | Notification behavior flags such as position updates |
| `Icon` | UI icon path |
| `LifeTime` | How long the notification remains active |
| `Sound` / `Music` | FMOD-style event or music hints when present |
| `FilterCategory` | Resolved category/filter reference |
| `FilterDuration` / `FilterRadius` | Category suppression/grouping hints |
| `ReferenceCount` | Number of detected reverse references from other entities |
| `ReferencedBy` | Source fields and entities that point to this notification or dialog |
| `LocalizationKeys` | Text-like keys found inside the notification/dialog entity |

## Current Local Snapshot

The latest local catalog generation reports:

| Catalog | Count |
| --- | ---: |
| Notification and narration rows | 495 |
| Notification rows | 152 |
| Narration dialog rows | 336 |
| Referenced notification and narration rows | 416 |
| Unreferenced notification and narration rows | 78 |

Unreferenced rows are research hints, not automatic errors. Some abstract templates, base dialogs, test entries, or runtime-triggered rows may legitimately have no direct GUID reference in the extracted XML.

## Modding Workflow

1. Find the objective, unit, building, resource, POI, or map event you want to change.
2. Search its GUID or name in `generated/catalog/notification-narration.csv`.
3. Check `ReferencedBy` to see which objective or event uses the notification/dialog.
4. Check `MessageKey`, `DialogLines`, and `LocalizationKeys`.
5. Search the text key in the localization catalog.
6. If you change a requirement or reward, verify that the related text key still describes the new behavior.
7. Regenerate the catalog and check the reference count again.

Useful searches:

```powershell
rg --no-ignore "CM1-D27|UiNarrationDialog|NotificationFilterCategory" .\game-gdb
rg --no-ignore "Notification - Owner: Alert Hostile" .\generated\catalog\notification-narration.md
rg --no-ignore "DLC1CM1-D" .\generated\catalog\filters\notifications\narration-dialogs.md
```

## Safer Edits

Safer first experiments:

- reuse an existing notification message key
- adjust a notification lifetime for local testing
- change a dialog line reference to another existing line key
- change an icon path only to another known working icon

Riskier edits:

- remove notifications used by objectives
- change dialog GUIDs
- remove narration dialogs from objective preconditions
- change `DialogType` without testing the full map flow
- remove filter categories used to throttle repeated warnings
- create new text keys without understanding the external localization pipeline

## Why This Matters

Notifications and narration are not just flavor. They are often part of scenario pacing:

```text
objective / map event
-> notification or narration dialog
-> player instruction or warning
-> next objective, reward, or trigger
```

A good gameplay mod should keep this chain understandable. The catalog gives modders a quick way to check whether changed mechanics still have matching player-facing feedback.
