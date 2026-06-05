# Generated Analysis

This directory is for local analysis artifacts generated from the extracted game database.

The generated files in this directory are ignored by Git because they are derived from local game data.

Generate them with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Expected local outputs:

- `analysis-summary.json`
- `entities.json`
- `references.json`

You can also generate a human-readable local catalog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Expected catalog outputs:

- `catalog/buildings.md` and `catalog/buildings.csv`
- `catalog/building-production.md` and `catalog/building-production.csv`
- `catalog/recipes.md` and `catalog/recipes.csv`
- `catalog/units.md` and `catalog/units.csv`
- `catalog/production-graph.md`
- `catalog/production-graph.mmd`
- `catalog/filters/package-summary.md`
- `catalog/filters/packages/<package>/...`
- `catalog/filters/production/...`
