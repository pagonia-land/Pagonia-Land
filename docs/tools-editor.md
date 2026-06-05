# 🪛 Tools And Editor Data

The `tools` package contains editor/Magmaview-oriented terrain and visualization data rather than normal gameplay economy data.

In the current extracted database, the package is represented by:

```text
game-gdb/tools/gdb/magmaview.gd.xml
```

The package currently has no generated buildings, units, recipes, resources, objectives, or production chains. That is expected. Its useful data lives in terrain, texture, vegetation, road, fluid, territory, and editor brush components.

## Generated Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Useful output files:

```text
generated/catalog/tools-editor.md
generated/catalog/tools-editor.csv
generated/catalog/filters/tools/terrain-sediments.md
generated/catalog/filters/tools/vegetation-groups.md
generated/catalog/filters/tools/vegetation.md
generated/catalog/filters/tools/textures.md
generated/catalog/filters/tools/globals-and-brushes.md
generated/catalog/filters/packages/tools/tools-editor.md
```

The current local catalog detects 29 tools/editor rows:

| Area | Rows |
| --- | ---: |
| `VisVegetation` | 10 |
| `VisTexture` | 7 |
| `TerrainSediment` | 4 |
| `VisVegetationGroup` | 3 |
| `EditorBrushes` | 1 |
| `VisFluid` | 1 |
| `VisRoad` | 1 |
| `VisTerrain` | 1 |
| `VisTerritory` | 1 |

## Main Areas

| Area | What It Describes |
| --- | --- |
| `VisTerrain` | Global terrain rendering settings, sediment texture resolution, shaping border masks, outdoor pile decals |
| `VisFluid` | Water normal, caustics, foam, and flow texture references |
| `VisRoad` | Road mask textures for road ends, curves, crossings, and building entries |
| `VisTerritory` | Territory border stone meshes and placement variation values |
| `EditorBrushes` | Editor brush references, currently the bulldozer brush texture |
| `TerrainSediment` | Terrain sediment texture links, vegetation group links, color, density, transition, influence, and sort values |
| `VisVegetationGroup` | Weighted vegetation sets and cluster group references |
| `VisTexture` | Terrain/editor texture asset maps and height scale values |
| `VisVegetation` | Plant texture references and scale, bend, tilt, overgrow, specular, and roughness values |

## Important Columns

| Column | Meaning |
| --- | --- |
| `Area` | Component type, such as `TerrainSediment`, `VisTexture`, or `VisVegetationGroup` |
| `GroupPath` | XML group path inside `magmaview.gd.xml`, such as `Sediments`, `Textures`, `Vegetation`, or `Editor` |
| `IsAbstract` | Whether the entity is an abstract base/template row |
| `LinkedTextures` | Resolved GUID references to texture entities, such as `TopTerrainTexture`, `SideTerrainTexture`, `RoadTexture`, or `BulldozerBrushTexture` |
| `VegetationGroup` | Resolved `TerrainSediment -> VisVegetationGroup` link |
| `VegetationItems` | Resolved vegetation entries inside a vegetation group |
| `ClusterGroups` | Resolved clustered vegetation group references |
| `AssetReferences` | Texture, mesh, and asset paths detected inside the component |
| `Values` | Compact scalar tuning values for quick inspection |

## Reading The Current Structure

The main chain in `tools` is:

```text
TerrainSediment
  -> TopTerrainTexture / SideTerrainTexture / RoadTexture
  -> VegetationGroup
      -> VegetationItems
          -> ColorTexture / GroundColorTexture
```

Examples from the current catalog:

- `FertileDuplicate` sediment links to `FertileDuplicate` terrain texture and `GrassDuplicate` vegetation group.
- `Lake ShoreDuplicate` sediment links to `LakeDuplicate` terrain texture and `LowGrassDuplicate` vegetation group.
- `SedimentBaseDuplicate` is an abstract base row with side and road texture links.
- `BulldozerTexture` is an editor texture row referenced by `EditorBrushes`.

## Useful Searches

Raw XML:

```powershell
rg --no-ignore "TerrainSediment|VisVegetationGroup|VisTexture|EditorBrushes" .\game-gdb\tools
rg --no-ignore "GUID-HERE" .\game-gdb
```

Generated catalog:

```powershell
rg --no-ignore "FertileDuplicate|VisVegetationGroup|BulldozerTexture" .\generated\catalog\tools-editor.md
```

Browser searches:

```text
tools-editor
TerrainSediment
VisVegetationGroup
BulldozerTexture
GrassDuplicate
VisRoad
```

## Modding Notes

This package is useful for map/editor and terrain-visual research. It is not the best first place for gameplay balance mods.

Safer first investigations:

- trace which sediment uses which terrain texture
- compare grass vegetation groups and their weighted vegetation entries
- inspect editor brush texture links
- compare road, fluid, and territory visual asset references

Riskier edits:

- changing texture paths without checking that the referenced asset exists
- changing sediment relationships, because that can affect terrain rendering or editor behavior
- changing vegetation density or scale ranges without visual testing
- editing global terrain, fluid, road, or territory visual settings

Treat this catalog as a map of editor-facing data. It helps locate relationships and assets, but compatibility still needs testing in the editor or map workflow.
