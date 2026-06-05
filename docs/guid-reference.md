# 🔑 GUID Reference

GUIDs are the backbone of the game database. If you understand how GUIDs work, the XML files become much less intimidating.

## What Is A GUID?

A GUID is a stable identifier that looks like this:

```text
f01e0070-4661-4c00-8b74-e240f8bd5c3f
```

Every entity has a `Guid` attribute:

```xml
<Entity Name="ExampleEntity" Guid="...">
```

Other XML fields then point to that entity by storing the GUID as text.

## Names Are Not The Main Link

Entity names are useful for humans:

```xml
<Entity Name="FirewoodFromSoftwoodRecipe" Guid="...">
```

But most relationships use the GUID, not the name.

That means changing a display-like name may be safe in some contexts, while changing a GUID is usually dangerous.

## The Golden Rule

Do not change existing GUIDs unless you know exactly what references them.

Changing a GUID is effectively changing the identity of an entity. Any other entity that points to the old GUID may stop working.

## Common GUID Reference Fields

Common fields that often contain GUID references:

| Field | Usually points to |
| --- | --- |
| `Resource` | Resource entities |
| `Unit` | Unit entities |
| `Building` | Building entities |
| `Recipe` | Production or shrine recipe entities |
| `Category` | Building/resource/UI categories |
| `Tag` | Tag entities used for grouping/filtering |
| `Notification` | Notification entities |
| `DepositType` | Deposit entities |
| `UnitAttachments` | Attachment sets or attachment entities |

The same field name can sometimes appear in different systems, so always verify the resolved target.

## Null GUIDs

This value appears often:

```text
00000000-0000-0000-0000-000000000000
```

Treat it as an explicit empty/default value. It is not automatically a broken reference.

## How To Trace A GUID

Use ripgrep:

```powershell
rg --no-ignore "f01e0070-4661-4c00-8b74-e240f8bd5c3f" .\game-gdb
```

This shows:

- where the entity is defined
- which files reference it
- whether references appear in recipes, buildings, objectives, UI, or campaign data

You can also use the local generated index:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Then inspect:

- `generated/entities.json`
- `generated/references.json`

## Safe GUID Practices

Safer:

- keep existing GUIDs unchanged
- duplicate an entity only if you assign a new GUID
- update every reference deliberately if you replace one entity with another
- use local generated indexes to check references before deleting anything

Risky:

- changing a GUID in place
- copying an entity without changing its GUID
- deleting an entity that still has references
- replacing a resource GUID with a unit/building/tag GUID
- editing map objective chains without checking every referenced notification and reward

## Adding New Entities

If you add a new entity, it needs a unique GUID.

In PowerShell:

```powershell
[guid]::NewGuid()
```

Before using the new GUID, make sure it does not already exist:

```powershell
rg --no-ignore "new-guid-here" .\game-gdb
```

Duplicate GUIDs should be considered a hard error.

## When A Reference Is Unresolved

An unresolved non-null GUID means the GUID does not point to any entity in the extracted XML set.

Possible explanations:

- the reference points to external or generated data
- an XML file is missing from `game-gdb/`
- a package was not extracted completely
- a mod accidentally changed or removed a target entity

Use `scripts\validate_database.ps1` after edits to catch this early.
