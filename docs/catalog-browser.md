# 🔍 Local Catalog Browser

The local catalog browser is a static search UI for generated database catalog data.

It does not commit extracted game files or generated database content. The browser app is committed, but the searchable index is generated locally from your own extracted XML files.

## Generate The Search Index

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

This writes:

```text
generated/catalog/search-index.json
```

The file is ignored by Git because it is derived from extracted game database files.

## Open The Browser

Two ways:

- **Live (recommended for casual browsing):** <https://pagonia-land.github.io/Pagonia-Land/catalog/> — the same `index.html` deployed via GitHub Pages, alongside the docs site at <https://pagonia-land.github.io/Pagonia-Land/>. Bring your own locally-generated `search-index.json` through the file picker. The repository never publishes the catalog data itself, so the live page starts empty until you load a file. (Your picked file is held only in memory — on the public Pages page you re-pick it after a reload, since the index data is never stored.)
- **Local from disk:**

```text
tools/catalog-browser/index.html
```

The browser auto-loads `generated/catalog/search-index.json` on every page visit and on every F5 / Ctrl-R refresh — once the default loads successfully, a boolean "auto-load worked" flag is remembered in `localStorage` and subsequent visits are silent. (Only that flag is stored, never the index contents; this path applies to the local repo / private-hosting variant that ships a default index.) No need to click anything between refreshes while you're navigating the catalog.

The auto-load tries two paths, first-hit wins:

1. `../../generated/catalog/search-index.json` — the standard local repo layout.
2. `./search-index.json` — a sibling of `index.html`. Used by the GitHub Pages deployment (see below) and by anyone hosting the browser alongside a single index file.

If both fail (some browsers block `fetch()` over the `file://` protocol; the snapshot may have moved or been removed), use the file picker to select `generated/catalog/search-index.json` (or any other index file).

The file picker is also useful for inspecting an alternative index — e.g. a `snapshots/<version>/generated/catalog/search-index.json` from an older game version.

## Hosting On GitHub Pages

The browser is a single static `index.html`. The repository ships a workflow at [`.github/workflows/pages.yml`](../.github/workflows/pages.yml) that builds the MkDocs docs site and combines `tools/catalog-browser/` at the `/catalog/` subpath on every push that touches either surface. Setup once:

1. **Repo Settings → Pages → Source: "GitHub Actions"** — switches Pages from the default branch-based mode to the workflow-driven mode the YAML expects.
2. **Push to main / master.** The pages workflow builds the docs site, combines `tools/catalog-browser/` at `/catalog/`, and deploys the result. The Pages URL appears in the workflow run's `deployment` step output. For this repository the catalog browser is at <https://pagonia-land.github.io/Pagonia-Land/catalog/>; forks land at `https://<your-user>.github.io/<your-fork>/catalog/`.
3. (Optional) **Custom domain:** point a domain at `<owner>.github.io` via DNS and set it in Settings → Pages. The next deploy will surface the custom URL.

**Important — what does NOT ship to Pages:** `generated/catalog/search-index.json` is gitignored proprietary game data and is excluded from the deploy. Visitors load their own locally-generated index through the file picker. Because no default index ships to Pages, the auto-load flag never gets set there — a reload of the public page starts empty again and you re-pick your file. The browser's auto-load second-try path (`./search-index.json` sibling) is only useful for private hosting setups that have explicit permission to ship the catalog data — the public Pages deployment intentionally does not.

See [NOTICE.md](../NOTICE.md) for the rules around extracted and generated artifacts.

## What You Can Search

The index currently includes:

- entities
- buildings
- building dependencies
- building-production links
- recipes
- units
- unit equipment
- resources
- resource flow
- resource usage
- localization
- objective flow
- tech tree
- unlock gates
- unlock rewards
- seasonal gates
- asset references
- visual/audio components
- tools/editor data
- notifications and narration
- terrain props
- deposit resource types
- deposits
- map generation
- NPC units
- NPC bases
- encounter and combat components
- factions
- production chains
- artifacts
- treasure hunter recipes
- treasure hunter targets
- treasure areas
- shrine buildings
- shrine abilities
- shrine recipes

Useful searches:

