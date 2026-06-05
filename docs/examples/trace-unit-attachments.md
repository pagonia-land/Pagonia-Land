# Trace Unit Attachments

This example is read-only. It shows how to trace visual/tool attachments for a unit.

Unit attachments are not the same thing as recruitment equipment. Recruitment equipment is economy-facing. Unit attachments are usually visual or animation-facing data.

The current generated unit equipment matrix helps with recruitment resources. It does not yet deeply catalog unit visual attachments, so this example uses direct XML and reference searches.

## Goal

Trace the Woodcutter's axe attachment:

```text
Woodcutter -> UnitAnimations/DefaultAttachments -> ToolAxe001
ToolAxe001 -> child ToolAxe001InUse
WoodcutterAxeAttachmentSet -> ToolAxe001InUse
```

## Step 1: Find The Unit

```powershell
rg --no-ignore "Woodcutter" .\game-gdb\core\gdb\units.gd.xml
```

The current XML contains:

```text
Entity Name="Woodcutter" Guid="57b9b120-96d3-479d-a89a-c186e6ba4d49"
```

Inside `UnitAnimations`, the Woodcutter references a default attachment:

```text
Attachment dc960fb3-92d2-4718-afc9-5d4b21430448
```

## Step 2: Resolve The Attachment GUID

Search the GUID:

```powershell
rg --no-ignore "dc960fb3-92d2-4718-afc9-5d4b21430448" .\game-gdb
```

The current XML resolves it to:

```text
ToolAxe001
```

Source file:

```text
game-gdb/core/gdb/unitattachments.gd.xml
```

`ToolAxe001` contains a `UnitAttachment` component with mesh, location, offsets, and an attachment locator.

## Step 3: Follow The In-Use Attachment

`ToolAxe001` has a child entity:

```text
ToolAxe001InUse
7347393a-86cb-4a66-a0d3-70db11554b6d
```

Search it:

```powershell
rg --no-ignore "7347393a-86cb-4a66-a0d3-70db11554b6d" .\game-gdb
```

The current XML shows it inside:

```text
WoodcutterAxeAttachmentSet
```

That attachment set uses:

```text
UnitActiveAttachmentSet
HideDefaultAttachmentsAt Spine
```

This suggests a common pattern:

| State | Attachment |
| --- | --- |
| Default/resting | `ToolAxe001` on the spine |
| Active/in-use | `ToolAxe001InUse` in the hand |

## Step 4: Check The Reference Index

Regenerate references:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Then search:

```powershell
rg --no-ignore "ToolAxe001|ToolAxe001InUse|WoodcutterAxeAttachmentSet" .\generated\references.json
```

This helps answer:

- which unit references the default attachment
- which active attachment set references the in-use attachment
- whether the same attachment is reused elsewhere

## What To Learn

Unit attachment edits are not first edits.

Before changing one, check:

- the unit using it
- the active attachment set
- the mesh path
- the attachment locator
- default versus in-use state
- whether the attachment is reused by another unit, animation, or map-specific entity

## Modding Notes

Safer experiments:

- trace attachments without editing
- compare two similar tools, such as `ToolAxe001` and `ToolPickaxe001`
- change nothing until you know which state is default and which state is active

Riskier experiments:

- replacing mesh paths
- changing attachment locations
- deleting child in-use attachments
- reusing an active attachment without its matching default attachment

If a visual attachment is wrong, the database may still validate. Visual correctness usually requires in-game testing.
