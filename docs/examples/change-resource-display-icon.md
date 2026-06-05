# Change A Resource Display Name Or Icon

This example shows how to make a small UI-facing resource edit while leaving gameplay relationships untouched.

It is a good first edit because it teaches the difference between display data and identity data.

## Goal

Change a resource display key or icon path for local testing.

Do not change the resource GUID, category, carry type, tags, or producer/consumer references in this example.

## Step 1: Pick A Resource

Use the generated catalog:

```text
generated/catalog/resources.md
generated/catalog/filters/packages/dlc1/resources.md
generated/catalog/filters/assets/icons.md
```

For example, the current DLC resource `Apples` has:

```text
Name: Apples
NamePlural: Apples
Description: Apples Description
Icon: dlc1/gui/icons/commodities/icon_com_apples_001.png
```

## Step 2: Find The Resource XML

Search by name:

```powershell
rg --no-ignore "Apples|icon_com_apples_001|Apples Description" .\game-gdb\dlc1\gdb\resources.gd.xml
```

Inside the resource entity, look for:

```text
ResourceDescription
Name
NamePlural
Description
Icon
```

## Step 3: Change Only Display Fields

Example style of change:

```xml
<Icon>dlc1/gui/icons/commodities/icon_com_apples_001.png</Icon>
```

to another known icon path that already exists in the database:

```xml
<Icon>dlc1/gui/icons/commodities/icon_com_grapes_001.png</Icon>
```

For a text-key experiment, change a display key only:

```xml
<Name>Apples</Name>
```

to:

```xml
<Name>Apples Test Name</Name>
```

This changes the key the UI asks for. It does not add a real translation by itself.

## Step 4: What Not To Edit

Leave these alone:

- `Guid`
- `ResourceCategory`
- `CarryType`
- `Tags`
- `Mesh`
- `CarryAttachmentLocator`
- producer recipes, deposits, and storage references

Those fields affect gameplay systems or cross-file relationships.

## Step 5: Validate And Regenerate

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then check:

```text
generated/catalog/resources.md
generated/catalog/asset-references.md
generated/catalog/localization-index.md
```

## Step 6: Test In Game

Use a new test save if possible.

Check:

- the resource still appears where expected
- the icon changed in relevant UI locations
- no missing icon placeholder appears
- the resource is still produced, stored, and consumed normally

## Why This Example Is Useful

This is a low-risk way to learn resource editing. It changes presentation, not identity.

If something breaks after this kind of edit, the most likely cause is a typo in the icon path or a missing localization key, not a damaged gameplay relationship.
