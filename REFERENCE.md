# Generated Database Reference

This document describes the local generated reference catalog — its purpose, what it currently contains, how to regenerate it, and how to read each output category.

The actual catalog output is written to `generated/catalog/` and is git-ignored because it is derived from locally extracted game database files.

## Contents

- [Why A Separate Reference Document?](#why-a-separate-reference-document)
- [Current Local Snapshot](#current-local-snapshot) — current counts, regenerated per snapshot
- [Generate The Catalog](#generate-the-catalog) — the PowerShell commands
- [Local Outputs](#local-outputs) — the full file tree under `generated/catalog/`
- [Filtered Catalogs](#filtered-catalogs) — package / production / localization / objective / progression / asset / tools / notification / world / combat slices

**Catalog topics:**

- [Visual, Audio, And Asset References](#visual-audio-and-asset-references)
- [Tools And Editor Data](#tools-and-editor-data)
- [Notification And Narration Catalog](#notification-and-narration-catalog)
- [Terrain Props, Deposits, And MapGen](#terrain-props-deposits-and-mapgen)
- [NPC, Encounter, And Combat](#npc-encounter-and-combat)
- [Tech Tree, Unlocks, And Seasonal Gates](#tech-tree-unlocks-and-seasonal-gates)
- [Building Dependency Matrix](#building-dependency-matrix)
- [Unit Equipment Matrix](#unit-equipment-matrix)
- [Resource Catalogs](#resource-catalogs)
- [Localization Index](#localization-index)
- [Objective Flow Catalog](#objective-flow-catalog)
- [Production Chain Catalogs](#production-chain-catalogs)

**Visualisation and access:**

- [Visual Graphs](#visual-graphs) — Mermaid graph outputs
- [Searchable Catalog](#searchable-catalog) — the `tools/catalog-browser/` data source

**Reading caveats:**

- [Notes About Timing](#notes-about-timing) — recipe timing is split between recipe and building entities
- [Notes About System Catalogs](#notes-about-system-catalogs) — treasure-hunter / shrine / artifact specifics

## Why A Separate Reference Document?

The hand-written `docs/` pages explain concepts and modding workflows.

The generated catalog answers current-database questions such as:

- Which building uses which production recipes?
- Which building needs which construction goods, workers, recipes, inputs, outputs, and storage resources?
- Which unit needs which equipment, and which buildings or recipes can produce that equipment?
- Which resources are needed as construction costs?
- Where is a resource produced, consumed, stored, recruited with, or used as construction cost?
- Which complete production chains lead from raw resources to building or unit requirements?
- Which recipe consumes and produces which resources?
- Which units have recruitment costs?
- Which artifacts and combat-boost artifacts exist?
- Which treasure hunter recipes can target which resources?
- Which shrine buildings expose which abilities and mana recipes?
- Which notifications and narration dialogs communicate objectives, warnings, and map events?
- Which icon, prefab, mesh, texture, character kit, VFX, and audio event references are used?
- Which visual/audio components carry ambience, attachments, animation, UI state, and visual variant data?
- Which editor/Magmaview terrain, sediment, vegetation, texture, road, fluid, territory, and brush rows exist in `tools`?
- Which terrain props, deposits, generated deposit types, and map generation rows shape the world economy?
- Which entities, systems, and GUIDs can be searched in a local browser?
- Which dependencies exist between buildings, units, recipes, and resources?
- Which relationships can be visualized as graphs?

This is version-specific data. When the game database changes, regenerate the catalog locally.

## Current Local Snapshot

The latest local catalog generation produced (as of `1.4.0-12032+195221`, 1.4.0 "Quality of Life & Pagonia Editor Update" (QoL 8), official release (Steam stable branch), 2026-06-24):

| Catalog | Count |
| --- | ---: |
| Buildings | 343 |
| Building dependency rows | 343 |
| Building-production links | 95 |
| Recipes | 140 |
| Units | 177 |
| Unit equipment rows | 83 |
| Resources | 149 |
| Resource flow rows | 149 |
| Resource usage rows | 1,489 |
| Localization rows | 6,182 |
| Unique localization keys | 4,890 |
| Declared localization tags | 271 |
| Referenced localization-like keys | 5,911 |
| Keys not in local tag index | 5,829 |
| Objective flow rows | 814 |
| Objective flow rows with preconditions | 249 |
| Objective flow rows with rewards | 124 |
| Objective flow rows with notifications | 228 |
| Objective flow tech tree rows | 21 |
| Tech tree rows | 21 |
| Tech tree rows with objectives | 10 |
| Unlock gate rows | 33 |
| DLC unlock gate rows | 24 |
| Unlock reward rows | 146 |
| Unlock reward rows for buildings | 82 |
| Unlock reward rows for recipes | 49 |
| Unlock reward rows for recruitments | 42 |
| Unlock reward rows for tech tree groups | 3 |
| Seasonal rows | 256 |
| Seasonal rows with allowed seasons | 1 |
| Asset reference rows | 5,406 |
| Icon references | 1,978 |
| Prefab references | 995 |
| Mesh references | 811 |
| Texture references | 473 |
| Audio event references | 944 |
| Character kit references | 125 |
| Visual/audio component rows | 2,760 |
| Visual/audio components with assets | 1,859 |
| Visual/audio components with audio | 418 |
| Tools/editor rows | 29 |
| Tools terrain sediment rows | 4 |
| Tools vegetation group rows | 3 |
| Tools vegetation rows | 10 |
| Tools texture rows | 7 |
| Tools globals and brush rows | 5 |
| Notification and narration rows | 496 |
| Notification rows | 153 |
| Narration dialog rows | 336 |
| Referenced notification and narration rows | 417 |
| Unreferenced notification and narration rows | 78 |
| Terrain props | 737 |
| Terrain props with blocking | 505 |
| Terrain props with sediment rules | 18 |
| Deposit resource types | 45 |
| Deposits | 175 |
| Deposits with harvest resources | 82 |
| Growing or regrowing deposits | 54 |
| Generated deposit types | 7 |
| MapGen rows | 224 |
| MapGen rows with deposits | 8 |
| NPC unit rows | 177 |
| NPC base rows | 61 |
| Encounter and combat rows | 734 |
| Faction rows | 95 |
| NPC bosses | 25 |
| Raid-capable NPC units | 57 |
| Custom raid NPC bases | 10 |
| NPC unit drop rows | 13 |
| Infection-related encounter rows | 13 |
| Production chains | 643 |
| Artifacts and artifact-like resources | 33 |
| Treasure hunter recipes | 9 |
| Treasure hunter targets | 65 |
| Treasure areas | 2 |
| Shrine buildings | 6 |
| Shrine abilities | 13 |
| Shrine recipes | 35 |
| Production graph edges | 277 |
| Production chain graph edges | 450 |
| Search index items | 26,882 |
| Package filters | 4 |
| Buildings with recipes | 26 |
| Buildings with dependencies | 146 |
| Buildings with storage | 131 |
| Recipes with inputs or outputs | 98 |
| Units with recruitment cost | 51 |
| Unit equipment rows with producers | 66 |
| Unit equipment rows without producers | 17 |
| Resources produced | 96 |
| Resources consumed | 47 |
| Resources stored | 118 |
| Production chains to buildings | 240 |
| Production chains to units | 368 |
| Production chains to shrines | 35 |

These numbers describe the currently extracted local database and should be refreshed after each game update.

## Generate The Catalog

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Recommended full refresh:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## Local Outputs

Expected local files:

```text
generated/catalog/
├── README.md
├── buildings.md
├── buildings.csv
├── building-dependency-matrix.md
├── building-dependency-matrix.csv
├── building-production.md
├── building-production.csv
├── recipes.md
├── recipes.csv
├── units.md
├── units.csv
├── unit-equipment-matrix.md
├── unit-equipment-matrix.csv
├── resources.md
├── resources.csv
├── resource-flow.md
├── resource-flow.csv
├── resource-usage.md
├── resource-usage.csv
├── localization-index.md
├── localization-index.csv
├── objective-flow.md
├── objective-flow.csv
├── tech-tree.md
├── tech-tree.csv
├── unlock-gates.md
├── unlock-gates.csv
├── unlock-rewards.md
├── unlock-rewards.csv
├── seasonal-gates.md
├── seasonal-gates.csv
├── asset-references.md
├── asset-references.csv
├── visual-audio-components.md
├── visual-audio-components.csv
├── tools-editor.md
├── tools-editor.csv
├── notification-narration.md
├── notification-narration.csv
├── terrain-props.md
├── terrain-props.csv
├── deposit-resource-types.md
├── deposit-resource-types.csv
├── deposits.md
├── deposits.csv
├── mapgen.md
├── mapgen.csv
├── npc-units.md
├── npc-units.csv
├── npc-bases.md
├── npc-bases.csv
├── encounter-combat.md
├── encounter-combat.csv
├── factions.md
├── factions.csv
├── production-chains.md
├── production-chains.csv
├── production-graph.md
├── production-graph.mmd
├── production-chain-graph.md
├── production-chain-graph.mmd
├── search-index.json
├── systems/
│   ├── artifacts.md
│   ├── artifacts.csv
│   ├── treasure-hunter-recipes.md
│   ├── treasure-hunter-recipes.csv
│   ├── treasure-hunter-targets.md
│   ├── treasure-hunter-targets.csv
│   ├── treasure-areas.md
│   ├── treasure-areas.csv
│   ├── shrine-buildings.md
│   ├── shrine-buildings.csv
│   ├── shrine-abilities.md
│   ├── shrine-abilities.csv
│   ├── shrine-recipes.md
│   ├── shrine-recipes.csv
│   ├── artifact-treasure-shrine-graph.md
│   └── artifact-treasure-shrine-graph.mmd
└── filters/
    ├── package-summary.md
    ├── packages/
    │   ├── core/
    │   ├── decorations1/
    │   ├── dlc1/
    │   └── tools/
    ├── localization/
    │   ├── declared-tags.md
    │   ├── referenced-keys.md
    │   └── not-in-local-tag-index.md
    ├── objectives/
    │   ├── with-preconditions.md
    │   ├── with-rewards.md
    │   ├── with-notifications.md
    │   └── tech-tree.md
    ├── progression/
    │   ├── tech-tree-with-objectives.md
    │   ├── dlc-unlock-gates.md
    │   ├── unlock-rewards-buildings.md
    │   ├── unlock-rewards-recipes.md
    │   ├── unlock-rewards-recruitments.md
    │   ├── unlock-rewards-tech-tree.md
    │   └── seasonal-with-allowed-seasons.md
    ├── assets/
    │   ├── icons.md
    │   ├── prefabs.md
    │   ├── meshes.md
    │   ├── textures.md
    │   ├── audio-events.md
    │   ├── character-kits.md
    │   ├── visual-components-with-assets.md
    │   └── visual-components-with-audio.md
    ├── tools/
    │   ├── terrain-sediments.md
    │   ├── vegetation-groups.md
    │   ├── vegetation.md
    │   ├── textures.md
    │   └── globals-and-brushes.md
    ├── notifications/
    │   ├── notifications.md
    │   ├── narration-dialogs.md
    │   ├── referenced.md
    │   └── unreferenced.md
    ├── world/
    │   ├── terrain-props-with-blocking.md
    │   ├── terrain-props-with-sediment-rules.md
    │   ├── deposits-with-harvest-resources.md
    │   ├── deposits-growing-or-regrowing.md
    │   ├── generated-deposit-types.md
    │   └── mapgen-with-deposits.md
    ├── combat/
    │   ├── npc-units.md
    │   ├── npc-bases.md
    │   ├── bosses.md
    │   ├── raid-capable-units.md
    │   ├── custom-raid-bases.md
    │   ├── drop-parameters.md
    │   ├── infection.md
    │   └── encounter-target-tags.md
    └── production/
        ├── buildings-with-recipes.md
        ├── buildings-with-gather-outputs.md
        ├── buildings-with-storage.md
        ├── buildings-with-dependencies.md
        ├── recipes-with-input-output.md
        ├── units-with-recruitment-cost.md
        ├── unit-equipment-with-producers.md
        ├── unit-equipment-without-producers.md
        ├── resources-produced.md
        ├── resources-consumed.md
        ├── resources-stored.md
        ├── production-chains-to-buildings.md
        ├── production-chains-to-units.md
        └── production-chains-to-shrines.md
```

Each catalog `.md` under `filters/` has a sibling `.csv` with the same stem (the generator emits both), just as the top-level catalogs do above; only the `.md` files are listed here to keep the tree readable.

## Filtered Catalogs

The generator creates several kinds of filtered views:

- Package filters under `generated/catalog/filters/packages/<package>/`
  Each package gets its own buildings, building dependency matrix, production, recipes, units, unit equipment matrix, resources, resource flow, resource usage, localization index, objective flow, tech tree, unlock gates, unlock rewards, seasonal gates, asset references, visual/audio components, tools/editor rows, notification/narration catalog, terrain props, deposits, mapgen, NPC units, NPC bases, encounter/combat rows, factions, production chains, artifacts, treasure hunter recipes, shrine abilities, shrine recipes, and production graph files.
- Production-focused filters under `generated/catalog/filters/production/`
  These are smaller lists for common modding questions, such as "which buildings produce something?" or "which recipes actually list inputs or outputs?"
- Localization-focused filters under `generated/catalog/filters/localization/`
  These split declared `LocaParkplatz` tags, referenced text-like keys, and references that are not present in the local tag index.
- Objective-focused filters under `generated/catalog/filters/objectives/`
  These split objective rows with prerequisites, rewards, notifications, and tech tree unlock rows.
- Progression-focused filters under `generated/catalog/filters/progression/`
  These split tech tree rows with objectives, DLC unlock gates, unlock rewards by target type, and seasonal rows with explicit allowed seasons.
- Asset-focused filters under `generated/catalog/filters/assets/`
  These split icons, prefabs, meshes, textures, audio events, character kits, visual components with assets, and visual components with audio.
- Tools/editor-focused filters under `generated/catalog/filters/tools/`
  These split Magmaview/editor terrain sediments, vegetation groups, vegetation rows, texture rows, and global terrain/fluid/road/territory/brush rows.
- Notification-focused filters under `generated/catalog/filters/notifications/`
  These split notification rows, narration dialog rows, referenced rows, and rows without detected reverse references.
- World-focused filters under `generated/catalog/filters/world/`
  These split terrain props with blocking or sediment rules, harvestable/growing deposits, generated deposit types, and mapgen rows with deposit references.
- Combat-focused filters under `generated/catalog/filters/combat/`
  These split NPC units, NPC bases, bosses, raid-capable units, custom raid bases, drop parameters, infection rows, and encounter target tag rows.

Use the full top-level files when you want the whole current database. Use the filtered files when you are tracing a specific package, production system, progression system, asset category, tools/editor area, world system, or combat system.

## Visual, Audio, And Asset References

The asset catalogs connect database rows to icon paths, prefabs, meshes, textures, character kits, VFX paths, audio events, ambience rows, animation rows, attachment data, and visual state components.

Generated files:

```text
generated/catalog/asset-references.md
generated/catalog/asset-references.csv
generated/catalog/visual-audio-components.md
generated/catalog/visual-audio-components.csv
generated/catalog/filters/assets/
generated/catalog/filters/packages/<package>/asset-references.md
generated/catalog/filters/packages/<package>/visual-audio-components.md
```

Useful asset filters:

| Filter | Purpose |
| --- | --- |
| `filters/assets/icons` | Icon path references for UI-facing database rows |
| `filters/assets/prefabs` | Prefab path references |
| `filters/assets/meshes` | Mesh path references |
| `filters/assets/textures` | Texture path references |
| `filters/assets/audio-events` | Audio event and audio-like references |
| `filters/assets/character-kits` | Character kit references |
| `filters/assets/visual-components-with-assets` | Visual/audio component rows with non-audio asset references |
| `filters/assets/visual-components-with-audio` | Visual/audio component rows with audio references |

See [docs/systems/visual-audio-assets.md](docs/systems/visual-audio-assets.md) for reading notes, examples, and modding cautions.

## Tools And Editor Data

The tools/editor catalog focuses on `game-gdb/tools/gdb/magmaview.gd.xml`. It extracts editor-facing terrain, fluid, road, territory, brush, sediment, vegetation group, texture, and vegetation rows.

Generated files:

```text
generated/catalog/tools-editor.md
generated/catalog/tools-editor.csv
generated/catalog/filters/tools/
generated/catalog/filters/packages/tools/tools-editor.md
```

Useful tools filters:

| Filter | Purpose |
| --- | --- |
| `filters/tools/terrain-sediments` | `TerrainSediment` rows with texture links, vegetation group links, and terrain tuning values |
| `filters/tools/vegetation-groups` | `VisVegetationGroup` rows with weighted vegetation and cluster group references |
| `filters/tools/vegetation` | `VisVegetation` rows with plant texture references and visual tuning values |
| `filters/tools/textures` | `VisTexture` rows with diffuse, normal, parameter, and height-scale data |
| `filters/tools/globals-and-brushes` | Global terrain/fluid/road/territory rows and editor brush references |

See [docs/tools-editor.md](docs/tools-editor.md) for reading notes and modding cautions.

## Notification And Narration Catalog

The notification and narration catalog shows player-facing event feedback.

Useful files:

```text
generated/catalog/notification-narration.md
generated/catalog/notification-narration.csv
generated/catalog/filters/notifications/notifications.md
generated/catalog/filters/notifications/narration-dialogs.md
generated/catalog/filters/notifications/referenced.md
generated/catalog/filters/notifications/unreferenced.md
generated/catalog/filters/packages/<package>/notification-narration.md
```

Important columns:

| Column group | Meaning |
| --- | --- |
| Identity | `Kind`, `Name`, `Entity`, `Guid`, `Package`, and `File` |
| Text | `MessageKey`, `TooltipKey`, `CoopMessageKey`, `DialogLines`, and `LocalizationKeys` |
| Dialog | `DialogType`, `DefaultSpeaker`, `LineSpeakers`, and `Camera` |
| Notification behavior | `Flags`, `Icon`, `LifeTime`, `Sound`, `Music`, `Announcement`, and filter category fields |
| Reverse references | `ReferenceCount` and `ReferencedBy` show detected source fields/entities that point to the row |

This catalog is especially useful after changing objectives, encounters, map events, or DLC story flow. It helps check whether the player-facing text, icon, and sound still match the changed behavior.

## Terrain Props, Deposits, And MapGen

The world catalogs connect harvestable resources, world props, terrain placement, and map generation rules.

Useful files:

```text
generated/catalog/terrain-props.md
generated/catalog/deposit-resource-types.md
generated/catalog/deposits.md
generated/catalog/mapgen.md
generated/catalog/filters/world/
generated/catalog/filters/packages/<package>/terrain-props.md
generated/catalog/filters/packages/<package>/deposit-resource-types.md
generated/catalog/filters/packages/<package>/deposits.md
generated/catalog/filters/packages/<package>/mapgen.md
```

They are useful when asking:

- where a harvestable resource comes from
- which deposits grow or regrow
- which deposits are visible on the minimap
- which terrain props use blocking or sediment rules
- which mapgen rows reference deposits, buildings, resources, or tags
- whether a DLC added new deposits or generated resource types

## NPC, Encounter, And Combat

The NPC and combat catalogs connect hostile units, animals, bosses, raids, NPC bases, infected areas, factions, and encounter components.

Generated files:

```text
generated/catalog/npc-units.md
generated/catalog/npc-units.csv
generated/catalog/npc-bases.md
generated/catalog/npc-bases.csv
generated/catalog/encounter-combat.md
generated/catalog/encounter-combat.csv
generated/catalog/factions.md
generated/catalog/factions.csv
generated/catalog/filters/combat/
generated/catalog/filters/packages/<package>/npc-units.md
generated/catalog/filters/packages/<package>/npc-bases.md
generated/catalog/filters/packages/<package>/encounter-combat.md
generated/catalog/filters/packages/<package>/factions.md
```

The high-level files are meant for lookup. The `encounter-combat` file is the lower-level component view and is useful when searching for specific components such as `UnitRaidParameters`, `AspectPatrollingNPCBoss`, `BuildingEncounterParameters`, `EncounterTargetTag`, or infection-related behavior.

Useful combat filters:

| Filter | Purpose |
| --- | --- |
| `filters/combat/bosses` | Units with `AspectPatrollingNPCBoss` |
| `filters/combat/raid-capable-units` | Units with `UnitRaidParameters` |
| `filters/combat/custom-raid-bases` | Bases with `AspectNPCCustomRaid` |
| `filters/combat/drop-parameters` | Units with `UnitDropParameters` |
| `filters/combat/infection` | Infection-related components and values |
| `filters/combat/encounter-target-tags` | Encounter target tag declarations and references |

See [docs/systems/npc-encounter-combat.md](docs/systems/npc-encounter-combat.md) for reading notes and modding workflow.

## Tech Tree, Unlocks, And Seasonal Gates

The progression catalogs connect tech tree tier groups, explicit `NeedsUnlock` gates, objective unlock rewards, DLC availability, and seasonal rows.

Generated files:

```text
generated/catalog/tech-tree.md
generated/catalog/tech-tree.csv
generated/catalog/unlock-gates.md
generated/catalog/unlock-gates.csv
generated/catalog/unlock-rewards.md
generated/catalog/unlock-rewards.csv
generated/catalog/seasonal-gates.md
generated/catalog/seasonal-gates.csv
generated/catalog/filters/progression/
generated/catalog/filters/packages/<package>/tech-tree.md
generated/catalog/filters/packages/<package>/unlock-gates.md
generated/catalog/filters/packages/<package>/unlock-rewards.md
generated/catalog/filters/packages/<package>/seasonal-gates.md
```

Useful progression filters:

| Filter | Purpose |
| --- | --- |
| `filters/progression/tech-tree-with-objectives` | Tech tree groups with primary or alternative unlock objectives |
| `filters/progression/dlc-unlock-gates` | `NeedsUnlock` rows that reference a DLC entity |
| `filters/progression/unlock-rewards-buildings` | Objective reward rows that unlock buildings |
| `filters/progression/unlock-rewards-recipes` | Objective reward rows that unlock gatherer farm recipes or production recipes |
| `filters/progression/unlock-rewards-recruitments` | Objective reward rows that unlock recruitments or units |
| `filters/progression/unlock-rewards-tech-tree` | Objective reward rows that unlock tech tree groups |
| `filters/progression/seasonal-with-allowed-seasons` | Seasonal rows with explicit `AllowedSeasons` values |

See [docs/systems/progression-unlocks-seasonal.md](docs/systems/progression-unlocks-seasonal.md) for reading notes and modding workflow.

## Building Dependency Matrix

The building dependency matrix is a one-row-per-building overview for modders who want to understand a building at a glance.

| Column group | Meaning |
| --- | --- |
| Construction | `ConstructionCosts` and `Builder` from buildup data |
| Production | `ProductionWorker`, `Recipes`, `RecipeIdentifiers`, `RecipeInputs`, `RecipeOutputs`, timing hints, and work-loop hints |
| Gathering | `GatherOutputs` from gatherer data |
| Storage | `StorageResources` from explicit pile/storage resource references |
| Summary | `DependencyResources`, `ProvidedResources`, and `HasProduction`/`HasGathering`/`HasStorage` flags |

Useful files:

```text
generated/catalog/building-dependency-matrix.md
generated/catalog/building-dependency-matrix.csv
generated/catalog/filters/production/buildings-with-dependencies.md
generated/catalog/filters/production/buildings-with-storage.md
generated/catalog/filters/packages/<package>/building-dependency-matrix.md
```

This matrix is intentionally redundant with several smaller catalogs. The goal is fast lookup: "what does this building need, who works there, what recipes does it run, and what does it provide?"

## Unit Equipment Matrix

The unit equipment matrix expands unit recruitment costs into one row per required equipment resource.

| Column group | Meaning |
| --- | --- |
| Unit | Unit name, tags, source recruitable unit, and unit GUID |
| Equipment | Required resource, amount, category, carry type, and resource GUID |
| Producers | Known producer buildings, producer recipes, and `Building -> Recipe` links |
| Recipe context | Inputs and outputs of the producer recipes |
| Chain context | Sample generated production chains ending in the unit recruitment cost |

Useful files:

```text
generated/catalog/unit-equipment-matrix.md
generated/catalog/unit-equipment-matrix.csv
generated/catalog/filters/production/unit-equipment-with-producers.md
generated/catalog/filters/production/unit-equipment-without-producers.md
generated/catalog/filters/packages/<package>/unit-equipment-matrix.md
```

Rows without producers are not automatically invalid. Some equipment may be placeholder, scenario-provided, abstract, or produced through data not yet covered by the catalog generator.

## Resource Catalogs

The generated resource catalogs answer the common resource-flow questions:

| Catalog | Purpose |
| --- | --- |
| `resources` | Resource definitions, categories, carry types, UI hints, asset paths, and value hints |
| `resource-flow` | One row per resource summarizing producers, consumers, construction costs, recruitment costs, storage, and treasure targets |
| `resource-usage` | One row per detected usage relationship, useful for detailed tracing |
| `filters/production/resources-produced` | Resources with at least one known recipe or gather output producer |
| `filters/production/resources-consumed` | Resources with at least one known recipe or shrine recipe consumer |
| `filters/production/resources-stored` | Resources with at least one explicit building pile/storage reference |

Resource flow data currently combines:

- `ProductionRecipe` input/output steps
- `AspectGatherer` outputs
- `AspectBuildup` construction costs
- `RecruitmentCost` resource costs
- `AspectPiles` explicit pile/storage references
- `ShrineRecipe` input resources
- `TreasureHunterRecipe` target resources

Storage rows are based on explicit `AspectPiles` resource references. Generic storage rules without explicit resource lists are not expanded to every possible accepted resource.

## Localization Index

The localization index maps technical UI-facing keys back to the entities and XML fields that use them.

Useful files:

```text
generated/catalog/localization-index.md
generated/catalog/localization-index.csv
generated/catalog/filters/localization/declared-tags.md
generated/catalog/filters/localization/referenced-keys.md
generated/catalog/filters/localization/not-in-local-tag-index.md
generated/catalog/filters/packages/<package>/localization-index.md
```

The generator currently detects declared tags from `LocaParkplatz` entities and likely references from name, description, title, subtitle, tooltip, hint, message, notification, and related fields.

Important: `LocaParkplatz` appears to be a tag or placeholder index, not a complete translation database. Rows in `not-in-local-tag-index` are research hints, not validation errors.

See [docs/localization.md](docs/localization.md) for the modding workflow and limitations.

## Objective Flow Catalog

The objective flow catalog maps objective-like and unlock-like rows to prerequisites, requirements, rewards, notifications, localization keys, and referenced buildings, units, resources, and points of interest.

Useful files:

```text
generated/catalog/objective-flow.md
generated/catalog/objective-flow.csv
generated/catalog/filters/objectives/with-preconditions.md
generated/catalog/filters/objectives/with-rewards.md
generated/catalog/filters/objectives/with-notifications.md
generated/catalog/filters/objectives/tech-tree.md
generated/catalog/filters/packages/<package>/objective-flow.md
```

The catalog is a navigation aid for map, campaign, and unlock research. It does not prove final in-game flow by itself, because objective behavior can depend on engine logic, map state, and scenario timing.

## Production Chain Catalogs

The production chain catalog derives longer paths from explicit recipe input/output links and known end-use references.

| Catalog | Purpose |
| --- | --- |
| `production-chains` | Full chain rows from a starting resource through recipes to a building, unit, or shrine recipe end use |
| `production-chain-graph` | Mermaid graph for the same chain relationships, limited by `-GraphEdgeLimit` |
| `filters/production/production-chains-to-buildings` | Chains that end in building construction costs |
| `filters/production/production-chains-to-units` | Chains that end in unit recruitment costs |
| `filters/production/production-chains-to-shrines` | Chains that end in shrine recipe inputs |

Example shape:

```text
Branch -> Tool3CopperRecipe -> Hammer -> RecruitmentCost: Builder (1)
Coal -> IronIngotRecipe [Smelting Works] -> Iron Ingot -> HammerIronRecipe [Toolsmith] -> Hammer -> RecruitmentCost: Builder (1)
```

The chain generator starts from resources that are gathered directly or have no known recipe producer, then follows `ProductionRecipe` input/output steps until it reaches a known end use. It is designed as a navigation aid, not a final economic simulation.

## Visual Graphs

The catalog generator emits Mermaid graph files. GitHub can render Mermaid diagrams inside Markdown files.

Current graph outputs:

- `generated/catalog/production-graph.md`
- `generated/catalog/production-chain-graph.md`
- `generated/catalog/systems/artifact-treasure-shrine-graph.md`

For larger or more complex graphs, Graphviz is also a good option for future tooling. Mermaid is used first because it works directly in GitHub Markdown and does not require a separate renderer for basic viewing.

## Searchable Catalog

The generator also writes:

```text
generated/catalog/search-index.json
```

This file powers the local static browser:

```text
tools/catalog-browser/index.html
```

See [docs/catalog-browser.md](docs/catalog-browser.md) for usage notes.

## Notes About Timing

Production timing is not always stored directly on the recipe entity. In many cases, the recipe contains animation loop counts, while the relevant work timing appears on the building aspect, such as `AspectProduction/Efficiency/TimeOfOptimalWorkStep`.

The generated catalog therefore reports:

- recipe work loop counts, when found
- building optimal work step time, when found

Treat these as database-derived timing hints, not final guaranteed in-game throughput.

## Notes About System Catalogs

Treasure hunter recipes describe target resource lists rather than deterministic production outputs.

Shrine recipes use a shrine-specific step model and should not be treated as normal `ProductionRecipe` rows.

The artifact catalog intentionally includes artifact-like resources. Rows with `CombatBoost` or `CombatWeight` are combat-boost artifacts. Rows in the `Artifacts` category are normal artifact-category resources.
