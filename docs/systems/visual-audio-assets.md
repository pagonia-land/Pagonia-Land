# 🎨 Visual, Audio, And Asset References

This page explains the first generated catalog for visual, audio, and asset-like references in the local GameDatabase XML files.

It is meant as a lookup aid for modders who want to answer questions such as:

- Which icon does this resource, building, notification, or tech tree row use?
- Which prefab, mesh, texture, character kit, or VFX path is referenced by an entity?
- Which units and props have audio ambience or selection audio?
- Which buildings have visual state overrides or cycleable visual variants?
- Which asset references come from `core`, `dlc1`, `decorations1`, or `tools`?

## Generated Files

Run the catalog generator from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Useful output files:

```text
generated/catalog/asset-references.md
generated/catalog/asset-references.csv
generated/catalog/visual-audio-components.md
generated/catalog/visual-audio-components.csv
generated/catalog/filters/assets/icons.md
generated/catalog/filters/assets/prefabs.md
generated/catalog/filters/assets/meshes.md
generated/catalog/filters/assets/textures.md
generated/catalog/filters/assets/audio-events.md
generated/catalog/filters/assets/character-kits.md
generated/catalog/filters/assets/visual-components-with-assets.md
generated/catalog/filters/assets/visual-components-with-audio.md
generated/catalog/filters/packages/<package>/asset-references.md
generated/catalog/filters/packages/<package>/visual-audio-components.md
```

The local catalog currently detects 5,406 asset reference rows and 2,759 visual/audio component rows.

## Asset Reference Catalog

`asset-references` is one row per detected asset-like value.

Important columns:

| Column | Meaning |
| --- | --- |
| `Package` | Source package folder such as `core`, `dlc1`, `decorations1`, or `tools` |
| `Name` / `Entity` | Display name and technical entity name |
| `Kind` | Broad entity kind inferred by the generator |
| `Component` | Component that contains the reference |
| `FieldPath` / `FieldName` | XML path to the value |
| `AssetType` | Heuristic category such as `Icon`, `Prefab`, `Mesh`, `Texture`, `AudioEvent`, `CharacterKit`, `VFX`, or `JsonAsset` |
| `AssetRoot` | First path segment or `event:` root |
| `Extension` | File extension if the reference has one |
| `Asset` | The raw referenced path or event string |
| `Components` | Other components on the same entity |
| `Guid` / `File` | Source identity and XML file |

Current asset type counts:

| Asset type | Count |
| --- | ---: |
| Icon | 1,976 |
| Prefab | 995 |
| AudioEvent | 943 |
| Mesh | 811 |
| Texture | 336 |
| JsonAsset | 157 |
| CharacterKit | 125 |
| VFX | 63 |

The catalog classifies references by field names and path-like values. It is intentionally broad enough to catch likely reusable assets, but it does not prove that every referenced file exists in a pak or that swapping it is safe.

## Visual And Audio Component Catalog

`visual-audio-components` is one row per visual/audio-related component or asset-bearing entity row.

It includes components and entity rows such as:

- `TerrainProp`
- `VisAudioAmbience`
- `VisCycleableBuilding`
- `UnitAnimations`
- `UnitAttachment`
- `UiBuildingStatesOverwrite`
- `ResourceDescription`
- `Deposit`
- `Notification`
- `ShrineAbility`
- `TechTreeTierGroup`
- `VisTexture`
- `VisVegetation`
- `VisFactionColor`

Important columns:

| Column | Meaning |
| --- | --- |
| `Component` | Component or entity row that carries visual/audio data |
| `AssetReferences` | Non-audio asset references detected inside the component |
| `AudioEvents` | Audio event references detected inside the component |
| `Values` | Compact scalar value summary for context |
| `Components` | Component list of the owning entity |
| `Guid` / `File` | Source identity and XML file |

Current high-volume components include 712 `TerrainProp` rows, 318 `VisAudioAmbience` rows, 315 `Building` rows, 203 `VisCycleableBuilding` rows, 177 `UnitAnimations` rows, and 176 `UnitAttachment` rows.

## Common Workflows

To find reusable icons:

```powershell
rg --no-ignore "icon_build|icon_resource|icon_unit" .\game-gdb\
```

Then compare with:

```text
generated/catalog/filters/assets/icons.md
```

To inspect unit visual and audio data:

```text
generated/catalog/visual-audio-components.md
```

Search for `UnitAnimations`, `UnitAttachment`, `SelectionAudio`, or the unit name.

To trace a building's visual states:

```text
generated/catalog/filters/assets/visual-components-with-assets.md
```

Search for the building name, `Building`, `VisCycleableBuilding`, or `UiBuildingStatesOverwrite`.

To find audio hooks:

```text
generated/catalog/filters/assets/audio-events.md
generated/catalog/filters/assets/visual-components-with-audio.md
```

Useful raw XML search:

```powershell
rg --no-ignore "event:/|SelectionAudio|VisAudioAmbience|StartLineMusic|Sound" .\game-gdb\
```

## Modding Notes

Asset references are usually safer to study than to edit blindly. A value may look like a file path, but the runtime can still expect a matching prefab structure, animation set, material setup, audio bank, or engine-side binding.

Good first experiments:

- compare two similar building icon references
- inspect a resource icon and trace where that resource appears in UI catalogs
- compare unit `SelectionAudio` values across similar units
- compare `TerrainProp` prefab and mesh references across similar props

Riskier changes:

- swapping character kits or animation sets between unrelated units
- replacing prefabs used by buildings with different size or attachment expectations
- changing notification sound or music events without testing scenario flow
- editing visual cycle components without checking all building states in game

Treat this catalog as a map. It tells you where visual and audio references live. Final compatibility still needs in-game testing.
