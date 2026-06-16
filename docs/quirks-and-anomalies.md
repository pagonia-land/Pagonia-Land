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

**Implication for modders.** In shipped content the engine never has to resolve "two mods both Replace the same entity" or "two mods both Incremental the same entity" — those scenarios don't occur in EE's own data. **But the resolution rule is no longer unknown.** EE confirmed it on 2026-06-06 ([mod-distribution.md → Dev review follow-up](mod-distribution.md#dev-review-follow-up-2026-06-06)):

- two mods `Replace` the same entity → **last-loaded mod wins** (load order decides);
- two mods `Incremental` the same entity → **both apply** (additions stack, no clobber).

So the rule is *settled*; what's still absent is *shipped example data* to regression-test against. That makes **cross-mod conflict detection on the entity-relation level** a now-buildable feature for our tooling (we can warn deterministically), not a blocked one — community mods are still the first real corpus to validate against.

## `InheritanceMode="Unload"` ships but is unused

A data scan of every shipped pak finds `InheritanceMode` in exactly three flavours — `Template` (18), `Replace` (14), `Incremental` (19) — and **zero** occurrences of `Unload`. For a while we read that absence literally: "the entity-level Unload mode doesn't ship; the encounter-level null-GUID `<ReplaceSelf>` pattern is the only removal mechanism."

**That inference was wrong, and it's an instructive miss.** EE confirmed on 2026-06-06 that `InheritanceMode="Unload"` **has been implemented in the engine since Meadowsong** — it's simply never used in EE's own content. A pure data scan is *structurally* blind to an engine feature with no data uses: there's nothing in the bytes to find. The only way to learn it exists is a dev statement (or decompiling the loader).

**What it does** (per the dev): a proxy entity A unloads another entity B, and the merged database then simply doesn't contain B — as if it never existed. The catch: it's only safe when **no other entity C still references B** (B's GUID must appear nowhere else). Because the game validates references in some subsystems but not others, an unsafe unload may crash in one place and silently dangle in another — so an *effective* unload usually means unloading B **and all its dependents together**, and finding the safe set is currently trial-and-error.

**Implication for modders.** `Unload` is a real, available primitive — but the sharpest-edged one. Reach for `Incremental` / `Template` first; treat `Unload` (and `Replace`) as last resorts, and expect to test in-game. See [DLC Patch And Override Model → Patch Behaviour Patterns](dlc-patch-override-model.md#patch-behaviour-patterns).

**Methodology lesson for this repo.** Where our docs infer a *negative* ("X doesn't exist") purely from a data scan, flag it as "not observed in shipped data" rather than "not supported" — the two are different, and `Unload` is the proof.

## Reference resolution rate drops with each new version

The fraction of references that resolve to a real entity is going **down** over time:

| Version | Total refs | Resolved | Null GUID | "Other" unresolved | Resolution rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1.2.2-11216+189567 | 26,162 | 21,208 | 4,942 | 12 | 81.06% |
| 1.3.0-11768+193445 | 31,776 | 24,451 | 7,313 | 12 | 76.95% |
| 1.3.1-11826+193733 | 31,761 | 24,420 | 7,311 | 30 | 76.89% |
| 1.3.2-11873+194094 | 31,763 | 24,422 | 7,311 | 30 | 76.89% |
| 1.4.0-11893+194274 | 31,763 | 24,422 | 7,311 | 30 | 76.89% |
| 1.4.0-11944+194631 | 31,763 | 24,422 | 7,311 | 30 | 76.89% |

The null-GUID share grew from **18.89%** (1.2.2) to **23.01%** (1.3.0 Meadowsong) and held at **23.02%** in the 1.3.1 hotfix — Meadowsong added 2,371 new null-GUID references (+47%) while only adding 615 new entities (+15%); the 1.3.1 hotfix barely moved the totals, the 1.3.2 "Free Beer" update moved them less still (+2 entities, +2 resolved refs, the "Other" column steady at 30, resolution rate flat at 76.89%), and the 1.4.0 "Pagonia Editor Update" beta held everything flat (+4 entities, but reference totals, resolved/null counts, and the 76.89% rate are all unchanged — the editor is the story, not the shipped data); its **Beta Update #1** (`11944`) moved nothing further — three `dlc1` value/schema edits, every count identical. Note the "Other" unresolved column jumping 12 → 30 in 1.3.1 is *not* reference rot: the 1.3.1 `NoMVP.*` resource cleanup deleted six placeholder resource entities but left four of them still referenced by recipes, adding 18 dangling references. See [VALIDATION_BASELINE.md → Unresolved Non-Null GUID References](../VALIDATION_BASELINE.md#1-unresolved-non-null-guid-references-30).

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

## Dead / cut content: fully-authored but unreachable

Roughly **60 entities ship with real, player-facing assets (icons, meshes, complete recipe bodies, recruitment costs) yet have no reachable path in normal play.** They are not bugs and not the null-GUID "intentionally empty field" pattern — they are *cut, superseded, or never-wired-up* content that EE left in the shipped database. They cluster into a handful of recognisable abandoned features.

**Methodology.** Candidates were surfaced fast from the generated entity catalog (`generated/catalog/resource-flow.csv` carries `ProducedBy` / `ConsumedBy` / `ConstructionCostFor` / `RecruitmentCostFor` / `UsageCount`; `building-production.csv` is the real building→recipe wiring), then each was re-verified adversarially against the raw `.gd.xml`. The catalog is only a *projection* — it tracks production / construction / recruitment / storage / treasure, but **not** references from objectives, landing parties, techtree, abilities, campaign maps, or mapgen — so the XML is authoritative. An entity counts as "cut" only when *every* reference to its GUID lives inside an `IsAbstract="true"` parent (or a dev/test container), and no reachability root (techtree unlock, a *non-abstract* building's `<ProductionRecipes>`, map placement, concrete landing party, NPC spawn) touches it.

> Two verification gotchas worth recording. (1) The indexed `Grep` tool silently returns **zero hits** for some `.gd.xml` GUID searches (a UTF-16/BOM artifact on the large `productionrecipes.gd.xml`); raw `grep`/`rg` via shell is authoritative — several entities here were nearly mis-filed as "not found" because of it. (2) `building-production.csv` does **not** filter abstract buildings, so an abstract (un-buildable) building's recipes look "produced" in the catalog. That is exactly how the cut *Stone Mason* chain hid: the catalog shows Border Stone as produced by "Stone Mason", but the building itself is `IsAbstract`.

### Cut transport / mechanisation feature (cogwheel + carts + wheels)

The clearest abandoned feature: a transport / mechanisation chain — a cogwheel, wooden and iron-bound wheels, a handcart and a wheelbarrow. The `Wooden Cogwheel` is fully modelled (icon, mesh, pile mesh, carry attachment) but `IsAbstract`; its only producing recipe is assigned to no building, and it is requested only by the abstract `Test all` landing party and orphan objective templates. The wheel/cart recipes carry EE's `NoMVP.` prefix (its internal "not part of the MVP" marker — see the NoMVP section above) and were superseded before ship.

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| Wooden Cogwheel (resource) | `80ca9b4e-06cd-4a53-9cfb-a63af8a00a08` | `core/gdb/resources.gd.xml` | All refs abstract; producing recipe in no building, not in techtree |
| WoodenCogWheelRecipe | `3c7515a2-752a-48c4-913f-b23ea8d9997d` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; assigned to no building |
| NoMVP.WoddenWheelRecipe | `ec10ab0a-f42c-485c-9efb-4d0fb594d90a` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; child of `BaseRecipe1In1Out` |
| NoMVP.IronboundWheelRecipe | `96398754-6d6b-4164-84de-bf0dec514936` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| NoMVP.HandcartRecipe | `78faccbf-fc66-4c2c-a76c-3095d8cf3756` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| NoMVP.WheelbarrowRecipe | `23fcda6d-a4e9-48da-b7f1-19cf2f4306a0` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; inputs point at deleted NoMVP resources |

### Unbuilt copper-tools tier

A complete first-generation **copper tool tier** — one fully-authored recipe per tool — that no building or techtree ever wires up. The game instead ships iron tools and a later workshop "3-copper" tier (`Axe3CopperRecipe`, `Pincers3CopperRecipe`, … are live); the single-tool copper recipes below, plus the orphaned `Pickaxe3CopperRecipe`, are the cut remnants. Their shared intermediate good, `Hardwood Boards`, is dead alongside them (consumed only by these cut recipes; the live chain uses Hardwood Trunk).

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| AxeCopperRecipe | `7f870c83-ab09-4acc-9644-5766ba2e0a04` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; superseded by `Axe3CopperRecipe` |
| HammerCopperRecipe | `632fb9a3-2e6d-4bf7-9dd6-dc3ad88d00d1` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| ChiselCopperRecipe | `984199e8-028d-4816-aa73-7f123e363208` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def (unlike live `ChiselIronRecipe`) |
| KnifeCopperRecipe | `4d8c53ee-5d75-4314-b314-c29818fe4b69` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| PickaxeCopperRecipe | `b67670de-ff96-40f2-8d73-5e0364a64392` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| PincersCopperRecipe | `2e74ff04-1a57-4992-ab5f-558abffb3093` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; live tier is `Pincers3CopperRecipe` |
| SawCopperRecipe | `793c130c-496e-4894-a365-642f59d4b53b` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; live saw is `SawIronRecipe` |
| ShovelCopperRecipe | `86d28a9e-46fb-4c5a-9ac8-3e84ac942b97` | `core/gdb/productionrecipes.gd.xml` | No reachable ref (GUID returns 0 hits — likely already removed) |
| SickleCopperRecipe | `42a83f80-1616-411c-b1d5-39b0d5ac0d7a` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| Pickaxe3CopperRecipe | `2522ca7a-d04a-42c6-a0fe-ee437038e544` | `core/gdb/productionrecipes.gd.xml` | Single self-def; orphaned 3-copper variant (siblings are live) |
| Hardwood Boards (resource) | `aa3e19cc-14d9-439f-b4d9-ceedfaa620f2` | `core/gdb/resources.gd.xml` | Consumed only by cut copper-tool recipes; live chain uses Hardwood Trunk |
| HardwoodBoardsRecipe | `184dd446-7031-459a-9c95-270cfa97f8e2` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |

### Cut "Stone Mason" building and its dressed/border-stone chain

An entire production **building** was cut: `Stone Mason` ships as an `IsAbstract` entity with full assets (`icon_build_stonemason_001.png`, name, description) but is not buildable. Its two recipes — `DressedStoneRecipe` (Stone Block) and `BorderStoneRecipe` (Border Stone) — and the `Border Stone` resource go with it. Note the live game still has the **Stone Block** resource: it survived because the Quarry / Master Quarry produce it directly, so only the Stone Mason *path* is dead, not its output good.

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| Stone Mason (building) | `8ba32781-9df4-426e-b32b-924d06479df9` | `core/gdb/buildings.gd.xml` | `IsAbstract`; not in techtree, not placed on any map; no reachable ref |
| DressedStoneRecipe | `1d493222-c92d-4937-8801-dbdbf3b653a2` | `core/gdb/productionrecipes.gd.xml` | Run only by the abstract Stone Mason; Stone Block comes from the Quarry instead |
| BorderStoneRecipe | `ecd1d7b5-671a-4451-b1e6-da13228c5866` | `core/gdb/productionrecipes.gd.xml` | Abstract self-def + one `<Recipe>` ref, both inside the abstract Stone Mason |
| Border Stone (resource) | `7486fadc-0bc5-4b88-ab3c-ed736549ff68` | `core/gdb/resources.gd.xml` | Fully modelled; every ref inside abstract entities; no live consumer |

### Cut elemental-sorceress upgrade line

The spiritual training building (Arcane Academy) trains a base sorceress, but the **elemental upgrade variants** — Mountain II/III and Water II/III — were cut, and they take their cut recruitment goods (gem staffs and wands) and the staff/wand production recipes down with them. The catalog shows the cross-links (e.g. `Ruby Staff` is `RecruitCostFor` `Sorceress Mountain III`, `Sapphire Wand` for `Sorceress Water II`) — but every entity in the chain is unreachable.

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| Sorceress Mountain II (unit) | `1c370f86-3b3a-46fa-9310-526e3d27b6d4` | `core/gdb/units.gd.xml` | Single abstract self-def; never spawned or recruited |
| Sorceress Mountain III (unit) | `179a94aa-5b09-4994-8939-eaa8a9098328` | `core/gdb/units.gd.xml` | Single abstract self-def |
| Sorceress Water II (unit) | `29c09296-1d1b-4087-b1ae-ac8d54d34d1f` | `core/gdb/units.gd.xml` | Single abstract self-def |
| Sorceress Water III (unit) | `fa678790-18eb-4183-b9ab-db86523b891b` | `core/gdb/units.gd.xml` | Single abstract self-def |
| Ruby Staff (resource) | `bff6017c-6ed4-4ad1-8440-062bb98fc8af` | `core/gdb/resources.gd.xml` | All refs abstract; recruit-cost for cut Sorceress Mountain III |
| Sapphire Staff (resource) | `c0ea5d13-f32f-4ed9-9902-7d92f24dffeb` | `core/gdb/resources.gd.xml` | All refs abstract; recruit-cost for cut Sorceress Water III |
| Iron Wand (resource) | `583dfb98-33d4-4ee1-acbe-e676d1ea8b11` | `core/gdb/resources.gd.xml` | Produced/consumed only by cut staff/wand recipes |
| Ruby Wand (resource) | `bf16030f-9b00-4024-8201-556efc8fe760` | `core/gdb/resources.gd.xml` | All refs abstract; recruit-cost for cut Sorceress Mountain II |
| Sapphire Wand (resource) | `48f7675f-d379-470c-9799-6def7c632cf8` | `core/gdb/resources.gd.xml` | All refs abstract; recruit-cost for cut Sorceress Water II |
| IronStaffRecipe | `06482375-1229-4f5c-9397-80224a025d5e` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| IronWandRecipe | `57a89026-cfb0-4c8d-b104-623f44d09602` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| EmeraldStaffRecipe | `d8353c68-6702-4079-bd6a-d0f86fdbd38e` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| DiamondStaffRecipe | `e53fd615-fa1b-403f-872c-a463bebe373e` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| RubyStaffRecipe | `91a530ef-4750-4586-8f5d-8212d112dc73` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| StaffRuby | `4450a182-fd22-4483-b1d0-2619be22223e` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| StaffSapphire | `9658c9a8-d802-4c0a-a14c-591fe8252711` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| WandRuby | `ec07af1d-f5bb-4cf7-83b5-49163bde8b89` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| WandSapphire | `699b3bb5-aad2-4e05-ad77-8dc5e7c68f61` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |
| NoMVP.SapphireStaffRecipe | `c9d9944a-3786-4d17-a051-9f6a49b3b28e` | `core/gdb/productionrecipes.gd.xml` | Single abstract NoMVP self-def |

### Superseded revisions (`*Old`, `Deprecated`, variant)

A textbook revision pattern: a recipe or building is reworked, the new version keeps the clean name, and the predecessor is renamed `…Old` / `… Deprecated` and left dangling. In each case the live twin is wired into a real building (and often a campaign map) while the `Old` GUID appears only at its own self-definition.

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| Copper Hut old (building) | `ec98dbc8-a92f-4f11-b080-07f314900876` | `core/gdb/buildings.gd.xml` | `IsAbstract`; refs only within its own block; live `Copper Hut` is separate |
| Artisan Blacksmith 3x4 Deprecated (building) | `2323e08a-8727-4a54-9888-8469ebc8f23e` | `core/gdb/buildings.gd.xml` | `IsAbstract`; single self-def, no external ref |
| Guild Hall 3x3 Deprecated (building) | `301c11c1-8a39-4d56-b6e0-09517f4f6c51` | `core/gdb/buildings.gd.xml` | `IsAbstract`; single self-def, no external ref |
| IronChainMailRecipeOld | `a5382b19-9d60-49f8-958f-493e058e1433` | `core/gdb/productionrecipes.gd.xml` | Single self-def; live `IronChainMailRecipe` in building + campaign map 2 |
| IronShieldRecipeOld | `ae7b6381-4bae-450d-ab85-2fbc10203262` | `core/gdb/productionrecipes.gd.xml` | Single self-def; live `IronShieldRecipe` in building + campaign map 2 |
| SteelDaggersRecipeOld | `ea74ce7e-6f51-4963-9cbd-9acd0b634fd3` | `core/gdb/productionrecipes.gd.xml` | Single self-def; live `SteelDaggersRecipe` in smith + campaign map 4 |
| SteelHalberdRecipeOld | `0522135b-4353-4e16-a00a-463ae77b97f6` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; live `SteelHalberdRecipe` exists |
| SteelMaceRecipeOld | `752e8098-b66b-4172-8b9c-15835210711e` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; replaced by `SteelMaceRecipe` |
| SteelStaffRecipeOld | `d391ee15-5bd2-406d-8135-8d4e6950f853` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; live `SteelStaffRecipe` in building |
| BronzeSwordRecipe | `90938792-54c8-4374-bbfe-de3cfc96da19` | `core/gdb/productionrecipes.gd.xml` | Single self-def; superseded by `BronzeSword1CopperRecipe` |
| ReinforcedLeatherArmorClothRecipe | `4a821cbf-2c0b-4a29-a110-715c46ba28de` | `core/gdb/productionrecipes.gd.xml` | Single self-def; superseded "cloth" armor variant |
| WoodenSpearRecipe | `333a3462-46df-4592-989b-47c57da8ea21` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def; superseded by `StoneSpearRecipe` |
| MacheteRecipe | `0d13639b-ad9e-45c8-871d-938c01fa7449` | `core/gdb/productionrecipes.gd.xml` | Single abstract self-def |

### Miscellaneous cut leftovers

| Entity | GUID | Defining file | Evidence |
| --- | --- | --- | --- |
| NoMVP.GoldCoinsRecipe | `a585e9c2-6656-4370-9c78-7e8387541c2f` | `core/gdb/productionrecipes.gd.xml` | Single abstract NoMVP self-def; live `GoldCoinsRecipe` ships |
| NoMVP.GoldIngotRecipe | `015acb0f-9113-4df1-b2c1-5e2c381b0e4d` | `core/gdb/productionrecipes.gd.xml` | Single abstract NoMVP self-def; live `GoldIngotRecipe` ships |
| Enhanced Robe (resource) | `1de66cab-46f5-4b53-b675-a03264eb8458` | `core/gdb/resources.gd.xml` | `IsAbstract` with assets; `UsageCount=0`, no producer/consumer |
| TotemFlash (ability) | `38d849eb-1afa-4161-9336-9871e7f44a04` | `core/gdb/abilities.gd.xml` | Concrete ability (InstantKill VFX) referenced by no unit/encounter |
| DecorativeBuilding006 "Village Tree" | `dd28349a-3856-4093-83af-12ae6a8a71e7` | `core/gdb/decorations.gd.xml` | Single abstract self-def; not placed by mapgen/terrainprops |
| DecorativeBuilding008 "Bird Bath" | `964b8004-ce22-4d7c-bd91-c3fde30fe58f` | `core/gdb/decorations.gd.xml` | Abstract; re-shipped later as the DLC1 Birdbath |
| DCL1DecorativeBuilding33 FestivalGround (building) | `d784a9d4-0f4e-442e-b01a-2c54290d2195` | `dlc1/gdb/buildings.gd.xml` | Concrete, real assets, but no techtree/map placement |
| Veteran Adventurer B (unit) | `5d7a7041-9f1d-478b-a02b-e27c00312bd5` | `core/gdb/units.gd.xml` | Single self-def; the unused half of a NoMVP A/B ranger pair |
| CheckifObsolete.Woodworks_Worker (unit) | `9d670423-7917-49df-830e-e59b70c81db9` | `core/gdb/units.gd.xml` | Abstract; the name *is* EE's own "check if obsolete" dev marker |

### What yielded nothing, and what this means for modders

- **Player-buildable buildings:** no fully-cut *buildable* building exists — every concrete building reaches a techtree node or a map. The cut buildings above are all `IsAbstract` (Stone Mason, the two `Deprecated`/`old` revisions) or an unplaced decorative DLC entity. The ~44 other "not in techtree" buildings are **system pseudo-entities** (ranges, markers, focus points, toggle-order buttons, `Tag Impress *` groups) — not content.
- **Implication.** Treat `IsAbstract="true"` entities, `NoMVP.*`, `*Old`, `* Deprecated`, and `CheckifObsolete.*` as **internal/cut** — do not target them by GUID or name in a mod; they may vanish or get renamed without notice (consistent with the `NoMVP.Beer → Beer` precedent above). If you *want* to revive one (e.g. re-enable the copper-tool tier), the recipe bodies are intact — you only need to assign the recipe to a building and add a techtree unlock.

### Reproducing and extending this audit

The fast path is **catalog-first**: read the relevant `generated/catalog/*.csv` before grepping raw XML. `resource-flow.csv` (`ProducedBy`/`ConsumedBy`/`UsageCount`) and `building-production.csv` (real building→recipe wiring) answer most reachability questions in one pass; only then confirm survivors against the `.gd.xml` (remembering the two gotchas above: shell-`grep`, not the indexed tool, on `productionrecipes.gd.xml`; and that `building-production.csv` keeps abstract buildings). If "is X reachable / used?" becomes a recurring question, it is worth promoting to a generated artifact — e.g. a `reachability.csv` that flags each entity `reachable | abstract-only | orphan` with its decisive reference — so future audits are a single table lookup rather than a re-derivation.

## How to refresh this log

Every game-version update worth a snapshot is also worth a quick pass through these probes. Re-run the queries in this doc against the new `game-gdb/`:

- `grep -rh 'InheritanceMode="[^"]*"' game-gdb/ | sort | uniq -c | sort -rn` to enumerate inheritance modes
- `find game-gdb -name "*.gd.xml" -type f | grep -v "/gdb/"` for files outside the canonical convention
- `node generated/_curiosities.js` would give the dangling-reference list if you have that local script around (it's transient — gitignored under `generated/`)
- for the [dead / cut content](#dead-cut-content-fully-authored-but-unreachable) set: re-diff `generated/catalog/recipes.csv` against `building-production.csv` (recipes assigned to no building) and scan `resource-flow.csv` for `UsageCount` 0 / abstract-only resources, then re-verify survivors against the raw XML. Watch for `NoMVP.*` / `*Old` / `* Deprecated` / `CheckifObsolete.*` naming markers appearing or disappearing across versions

When a new quirk shows up, add it here. When an old one resolves itself in a new version, mark it resolved rather than deleting the entry — the history of "what was once weird" is valuable context.
