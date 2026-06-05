# ⚔️ Combat Boosts

Combat boosts appear in several places: resource artifacts, unit tags, difficulty settings, objectives, and campaign-specific reward logic. The catalog currently focuses on `CombatBoostArtifact` resources because they are concrete loot items that can be found through treasure hunting or map logic.

## Main Files

| File | Role |
| --- | --- |
| `game-gdb/core/gdb/resources.gd.xml` | Combat-boost artifact resources |
| `game-gdb/core/gdb/treasurehunterrecipes.gd.xml` | Treasure hunter categories that can include combat-boost artifacts |
| `game-gdb/core/gdb/campaign map *.gd.xml` | Map-specific combat boost artifacts and objectives |
| `game-gdb/core/gdb/objectives.gd.xml` | Objective types that can check combat boost totals |
| `game-gdb/dlc1/gdb/*.gd.xml` | DLC difficulty and objective data that can reference combat boost tags |

## Combat-Boost Artifact Resources

Combat-boost artifacts are resources with an additional `CombatBoostArtifact` component.

Important fields:

| Field | Meaning |
| --- | --- |
| `Boost` | Numeric combat boost value. |
| `Weight` | Likely reward weighting/selection value. |
| `ResourceDescription` | Normal resource presentation, carry, icon, mesh, and storage behavior. |

Generated catalog:

- `generated/catalog/systems/artifacts.md`

Filter rows where `CombatBoost` is not empty to inspect combat-boost artifacts.

## Combat-Boost Tags

Some entities use `CombatBoostArtifactTag`. These references appear in campaign, unit, and difficulty-related data. They are useful for tracing how an artifact reward becomes relevant to combat or objective logic.

The current catalog lists these indirectly through entity components and references. A future validation pass should add a dedicated combat-boost-tag reference check.

## Modding Notes

Safer first edits:

- compare boost and weight values
- trace where a combat-boost artifact is referenced before editing it
- test changes in a throwaway save

Riskier edits:

- change boost values used by campaign objectives
- delete combat-boost resources
- change tags without checking difficulty settings and unit/objective references
- rebalance final campaign rewards without testing enemy strength and objective thresholds

## Suggested Trace

```text
CombatBoostArtifact resource
-> treasure hunter target or map reward
-> combat boost tag / objective requirement
-> scenario difficulty or combat outcome
```

