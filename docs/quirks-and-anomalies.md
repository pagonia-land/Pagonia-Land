# Quirks And Anomalies

A running log of empirical findings about the Pioneers of Pagonia game database that don't fit neatly into the structured docs but are worth keeping on the radar — for modders who hit them, for future contributors who'd otherwise be surprised, and as cross-checks during version updates.

Each entry says: what was observed, where it shows up, what it likely means, and what (if anything) to do about it.

## The 12 dangling "unit" and "faction" references

The validation pass consistently reports **12 unresolved GUID references** that are *not* null GUIDs — they're real-looking GUIDs that point at no entity in the shipped XML set. They have been there since at least 1.2.2 and remain in 1.3.1. (1.3.1 added 18 further, transient `NoMVP.*` orphans on top of these, for 30 unresolved references total — see [Validation Baseline](../VALIDATION_BASELINE.md). The 12 described here are the stable ones.)

These 12 references resolve to only **two distinct GUIDs**:

| GUID | Times referenced | XML element context | Source files |
| --- | ---: | --- | --- |
| `ac941c5f-8266-4456-80f6-d32c777017dc` | 7 | `<Unit>` (and one `<Item>`, one `<Content>`) | `core/gdb/buildings.gd.xml`, `core/gdb/landingparties.gd.xml`, `core/gdb/objectives.gd.xml` |
| `d860e55d-71de-4200-b7e1-46f2143d6ebd` | 5 | `<CustomFaction>` | `core/gdb/narration.gd.xml` |

Neither GUID appears in `generated/entities.json`. The 1.2.2 → 1.3.0 diff:

- 1.2.2 also reported exactly 12 "other unresolved", but only 2 of those 12 GUIDs survived into 1.3.0 — the rest got reshuffled. The total stayed at 12 by coincidence (or by intent — the same engine-magic shape was reused).
- The two surviving GUIDs are exactly the two listed above. They're stable across the version change.

**Working hypothesis.** These are **engine-special "magic GUIDs"** that the engine recognises outside the XML dataset: probably a wildcard / placeholder Unit token (use-any-unit, no-unit, generic-NPC?) and a wildcard CustomFaction marker (no-faction, neutral). The element contexts (`<Unit>`, `<CustomFaction>`) suggest they're consumed as "slot reservations" rather than concrete references.

**Implication for modders.** Don't try to "fix" these by pointing them at real entities — they're not bugs. If your validator flags them, treat them as known engine constants. If you ship a mod that references an existing core entity that uses one of these GUIDs internally, leave the reference alone.

## Only one file lives outside `<pak>/gdb/`

Every shipped `*.gd.xml` lives under `<pak>/gdb/<file>.gd.xml` — except one:

```text
game-gdb/dlc1/maps/meadowsong map database.gd.xml
```

That's the only exception across `core`, `decorations1`, `dlc1`, and `tools`. core's own campaign-map data lives under `core/gdb/campaign map 1 - tutorial.gd.xml` etc. — same content type, different convention.

**Likely meaning.** EE introduced the `<pak>/maps/` subfolder convention with the Meadowsong DLC. If EE keeps this convention, future DLC modules would likely also use `<dlc-name>/maps/<...>.gd.xml` for map-specific gdb content rather than mixing it into the main `gdb/` folder. core's own campaign files probably stay under `core/gdb/` for backwards compatibility.

The paker's classify command already handles this — files under `<m>/maps/` count as map-specific content, not as standalone overlay overrides. See [docs/mod-distribution.md → The Pak Loading Model](mod-distribution.md#the-pak-loading-model).

## tools.pak's unusual layout

Every shipped pak places its `<m>.gd.bin` index under `<m>/`. Except `tools.pak`, which puts it at the **pak root**:

```text
tools.pak
├── tools.gd.bin              (at pak root — not under tools/!)
├── tools/files.json          (rest of the skeleton is under tools/)
├── tools/manifest.json
├── tools/memory.bin
└── tools/gdb/magmaview.gd.xml
```

`tools/files.json` points at `tools.gd.bin` (root path) rather than `tools/tools.gd.bin`. The engine loads it correctly.

**Why it matters.** Our classifier (`pagonia-paker classify`) deliberately accepts both layouts — `<m>/<m>.gd.bin` and root-level `<m>.gd.bin` — so a custom mod can choose either shape. If the engine ever supports only one layout, the tools pak's behaviour will tell us which one is canonical.

**Implication for modders.** When writing a Pattern B overlay pak by hand, putting the `.gd.bin` under `<m>/` is the safer choice (matches core/dlc1/decorations1 majority). The root-level placement works because tools.pak does it, but expect less testing/diagnostic coverage from third-party tools.

