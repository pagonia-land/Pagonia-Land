# Create A Decoration Variant

This example describes the safe planning process for creating a new decorative buildable based on an existing one.

It is more advanced than changing a cost because it requires copying an entity and assigning a new GUID.

## Goal

Create a new decorative building variant by copying an existing decoration entity.

This page explains the workflow and safety checks. It does not prescribe one exact final decoration because visual assets and build menu behavior should be tested locally.

## Step 1: Pick A Similar Decoration

Decoration data appears in:

```text
game-gdb/core/gdb/decorations.gd.xml
game-gdb/decorations1/gdb/decorations.gd.xml
```

Use:

```powershell
rg --no-ignore "Entity Name=|Buildable|Building|UiBuildingGroup" .\game-gdb\core\gdb\decorations.gd.xml .\game-gdb\decorations1\gdb\decorations.gd.xml
```

Pick an existing decoration that is close to what you want.

## Step 2: Copy The Whole Entity

Copy the complete entity, not only the `Building` component.

Keep the component structure intact:

- `Building`
- `Buildable`
- `AspectBuildup`
- placement components
- visual/audio components
- child structure, if present

## Step 3: Assign A New GUID

The copied entity must get a new unique GUID.

In PowerShell:

```powershell
[guid]::NewGuid()
```

Replace only the copied entity's own `Guid` attribute first.

Do not change referenced GUIDs unless you intentionally want to point to different resources, categories, notifications, or assets.

## Step 4: Change Safe Display Fields

Good first changes:

- internal `Name` attribute
- `Building/Name`
- `Building/Description`
- `Buildable/SortOrder`
- construction cost amount

Be careful with:

- `Mesh`
- `Icon`
- terrain blocking
- `GridSize`
- category GUIDs

## Step 5: Validate References

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Then search the new GUID:

```powershell
rg --no-ignore "NEW-GUID-HERE" .\game-gdb
```

For a new decoration entity, the new GUID may appear only once unless you also added menu/unlock/objective references.

## Step 6: Check Build Menu Behavior

If the decoration does not appear, inspect:

- `Buildable`
- category references
- sort order
- unlock-related fields
- package loading
- whether the copied entity is abstract or template-only

Compare against the original decoration until you find the difference.

## Risk Summary

| Edit | Risk |
| --- | --- |
| Copy existing decoration and assign new GUID | Medium |
| Change display text keys | Low to medium |
| Change construction cost amount | Low to medium |
| Change category | Medium |
| Change mesh/icon paths | High |
| Change placement/grid/terrain blocking | High |

This is a good example to attempt only after simple cost edits are working.
