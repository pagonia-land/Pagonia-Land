# ✅ Safe Edits

This page helps new modders choose first experiments that are less likely to break the database.

No edit is guaranteed safe. Always validate and test.

For a broader explanation of distributed balance, rare components, objectives, tech tree data, and visual attachments, see [Modding Risk Map](modding-risk-map.md).

## Best First Edits

These are good learning targets because they usually preserve entity identity and component structure.

| Edit | Risk | Why |
| --- | --- | --- |
| Change a construction cost amount | Low to medium | Keeps the same building and resource GUID |
| Change a unit recruitment cost amount | Low to medium | Keeps the same unit and resource GUID |
| Change build menu sort order | Low to medium | Affects UI ordering without changing dependencies |
| Trace a resource, recipe, or building without editing | Low | Builds understanding before changing anything |
| Compare two similar entities | Low | Helps reveal local patterns |

Good examples:

- [Change A Building Construction Cost](examples/change-building-cost.md)
- [Change A Unit Recruitment Cost](examples/change-unit-recruitment-cost.md)
- [Trace The Sawmill Production Chain](examples/trace-sawmill-chain.md)

## Medium-Risk Edits

These can work, but require more tracing.

| Edit | Risk | What To Check |
| --- | --- | --- |
| Replace one cost resource with another existing resource | Medium | Target must be a valid resource and available in game |
| Change recipe input/output resource | Medium | Production chain, storage, UI, and availability |
| Change worker count | Medium | Building employment and unit availability |
| Change production timing hints | Medium | Building `AspectProduction/Efficiency` and in-game pacing |
| Change unit combat strength | Medium | Encounter balance and objective behavior |
| Add a copied decoration variant | Medium | New GUID, build menu, placement, assets |

Useful validation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## High-Risk Edits

Avoid these until you can trace references confidently.

| Edit | Risk | Why |
| --- | --- | --- |
| Change existing GUIDs | High | Breaks references across the database |
| Delete entities | High | Other packages or maps may reference them |
| Remove components | High | Systems may expect the component to exist |
| Change inheritance/template links | High | Can affect many derived entities |
| `Replace` or `Unload` a core/dlc entity in a shipped overlay | High | Destructive + last-loaded-wins across mods; prefer additive `Incremental`/`Template`. Run `pagonia-patcher validate-mod` — its [authoring advisor](mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor) flags this |
| Change terrain blocking or grid size | High | Can break placement and saves |
| Change objective triggers or rewards | High | Can block campaign/map progression |
| Replace mesh, icon, texture, or audio paths | High | Asset may be missing or incompatible |
| Change tags without tracing them | High | Tags are often used by systems, objectives, and UI |

## A Safe Editing Routine

Use this every time:

1. Pick one target entity.
2. Search every GUID you plan to touch.
3. Change one value only.
4. Run validation.
5. Regenerate the catalog if relevant.
6. Test in a new save or test map.
7. Write down old value, new value, file, and result.

## What Validation Can Catch

The validation script catches many reference mistakes:

- duplicate entity GUIDs
- unresolved non-null GUID references
- building production recipe links pointing to non-recipes
- recipe resources pointing to non-resources
- construction costs pointing to non-resources
- employment fields pointing to non-unit entities
- recruitment costs pointing to non-resources

It cannot prove that a mod is fun, balanced, save-safe, or visually correct.

## Good First Mod Ideas

| Idea | Difficulty |
| --- | --- |
| Make a single building slightly cheaper | Beginner |
| Make one worker require one extra tool | Beginner |
| Move a building within a build menu group | Beginner |
| Trace Bakery -> BreadRecipe -> resources | Beginner |
| Make one production building slightly faster | Intermediate |
| Add a copied decoration variant | Intermediate |
| Change a combat unit cost | Intermediate |
| Change campaign objective requirements | Advanced |

## Stop And Reconsider If

- validation errors appear
- a GUID target is not the entity type you expected
- the entity is abstract or inherited
- the change affects campaign/map files
- the change touches DLC and core at the same time
- you need to edit more than one system to make it work

That does not mean the mod is impossible. It means it is no longer a first experiment.
