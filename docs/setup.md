# 🛠️ Local Setup

This guide gets you from a fresh checkout to a validated, browseable local copy of the Pioneers of Pagonia game database — everything you need to analyse the data or build your own mods.

!!! tip "First time here? Get a local copy of the repository"
    The instructions below assume you have this repository on disk.

    - **Clone with git** (recommended if you'll update later):
      `git clone https://github.com/pagonia-land/Pagonia-Land.git`
    - **Download as ZIP** (no git needed): on
      [github.com/pagonia-land/Pagonia-Land](https://github.com/pagonia-land/Pagonia-Land)
      click the green **Code** button → **Download ZIP** → unzip anywhere.

    **Just want to install mods?** You don't need to clone anything — grab the
    `pagonia-manager` binary from the [Download](downloads.md) page.

## Quick Path

For readers who want the speedrun, here's the whole flow as a checklist. Each step links to the section below with the full command and explanation.

1. [Check prerequisites](#prerequisites) — game install, PowerShell, optionally git + ripgrep.
2. [Extract the game database XMLs](#extract-the-game-database-xmls) into `game-gdb/`.
3. [Run validation](#validate-the-local-database) — baseline is 0 errors / 2 warnings.
4. [Generate local indexes](#generate-local-indexes) — JSON snapshots under `generated/`.
5. [Generate the local catalog](#generate-the-local-catalog) — browseable Markdown.
6. [Read one example](#read-one-example) — sawmill trace or a small first edit.

The whole loop is repeatable: after a game update or a local edit, run validate → analyze → catalog again.

## Scope

This repository covers **GameDatabase modding** — changing buildings, units, recipes, costs, objectives — including the GameDatabase changes the 1.4.0 Pagonia Editor authors (it publishes them as ordinary `.pak` mods our tools handle). **Map authoring** itself (terrain, layout, placement, texture formats) and the editor's own project format are the [community wiki on wiki.gg](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding)'s slice, not documented here.

The repository contains documentation, scripts, local analysis workflows, and modding examples. It does **not** publish extracted game database XMLs — you bring your own from your own Pioneers of Pagonia installation. Only `game-gdb/README.md` is committed; the package folders below it stay local.

## Prerequisites

- A local installation of Pioneers of Pagonia.
- Access to the relevant `.pak` files. They ship inside the install under a
  `pak/` subfolder. On a default Steam install that is
  `C:\Program Files (x86)\Steam\steamapps\common\Pioneers of Pagonia\pak\`.
  (In Steam: right-click the game → **Manage → Browse local files**, then open `pak/`.)
- PowerShell. The local analysis, validation, and catalog scripts below are
  written for Windows PowerShell (`powershell` / `winget`); on Linux/macOS run
  them with `pwsh`. The tool binaries themselves are cross-platform — see
  [Downloads](downloads.md).
- `git`, if you want to contribute documentation or scripts.
- `rg`/ripgrep for fast text searches. On Windows:

```powershell
winget install BurntSushi.ripgrep.MSVC
```

## Repository Layout

```text
.
├── docs/
├── game-gdb/
├── generated/
├── scripts/
├── README.md
├── DATABASE_OVERVIEW.md
└── UPDATE_PLAYBOOK.md
```

The local game database files belong under `game-gdb/`:

```text
game-gdb/
├── core/
├── decorations1/
├── dlc1/
└── tools/
```

## Extract The Game Database XMLs

Extract the XMLs with the `pagonia-paker` CLI — a single-file binary that
lists, unpacks, packs, patches, and classifies `.pak` archives. Get it either
from the [Downloads](downloads.md) page (a prebuilt binary) or by running it
from source with the .NET SDK:

```powershell
dotnet run --project tools/pagonia-paker/src/PagoniaLand.Paker.Cli -- unpack <pak> <output-dir>
```

The [`tools/pagonia-paker/`](../tools/pagonia-paker/) folder in this repository
holds the **source**, not a ready-to-run binary. See
[tools/pagonia-paker/CLI.md](../tools/pagonia-paker/CLI.md) for the full command reference.

Unpack each `.pak` into its package folder below `game-gdb/`. `unpack` preserves
the archive's internal paths, so each pak's own `gdb/` folder (and `dlc1.pak`'s
`maps/` folder) lands in exactly the right place — adjust the install path to yours:

```powershell
$pak = 'C:\Program Files (x86)\Steam\steamapps\common\Pioneers of Pagonia\pak'
pagonia-paker unpack "$pak\core.pak"         game-gdb\core
pagonia-paker unpack "$pak\decorations1.pak" game-gdb\decorations1
pagonia-paker unpack "$pak\dlc1.pak"         game-gdb\dlc1
pagonia-paker unpack "$pak\tools.pak"        game-gdb\tools
```

This produces the canonical layout:

| Pak file | Local target folder |
| --- | --- |
| `core.pak` | `game-gdb/core/gdb/` |
| `decorations1.pak` | `game-gdb/decorations1/gdb/` |
| `dlc1.pak` | `game-gdb/dlc1/gdb/` |
| `tools.pak` | `game-gdb/tools/gdb/` |

**General rule:** every file from the `gdb/` folder inside each `.pak` ends up in the matching folder below `game-gdb/`. Relative paths are preserved exactly — file names contain spaces and that's intentional.

**Known exception:** `dlc1.pak` carries `meadowsong map database.gd.xml` under a `maps/` subfolder rather than `gdb/`. The `unpack` step above preserves that automatically — the file ends up at:

```text
dlc1.pak maps/meadowsong map database.gd.xml -> game-gdb/dlc1/maps/meadowsong map database.gd.xml
```

This is the only XML file in the shipped database that lives outside the canonical `<pak>/gdb/` layout. Future Meadowsong-style DLCs are likely to keep using `<pak>/maps/` for scenario databases — see [Quirks And Anomalies → Only one file lives outside `<pak>/gdb/`](quirks-and-anomalies.md#only-one-file-lives-outside-pakgdb).

## Validate The Local Database

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Baseline is **0 errors / 2 warnings** — see [Validation Baseline](../VALIDATION_BASELINE.md) for the expected warning samples. If validate prints more than that, a recent edit or extraction introduced something new.

The validation script checks:

- required local package folders
- XML parse errors
- duplicate entity GUIDs
- unresolved non-null GUID references
- building production recipe links
- recipe input/output resource references
- building construction cost resource references
- employment unit references
- unit recruitment cost resource references
- source recruitable unit references
- generated JSON validity, if those files exist

Null GUIDs such as `00000000-0000-0000-0000-000000000000` are treated as empty/default references, not as broken links.

## Generate Local Indexes

After placing the XML files under `game-gdb/`, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

This creates local JSON files under `generated/`:

- `generated/entities.json`
- `generated/references.json`
- `generated/analysis-summary.json`

All ignored by Git because they're derived from local game data.

## Generate The Local Catalog

To create Markdown / CSV / Mermaid overviews for buildings, production recipes, units, construction costs, workers, timing hints, and relationship graphs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

This creates browseable files under `generated/catalog/`:

```text
generated/catalog/README.md
generated/catalog/buildings.md
generated/catalog/building-dependency-matrix.md
generated/catalog/building-production.md
generated/catalog/recipes.md
generated/catalog/units.md
generated/catalog/unit-equipment-matrix.md
generated/catalog/localization-index.md
generated/catalog/objective-flow.md
generated/catalog/filters/package-summary.md
```

This is the fastest way to browse buildings, units, recipes, costs, workers, production links, text/localization keys, and objective flow rows. See [Generated Database Reference](../REFERENCE.md) for the full catalog layout.

## Read One Example

Once the catalog is generated, try one of these to get a feel for the data:

- [Trace The Sawmill Production Chain](examples/trace-sawmill-chain.md) — read-only walkthrough across multiple files.
- [Change A Building Construction Cost](examples/change-building-cost.md) — small, safe first edit.

## Recommended Workflow

The same loop applies to game updates, modding experiments, and re-syncing after a pull:

1. Update or replace XML files under `game-gdb/`.
2. Run `scripts\validate_database.ps1`.
3. Run `scripts\analyze_database.ps1`.
4. Run `scripts\generate_catalog.ps1`.
5. Read `generated/analysis-summary.json` and the files under `generated/catalog/`.
6. Update documentation if structure, counts, or behavior changed.
7. Commit only documentation and scripts, not extracted game files.

## Don't Commit Local Game Data

Before committing anything, check:

```powershell
git status --short --ignored
```

It is normal to see ignored folders like:

```text
!! game-gdb/core/gdb/
!! generated/catalog/
```

Do not force-add them. The following are ignored by `.gitignore` and should stay that way:

- `game-gdb/core/gdb/`
- `game-gdb/decorations1/gdb/`
- `game-gdb/dlc1/gdb/`
- `game-gdb/tools/gdb/`
- `generated/*.json`
- `generated/catalog/`

## Useful Commands

The `game-gdb/` folder is ignored by Git, and ripgrep respects ignore rules by default. Use `--no-ignore` when searching local game files from this repository.

List XML files:

```powershell
rg --no-ignore --files .\game-gdb -g "*.xml"
```

Count XML files:

```powershell
(Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml).Count
```

Find an entity or component by text:

```powershell
rg --no-ignore "FirewoodFromSoftwoodRecipe|AspectProduction|ResourceDescription" .\game-gdb
```

Find a GUID:

```powershell
rg --no-ignore "f01e0070-4661-4c00-8b74-e240f8bd5c3f" .\game-gdb
```

## Different Goal: Installing Mods Instead?

The flow above prepares the repository for **analysing** the game database and **building** your own mods.

If your goal is to **install** mods (yours or someone else's) into a real game install, see [`tools/pagonia-manager/`](../tools/pagonia-manager/). The manager handles mod stores, profiles, planning, deploys, and rollbacks. Run it with no arguments for an interactive menu, or pass `--help` for the scripted CLI surface.

## Next Reading

If this worked, continue with:

1. [Common Fields and Patterns](common-fields.md)
2. [Safe Edits](safe-edits.md)
3. [Worked Examples](examples/README.md)
4. [First Modding Experiments](first-mods.md)
5. [Troubleshooting](troubleshooting.md)
