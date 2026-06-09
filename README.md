<p align="left"><img src="assets/icon-128.png" alt="Pagonia Land project icon" width="96" height="96"></p>

# Pagonia Land

[![tools CI](https://github.com/pagonia-land/Pagonia-Land/actions/workflows/tools.yml/badge.svg)](https://github.com/pagonia-land/Pagonia-Land/actions/workflows/tools.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Code of Conduct](https://img.shields.io/badge/Code%20of%20Conduct-Contributor%20Covenant-blueviolet.svg)](CODE_OF_CONDUCT.md)

**A community workshop for modding [Pioneers of Pagonia](https://pioneersofpagonia.com/).** Pagonia Land brings together the things a modder actually reaches for:

- **A structured map of the game's XML database** — entity model, GUID references, system guides.
- **Three command-line tools** covering the full mod lifecycle: editing → installing → deploying.
- **JSON Schemas** that any third-party tool can read against — the integration contract for GUIs, IDE plugins, CI scripts.
- **A growing library of worked examples** — from "change one construction cost" walkthroughs to building a custom Sanctuary ability end-to-end.

The goal is to shorten the path from "I have an idea" to "it works in-game" — whether you're writing your first patch, installing someone else's mod, or building a GUI on top of the contracts we ship.

**New here? Pick a path:**

- **Install mods into the game** → [Get the tools](#get-the-tools) below
- **Write your own mod or explore the database** → [Local Setup](docs/setup.md) (its **Quick Path** section is the 10-minute speedrun)
- **Build a GUI / IDE plugin against the contracts** → [`schemas/`](schemas/) (every CLI command emits the schema-validated JSON reports)
- **Evaluating the project before diving in** → [REVIEWING.md](REVIEWING.md)

## Why This Exists

Pioneers of Pagonia modding has two distinct slices, and they don't overlap much:

- **Map editor workflow** — making custom maps with the official editor and packaging them for distribution. Documented in the [Pioneers of Pagonia community wiki on wiki.gg](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding), which covers the editor tooling, map package structure, and a handful of file formats (textures, scenario data).
- **GameDatabase modding** — changing what already exists in the game: buildings, units, recipes, costs, objectives. Covered by this repository.

Pagonia Land focuses on the second slice: a structured map of the game's XML data, a declarative patch format with conflict detection, command-line tooling for the install / deploy / rollback lifecycle, and end-to-end worked examples. It does not try to be the place for map editing — for that, the wiki is the right starting point.

## What's Inside

Current snapshot: **`1.3.2-11873+194094`** (1.3.2 Hotfix #2 / "Free Beer" core update, 2026-06-09).

> **Work in progress.** This repository is actively maintained alongside Pioneers of Pagonia itself — every game update gets a snapshot refresh, every new mechanic gets documented, every reported bug or doc gap gets fixed. Things will move and improve. If something looks off or out of date, the next refresh probably covers it.

| Area | What you get |
| --- | --- |
| Database snapshot | 59 XML files · 4,711 entities · 31,763 GUID references · 0 errors / 2 stable warnings ([details](VALIDATION_BASELINE.md)) |
| Documentation | 30+ guides across six chapters — orientation, database concepts, gameplay data, cross-file systems, modding practice, generated reference ([jump](#documentation-chapters)) — plus a [catalog browser](https://pagonia-land.github.io/Pagonia-Land/catalog/) to explore every entity, recipe, building, and unit in your browser |
| Patcher | `pagonia-patcher` — declarative `mod.yaml`, ten XML operations, binary pak-entry ops, conflict detection, collections + lockfiles, AOT single-file binary ([CLI](tools/pagonia-patcher/CLI.md)) |
| Paker | `pagonia-paker` — list / unpack / pack / patch / compress / decompress / classify, `.gd.bin` index encode-decode, Pattern B overlay scaffold, parallel encoding, AOT single-file binary ([CLI](tools/pagonia-paker/CLI.md)) |
| Manager | `pagonia-manager` — install / uninstall / enable / disable / move / profile / collection install / tweak / plan / deploy / rollback against a real game install; profile-scoped mod selection, per-profile [tweak configuration](docs/mod-tweaks.md), atomic deploy with full backup + byte-identical rollback, Pattern B overlay-pak deploy, schema-validated JSON reports on every command, AOT single-file binary ([CLI](tools/pagonia-manager/CLI.md)) |
| Sandbox | [`sandbox/`](sandbox/README.md) — one-command apply for your own mods, git-ignored output folder, ready-to-copy templates, [end-to-end manager walkthrough](sandbox/examples/manager-walkthrough/) |
| Generated catalogs | Local Markdown / CSV / Mermaid catalogs plus a static search browser ([reference](REFERENCE.md) · [browser docs](docs/catalog-browser.md) · [live](https://pagonia-land.github.io/Pagonia-Land/catalog/)) — bring your own `search-index.json` via the file picker |
| Integration contracts | JSON Schemas under [`schemas/`](schemas/) — mod manifests, patch files, collections, lockfiles, plus per-tool report shapes ([`mod-patches/`](schemas/mod-patches/) for the patch format, [`manager/`](schemas/manager/) for manager reports, [`patcher/`](schemas/patcher/) + [`paker/`](schemas/paker/) for their respective reports) |

Extracted game files themselves are never published here. Place them locally under [`game-gdb/`](game-gdb/README.md); that directory is git-ignored. See [NOTICE.md](NOTICE.md) for the rules on derived artifacts, and [Repository Layout & Local Data](docs/repository-layout.md) for how the committed tree and the local-only folders are organised.

## Get The Tools

Download the latest Pagonia Land tools as Native AOT single-file binaries — **no .NET runtime required at the destination**:

[![Latest release](https://img.shields.io/github/v/release/pagonia-land/Pagonia-Land?include_prereleases&label=latest%20release)](https://github.com/pagonia-land/Pagonia-Land/releases/latest)

| Platform | Manager | Patcher | Paker |
|---|---|---|---|
| Windows x64 | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-win-x64.zip) | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-win-x64.zip) | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-win-x64.zip) |
| Linux x64 | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-linux-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-linux-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-linux-x64.tar.gz) |
| macOS Intel | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-x64.tar.gz) |
| macOS Apple Silicon | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-arm64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-arm64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-arm64.tar.gz) |

**Schemas only** (for mod-manager devs, IDE plugins, web validators): [⬇ pagonia-schemas.zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-schemas.zip)

**Verify your download:** every release ships a [`SHA256SUMS.txt`](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/SHA256SUMS.txt). Check with `sha256sum -c SHA256SUMS.txt` on Linux/macOS or `Get-FileHash` on Windows.

The download URLs above always point at the latest published release — no need to update them. The filename inside each archive carries the version (e.g. `pagonia-manager-0.1.0-win-x64/`).

**Building from source?** Each tool's README has the `dotnet publish` recipe. See [`tools/pagonia-manager/README.md`](tools/pagonia-manager/README.md), [`tools/pagonia-patcher/README.md`](tools/pagonia-patcher/README.md), [`tools/pagonia-paker/README.md`](tools/pagonia-paker/README.md).

## Start Here

If you are new, read these in order:

1. [Local Setup](docs/setup.md) — get the game data extracted, validated, and indexed (the **Quick Path** section at the top is the 10-minute speedrun)
2. [First Modding Experiments](docs/first-mods.md) — your first small, safe edits
3. [Database Overview](DATABASE_OVERVIEW.md) — the high-level map
4. [Worked Examples](docs/examples/README.md) — traces and fuller walkthroughs to learn by doing

The chapters below are a table of contents, not required reading order.

> [!WARNING]
> **Modding is at your own risk.** Mods change official Pioneers of Pagonia files and modify the experience from the original product developed and published by Envision Entertainment GmbH. EE cannot provide technical support for modded installs. Changes can introduce bugs, instability, or save corruption. If something goes wrong (black screens, crashes), the EE support team cannot help until all modifications are reverted.

## Documentation Chapters

### 1. Orientation

| Page | What it covers |
| --- | --- |
| [Glossary](docs/glossary.md) | Short, definitive explanations of database and modding terms |
| [FAQ](docs/faq.md) | Local data, generated files, validation warnings, first mods, contributing |
| [Repository Layout & Local Data](docs/repository-layout.md) | Where everything lives — committed tree, the local-only `game-gdb/` / `generated/` / `snapshots/` folders, package folders, generated indexes |
| [Notice](NOTICE.md) | Rules for local game data and derived artifacts |
| [Game Database Changelog](CHANGELOG.md) | Per-version observations: validation deltas, removed/added entities, notable shape changes |

### 2. Core Database Concepts

| Page | What it covers |
| --- | --- |
| [Entity Model](docs/entities.md) | Entities, GUIDs, groups, components, abstract parents, safe-editing assumptions |
| [GUID Reference](docs/guid-reference.md) | GUID identity, references, null GUIDs, tracing across files |
| [Common Fields and Patterns](docs/common-fields.md) | Recurring XML structures: `Values`, lists, amounts, time fields, asset paths |
| [Package Loading and Overlays](docs/package-loading.md) | Source packages, the effective combined database, cross-package references |
| [Package Layering and DLC Architecture](docs/package-layering.md) | How `core`, `dlc1`, `decorations1`, and `tools` combine; not to be confused with Pattern B overlay paks |
| [DLC Patch and Override Model](docs/dlc-patch-override-model.md) | Additive package patches, variant entities, override evidence, the `InheritanceMode` mechanism |

### 3. Gameplay Data

| Page | What it covers |
| --- | --- |
| [Resources](docs/resources.md) | Resource descriptions, categories, icons, carry types, downstream usage |
| [Buildings](docs/buildings.md) | Building components, construction costs, build menu fields, production links, placement risks |
| [Production Recipes](docs/production.md) | Recipe step machines, resource links, production chains, building links, workers |
| [Units](docs/units.md) | Workers, recruitment costs, tags, combat fields, employment references, safer unit edits |
| [Objectives](docs/objectives.md) | Objective components, notifications, rewards, triggers, map-specific objective data |
| [Notifications And Narration](docs/notification-narration.md) | Notification definitions, narration dialogs, message keys, speakers, sounds, player-facing flow |
| [Localization](docs/localization.md) | Mapping technical text keys back to entities and source files |

### 4. Cross-File Systems

| Page | What it covers |
| --- | --- |
| [System Guides](docs/systems/README.md) | Index for systems that span multiple XML files |
| [Artifacts](docs/systems/artifacts.md) | Artifact resources, combat-boost artifacts, treasure hunter links |
| [Treasure Hunting](docs/systems/treasure-hunting.md) | Treasure Hunter behavior, recipes, target resources, treasure areas |
| [Shrines and Sanctuary](docs/systems/shrines.md) | Shrine abilities, shrine recipes, mana production, DLC sanctuary data |
| [Combat Boosts](docs/systems/combat-boosts.md) | Combat-boost artifacts, tags, objectives, scenario balance notes |
| [Map, POI, And Objective Systems](docs/systems/map-poi-objectives.md) | Map-specific POIs, objective flow, shrines, sanctuary content, campaign logic |
| [Terrain Props, Deposits, And MapGen](docs/systems/world-mapgen.md) | Terrain props, harvestable deposits, map generation, map economy placement |
| [NPC, Encounter, And Combat Systems](docs/systems/npc-encounter-combat.md) | NPC units, bases, factions, raids, bosses, drops, infection, combat filters |
| [Tech Tree, Unlocks, And Seasonal Gates](docs/systems/progression-unlocks-seasonal.md) | Tech tree groups, DLC gates, objective unlock rewards, seasonal rows |
| [Visual, Audio, And Asset References](docs/systems/visual-audio-assets.md) | Icons, prefabs, meshes, textures, VFX, audio events, ambience, attachments |
| [Tools and Editor Data](docs/tools-editor.md) | Magmaview / editor terrain, sediments, textures, vegetation groups, brushes |

### 5. Modding Practice

| Page | What it covers |
| --- | --- |
| [Safe Edits](docs/safe-edits.md) | Beginner-friendly edits, medium-risk changes, high-risk areas |
| [Modding Risk Map](docs/modding-risk-map.md) | Practical risk map: objectives, tech tree, unit attachments, rare components |
| [Mod Distribution Patterns](docs/mod-distribution.md) | The three mod shapes — patched canonical pak (A), side-by-side overlay pak (B), user-map pak (C) — and the Meadowsong cross-pak entity merging primitives |
| [First Modding Experiments](docs/first-mods.md) | Guided first edits: costs, recipe amounts, build menu order, tracing |
| [Worked Examples](docs/examples/README.md) | Step-by-step traces and small edits for resources, buildings, recipes, units, DLC, objectives, artifacts, tools data |
| [Declarative Mod Patch Format](docs/mod-patch-format.md) | v0.1 format for describing mods as validated patch operations |
| [Mod Tweaks](docs/mod-tweaks.md) | User-adjustable mod values end-to-end: author declares them, a curator presets them in a collection, a player configures them in the manager |
| [Mod Patch Conflict Model](docs/mod-conflicts.md) | Conflict and safety model for combining multiple patch-based mods |
| [Mod Tags Vocabulary](docs/mod-tags.md) | Recommended `tags` values for `mod.yaml` / `collection.yaml` / `catalog.yaml` / `index.yaml` — content domain, intent, technical, maturity, language |
| [Patcher And Mod Manager Architecture](docs/mod-manager-architecture.md) | Shared patcher core, archive layout, validation flow, the integration surface |
| [Mod Collections](docs/mod-collections.md) | Curated mod lists, load order, lockfiles, profiles, collection safety |
| [Manager Walkthrough](sandbox/examples/manager-walkthrough/) | 13-stage end-to-end driver: install two fixture mods, enable + reorder, plan, deploy, rollback, SHA-256 round-trip verification — runs against the framework build or any published `pagonia-manager.exe` |
| [Troubleshooting](docs/troubleshooting.md) | XML parse errors, duplicate GUIDs, unresolved references, missing catalog data, crashes, recovery |

### 6. Generated Reference And Updates

| Page | What it covers |
| --- | --- |
| [Generated Database Reference](REFERENCE.md) | How to generate and read local Markdown / CSV catalogs and Mermaid graphs |
| [Local Catalog Browser](docs/catalog-browser.md) | Static search UI for the generated catalog index — runs locally from disk or [live on GitHub Pages](https://pagonia-land.github.io/Pagonia-Land/catalog/) (bring your own `search-index.json`) |
| [Validation Baseline](VALIDATION_BASELINE.md) | Current validation counts, known warning samples, expected behavior |
| [Local Snapshots](docs/snapshots.md) | Preserving extracted versions, comparing updates |
| [Database Update Playbook](UPDATE_PLAYBOOK.md) | Repeatable workflow for refreshing analysis after a new game version |
| [Catalog Coverage](docs/catalog-coverage.md) | Coverage check, known blind spots, and data health for the generated catalogs |
| [Quirks And Anomalies](docs/quirks-and-anomalies.md) | Empirical oddities worth keeping on the radar — magic GUIDs, unique pak layouts, `NoMVP.` prefix, version trends |
| [Mod Patch Schemas](schemas/mod-patches/README.md) | v0.1 JSON Schemas for `mod.yaml` manifests, patch files, collections, lockfiles |
| [Manager Report Schemas](schemas/manager/README.md) | v0.1 JSON Schemas for every `pagonia-manager --json` report (install, deploy, rollback, status, collection install, ...) |

Project metadata — [CONTRIBUTING.md](CONTRIBUTING.md), [NOTICE.md](NOTICE.md), [CHANGELOG.md](CHANGELOG.md), [LICENSE](LICENSE), [LICENSE-DOCS.md](LICENSE-DOCS.md) — sits at the repository root.

## Collaborate

- **Quick questions, feedback, modding chat:** [Pagonia Land Discord](https://discord.pagonia.land) — the lowest-friction way to reach the community and the maintainers.
- **Bug reports, compatibility issues, doc-improvement requests:** [issue tracker](https://github.com/pagonia-land/Pagonia-Land/issues). Templates for both ship under [`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/).

To contribute changes:

1. Fork the [repository](https://github.com/pagonia-land/Pagonia-Land/fork)
2. Apply your changes on a branch
3. Run `scripts\preflight.ps1` — builds all three tools (patcher + paker + manager), runs every test suite, runs `schema-validate` against every sandbox example, and drives the [manager walkthrough](sandbox/examples/manager-walkthrough/) end-to-end against a fixture game tree
4. Submit a [pull request](https://github.com/pagonia-land/Pagonia-Land/pulls) — describe what changed and why

GitHub Actions ([`.github/workflows/tools.yml`](.github/workflows/tools.yml)) runs the same preflight on every push and PR plus an AOT publish smoke that re-runs the walkthrough against the published binaries. Releases are cut manually via [`.github/workflows/release.yml`](.github/workflows/release.yml) — admin-only workflow that builds + signs + bundles all three tools for win-x64 / linux-x64 / osx-x64 / osx-arm64 plus a schemas-only zip. See [CONTRIBUTING.md](CONTRIBUTING.md) for the local-check workflow, the tool/schema sync rule, and the strict ban on committing anything from `game-gdb/`, `game-paks/`, `generated/`, or `snapshots/`.

## Contributing

Contributions are welcome — documentation fixes, new worked examples, validation checks, tool improvements, update notes after a game patch, even just reporting a confusing doc page or a workflow that didn't click for you.

- **Just evaluating the project?** Start with [REVIEWING.md](REVIEWING.md) — it lists where outside input is most valuable
- **Ready to contribute?** [CONTRIBUTING.md](CONTRIBUTING.md) covers local setup, the PR checklist, the tool/schema sync rule, and what not to commit
- **Reporting an issue?** Use one of the [issue templates](.github/ISSUE_TEMPLATE/) — bug report, docs improvement, or game update
- **Security concerns?** See [SECURITY.md](SECURITY.md) — please use a private channel
- **Code of conduct:** [Contributor Covenant 2.1](CODE_OF_CONDUCT.md) applies to all project spaces

A list of everyone who has contributed is on the [GitHub contributors graph](https://github.com/pagonia-land/Pagonia-Land/graphs/contributors).

## License

| Surface | License |
| --- | --- |
| Original scripts, schemas, and tooling source | [MIT](LICENSE) |
| Original documentation | [CC BY 4.0](LICENSE-DOCS.md) |
| Extracted game data (`game-gdb/`, `game-paks/`, `game-maps/`) | **Not covered** — proprietary, never commit. See [NOTICE.md](NOTICE.md) |
| Derived artifacts under `generated/` and `snapshots/` | Treated as derivatives of the above — local-only, git-ignored |

## Credits

[Pioneers of Pagonia](https://pioneersofpagonia.com/) is a trademark of [Envision Entertainment GmbH](https://www.envision-entertainment.de/). This is an independent modding community project — not affiliated with, endorsed by, or supported by EE.

Copyright (c) 2026 — [Pagonia Land](https://pagonia.land/) and [contributors](https://github.com/pagonia-land/Pagonia-Land/graphs/contributors). Maintained by [Lava Block](https://lava-block.com/).

Developed with assistance from [OpenAI Codex](https://openai.com/codex/) (initial scaffolding) and [Anthropic Claude](https://www.anthropic.com/claude) (ongoing development).
