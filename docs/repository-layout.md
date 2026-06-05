# Repository Layout & Local Data

Where everything lives: which parts of the tree are committed, which stay local on your machine, and how the extracted game data, generated indexes, and version snapshots are organised.

For the high-level map of the database *content* (packages, systems, how entities combine), see [Database Overview](../DATABASE_OVERVIEW.md). This page is about the *files and folders*.

## Repository Layout

**Committed:**

| Path | Contents |
| --- | --- |
| `docs/` | All long-form guides, organised by topic |
| `schemas/` | JSON Schemas for mod manifests, patch files, collections, lockfiles, plus per-tool report shapes under `manager/`, `patcher/`, `paker/` |
| `scripts/` | PowerShell scripts for validation, analysis, catalog generation, version diffs, snapshot management, preflight |
| `tools/pagonia-patcher/` | C# patcher source, tests, fixtures, CLI contract |
| `tools/pagonia-paker/` | C# paker source, tests, fixtures, CLI contract |
| `tools/pagonia-manager/` | C# manager source, tests, CLI contract — install / deploy / rollback / profiles / collections on top of the patcher + paker cores |
| `tools/catalog-browser/` | Static HTML browser for the generated catalog index |
| `sandbox/` | Modder workspace — templates plus your own `mods/`, `collections/`, `out/` (all but templates ignored); also hosts the [end-to-end manager walkthrough](../sandbox/examples/manager-walkthrough/) |

**Local-only (git-ignored):**

| Path | Contents |
| --- | --- |
| `game-gdb/` | Extracted `*.gd.xml` packages — never committed |
| `game-paks/` | Real `.pak` archives used for paker smoke tests — never committed |
| `generated/` | Indexes, catalogs, and diffs derived from `game-gdb/` |
| `snapshots/<version>/` | Frozen per-version copies of `game-gdb/`, `generated/`, and notes |

Extracted game files themselves are never published here — see [NOTICE.md](../NOTICE.md) for the rules on local game data and derived artifacts.

## Extracted Database Packages

The four package folders under `game-gdb/`:

| Folder | Contents |
| --- | --- |
| `game-gdb/core/gdb/` | Base game: resources, buildings, units, recipes, terrain, objectives, campaign, NPCs, UI, audio |
| `game-gdb/decorations1/gdb/` | Decorative buildables expansion |
| `game-gdb/dlc1/gdb/` | Meadowsong expansion: new resources, buildings, units, NPCs, objectives |
| `game-gdb/dlc1/maps/` | Meadowsong scenario map database (the only file outside a `gdb/` folder) |
| `game-gdb/tools/gdb/` | Editor / Magmaview terrain, sediments, vegetation, brushes |

**Extraction rule:** for each `.pak` archive, copy every file from the `gdb/` folder inside into the matching folder under `game-gdb/`. The known exception is `dlc1.pak`, which adds a `maps/` subfolder for the Meadowsong scenario database.

To extract `.pak` archives, use the bundled [`pagonia-paker`](../tools/pagonia-paker/) CLI — see [Setup](setup.md) for the step-by-step workflow.

## Local Generated Indexes

Run the scripts under [`scripts/`](../scripts/) to regenerate machine-readable analysis files into `generated/`:

| File or folder | Purpose |
| --- | --- |
| `generated/entities.json` | One entry per entity: GUID, name, package, file, group path, parent, child count, component/value types |
| `generated/references.json` | One entry per GUID-like reference: source entity/path, target if resolved, package crossing, null-GUID marker |
| `generated/analysis-summary.json` | Compact summary of the current scan |
| `generated/catalog/` | Markdown / CSV catalogs plus Mermaid graphs — buildings, production, units, resources, objectives, tech tree, NPCs, artifacts, shrines, terrain, assets, and more ([full list](../REFERENCE.md)) |
| `generated/catalog/search-index.json` | Index that powers `tools/catalog-browser/index.html` |
| `generated/diffs/<old>_to_<new>/` | Per-version diff reports produced by `scripts/diff_versions.ps1` |

Current scan: **4,709 entities · 31,761 references · 24,420 resolved · 7,311 explicit null GUIDs · 30 other unresolved (12 known-stable engine wildcards + 18 transient `NoMVP.` orphans)**. Full breakdown in [Validation Baseline](../VALIDATION_BASELINE.md).

## Local Snapshots And Version Updates

For update analysis, frozen per-version copies live under `snapshots/<version>/`:

```text
snapshots/<version>/game-gdb/      # the extracted XMLs at that version
snapshots/<version>/generated/     # indexes + catalogs generated against them
snapshots/<version>/notes/         # version-specific observations
```

When a new game version is extracted, run the [Database Update Playbook](../UPDATE_PLAYBOOK.md): refresh `game-gdb/`, run `scripts/validate_database.ps1`, regenerate indexes and catalogs, diff against the previous snapshot, record the delta in [CHANGELOG.md](../CHANGELOG.md). The [Local Snapshots](snapshots.md) doc explains the workflow in detail.
