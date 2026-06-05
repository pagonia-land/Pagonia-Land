# 👷 Units

Units describe workers, carriers, guards, specialists, animals, NPCs, and many encounter actors. They are one of the main connection points between buildings, resources, production, recruitment, combat, and objectives.

Most base game units are defined in:

```text
game-gdb/core/gdb/units.gd.xml
```

DLC units are defined in:

```text
game-gdb/dlc1/gdb/units.gd.xml
```

NPC-specific units also appear in files such as:

```text
game-gdb/core/gdb/npcunits.gd.xml
game-gdb/dlc1/gdb/npcunits.gd.xml
```

## Current Catalog Snapshot

The current generated local catalog reports:

| Package | Units |
| --- | ---: |
| `core` | 140 |
| `dlc1` | 37 |
| `decorations1` | 0 |
| `tools` | 0 |

There are currently 51 units with explicit recruitment costs.

Regenerate these numbers with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/units.md
generated/catalog/unit-equipment-matrix.md
generated/catalog/filters/production/units-with-recruitment-cost.md
```

## Units Are Component Compositions

A unit entity is usually made of several components rather than one isolated unit block.

Common unit-related components:

| Component | Purpose |
| --- | --- |
| `Unit` | Base unit identity, display, movement, visuals, interaction, sounds |
| `TaggedUnit` | Unit tags used by logic, recruitment, combat, objectives, or UI |
| `RecruitmentCost` | Resources or tools needed to recruit or convert the unit |
| `UnitAnimations` | Animation set and action animation names |
| `UnitEncounterParameters` | Combat and encounter behavior |
| `AspectWorker` | Worker behavior and job eligibility |
| `AspectHomeResident` | Home/residence behavior |
| `AspectTransporter` | Carrier/transport behavior |
| `TerritoryDefender` | Defensive behavior for combat units |
| `AspectEncounterInitiator` | Ability to initiate encounters |
| `UiUnitTraitsInfo` | UI-facing trait information |
| `AchievementUnit` | Achievement tracking hooks |

The exact component list depends on the unit type.

## Recruitment Costs

`RecruitmentCost` is one of the safest unit entry points for early experiments.

Common fields:

| Field | Meaning |
| --- | --- |
| `ResourceCosts` | List of required resources/tools/equipment |
| `NeedsManualRecruitment` | Whether the unit appears to require manual recruitment |
| `SourceRecruitableUnit` | Optional source unit for conversions or upgrade paths |

Typical examples include workers requiring tools and combat units requiring weapons or armor.

Safer edits:

- change a resource amount
- replace one required tool with another existing tool
- compare before/after behavior in a new test save

Riskier edits:

- remove all costs from a unit that is normally manually recruited
- point `SourceRecruitableUnit` at an unrelated unit
- use a resource that is not available through any production chain

## Unit Equipment Matrix

For quick lookup, regenerate the local catalog and open:

```text
generated/catalog/unit-equipment-matrix.md
generated/catalog/unit-equipment-matrix.csv
```

This matrix gives one row per unit equipment requirement. It connects:

- unit and recruitment cost
- equipment resource, amount, category, and carry type
- known producer buildings
- known producer recipes
- recipe inputs and outputs
- sample production chains ending in the unit requirement

Useful filtered views:

```text
generated/catalog/filters/production/unit-equipment-with-producers.md
generated/catalog/filters/production/unit-equipment-without-producers.md
```

Rows without producer recipes are worth manual review, but they are not automatically errors. Some resources may be scenario-provided, placeholder, abstract, or produced through systems not yet covered by the catalog generator.

## Worker Dependencies

Buildings often reference units through employment fields, especially in:

```text
AspectBuildup/Employment/Unit
AspectProduction/Employment/Unit
```

This means a unit can be required by a building even if the unit itself does not point back to that building.

Good tracing direction:

1. Find the unit GUID in `generated/entities.json`.
2. Search for that GUID in `generated/references.json` or with `rg`.
3. Check whether buildings use it as a builder, worker, gatherer, transporter, or special role.

Useful command:

```powershell
rg --no-ignore "UNIT-GUID-HERE" .\game-gdb
```

## Tags

`TaggedUnit` connects units to broader logic. Tags may be used by:

- employment matching
- combat categories
- objective checks
- UI filters
- encounter behavior
- DLC-specific mechanics

Do not treat tags as cosmetic. A harmless-looking tag change can make a unit invisible to a system that expects it.

Safer approach:

1. Add or remove tags only in a copy of an existing similar unit.
2. Search for every tag GUID before editing.
3. Test recruitment, employment, combat, and objective behavior.

## Combat Fields

Combat-capable units often include fields such as:

- `CombatStrength`
- `AttackHit`
- `AttackMiss`
- `CombatIdle`
- combat animation names
- encounter parameters
- defender or initiator components

Combat tuning is higher risk than recruitment cost tuning. It can affect AI encounters, mission pacing, balance, and save behavior.

Safer edits:

- small numeric changes to known combat values
- animation changes only to known compatible animation names

Riskier edits:

- removing combat components
- mixing civilian worker components with combat-only behavior
- changing encounter initiator behavior without checking NPC bases and objectives

## Suggested Unit Modding Workflow

1. Pick a simple worker unit first.
2. Find it in `generated/catalog/units.md`.
3. Locate the source entity in `game-gdb/core/gdb/units.gd.xml` or `game-gdb/dlc1/gdb/units.gd.xml`.
4. Change one recruitment cost or one clearly understood numeric field.
5. Run validation.
6. Start a new test save and confirm the unit can still be recruited and assigned.

Validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## Risk Summary

| Edit | Risk |
| --- | --- |
| Change recruitment cost amount | Low to medium |
| Replace one cost resource with a known existing resource | Medium |
| Change `NeedsManualRecruitment` | Medium |
| Change tags | Medium to high |
| Change combat strength | Medium |
| Remove worker/combat components | High |
| Change unit GUIDs | High |
| Change animation names to unknown values | High |

For first experiments, prefer recruitment costs and small numeric tuning. Leave GUIDs, tags, component structure, and animation sets alone until you have a clean test process.
