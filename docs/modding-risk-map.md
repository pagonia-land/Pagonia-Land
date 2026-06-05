# ⚠️ Modding Risk Map

This page turns the current database analysis into practical modding judgement.

The XML database is flexible because it is an entity-component graph. That also means many relationships are implicit — the game trusts that the database is coherent and rarely revalidates at runtime.

Use this page as a first risk check before deciding what to edit. For a beginner-friendly counterpart that recommends specific first experiments, see [Safe Edits](safe-edits.md).

## Good First Targets

These edits usually keep identity, references, and component shape intact.

| Target | Typical Risk | Why |
| --- | --- | --- |
| Construction cost amount | Low to medium | Same building, same resource, same component |
| Recruitment cost amount | Low to medium | Same unit, same resource, same component |
| Sort order | Low to medium | Mostly affects UI ordering |
| Display/localization key reuse | Low to medium | Does not change GUID relationships |
| Read-only tracing | Low | Builds understanding before changing data |

Recommended examples:

- [Change A Building Construction Cost](examples/change-building-cost.md)
- [Change A Unit Recruitment Cost](examples/change-unit-recruitment-cost.md)
- [Trace The Sawmill Production Chain](examples/trace-sawmill-chain.md)

## Distributed Balance

A single gameplay result can be spread across several files and components.

Example production balance can involve:

| Question | Likely Data |
| --- | --- |
| What building does the work? | `Building`, `AspectProduction`, `AspectGatherer` |
| Who works there? | `AspectProduction/Employment`, `AspectGatherer/Employment`, `AspectBuildup/Employment` |
| What recipe runs? | `AspectProduction/Recipes` |
| What resources go in or out? | `ProductionRecipe` steps |
| How fast does it feel? | Recipe steps, building efficiency hints, worker behavior |
| Is it available yet? | `NeedsUnlock`, objectives, tech tree groups, start rewards |
| What does the player see? | Localization keys, notifications, icons, UI state overrides |

This is why a small edit can have a large effect.

## Medium-Risk Targets

These can be useful, but they require tracing before editing.

| Target | What To Trace |
| --- | --- |
| Recipe input/output resource | Producers, consumers, storage, construction costs, recruitment costs |
| Worker unit on a building | Whether the unit exists, can be recruited, and fits the building role |
| Unit attachment | Default attachment, active attachment set, mesh, locator, animation context |
| Objective amount | Objective category, prerequisites, rewards, notifications, map flow |
| Build menu availability | `NeedsUnlock`, tech tree group, objective rewards, package overlays |
| Deposit/resource placement | Deposit type, resource, terrain deposit, mapgen rules, gatherer compatibility |

Useful commands:

```powershell
rg --no-ignore "GUID-OR-NAME-HERE" .\game-gdb
rg --no-ignore "GUID-OR-NAME-HERE" .\generated\references.json
```

## High-Risk Targets

Avoid these until you can trace the whole chain.

| Target | Why It Is Risky |
| --- | --- |
| Existing GUIDs | Other entities and packages may reference them |
| Entity deletion | References may survive in other XML files |
| Component removal | Engine systems may expect the component shape |
| Objective triggers and rewards | Can block campaign or map progression |
| Tech tree unlock conditions | Can unlock content too early or never |
| Terrain blocking and placement sizes | Can break placement, pathing, or saves |
| NPC encounters, bosses, raids | Often tied to objectives, maps, rewards, and combat balance |
| Internal-looking or rare components | May be engine-only, tool-only, or map-specific |

## Rare Component Rule

Some components appear only in very specific files or contexts. That does not make them bad, but it makes them harder to reason about.

Before editing a rare component:

1. Count where it appears.
2. Check which files use it.
3. Search whether anything references the entity.
4. Compare at least two similar entities.
5. Test in a new save or test map.

Do not assume that a component works everywhere just because it appears once.

For the current coverage audit, see [Catalog Coverage](catalog-coverage.md).

## Notes From Earlier Analyses

Older analyses of earlier XML dumps are useful for themes, but not for current facts.

Examples of useful themes:

- the database is an entity-component graph
- GUID relationships matter more than file order
- objectives are more than quests
- tech tree behavior is objective-like and condition-driven
- unit attachments are a good example of data that is not pure economy but still affects presentation and player understanding
- validation tooling is important because many constraints are implicit

Examples that must be checked against the current XMLs before documenting:

- exact entity counts
- exact reference counts
- rare component names
- claims about internal-only components
- claims about engine behavior that is not visible in XML

In the current local XML set, component names such as `AspectInternalOnly`, `AspectAutoPlacement`, `AspectInitialUnlock`, and `AspectNotificationOverride` were not found. They should not be treated as current facts for this repository unless a later game version introduces them.
