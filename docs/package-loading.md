# 📦 Package Loading And Overlays

The extracted database is split into packages that appear to be loaded together into one effective game database.

For the focused patch/override interpretation of `core`, `dlc1`, `decorations1`, and `tools`, see [DLC Patch And Override Model](dlc-patch-override-model.md).

Current local package folders:

```text
game-gdb/core/gdb/
game-gdb/decorations1/gdb/
game-gdb/dlc1/gdb/
game-gdb/tools/gdb/
```

The package folders mirror the `.pak` files:

| Pak file | Local folder |
| --- | --- |
| `core.pak` | `game-gdb/core/gdb/` |
| `decorations1.pak` | `game-gdb/decorations1/gdb/` |
| `dlc1.pak` | `game-gdb/dlc1/gdb/` |
| `tools.pak` | `game-gdb/tools/gdb/` |

The current known extraction exception is `dlc1.pak`, where `meadowsong map database.gd.xml` comes from the `maps` folder instead of `gdb`.

## Additive Package Model

The current XML set suggests an additive model:

- `core` provides the base game database.
- `decorations1` adds decorative buildables.
- `dlc1` adds and extends DLC content.
- `tools` adds editor/Magmaview data.

Packages can reference entities from other packages by GUID. For example, DLC entities can reference core resources, workers, buildings, notifications, or abstract templates.

This is why the database should be analyzed as one combined set instead of four isolated folders.

## Overlay-Like Behavior

The packages do not only add unrelated new entities. Some DLC entities inherit from or reference core entities. This creates overlay-like behavior:

- new DLC entities can reuse core templates
- DLC objectives can inherit from core objective templates
- DLC buildings can use core workers or resources
- DLC maps can reference DLC and core data
- decoration packages can add buildables that still use core categories or systems

In the XML, these relationships usually appear as GUID references or inheritance fields such as:

```text
InheritedGuid
InheritanceMode
```

Do not assume package boundaries are gameplay boundaries.

## Modding Implications

When editing package data:

- Changing a core entity can affect core, DLC, decorations, and map content.
- Changing a DLC entity may still affect core systems if it references core templates or categories.
- Removing a GUID target can break references from another package.
- Package-specific testing matters: test with the same DLC set enabled that your mod targets.

Safer package-level edits:

- add a new entity copied from an entity in the same package
- tune values on a leaf entity that is not widely referenced
- adjust package-local display/order fields

Riskier package-level edits:

- change core template entities
- remove entities referenced by DLC
- change inherited GUIDs
- change package-crossing references without tracing them

## How To Trace Package References

Use the generated indexes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Then inspect:

```text
generated/references.json
generated/entities.json
```

Use the generated catalog for package summaries:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/filters/package-summary.md
generated/catalog/filters/packages/core/
generated/catalog/filters/packages/dlc1/
generated/catalog/filters/packages/decorations1/
generated/catalog/filters/packages/tools/
```

## Practical Rule

For modding, think in two layers:

1. **Physical source package:** where the XML entity is stored.
2. **Effective database:** the combined loaded database after all enabled packages are included.

Most references care about the effective database. Your file organization cares about the physical source package.

This distinction is especially important for DLC and future game updates.
