# 🏛️ Package Layering and DLC Architecture

> **Naming note.** "Overlay" is used two different ways in the Pagonia Land docs. This page is about **logical layering of packages on top of `core`** — the way `dlc1`, `decorations1`, and `tools` extend the base database. For the distinct mod-distribution pattern of *side-by-side overlay paks* (a separate `.pak` that adds files alongside the shipped paks), see [Mod Distribution Patterns → Pattern B](mod-distribution.md#pattern-b-side-by-side-overlay-pak).

The game database is built as a package-based layered system.

`core` provides the base game data. Additional packages — `dlc1`, `decorations1`, `tools` — contribute more XML files that are loaded into the same global entity registry.

This matters for modding because a package usually does not need to replace core files directly. It can add new entities and connect them to existing core systems through GUID references — and, since Meadowsong shipped, also through the `InheritanceMode` + `InheritedGuid` primitives that the engine resolves at load time.

For a focused explanation of additive patches, variants, the Meadowsong inheritance primitives (Template / Replace / Incremental / entity-level Unload — the last EE-confirmed as engine-implemented but unused in shipped data — plus the separate encounter-level null-GUID unload), and why same-GUID byte-level override is still not a thing, see [DLC Patch And Override Model](dlc-patch-override-model.md). The complete Meadowsong picture lives in [Cross-Pak Entity Merging (Meadowsong)](mod-distribution.md#cross-pak-entity-merging-meadowsong).

## Main Idea

Think of the game database as a global registry of entities.

Each package contributes entities:

```text
core/
  resources.gd.xml
  buildings.gd.xml
  units.gd.xml
  ...

dlc1/
  resources.gd.xml
  buildings.gd.xml
  units.gd.xml
  ...

decorations1/
  decorations.gd.xml

tools/
  magmaview.gd.xml
```

When packages are active, their XML entities appear to be combined into one usable database. Relationships between packages are expressed through GUID references.

## Evidence From The Current Data

Current scan (`1.3.2-11873+194094`, regenerated 2026-06-09):

| Observation | Count |
| --- | ---: |
| Total entities | 4,711 |
| Duplicate entity GUIDs | 0 |
| `core` entities | 4,147 |
| `dlc1` entities | 519 |
| `decorations1` entities | 19 |
| `tools` entities | 26 |
| `<Entity InheritanceMode="...">` uses (across packages) | 51 |
| `<InheritedIndex>` list-merge markers | 4,447 |

There are no duplicate entity GUIDs. `dlc1` does not override core entities by redefining the same GUID — that pattern is **not** how overrides work. Instead, two distinct strategies show up:

- **Additive references.** Most of `dlc1` adds new entities and points them at existing core categories, tags, resources, buildings, units, sediments, and global systems by GUID.
- **Inheritance-mode relations.** Where the new entity is meant to replace, extend, or template-clone a core entity, it gets its own new GUID plus an `InheritanceMode` attribute (`Template`, `Replace`, or `Incremental`) and an `InheritedGuid` pointing at the core entity. The engine resolves these into the merged dataset at load. See [mod-distribution.md → Confirmed primitives](mod-distribution.md#confirmed-primitives) for the per-primitive examples.

Together, these two strategies cover everything Meadowsong does on top of core, without ever sharing GUIDs.

## Core As The Base Layer

`core` defines the base systems and most shared entities:

- base resources
- resource categories
- buildings
- building categories
- units
- unit tags
- production recipes
- deposits
- sediments and terrain tags
- objectives
- notifications
- tech tree systems
- DLC metadata

Other packages depend on these systems.

For example, a DLC building can be placed into an existing construction menu by referencing a core building category GUID. A DLC resource can be grouped by referencing a core resource category GUID.

## DLC Metadata In Core

The base game knows about DLC packages through `game-gdb/core/gdb/dlcs.gd.xml`.

For `dlc1`, the current data contains an entity similar to:

```text
Entity: DLC DLC1
Package: dlc1
DLCType: InfluencesGameplay
```

The DLC entity has its own GUID. DLC content can then use `NeedsUnlock` to connect content availability to that DLC.

## `NeedsUnlock`

Many DLC entities contain `NeedsUnlock`.

This component appears to gate content behind DLC ownership, unlock progression, or both.

Example pattern:

```xml
<NeedsUnlock>
  <DLC>dlc-guid</DLC>
  <DLCNeedsOwn>True</DLCNeedsOwn>
</NeedsUnlock>
```

Some entities also use empty `NeedsUnlock` components, which may still mark them as part of a broader unlock/progression system.

For modding, this means:

- a new DLC-like entity may need unlock metadata
- an entity can exist in the database but not be available in normal gameplay
- build menu visibility and actual buildability may depend on unlock state

## Present vs Owned vs Effective

> **Tooling model.** This section describes how `pagonia-manager` reasons about expansions when planning a deploy. The runtime-gating background below is **flagged "verify against game behaviour"** — it is the manager's working model of how the engine treats DLC content, not a documented engine contract.

A pak being **on disk** does not mean the expansion's content is **active for this player**. Envision Entertainment ships every pak — `core`, `decorations1`, `dlc1`, `tools` — to *every* player regardless of what they bought. That is what lets a co-op host carry the DLC for a lobby where not everyone owns it: the paks are present for everyone, and the engine gates the actual content at runtime (via `DLCNeedsOwn` / `NeedsUnlock`, see above), active only for the entitled player or the host-driven session.

So `dlc1.pak` sitting in `pak/` tells the tooling nothing about whether *this* player owns Meadowsong. To avoid telling a non-owner that a `dlc1`-patching mod "took effect" when it is a silent no-op in their single-player game, the manager separates three states:

| State | Meaning | Source |
|---|---|---|
| **Present** | the pak exists on disk | detected (EE ships all → usually true) |
| **Owned** | the player is entitled to the expansion | **user-declared** per game install; default `unknown` |
| **Effective** | the content is actually active for this player in **solo** play | derived: `Present ∧ Owned` |

`core` and `tools` are the base game + editor data and are **always** treated as owned; only `decorations1` and `dlc1` are declarable. Ownership is **user-declared** (the file-level tool has no store-account access), defaults to `unknown`, and is stored **per game install**, never in a portable profile — entitlement is a fact about the installation/account, stable across every profile.

**The load-bearing rule: ownership never gates deployment — only presence does.** Because EE ships `dlc1.pak` to everyone, a non-owner can still *write* a mod's `dlc1` patches into their present `dlc1.pak`, and in co-op they **must** be able to, to match a host who owns the DLC. So:

- **Presence is a hard constraint** — you cannot patch a pak that isn't on disk → absent ⇒ error.
- **Ownership is advisory only** — present-but-not-owned ⇒ warn and proceed, never block.

`Effective = Present ∧ Owned` therefore describes **solo runtime effect** (used for honest messaging — "this stays inactive in single-player"), **not** deploy-ability. `Owned = unknown` resolves to `Effective = false` (the tool never silently assumes ownership) but stays distinct from a hard `not-owned`, so a surface can *prompt you to declare* rather than mislead you into thinking you don't own it.

Co-op parity (everyone deploying the same mod bytes) is a separate concern from ownership, handled by the manager's `profile export` → collection mechanism — not by flipping an ownership flag.

## Same File Names, Different Contributions

`core` and `dlc1` share many file names:

```text
core/gdb/resources.gd.xml
dlc1/gdb/resources.gd.xml

core/gdb/buildings.gd.xml
dlc1/gdb/buildings.gd.xml

core/gdb/units.gd.xml
dlc1/gdb/units.gd.xml

core/gdb/productionrecipes.gd.xml
dlc1/gdb/productionrecipes.gd.xml
```

This does not appear to mean that the DLC file replaces the core file.

The better interpretation is:

- `core/gdb/resources.gd.xml` contributes core resources
- `dlc1/gdb/resources.gd.xml` contributes additional resources
- both resource sets are loaded into the same resource system

The same pattern applies to buildings, units, recipes, notifications, objectives, and other systems.

## Additive Changes, Inheritance, and Variants

DLC1 mixes three approaches:

**Pure additions.** Most of the Meadowsong content is brand-new entities with brand-new GUIDs, hooked into existing core systems via cross-package references:

- new resources such as apples, grapes, wine, eggs, cleansing plants, cleansing potions
- new buildings such as Animal Farm, Fruit Farm, Winery, Sanctuary
- new deposits such as apple trees, grapevines, medical plants, cleansing plants
- new production recipes, units, NPC units, abilities, infection-related effects
- new tech-tree groups
- new campaign/scenario database

**Inheritance-mode relations** (Meadowsong-introduced; 49 uses in 1.3.0). Where DLC1 wants to formally relate to a core entity rather than just point at it:

- `InheritanceMode="Template"` — a new abstract base entity clones structure from a core base. DLC1's `BuildingBase` (Guid `07a9d5fe-...`) inherits from core's building base and overrides DLC-specific fields.
- `InheritanceMode="Replace"` — a new entity wholesale replaces the inherited one in the merged dataset. Campaign Map 1 swaps in `TechTreeUnlockedT4StoneBlocks DLC1CM1 replace` instead of the core node.
- `InheritanceMode="Incremental"` — a new entity adds list items on top of the inherited entity's lists, referencing preserved positions via `<InheritedIndex>N</InheritedIndex>`. `Bakery DLC1 extension`, `Brewery DLC1 extension` extend the base-game buildings' Costs / BuildupLocators lists.

**Variant by reference** (the pre-Meadowsong "safer than override" pattern, still valid). Some content looks like a modified variant of core content, but is still represented as a brand-new entity with a new GUID, no inheritance attribute. The scenario/unlock/recipe/building list points to the variant.

For example, `dlc1/gdb/buildings.gd.xml` contains infected residence variants. These are not the same GUIDs as core residences and don't use `InheritanceMode` either. They are separate DLC entities with additional infection-related data, surfaced through scenario logic.

The three strategies coexist in DLC1's XMLs. For a modder, the choice is usually:

- "Brand-new content that hooks into existing systems" → pure addition + GUID references.
- "Extend an existing entity's lists (add a recipe to the Toolmaker)" → `InheritanceMode="Incremental"`.
- "Replace an existing entity wholesale for one campaign/scenario" → `InheritanceMode="Replace"`.
- "Remove an entity from the merged dataset entirely" → `InheritanceMode="Unload"` (engine-implemented since Meadowsong, unused in shipped content; safe only when nothing else references the target — usually unload its dependents too).
- "Provide an alternative entity that scenarios/unlocks can opt into" → variant by reference (no inheritance attribute).

**Conflict-minimisation guidance (EE, 2026-06-06).** When several gdb mods may run together, prefer the *additive, stackable* modes (`Incremental`, `Template`) over the *destructive, last-loaded-wins* ones (`Replace`, `Unload`). Two mods that `Incremental` the same entity both apply; two mods that `Replace` the same entity fight, and only the last-loaded one survives. The fewer entities a mod `Replace`s or `Unload`s, the more cleanly it co-exists in a player's mod set.

## Scenario Overlays

Campaign and scenario files also behave like local overlays.

`dlc1/meadowsong map database.gd.xml` contains scenario-specific entities:

- objectives
- dialogues
- points of interest
- NPCs
- local unlocks
- map-specific buildings/deposits
- tech tree unlock steps

This file references both core and DLC content. It acts like a scenario-level package on top of the global core+DLC database.

## decorations1

`decorations1` is a much smaller package. It appears to add decorative buildables.

It references core systems, such as construction categories and materials, but does not introduce a broad new gameplay system like `dlc1`.

This supports the same package model:

- package contributes new entities
- new entities reference core systems
- availability may be controlled by unlock/DLC metadata

## tools

`tools` appears different. It contains editor/Magmaview data rather than normal gameplay content:

- editor brushes
- terrain visualization
- sediment visualization
- vegetation groups
- road/fluid/territory visualization

It mostly references itself. This suggests that not every package is gameplay content. Some packages may exist to support tools, editors, or debug views.

## Implications For Modding

This design suggests a safer modding strategy:

1. Prefer adding new entities over editing core entities in place.
2. Reference existing core categories, tags, units, resources, and systems.
3. Use new GUIDs for new content.
4. Use `NeedsUnlock` or scenario-specific unlock data when content should not be globally available.
5. Avoid replacing core GUIDs unless you fully understand all references.

## Modding Pattern: Add A New Resource

Conceptual pattern:

1. Add a resource entity with a new GUID.
2. Reference an existing core `ResourceCategory`.
3. Add icon/mesh/carry/display fields.
4. Add the resource to a recipe, deposit, trade objective, or building cost.
5. Validate references.

This follows the DLC pattern: new content, existing system.

## Modding Pattern: Add A New Building Variant

Conceptual pattern:

1. Copy a similar building as a starting point.
2. Assign new GUIDs to the new entity and any nested child entities.
3. Keep known-good core references for categories, workers, sounds, and terrain tags.
4. Change only the fields needed for the variant.
5. Gate it through unlock/scenario logic if needed.
6. Validate duplicate GUIDs and unresolved references.

This is likely safer than editing an important core building directly.

## What Still Needs Research

Mostly resolved by Meadowsong's release:

- ~~whether later packages can intentionally override entities by GUID.~~ **They can't** (same-GUID is still forbidden). Override semantics are expressed via `InheritanceMode="Replace"` + `InheritedGuid` instead.
- ~~how conflicts are resolved if duplicate GUIDs exist.~~ **They don't exist by design.**
- ~~whether non-DLC community packages can be loaded by the game without replacing official files.~~ **Yes**, via Pattern B overlay paks. See [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md) and [`docs/mod-patch-format.md`](mod-patch-format.md#standalone-overlay-paks-pak-block) for the four-file module scaffold.
- ~~which fields are required for a fully valid custom package.~~ Documented in [mod-distribution.md → The Pak Loading Model](mod-distribution.md#the-pak-loading-model).

> The consolidated cross-doc register is [What We Know About the Engine](engine-claims.md); the list below is this page's slice.

Resolved by EE dev review (2026-06-06, see [mod-distribution.md → Dev review follow-up](mod-distribution.md#dev-review-follow-up-2026-06-06)):

- ~~what happens when two mods target the same `InheritedGuid`.~~ **Load order decides: last-loaded `Replace` wins; `Incremental` stacks.**
- ~~whether a dedicated Entity-level `Unload` `InheritanceMode` ships.~~ **It already ships** (engine-implemented since Meadowsong, unused in shipped data).

Still open:

- exact package load *order* determination (which module loads when), even though the collision-*winner* rule is now known.
- whether `DLC/Package` names are used directly by the loader or routed through the `manifest.json`.
- whether `tools` is loaded only for editor/debug contexts.

The combined model — additive base + Meadowsong inheritance primitives — fits the current XML structure end-to-end. The override-precedence rule for multi-mod scenarios is now EE-confirmed (load order, last-loaded-wins for destructive modes); only the exact load-*order* determination still wants in-game confirmation.

## Practical Advice

For now, modders should assume:

- `core` is the base layer
- additional packages extend the global database
- GUIDs are global
- package folders are organizational, not isolated namespaces
- DLC content often becomes available through unlock systems rather than by replacing core data

That model fits the current XML structure and is the safest way to reason about changes.
