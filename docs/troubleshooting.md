# 🔧 Troubleshooting

This page collects common problems when working with extracted XML database files and local generated catalogs.

## The Game Crashes Or Shows A Black Screen

Possible causes:

- XML is not well-formed.
- A required GUID reference points to a missing entity.
- A component was removed but another system expects it.
- An asset path points to a missing or incompatible file.
- A map objective or trigger chain is broken.

First checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Then inspect the most recent XML file you changed.

## XML Parse Errors

Common causes:

- missing closing tag
- invalid nesting
- unescaped `&`
- accidental deletion of `<Item>` or `<Content>` wrappers
- broken copy/paste around large lists

Fix XML parse errors before doing any deeper analysis. Nothing else is reliable until XML parsing succeeds.

## Duplicate GUIDs

Duplicate GUIDs are high risk. They can cause references to resolve unpredictably or overwrite identity assumptions.

If you copy an entity to create a new one:

- assign a new GUID to the copied entity
- keep references to existing dependencies unchanged unless you intentionally want to redirect them
- search for the old and new GUIDs

Validation checks duplicate entity GUIDs.

## Unresolved GUID References

The validation script reports unresolved non-null GUIDs. The current database has a small known number of them.

Interpretation:

- `00000000-0000-0000-0000-000000000000` is treated as a null/default reference.
- unresolved non-null GUIDs deserve inspection.
- some unresolved references may still be valid external or generated references.

Strict check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1 -TreatUnresolvedAsError
```

## Production Or Recruitment Validation Errors

The validation script also checks several modding-sensitive reference types:

- building `AspectProduction` recipe links should resolve to `ProductionRecipe` entities
- recipe input/output resources should resolve to resource entities
- building construction costs should resolve to resource entities
- employment units should resolve to unit-like entities
- recruitment costs should resolve to resource entities
- source recruitable units should resolve to unit-like entities

If one of these fails after a mod edit, inspect the GUID you changed first. The most common causes are:

- copied the wrong GUID
- replaced a resource GUID with a unit/building/recipe GUID
- deleted or renamed an entity instead of only changing a value
- copied an entity but did not preserve its references correctly

Use:

```powershell
rg --no-ignore "GUID-HERE" .\game-gdb
```

Then confirm the target entity has the expected component, such as `ResourceDescription`, `ProductionRecipe`, or unit-related components.

The script may also warn that some production recipes have no non-null input/output resources. This can be valid for templates, animation-only recipes, map-specific recipes, or unfinished/unused data. Treat it as a review prompt, not automatically as a broken mod.

## Generated Catalog Is Missing Data

The generated catalog only extracts known catalog types:

- buildings
- building-production links
- recipes
- units
- production graphs

If a package has no results in the catalog, it may still contain useful data. For example, `tools` currently contains editor/terrain data rather than buildings or recipes.

Regenerate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## A Recipe Shows No Amounts

Some production recipes reference input/output resources but do not include explicit amounts in the recipe step. This is normal for parts of the current database.

Do not assume an empty amount means the recipe is broken. Inspect similar recipes and test in game.

## A Building Has No Builder Or Worker

Some entities may omit builder or worker data, inherit it, use a template, or be special-purpose/map-specific. Check:

- inherited/template entity
- package references
- map-specific variants
- generated reference links

Useful search:

```powershell
rg --no-ignore "BUILDING-GUID-HERE" .\game-gdb
```

## Changes Do Not Appear In Game

Possible causes:

- the game is loading a different package/file than the one you edited
- a DLC or map-specific entity overrides the behavior you expected
- you patched a DLC entity (e.g. in `dlc1`) but the install doesn't **own** that expansion — the engine gates DLC content at runtime, so the change stays inactive in single-player even though the bytes were written. The game ships every pak to everyone, so "the pak is present" doesn't mean "the content is active for you". See [Present vs Owned vs Effective](package-layering.md#present-vs-owned-vs-effective); if you deploy with `pagonia-manager`, it surfaces this directly (`pagonia-manager expansions list` and the [plan/deploy ownership gate](../tools/pagonia-manager/CLI.md#expansions-ownership)).
- cached data or old files are still present
- you edited display data but the visible text comes from localization
- the entity is abstract or only used as a template

Check whether the entity is abstract and whether other entities inherit from it.

## Safe Recovery

Keep extracted originals untouched or easy to restore. The safest workflow is:

1. Keep the original `.pak` extraction outside your mod work folder.
2. Copy XMLs into `game-gdb/` for analysis.
3. Make mod experiments in a separate working copy.
4. Validate after each small change.
5. Test on a new save unless you are sure the change is save-safe.

## Useful Commands

Validate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Regenerate indexes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Regenerate catalogs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Find text:

```powershell
rg --no-ignore "SearchText" .\game-gdb
```

Find a GUID:

```powershell
rg --no-ignore "GUID-HERE" .\game-gdb
```
