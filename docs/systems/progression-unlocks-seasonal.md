# 🌱 Tech Tree, Unlocks, And Seasonal Gates

This page explains the first generated catalog pass for progression and availability data: tech tree tier groups, explicit `NeedsUnlock` gates, objective unlock rewards, and seasonal gates.

The important modding idea is that availability is not stored in one place. A building, unit, recipe, map, or DLC item may be available because of a tech tree group, a campaign objective reward, a DLC gate, or a seasonal marker.

## Generated Catalogs

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/tech-tree.md
generated/catalog/unlock-gates.md
generated/catalog/unlock-rewards.md
generated/catalog/seasonal-gates.md
generated/catalog/filters/progression/
generated/catalog/filters/packages/<package>/tech-tree.md
generated/catalog/filters/packages/<package>/unlock-gates.md
generated/catalog/filters/packages/<package>/unlock-rewards.md
generated/catalog/filters/packages/<package>/seasonal-gates.md
```

The latest local generation produced:

| Catalog | Count |
| --- | ---: |
| Tech tree rows | 21 |
| Tech tree rows with objectives | 10 |
| Unlock gate rows | 33 |
| DLC unlock gate rows | 24 |
| Unlock reward rows | 146 |
| Unlock reward rows for buildings | 82 |
| Unlock reward rows for recipes | 49 |
| Unlock reward rows for recruitments | 42 |
| Unlock reward rows for tech tree groups | 3 |
| Seasonal rows | 256 |
| Seasonal rows with explicit allowed seasons | 1 |

These counts are snapshot-specific and should be regenerated after each game update.

## Main XML Areas

| Area | Typical files | What to look for |
| --- | --- | --- |
| Tech tree | `game-gdb/core/gdb/techtree.gd.xml`, `game-gdb/dlc1/gdb/globals.gd.xml` | `TechTreeTierGroup`, unlocked buildings, units, objectives, notifications |
| DLC and explicit gates | `decorations1`, `dlc1`, selected `core` files | `NeedsUnlock`, usually with a `DLC` reference |
| Objective rewards | campaign/map XML files, `campaignglobals.gd.xml`, `techtree.gd.xml` | `ObjectiveStartRewards`, `ObjectiveRewards`, unlocked buildings, recipes, recruitments, abilities |
| Seasonal pools | `objectives.gd.xml`, DLC objectives | `Seasonal`, `AllowedSeasons` |

## Catalog Meaning

`tech-tree` is one row per `TechTreeTierGroup`.

Important columns:

| Column | Meaning |
| --- | --- |
| `TierGroup` | Technical entity name or display name |
| `Tier` | Tier label when present, such as `Two`, `Three`, or `Eight` |
| `Buildings` | Buildings unlocked or associated with the tier group |
| `Units` | Units unlocked or associated with the tier group |
| `PrimaryObjective` | Main objective that unlocks or represents the tier group |
| `AlternativeObjectives` | Alternative unlock objectives |
| `OnUnlockedNotification` | Notification shown when the group unlocks |

`unlock-gates` is one row per entity with `NeedsUnlock`.

This is especially useful for DLC and decoration content. Many rows point to DLC entities, which means the content is present in the database but gated by ownership or availability.

`unlock-rewards` is one row per objective reward component.

It summarizes:

- unlocked buildings
- gatherer farm recipe identifiers
- unlocked production recipes
- unlocked recruitments
- shrine abilities
- tech tree tier groups
- selected NPC/base modifications

`seasonal-gates` is one row per entity with `Seasonal`.

Most current seasonal rows are objective pool rows and do not explicitly list allowed seasons. One current row lists `Default, Easter, Halloween, Christmas`.

## Useful Filter Views

| Filter | Use |
| --- | --- |
| `filters/progression/tech-tree-with-objectives` | Tech tree groups with primary or alternative unlock objectives |
| `filters/progression/dlc-unlock-gates` | `NeedsUnlock` rows that reference a DLC |
| `filters/progression/unlock-rewards-buildings` | Objectives that unlock buildings |
| `filters/progression/unlock-rewards-recipes` | Objectives that unlock gatherer or production recipes |
| `filters/progression/unlock-rewards-recruitments` | Objectives that unlock recruitments or units |
| `filters/progression/unlock-rewards-tech-tree` | Objectives that unlock tech tree groups |
| `filters/progression/seasonal-with-allowed-seasons` | Seasonal rows with explicit `AllowedSeasons` |

## Modding Workflow

1. Search `generated/catalog/tech-tree.md` for the building, unit, or tier group you care about.
2. Check `PrimaryObjective`, `AlternativeObjectives`, and `OnUnlockedNotification`.
3. Search the same target in `generated/catalog/unlock-rewards.md` to see campaign or scenario rewards.
4. Check `generated/catalog/unlock-gates.md` for DLC or explicit `NeedsUnlock` gates.
5. Check `generated/catalog/seasonal-gates.md` if the entity comes from objective pools or event-like content.
6. After editing, regenerate catalogs and run validation.

Useful Windows command:

```powershell
rg --no-ignore "TechtreeGroupTier2|NeedsUnlock|ObjectiveStartRewards|Seasonal" .\game-gdb\
```

If `rg` is not installed:

```powershell
winget install BurntSushi.ripgrep.MSVC
```

## Reading Notes

Do not treat a single unlock row as the complete truth. Availability can be layered:

- `TechTreeTierGroup` can list buildings and units.
- Objective rewards can unlock buildings, recipes, recruitments, abilities, or tech tree groups.
- `NeedsUnlock` can still gate content even if it is otherwise defined.
- DLC packages can add their own tier groups or map-specific reward flow.
- Seasonal rows may be used as pools or weighted objective candidates rather than player-visible date gates.

When changing availability, always trace both directions: what unlocks the content, and what content the unlock grants.