## The single 1.2.2 `InheritanceMode="Template"` use

In pre-Meadowsong 1.2.2, the `InheritanceMode` attribute machinery already shipped with the engine — but it was used by exactly **one entity** across the entire database:

```xml
<Entity Name="DecorativeBuilding"
        Guid="9edc01cd-15b7-48a4-bfca-26ae9a3702af"
        InheritanceMode="Template"
        InheritedGuid="0913f2c3-524b-436b-af6b-1af8617a110a">
```

(File: `decorations1/gdb/decorations.gd.xml`)

This entity survived unchanged into 1.3.0 — same GUID, same `InheritedGuid`, same file. Meadowsong didn't redefine it.

**Why it's interesting.** It looks like the **test-bed entity** EE used to prove the InheritanceMode machinery worked before opening the floodgates with Meadowsong (which jumped to 49 uses across Template / Replace / Incremental). The fact that `decorations1` rather than `core` hosts it suggests `decorations1` was the lower-risk pak to ship the feature in.

**Implication.** None practical for modders. But if you see a contributor wondering "when did InheritanceMode appear?" — point them at `DecorativeBuilding` as the earliest known use.

## Each InheritedGuid target is hit exactly once

Across all 49 `InheritanceMode` uses in 1.3.0, the **InheritedGuid distribution is perfectly flat**: 49 distinct target entities, each targeted by exactly one inheriting entity. No two inheriting entities compete for the same parent.

This is a *very* clean property:

- No conflict between two `Replace` entities competing for the same core target.
- No "Template + Incremental on the same base" combination shipped.
- No two extensions stacking on the same core entity.

**Implication for modders.** In shipped content the engine never has to resolve "two mods both Replace the same entity" or "two mods both Incremental the same entity" — those scenarios are theoretical. When a third-party mod creates such a conflict, the engine's resolution rule is unobserved.

This is the empirical reason **cross-mod conflict detection on entity-relation level** stays an open gap for our tooling. There's nothing in shipped content to learn from; community mods will be the first real test.

## Reference resolution rate drops with each new version

The fraction of references that resolve to a real entity is going **down** over time:

| Version | Total refs | Resolved | Null GUID | "Other" unresolved | Resolution rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1.2.2-11216+189567 | 26,162 | 21,208 | 4,942 | 12 | 81.06% |
| 1.3.0-11768+193445 | 31,776 | 24,451 | 7,313 | 12 | 76.95% |
| 1.3.1-11826+193733 | 31,761 | 24,420 | 7,311 | 30 | 76.89% |