```text
TreasureHunterArtifacts
Artifact 1
ShrineAbility
Sanctuary
Wine
resource-flow
production-chain
building-dependency
unit-equipment
localization
IconicBuilding Name Sanctuary
objective-flow
tech-tree
unlock-gate
unlock-reward
seasonal-gate
asset-reference
visual-audio-component
tools-editor
TerrainSediment
VisVegetationGroup
BulldozerTexture
prefab.json
event:/ui
icon_build
characterkit
NeedsUnlock
TechtreeGroupTier2
ObjectiveStartRewards
Seasonal
notification-narration
DLC1CM1-D
Notification - Owner
terrain-prop
deposit
mapgen
MapGenDepositDistributionGroup
RaspberriesBushDeposit
npc-unit
encounter-combat
AspectPatrollingNPCBoss
UnitRaidParameters
EncounterTargetTag
Witch
Scavenger Boss
TechTreeTierGroup
UnlockObjectiveT2ProduceCopper
StoragePile
Backpack
c0de7454-db89-4068-b32d-0a4213e2971e
game-gdb/core/gdb/resources.gd.xml
```

## Filters

The browser exposes six filters that combine as AND.

| Filter | What it does |
| --- | --- |
| **Search** | Free-text query against name, GUID, file, components, terms, and every field value. Space-separated tokens are AND-composed. |
| **Type** | Narrow results to one catalog kind, e.g. `building`, `recipe`, `artifact`, `shrine-ability`, `npc-unit`, `encounter-combat`, `tech-tree`, `unlock-reward`, `asset-reference`, `visual-audio-component`, `tools-editor`. |
| **Package** | Source package: `core`, `dlc1`, `decorations1`, `tools`. |
| **Scope** | "Main GDB only" excludes campaign maps and map-specific files; "Campaigns & map-specific" keeps just those. Useful for "show me only core buildings without the campaign variants" — pick `Package = core`, `Type = building`, `Scope = Main GDB only`. |
| **Category** | The in-game UI category an entity belongs to, e.g. `Storage & Trade`, `Food Supply`, `Subsurface Mining`, `Equipment`. Cascades from Type: when you pick Type, the Category dropdown narrows to the categories that actually exist for that type, instead of mixing building categories with objective categories. |
| **UI Group** | The finer-grained UI-grouping inside a category, e.g. `Storage` and `Trade` within `Storage & Trade`. Cascades from Category: picking a Category narrows UI Group to the groups that exist inside that category. |

Each result card and the detail panel surface the Category and UI Group as pills so the classification is visible at a glance — pick `Storage & Trade` and the Big Storage entry shows `Storage` next to `Storage & Trade` directly in the card. The detail panel still lists every field of the selected item below the pill row.

**Worked example.** "Show me all main-game-only storage buildings":

1. Type = `building`
2. Package = `core`
3. Scope = `Main GDB only`
4. Category = `Storage & Trade`

The dropdowns narrow down as you go (UI Group ends up offering `Storage` and `Trade` only). The status bar shows how many items match.

## Click-Through Navigation Between Entities

Field values that reference other catalog entries are rendered as **clickable links** in the detail panel. Selecting a link jumps the panel straight to the referenced entity — no extra search needed.

What's linked today:

| Item type | Field | Links to |
| --- | --- | --- |
| `building` | `ConstructionCosts`, `GatherOutputs` | resources |
| `building` | `Builder` | the worker unit |
| `building` | `ProductionRecipes` | recipes |
| `unit` / `npc-unit` | `RecruitmentCosts` | resources |
| `unit` | `SourceRecruitableUnit` | base unit |
| `recipe` | `Inputs`, `Outputs` | resources |
| `tech-tree` | `Buildings`, `Units`, `PrimaryObjective`, `AlternativeObjectives` | buildings, units, objectives |
| `unlock-reward` | `UnlockedBuildings`, `UnlockedRecruitments`, `UnlockedProductionRecipes`, `UnlockShrineAbilities`, `UnlockTechTreeTierGroups`, `ModifiedNPCBases`, `KilledUnits`, `OtherReferences` | the unlocked / referenced entity |
| `objective-flow` | `PreconditionObjectives`, `SkipObjectives`, `FailObjectives`, `ResourceRefs`, `BuildingRefs`, `UnitRefs`, `StartRewards`, `Rewards`, `Notifications` | the chained entity |
| `building` | `DepositResourceType` | resources (deposits) |
| `deposit` | resource type + producing buildings | resources, buildings |
| `treasure-hunter-recipe` / `shrine-recipe` | recipe inputs/outputs | resources |
| `shrine-building` | shrine recipes + abilities | recipes, abilities |
| `mapgen` | terrain/deposit references | the referenced entity |

The table lists the most common cases; the authoritative set is the `LINKABLE_FIELDS` registry in the browser's `index.html`. On top of the per-field registry, **any GUID anywhere in any field becomes a link** when that GUID maps to a known entity in the loaded catalog — so fields outside the registry (Components, hand-rolled `OtherReferences`, ad-hoc strings that happen to contain a GUID) still navigate.

