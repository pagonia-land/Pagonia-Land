# 🌐 Localization

The game database uses many text-like keys for names, descriptions, tooltips, notifications, objectives, hints, and other UI-facing strings.

For modding, these keys matter because a building, unit, recipe, objective, or artifact may not store final translated text directly. It often stores a technical key such as:

```text
Resource Apple Name
MapSelection TutMap1 Name
IconicBuilding Name Sanctuary
```

The local catalog generator creates a localization index that helps connect those keys back to the entities and files that use them.

## Generated Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then open:

```text
generated/catalog/localization-index.md
generated/catalog/localization-index.csv
generated/catalog/filters/localization/declared-tags.md
generated/catalog/filters/localization/referenced-keys.md
generated/catalog/filters/localization/not-in-local-tag-index.md
generated/catalog/filters/packages/<package>/localization-index.md
```

The CSV file is usually best for filtering. The Markdown file is good for quick reading.

## What The Index Contains

| Column | Meaning |
| --- | --- |
| `Package` | Source package such as `core`, `dlc1`, `decorations1`, or `tools` |
| `Key` | The localization-like key or visible text value |
| `Declared` | Whether this key appears in a local `LocaParkplatz` tag list |
| `UsageType` | `DeclaredTag` or `ReferencedKey` |
| `Entity` | Source entity name |
| `DisplayName` | Best available generated display name for the entity |
| `Kind` | Generated kind such as building, unit, resource, recipe, or generic entity |
| `FieldPath` | XML path below `Values` where the key was found |
| `Comment` | Optional comment from `LocaParkplatz`, when present |
| `Guid` | Source entity GUID |
| `File` | Source XML file |

## Important Limitation

The extracted `LocaParkplatz` files currently look like placeholder or tag index files, not a complete translation database.

That means:

- `Declared = True` is useful evidence that the key appears in the local tag index.
- `Declared = False` does not automatically mean the key is broken.
- `not-in-local-tag-index` is a research filter, not a validation error list.

Many normal database keys are likely resolved by localization data outside the extracted GameDatabase XML set.

## Useful Searches

Search a technical key in the generated catalog:

```powershell
rg --no-ignore "IconicBuilding Name Sanctuary" .\generated\catalog\localization-index.md
```

Search where a name field comes from in the extracted XML:

```powershell
rg --no-ignore "Resource Apple Name" .\game-gdb\core
```

Search all objective-style text keys:

```powershell
rg --no-ignore "Objective|Notification|Reward" .\game-gdb
```

## Modding Workflow

1. Find the entity you want to change in the building, unit, resource, objective, artifact, or recipe catalog.
2. Copy its name, description, tooltip, notification, or title key.
3. Search that key in `generated/catalog/localization-index.csv`.
4. Check whether the key is only referenced or also declared in `LocaParkplatz`.
5. Search the key in `game-gdb/` to find every direct database usage.
6. If you edit a key, regenerate the catalog and check that the old and new references make sense.

For simple experiments, changing a key reference can be useful to reuse an existing visible text entry.

Creating truly new translated text probably requires understanding the game's localization pipeline outside these GameDatabase XML files.

## Why This Helps

The index is useful for:

- finding all entities that share a visible name or tooltip key
- discovering objective and notification text keys
- spotting DLC or package-specific text additions
- checking whether a mod changed display keys consistently
- preparing future tooling that can join GameDatabase entities with external localization files if those become available

## Current Local Snapshot

The latest local generation reports:

| Metric | Count |
| --- | ---: |
| Localization rows | 6,166 |
| Unique localization keys | 4,874 |
| Declared `LocaParkplatz` tags | 261 |
| Unique declared tags | 261 |
| Referenced localization-like keys | 5,905 |
| Unique referenced keys | 4,679 |
| References not in the local tag index | 5,823 |
| Unique keys not in the local tag index | 4,611 |

These numbers describe the current local extraction only and should be regenerated after each game update.
