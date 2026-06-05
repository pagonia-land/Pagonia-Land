# 👹 NPC, Encounter, And Combat Systems

This page explains the first generated catalog pass for NPC units, NPC bases, factions, encounters, raids, bosses, drops, and infection-related combat data.

The goal is practical modding orientation. The game database does not store "combat" in one file or one table. It combines unit entities, building/base entities, faction metadata, map generation rules, objective references, and encounter components.

## Generated Catalogs

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/npc-units.md
generated/catalog/npc-bases.md
generated/catalog/encounter-combat.md
generated/catalog/factions.md
generated/catalog/filters/combat/
generated/catalog/filters/packages/<package>/npc-units.md
generated/catalog/filters/packages/<package>/npc-bases.md
generated/catalog/filters/packages/<package>/encounter-combat.md
generated/catalog/filters/packages/<package>/factions.md
```

The latest local generation produced:

| Catalog | Count |
| --- | ---: |
| NPC unit rows | 177 |
| NPC base rows | 61 |
| Encounter and combat component rows | 734 |
| Faction rows | 95 |
| Boss rows | 25 |
| Raid-capable NPC unit rows | 57 |
| Custom raid NPC base rows | 10 |
| NPC unit drop rows | 13 |
| Infection-related encounter rows | 13 |

These counts are snapshot-specific and should be regenerated after each game update.

## Main XML Areas

| Area | Typical files | What to look for |
| --- | --- | --- |
| NPC units | `game-gdb/core/gdb/npcunits.gd.xml`, `game-gdb/dlc1/gdb/npcunits.gd.xml` | Animal units, hostile units, boss units, raid parameters, encounter parameters, unit drops |
| NPC bases | `game-gdb/core/gdb/npcbases.gd.xml`, `game-gdb/dlc1/gdb/npcbases.gd.xml` | Dens, camps, infected area objects, spawn units, base encounter parameters, map generation links |
| Factions | `game-gdb/core/gdb/factionparameters.gd.xml`, NPC base/unit files | `Faction`, `SubFaction`, `UiNpcFactionName`, `VisFactionColor`, `UiFactionOverride` |
| Map placement | `npcbases.gd.xml`, mapgen files | `MapGenAnimalSpawn`, `MapGenNpcBuildingRule`, spawn locations, sediment/tag restrictions |
| Campaign and POI variants | campaign/map XML files | Map-specific boss spawns, POIs, objective-linked units and bases |

## How The Catalogs Are Structured

`npc-units` is one row per unit that either comes from an NPC unit file or carries encounter/combat-related components.

Important columns:

| Column | Meaning |
| --- | --- |
| `Unit` | Display name or localization key from the unit component |
| `EncounterNPCUnit` | Whether `AspectEncounterNPCUnit` is present |
| `EncounterInitiator` | Whether the unit can initiate encounters |
| `RaidCapable` | Whether `UnitRaidParameters` is present |
| `Boss` | Whether `AspectPatrollingNPCBoss` is present |
| `Drops` | Whether `UnitDropParameters` is present |
| `Infection` | Whether `InfectionChanceParameters` is present |
| `EncounterRefs` | Resolved GUID references found in encounter-like unit data |
| `RaidValues`, `EncounterValues`, `BossValues`, `DropValues` | Compact scalar summaries for the respective components |

`npc-bases` is one row per NPC-like building/base entity.

Important columns:

| Column | Meaning |
| --- | --- |
| `Base` | Display name or localization key from the building component |
| `EncounterNPCBase` | Whether `AspectEncounterNPCBase` is present |
| `CustomRaid` | Whether `AspectNPCCustomRaid` is present |
| `WildAnimalBase` | Whether `AspectWildAnimalNPCBase` is present |
| `SpawnUnits` | Resolved unit references found in spawn data |
| `MapGenRefs` | Map generation tags, sediment tags, deposit types, and placement references |
| `EncounterValues`, `WildAnimalValues`, `InfectionValues` | Compact scalar summaries for base behavior |

`encounter-combat` is the low-level component view. It emits one row per detected component such as `UnitEncounterParameters`, `AspectEncounterInitiator`, `UnitRaidParameters`, `AspectPatrollingNPCBoss`, `BuildingEncounterParameters`, `EncounterTargetTag`, `SpawnUnitsAbility`, `EffectEncounterSpecialAbility`, or infection-related components.

This is the best table when you want to search for a specific component name.

## Useful Filter Views

| Filter | Use |
| --- | --- |
| `filters/combat/npc-units` | All generated NPC/combat unit rows |
| `filters/combat/npc-bases` | All generated NPC/combat base rows |
| `filters/combat/bosses` | Units with `AspectPatrollingNPCBoss` |
| `filters/combat/raid-capable-units` | Units with `UnitRaidParameters` |
| `filters/combat/custom-raid-bases` | Bases with `AspectNPCCustomRaid` |
| `filters/combat/drop-parameters` | Units with `UnitDropParameters` |
| `filters/combat/infection` | Infection-related components and values |
| `filters/combat/encounter-target-tags` | Rows that declare or reference encounter target tags |

## Reading The Data Safely

The catalogs are relationship maps, not a full combat simulator.

Use them to answer questions such as:

- which units are encounter-capable?
- which units can participate in raids?
- which units are bosses?
- which bases spawn which units?
- which DLC package adds new hostile units or infected-area behavior?
- which components should be inspected before changing encounter balance?

Do not assume that one catalog row equals one final in-game spawn. Some entities are abstract, inherited, map-specific, or scenario-specific. The same visible name can appear multiple times when the database has variants.

## Modding Workflow

1. Start with `generated/catalog/filters/combat/bosses.md` or `raid-capable-units.md` if you are looking for dangerous units.
2. Open the matching row in `generated/catalog/npc-units.csv` for spreadsheet filtering.
3. Search the `Guid` or `Entity` in `game-gdb/` to inspect the exact XML component.
4. Check `generated/catalog/npc-bases.md` for spawn sources and base behavior.
5. Check `generated/catalog/factions.md` for faction presentation and grouping.
6. Check `generated/catalog/objective-flow.md` and `notification-narration.md` before changing map-specific enemies, because objectives or messages may reference them.
7. Regenerate catalogs and run validation after each change.

Recommended command:

```powershell
rg --no-ignore "Scavenger Boss|AspectPatrollingNPCBoss|UnitRaidParameters" .\game-gdb\
```

If `rg` is not installed on Windows:

```powershell
winget install BurntSushi.ripgrep.MSVC
```

## Known Limits

The current catalog pass extracts component presence, scalar values, references, and useful filters. It does not yet model final combat formulas, pathfinding, animation timing, engine-side AI behavior, or all encounter state transitions.

For deeper balancing work, compare:

- `UnitEncounterParameters`
- `UnitRaidParameters`
- `AspectEncounterNPCUnit`
- `AspectEncounterInitiator`
- `AspectPatrollingNPCBoss`
- `BuildingEncounterParameters`
- `AspectEncounterNPCBase`
- `EncounterTargetTag`
- `Faction` and `SubFaction`

Those components together describe much more than any single row.