The null-GUID share grew from **18.89%** (1.2.2) to **23.01%** (1.3.0 Meadowsong) and held at **23.02%** in the 1.3.1 hotfix — Meadowsong added 2,371 new null-GUID references (+47%) while only adding 615 new entities (+15%); the 1.3.1 hotfix barely moved the totals. Note the "Other" unresolved column jumping 12 → 30 in 1.3.1 is *not* reference rot: the 1.3.1 `NoMVP.*` resource cleanup deleted six placeholder resource entities but left four of them still referenced by recipes, adding 18 dangling references. See [VALIDATION_BASELINE.md → Unresolved Non-Null GUID References](../VALIDATION_BASELINE.md#1-unresolved-non-null-guid-references-30).

**Likely meaning.** Meadowsong-era XML uses more optional-with-default-null fields than 1.2.2 XML did. The encounter-level `<ReplaceSelf><ReplaceWithEntity>null</ReplaceWithEntity></ReplaceSelf>` "unload" pattern is one example, but the bulk of the increase is probably new component shapes that ship with empty-by-default sub-fields.

**Implication for modders.** Don't be alarmed if your validator reports tens of thousands of null-GUID references — they're the engine's "field exists but is intentionally empty" pattern, not bugs. The "Other unresolved" count is the more interesting metric to watch for actual reference rot.

## Meadowsong added 33 new component types (and removed 1 typo)

The `valueTypes` vocabulary (the `<Aspect*>`, `<Vis*>`, `<Effect*>`, etc. elements that hang off `<Entity><Values>`) grew from **281 types in 1.2.2 to 313 in 1.3.0** — **net 33 new, 1 retired**.

The retired one is `UiGlobalUnitTraits` → renamed to `UiGlobalUnitTrait` (singular). Plain spelling cleanup, same intent.

Of the 33 additions, **26 are dlc1-exclusive** (never used by core entities — clean DLC content additions) and **7 are also used by core entities**, which is more interesting: core got extended to interoperate with the DLC. The 7:

| Component | What it likely does | First appearance |
| --- | --- | --- |
| `AspectAnimalSpawnLocation` | Marks a building as a site where farm animals spawn | Added to core's `Hut` (so player houses spawn animals now!) and dlc1's `Infected Hut` |
| `UiCampaign` | Campaign-UI state shape | Used by both core's `Campaign Map` and dlc1's `DLC1.Campaign` |
| `UiGameHint` + `UiGameHintReferences` | New in-game-hints system | core + dlc1 contribute hints |
| `UiGlobalUnitTrait` | The renamed singular form (was `UiGlobalUnitTraits` in 1.2.2) | core + dlc1 |
| `UiMarketplaceGlobals` | Market-stall globals | core + dlc1 |
| `UniqueUnitSpawn` | Spawn-unique-units mechanic | core + dlc1 |
| `UnitRaidParameters` | **Bulk-added to 57 existing core NPC units** — every campaign NPC now has raid parameters | core (heavy adoption) + dlc1 |

The dlc1-only 26 cluster around four Meadowsong systems: **animal husbandry** (`AspectAnimalFarm`, `AspectFarmAnimal`, `AspectFeedingStation`, `AspectGuardAnimal`), **infection / the Withering** (`AspectInfected*`, `EffectInfected`, `EffectInfectionImmunity`, `InfectionChance*`), **shrine/sanctuary** (`ShrineManaResource`), and **DLC-aware progression** (`DLC1AchievementParameters`, `DLC1CM1AchievementParameters`, several `*AreaAbility` types for the Sanctuary's effect zones).

**Implication for modders.** When EE ships a major update, expect new `valueTypes` to appear. If your mod targets `Hut` by GUID and the engine adds `AspectAnimalSpawnLocation` to it, your mod still works — your component edits don't conflict with the new aspect. But the merged entity now has more shape than your local copy understands. Treat new aspects as additive context, not as overrides.

## Most "entity changed" diffs are one bulk component addition

The 1.2.2 → 1.3.0 diff reports **68 entities with metadata changes** (same GUID, something different about them). Breaking that down:

| Change kind | Count |
| ---: | --- |
| **components** changed | 57 |
| **name** changed (renamed) | 11 |

Of the 57 component-change entries, the **overwhelming majority are a single bulk rollout**: 57 existing core NPC entities (campaign-map bosses, soldiers, ghosts, werewolves, etc. across CM3 through CM7) all gained the new `UnitRaidParameters` component in the same release. So functionally the 57 = "every campaign NPC now supports the raid-parameter system", not 57 independent edits.

If you're diffing two game versions and find "57 entities changed", check whether they share a single new component. If yes, it's likely one cross-cutting design change. If no, the diff is doing real per-entity work.

## EE's internal "NoMVP." prefix on pre-release entities

Among the 11 rename diffs, one stands out:

```
NoMVP.Beer  →  Beer        (resource, GUID 07faecf2-...)
```

And among the 10 removed entities:

```
NoMVP.BeerRecipe            (production recipe, GUID ac2451f6-...)
```

**Observed pattern.** EE uses `NoMVP.` as an internal prefix to mark **entities present in the codebase but not yet in shipping content** ("not part of the MVP"). When a feature actually ships publicly — like the Brewery + Beer in the Free Beer update that landed with Meadowsong — the prefix gets stripped. The recipe shape changed enough that the old `NoMVP.BeerRecipe` entity was retired and replaced with a fresh entity rather than renamed in-place. Confirmed by direct evidence: the rename `NoMVP.Beer → Beer` and the deletion of `NoMVP.BeerRecipe` lined up exactly with the public Beer feature shipping; the `NoMVP.` prefix appears on no other entity surviving into 1.3.0.

**Implication for modders.** If you find entities prefixed `NoMVP.` in any future game version, treat them as not-yet-shipped placeholder content. They may disappear or get renamed in the next release. Don't ship a mod that targets a `NoMVP.` entity by name or GUID.

## Same-GUID renames worth flagging

Renames preserve GUIDs but change the human-readable name. The full 1.2.2 → 1.3.0 rename list:

| 1.2.2 name | 1.3.0 name | Note |
| --- | --- | --- |
| Foreman Jadrick Speaker | Foreman Jadrik Speaker | typo fix |
| Maira Speaker | Scholar Maira Speaker | title added |
| PorcinoDepositBase | MushroomDepositBase | specific → generic |
| PorciniDeposit_001 | MushroomsDeposit_001 | specific → generic |
| PorciniDeposit_002 | MushroomsDeposit_002 | specific → generic |
| PorciniDeposit_003 | MushroomsDeposit_003 | specific → generic |
| PorciniDeposit_004 | MushroomsDeposit_004 | specific → generic |
| Campaign | Campaign Map | refinement |
| Alcoholic Drinks Market Stall | Beverage Market Stall | scope broadened |
| Beverage Meal Ingredients | Processed Ingredients | scope broadened |
| NoMVP.Beer | Beer | shipping (see above) |

**Implication for modders.** Targeting entities by GUID is robust across renames. Targeting them by display name is fragile — every renamed entity above would break a name-only patch. This is why our [mod patch format](mod-patch-format.md) uses GUIDs as the primary target identifier.

## Core never references DLC content (one-way dependency)

A reference-direction probe across all 31,776 GUID-like references in 1.3.0:

| Direction | Resolved references |
| --- | ---: |
| `core` → `dlc1` | **0** |
| `core` → `decorations1` | **0** |
| `core` → `tools` | **0** |
| anything → `core` | many |

Confirms that the layered package model is strictly one-way: **non-core packages reference core, never the other way around**. Architecturally important — it means a mod can extend core without core needing to know the mod exists, and removing a DLC can never leave dangling references in core.

This is the empirical foundation under the "Pattern B mods don't need core to be modified" claim in [`docs/mod-distribution.md`](mod-distribution.md). Now it's a measured fact, not just a working assumption.

## `tools.pak` is the structural outlier (twice over)

We already noted that `tools.pak` puts its `.gd.bin` at the pak root rather than under `tools/`. Probing further: it's also the only shipped pak with `byte[1] = 0x01` in its `.gd.bin` header.

| Pak | `.gd.bin` location | Header bytes (hex) | byte[1] |
| --- | --- | --- | --- |
| core | `core/core.gd.bin` | `03 00 02 2A 00 00 00` | 0x00 |
| dlc1 | `dlc1/dlc1.gd.bin` | `03 00 02 0E 00 00 00` | 0x00 |
| decorations1 | `decorations1/decorations1.gd.bin` | `03 00 02 01 00 00 00` | 0x00 |
| tools | `tools.gd.bin` (root!) | `03 01 02 01 00 00 00` | **0x01** |

`tools.pak` was structurally different on both axes in 1.2.2 too. The semantics of `byte[1]` remain unverified — it's possibly a minor-format-version flag, a feature bit, or an authoring-tool fingerprint. Either way: when modders write their own paks, **use `byte[1] = 0x00`**; `0x01` may signal "this pak was produced by the EE toolchain in a specific way" that community paks don't need to mimic.

## `memory.bin` carries per-pak memory-allocation hints

Every shipped pak ships a 28-byte `<m>/memory.bin` blob. Decoding the bytes:

```text
01 00 00 00 00 00          6-byte header (constant)
<8 bytes>                  uint64 #1
<8 bytes>                  uint64 #2
<8 bytes>                  uint64 #3 (only 6 bytes here actually; last 2 padding?)
```

Concrete observed values:

| Pak | First uint | Second uint | Third uint |
| --- | ---: | ---: | ---: |
| core | (varies, large) | (varies) | (varies) |
| dlc1 | 0x000098b3 (39,091) | 0x00003cfc (15,612) | 0x0000265b (9,819) |
| tools | 0x0e f7 (3,831) | 0x03 da (986) | 0x01 13 (275) |
| user-map 4r70_DnD | 0x000f (15) | 0x0004 (4) | 0x0002 (2) |

The numbers scale roughly with pak size and content complexity. Working hypothesis: **per-category memory-allocation hints** the engine uses to pre-size buffers when loading the pak (textures vs. audio vs. xml byte counts, for example). For a fresh scaffolded mod pak, writing 28 zeros has worked so far — the engine probably either ignores them, recalculates on first load, or accepts an over-allocation hint.

**Implication for modders.** The bundled paker's Pattern B scaffold writes 28 zero bytes, and this hasn't broken anything observably. Don't try to engineer the values without verification — the field is undocumented.

## How to refresh this log

Every game-version update worth a snapshot is also worth a quick pass through these probes. Re-run the queries in this doc against the new `game-gdb/`:

- `grep -rh 'InheritanceMode="[^"]*"' game-gdb/ | sort | uniq -c | sort -rn` to enumerate inheritance modes
- `find game-gdb -name "*.gd.xml" -type f | grep -v "/gdb/"` for files outside the canonical convention
- `node generated/_curiosities.js` would give the dangling-reference list if you have that local script around (it's transient — gitignored under `generated/`)

When a new quirk shows up, add it here. When an old one resolves itself in a new version, mark it resolved rather than deleting the entry — the history of "what was once weird" is valuable context.
