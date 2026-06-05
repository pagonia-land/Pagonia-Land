# Trace A Tools Editor Terrain Sediment

This example is read-only. It shows how to use the tools/editor catalog to understand `game-gdb/tools/gdb/magmaview.gd.xml`.

The `tools` package is not normal gameplay economy data. It appears to contain Magmaview/editor terrain, vegetation, texture, road, fluid, territory, and brush data.

## Goal

Trace one terrain sediment from:

```text
TerrainSediment
-> terrain texture
-> vegetation group
-> vegetation entries
-> texture assets
```

## Step 1: Generate The Tools Catalog

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Open:

```text
generated/catalog/tools-editor.md
generated/catalog/filters/tools/terrain-sediments.md
generated/catalog/filters/tools/vegetation-groups.md
generated/catalog/filters/tools/vegetation.md
generated/catalog/filters/tools/textures.md
```

## Step 2: Pick A Sediment

Use `FertileDuplicate`.

In the current catalog, it links to:

```text
TopTerrainTexture=FertileDuplicate
VegetationGroup=GrassDuplicate
VegetationLimiter=core/textures/tech/sediment_grass_masks/field_base_001_mask.texture.json
```

It also has scalar values such as color, vegetation density, scale range, interval, transition size, and sort order.

## Step 3: Inspect The Raw XML

Search:

```powershell
rg --no-ignore "FertileDuplicate|GrassDuplicate|field_base_001_mask" .\game-gdb\tools\gdb\magmaview.gd.xml
```

Read the matching components:

```text
TerrainSediment
VisVegetationGroup
VisTexture
VisVegetation
```

## Step 4: Follow The Vegetation Group

Open:

```text
generated/catalog/filters/tools/vegetation-groups.md
```

Search for:

```text
GrassDuplicate
```

The group lists weighted vegetation entries such as:

```text
Grass001Duplicate
Grass002Duplicate
Grass003Duplicate
Grass004Duplicate
```

It can also reference cluster groups, such as `HighGrassDuplicate`.

## Step 5: Follow The Vegetation Assets

Open:

```text
generated/catalog/filters/tools/vegetation.md
generated/catalog/filters/assets/textures.md
```

Search for a vegetation entry, for example:

```text
Grass001Duplicate
```

Look for:

```text
ColorTexture
GroundColorTexture
Size
RandomMinScale
RandomMaxScale
MinBend
MaxBend
MaxTilt
Overgrow
Specular
Roughness
```

## Step 6: What This Tells You

The chain is:

```text
FertileDuplicate sediment
  -> FertileDuplicate texture row
  -> GrassDuplicate vegetation group
  -> grass vegetation entries
  -> grass texture assets
```

This helps map/editor researchers understand which data controls terrain appearance and vegetation selection.

## What Not To Edit Yet

Avoid these until you have a safe editor/map test workflow:

- changing sediment GUIDs
- changing texture GUID links
- changing vegetation group membership
- changing density or scale ranges heavily
- changing asset paths to unknown files

## Validation

For read-only tracing, no validation is needed.

After a real edit, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then test in the relevant editor or map workflow. Normal gameplay testing may not reveal tools/editor issues.
