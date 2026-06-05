# 🏷️ Mod Tags — Vocabulary Reference

Tags help users find your mod, catalogs filter their listings, and tooling group similar entries. The schemas under [`schemas/mod-patches/`](../schemas/mod-patches/) accept tags in five places:

- `mod.yaml` — per-mod tags
- `collection.yaml` — per-collection tags
- `catalog.yaml` — top-level catalog tags + per-listed-repo tags
- `index.yaml` — per-mod + per-collection tags inside a repo's index

**Tags are free-form** — the only schema constraints are lowercase ASCII, digits, and hyphens (regex `^[a-z0-9][a-z0-9-]*$`), max length 40, unique within a list. You can invent any tag you need.

But unstructured free-form tags hurt discovery: ten modders write `quality-of-life`, `qol`, `qol-fix`, `convenience`, `comfort` for the same thing and nobody can filter. This document lists a **recommended vocabulary** so the common cases converge on the same spellings. The schemas under `schemas/mod-patches/` also surface this list via JSON Schema `examples` so IDEs with schema support can offer it as autocomplete.

## How To Use This List

- **Pick from the recommended tags first.** If your mod fits one of the categories below, use that exact spelling.
- **Combine freely.** Most mods sit in 2–5 tags spanning different clusters (e.g. `qol` + `production` + `xml-only`).
- **Add custom tags when the standard list doesn't cover your case** — e.g. `sawmill`, `windmill`, `marketplace` for very-specific building tweaks. The schema doesn't reject those.
- **Keep tags lowercase and use hyphens, not underscores.** `tech-tree`, not `tech_tree` or `techTree`.
- **Avoid duplicating descriptions.** If your mod is already tagged `military`, you don't also need `combat` unless it covers both the combat-system and military-structure changes.

## Recommended Vocabulary

### 1. Content Domain — what part of the game does the mod change?

| Tag | Use when the mod touches… |
|---|---|
| `buildings` | Building entities — costs, names, hitpoints, slot counts, build menu placement |
| `production` | Production chains — recipe outputs, processing time, worker requirements |
| `recipes` | Specific recipe rows (resource amounts in / out) without changing the chain |
| `economy` | Trade values, marketplace, currency, taxation |
| `resources` | Resource definitions, deposits, gather rates |
| `units` | Settler / soldier / specialist entities — costs, stats, abilities |
| `combat` | Combat rules, damage formulas, formations |
| `military` | Bigger umbrella for `units` + `combat` + military buildings together |
| `tech-tree` | Research progression, unlocks, technology requirements |
| `decoration` | Decorative objects, variants, props |
| `cosmetic` | Pure visual changes (skins, icons, colors) — no gameplay impact |
| `ui` | HUD, menus, tooltips, dialogs |
| `audio` | Sound effects, music, ambience |
| `maps` | Map content, scenarios, objectives, treasure-shrines, artifacts |
| `artifacts` | Artifact / treasure / shrine systems specifically |
| `localization` | Text translations only |
| `tools-editor` | Map editor / tools sub-system |

### 2. Intent — what's the mod's purpose?