If two items share a display name (which happens e.g. when a building's name is also used by a `building-production` row), the link picks the first hit by preferred-type order and shows a small `*` marker — hover the marker for the explanation.

## Referenced By

The detail panel also renders an inverse "Referenced by" block below the regular fields. It lists every other catalog entry that points at the selected item, grouped by source type with counts: e.g. picking the resource **Stone Block** surfaces "buildings (47) · recipes (3) · objective-flow (2)" with one clickable pill per referring entity. Every backlink is rendered — popular resources stay readable through the detail panel's own scrollbar rather than a per-group cap.

The inverse index is built from the same forward-link registry plus GUID auto-detect, so anything that produces a forward link in the detail panel also produces a backlink on the target. This is how you walk from a resource to "who consumes me?" without having to re-query.

Generic lookup row types — `resource-usage`, `building-production`, `production-chain`, `resource-flow`, `unit-equipment`, plus the raw `entity` / `asset-reference` / `visual-audio-component` / `building-dependency` shadows — are deliberately **not** surfaced as backlink sources. They duplicate information that the typed groups already carry (a `resource-usage` row with `UsageType=ConstructionCost` says the same thing as the building's `ConstructionCosts` forward link); displaying both produced confusing "one pill + N more" lists where the duplicates had been deduped away. The cross-coverage is verified by [`scripts/validate-catalog-browser-coverage.py`](../scripts/validate-catalog-browser-coverage.py) — it loads the current `generated/catalog/search-index.json` and confirms every relationship encoded by a lookup row is also reachable through a typed forward link. Run it after any change to the registry or to `scripts/generate_catalog.ps1`.

## Back / Forward Navigation And Shareable URLs

Every selection — whether through a result-list click, a forward link in a field value, or a backlink pill — updates the URL hash to `#guid=<entity-guid>`. That means:

- **The browser's Back and Forward buttons work natively.** Click through ConstructionCosts → resource → its backlinks → another building, then Back walks the chain in reverse.
- **A small `← Back` button** sits next to the title in the detail panel for the same job, in case the browser chrome isn't visible (full-screen, embedded view, touch device). It calls `history.back()` under the hood.
- **URLs are shareable.** Copy the address bar and send it to someone — they land on the same entity if they've generated the catalog at a snapshot that still contains the GUID. Cross-version drift is possible (a GUID can be removed between game versions); when the loaded index doesn't know the GUID, the browser keeps the current selection and the URL as a hint so you can swap in a different snapshot.
- **The fresh-load path also reads `#guid=...`** — open `tools/catalog-browser/index.html#guid=<some-guid>` and the entity is selected after the index loads.

## Where Does This Become Available?

The detail panel of a building, unit, or recipe shows a **Where to find this** section that lists every tech-tree tier and objective unlock that grants the entity. This is computed on the fly from the catalog — no separate index needed.

For example, picking **Big Storage** surfaces:

- **Tech tree tier**: `TechtreeGroupTier3Cloth`, `TechtreeGroupTier3TreeTrunks` — two different research paths grant it.
- **Objective unlocks** (when applicable): campaign objectives like `CM2 O Construct Trading Post` that include the entity as a reward.

Each entry is a clickable pill — selecting one navigates the detail panel to that tier or reward, so you can walk the unlock chain (building → tier that grants it → other entities that tier grants) without leaving the browser.

Coverage notes:

- The cross-reference uses **display names** as the join key (the catalog encodes tech-tree and unlock-reward target lists as `Building=Name; Building=Name` strings). If two entities share a display name, both will appear; pick the one whose package / file matches your context.
- Resources aren't directly reached by this section — they enter the picture indirectly via the buildings that produce or gather them. Use the `building-production` rows to trace from a resource to its producers. (Those rows are hidden as *Referenced-By* backlink sources to avoid duplicate pills, but they still appear in normal search results — which is exactly how you reach them here.)
- Tech-tree items themselves do not show "what unlocks me" — they sit at the top of their own tier.

## Why The Data Is Local

The browser is intended to be safe for a public repository. It provides the tool, not the extracted or generated game-derived content.

If permission is granted to publish derived catalog data later, the same browser can be used as the basis for a GitHub Pages version.

## Changelog

The catalog browser keeps its own [changelog](../tools/catalog-browser/CHANGELOG.md) — it ships continuously with this site rather than in the tagged tool releases, so it is tracked separately from the [manager / patcher / paker changelog](../tools/pagonia-manager/CHANGELOG.md).
