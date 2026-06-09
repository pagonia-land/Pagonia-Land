# 📖 Glossary

Short explanations for common terms used in this repository.

## Arithmetic Operation

A patch operation that computes a new value *relative* to the existing one rather than replacing it with a literal: `multiplyValue` (vanilla × factor) and `addValue` (vanilla + delta). The factor/delta can come from a [tweak](#tweak), so one shared multiplier scales many targets. See the [Patch Format → Arithmetic operations](mod-patch-format.md#arithmetic-operations-multiplyvalue-addvalue).

## Aspect

A gameplay component attached to an entity.

Examples:

- `AspectBuildup`
- `AspectProduction`
- `AspectStorage`
- `AspectWorker`

Most complex entities are built from several aspects/components.

## Buildable

The component that controls whether and how a building appears in construction UI. It can contain category, sort order, UI grouping, and buildability fields.

## Catalog

A generated local set of Markdown and CSV files under:

```text
generated/catalog/
```

It summarizes buildings, recipes, units, production links, costs, and graphs. It is derived from local game data and is ignored by Git.

## Component

A child element under an entity's `Values` block.

Example:

```xml
<Values>
  <Building>...</Building>
  <Buildable>...</Buildable>
  <AspectBuildup>...</AspectBuildup>
</Values>
```

## Effective Database

The combined database after the enabled packages are loaded together.

Even if an entity lives in `dlc1`, it can reference entities from `core`.

## Entity

The main unit of database data. An entity has a name, a GUID, optional children, and components under `Values`.

## EntityGroup

A grouping structure in XML files. Most XML files use `EntityGroup` as the root element and may contain nested groups.

## Generated Files

Local files created by scripts, such as:

```text
generated/entities.json
generated/references.json
generated/analysis-summary.json
generated/catalog/
```

These files are ignored by Git because they are derived from extracted game data.

## GUID

A globally unique identifier used to connect entities.

Example:

```text
00000000-0000-0000-0000-000000000000
```

Do not change existing GUIDs during normal tuning.

## Inheritance

A relationship where one entity derives behavior or structure from another entity. Editing an inherited-from entity propagates to every entity that inherits from it.

Two complementary mechanisms ship with the engine:

- **Authoring-time inheritance.** `IsAbstract="true"` plus nested `<Children>` form templates and category bases. This has always been part of the format.
- **Cross-pak entity merging (Meadowsong-era).** `InheritanceMode="Template" | "Replace" | "Incremental"` paired with `InheritedGuid="<target-guid>"` lets one pak modify entities from another pak at load time, without byte-patching the shipped XML. `<InheritedIndex>N</InheritedIndex>` markers inside `Incremental` blocks indicate where to splice into a target's list. See [Mod Distribution Patterns → Cross-Pak Entity Merging](mod-distribution.md#cross-pak-entity-merging-meadowsong) for the modder-facing semantics.

## Null GUID

The all-zero GUID:

```text
00000000-0000-0000-0000-000000000000
```

Usually means none, empty, default, or not set. It is not treated as a broken reference.

## Package

A local folder matching a `.pak` source.

Examples:

- `game-gdb/core/gdb/`
- `game-gdb/decorations1/gdb/`
- `game-gdb/dlc1/gdb/`
- `game-gdb/tools/gdb/`

## Production Recipe

An entity with a `ProductionRecipe` component. It describes recipe steps such as input, output, wait, and work behavior.

Recipes are usually connected to buildings through `AspectProduction`.

## Reference

A GUID value inside one entity that points to another entity.

Examples:

- resource reference
- recipe reference
- unit reference
- category reference
- notification reference

## ResourceDescription

The component that marks an entity as a resource-like item. Resources are used by construction costs, recipes, recruitment costs, storage, objectives, and UI.

## Source Package

The package folder where an entity's XML definition is physically stored.

This is different from the effective database, where all packages can reference each other.

## Tweak

A value a mod author marks as user-adjustable (a `tweaks:` entry in `mod.yaml`), so a player picks it in the mod manager without editing the mod. Has a type (`number` / `integer` / `boolean` / `enum`), a default, and optional bounds; patch operations read it with a `{{ tweaks.<id> }}` placeholder.

See the [Mod Tweaks guide](mod-tweaks.md).

## Validation Baseline

The currently known validation status documented in:

```text
VALIDATION_BASELINE.md
```

Use it to decide whether a warning is known or new.

## Values

The XML block that contains an entity's components.

Example:

```xml
<Values>
  ...
</Values>
```

If you want to understand what an entity does, inspect its `Values` children first.