| Tag | Use when the mod… |
|---|---|
| `qol` | Quality of life — small convenience improvements (faster build, better tooltips, defaults that don't suck) |
| `balance` | Re-balances values without inventing new content |
| `realism` | Makes the game more realistic / consistent |
| `hardcore` | Makes the game harder |
| `cheat` | Makes the game noticeably easier or removes constraints |
| `cheaper` | Lowers building / unit / recipe costs (narrower than `cheat`) |
| `expansion` | Adds new content (buildings, units, resources, maps) |
| `fix` | Patches a vanilla bug or oversight |
| `tweak` | Small numeric adjustment that doesn't fit `balance` cleanly |
| `overhaul` | Large-scale reworking of a system |
| `compatibility` | Patch-level fix for mod ↔ mod interop |
| `safe-edits` | Mod sticks to "known-safe" XML changes only (no `entries:` ops, no `pak:` block) |

### 3. Technical — how is the mod distributed?

| Tag | Use when the mod… |
|---|---|
| `xml-only` | Pattern A — XML patches only, no `pak:` block |
| `pak` | Pattern B — ships an overlay-pak |
| `standalone` | Runs without requiring any other mod |
| `vanilla-compatible` | Works against the unmodded base game (most mods do; useful tag for collections to highlight) |
| `requires-dlc-<id>` | Needs a specific DLC. The exact suffix depends on what DLCs the game ships with — `requires-dlc-core` for the base game, `requires-dlc-dlc1` for the first DLC, etc. |
| `dlc-aware` | Adapts behaviour based on which DLC is installed but doesn't strictly require one |
| `tweakable` | Exposes a `tweaks:` block — the user can adjust values (multipliers, thresholds, on/off flags) in the manager without editing the mod (see the [Mod Tweaks guide](mod-tweaks.md)). Lets catalog browsers filter for "mods I can adjust". |
| `preset` | This is a Collection — a curated combination of mods, not a single mod (collection-level only) |

### 4. Maturity

| Tag | Use when the mod… |
|---|---|
| `experimental` | Author is still figuring out the design — expect rough edges |
| `alpha` | Functional but unfinished |
| `beta` | Feature-complete, polish in progress |
| `stable` | Author considers it production-quality |
| `archived` | No longer maintained; documented for historical reference |

### 5. Language (Localization Mods)

Descriptive form, not ISO codes — easier to grep and consistent with Anno modding convention.

| Tag | Language |
|---|---|
| `language-de` | German |
| `language-en` | English |
| `language-fr` | French |
| `language-es` | Spanish |
| `language-it` | Italian |
| `language-pl` | Polish |
| `language-ru` | Russian |
| `language-zh` | Chinese |
| `language-ja` | Japanese |
| `language-ko` | Korean |
| `language-pt-br` | Brazilian Portuguese (use `-pt-br` / `-pt-pt` to disambiguate) |

Use one tag per supported language. A mod that ships both German and English text strings carries both `language-de` and `language-en`.

## Examples

```yaml
# A QoL recipe tweak in mod.yaml
modVersion: 0.1
id: my-handle.cheaper-sawmill
version: 0.1.0
name: Cheaper Sawmill
tags:
  - production       # touches a production chain
  - recipes          # specifically the recipe row
  - cheaper          # intent: lowers cost
  - qol              # small convenience improvement
  - xml-only         # Pattern A
```

```yaml
# A catalog tagging itself + its repos in catalog.yaml
catalog:
  name: My Curated List
  tags: [community, qol]

repos:
  - owner: TheLavaBlock
    repo: pop-qol-bundle
    summary: Quality-of-life tweaks
    tags:
      - qol
      - production
      - language-de
      - language-en
```

```yaml
# A collection preset in collection.yaml
collectionVersion: 0.1
id: my-handle.first-playthrough
name: First Playthrough — Easy + Cosmetic Polish
tags:
  - preset
  - cheaper
  - cosmetic
  - decoration
```

## Custom Tags

If your mod doesn't fit the recommended vocabulary, invent a tag. The schema accepts anything matching `^[a-z0-9][a-z0-9-]*$`. Common custom-tag patterns we've already seen in the wild:

- **Building-specific:** `sawmill`, `windmill`, `marketplace`, `armory`
- **Resource-specific:** `wood`, `stone`, `iron`
- **Scenario-specific:** `meadowsong`, `winter-map`
- **Author-themed:** `<author>-style` for a series of mods sharing a design language

The catalog browser's "vouched by N catalogs" dedup runs on `(owner, repo)`, not tags — so two catalogs can apply slightly different tag sets to the same repo and the trust-signal still works.

## What's NOT Recommended

- **All-uppercase tags** (`QOL`, `FIX`) — schema rejects them (regex enforces lowercase first char).
- **Spaces** (`tech tree`) — schema rejects them (use hyphens).
- **Underscores** (`tech_tree`) — schema rejects them.
- **Long descriptive tags** (`makes-the-sawmill-cheaper-by-50-percent`) — keep tags under ~20 chars where possible; put detail in the mod's `summary` / `description` field instead. The schema cap is 40 chars but anything over ~20 reads as a description, not a tag.
- **Version numbers as tags** (`1-0-0`, `v2`) — use the `version` field in the manifest, not a tag.
- **Tool / language tags** (`yaml`, `xml`, `csharp`) — those describe how the mod is _written_, not what it _does_. Skip them.

## Related Documents

- [Mod Distribution Patterns](mod-distribution.md) — channel-status table + Pattern A vs Pattern B distinction the technical tags reference.
- [Mod Catalogs](mod-catalogs.md) — how the `tags` field on catalog entries affects discovery.
- [`schemas/mod-patches/`](../schemas/mod-patches/) — JSON Schema source-of-truth.
