# Editor Mods and Pagonia Land

The **Pagonia Editor** that ships with the game (since the 1.4.0 "Quality of Life & Pagonia Editor Update") is now the primary, EE-supported way to author GameDatabase mods. This page explains how editor-authored mods relate to Pagonia Land's tools — where the two meet, where they don't, and which to reach for.

The short version: **the editor owns *authoring*; Pagonia Land owns *understanding*, *managing*, and *reproducing*.** They are complementary. Most modders will author in the editor and still use this project's reference catalog and mod manager beside it.

## What the editor authors

As of the 1.4.0 official release the Pagonia Editor authors, through a UI:

- **Map-scoped GameDatabase changes** — entity changes active only while playing a specific editor map (scenarios, custom objectives, story telling).
- **Globally-active GameDatabase changes** — entity changes that apply across every map and game mode while the mod is enabled, including **balancing** (population growth, production ratios, combat units, raids, plant growth, recruitment/production/building costs, worker counts).
- **Single-language localization** — e.g. a custom name for a new building.
- New objectives, dialogues, characters, triggers; new buildings/recipes consuming existing or new commodities.

It does this with "**create, replace, unload or enhance … by using proxies**". A "proxy" is the user-facing name for the engine's `InheritanceMode` inheritance model that this repo documents under [Confirmed primitives](mod-distribution.md#confirmed-primitives) (`Template` / `Replace` / `Incremental` / `Unload`) — it is not a new primitive. Custom 3D-mesh import is still not supported; new entities reuse shipped meshes/textures/audio.

For *using* the editor itself, see EE's [Map database modding wiki](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding). This project deliberately does **not** document the editor's UI or its internal project format — that is EE's, and it changes fast.

## The integration point: a published `.pak`

When you publish an editor mod, the editor compiles its project into an ordinary `.pak` in the same module layout the shipped game paks use (GameDatabase + compiled localization + any map). **That published pak — not the editor's internal project — is the only thing Pagonia Land's tools interact with.**

So a published editor mod is, to this project's tools, just another pak:

- [`pagonia-paker list` / `unpack`](../tools/pagonia-paker/CLI.md) inspect or extract its contents.
- [`pagonia-paker classify`](../tools/pagonia-paker/CLI.md) reports whether it carries `global` vs `map-scoped` GameDatabase content (`GdbScopes`), its `PopmapCount`, and its declared DLC dependencies — useful for understanding a downloaded mod before enabling it.
- [`pagonia-paker gdbin info` / `loca info`](../tools/pagonia-paker/CLI.md) read its module index and compiled localization blob.

## Common questions

**Can I open or convert an editor mod into the patcher's `mod.yaml` format?**
No. The editor's editable *project* format is EE's and is not something this project reads or converts. You can inspect a *published* editor mod's pak with `pagonia-paker` (above), but there is no editor-project → `mod.yaml` importer, by design — it would chase a moving, EE-owned format. The two authoring formats are independent.

**Can the manager install and deploy an editor-published mod?**
If you have the published `.pak` as a file (or in a folder/zip/git repo), the [manager](../tools/pagonia-manager/CLI.md) handles it like any pak-carrying mod. The caveat is *distribution*: for Pioneers of Pagonia, mod.io currently only exposes the **Map** mod type, which the in-game subscription/UGC flow owns — so editor mods distributed through the in-game browser live in that flow, not in `pagonia-manager`. The manager's reach is paks/mods you point it at from a folder, zip, direct URL, GitHub repo, or a subscribed catalog. See [Mod Distribution Patterns → Distribution Channels](mod-distribution.md#distribution-channels).

**How do an editor mod and a hand-authored (patcher) mod behave together on one install?**
Both end up as paks the engine merges at load time, resolved by load order — last-loaded `Replace`/`Unload` wins, `Incremental` stacks (see [the conflict model](mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor)). The manager's cross-mod conflict advisory reasons about the mods *it* manages; it cannot see inside an editor mod's authoring intent, but `classify` can still report that mod's scope and whether it destructively targets core content. Two mods (editor- or hand-authored) that both `Replace` the same entity collide the same way regardless of how each was made.

## Which tool for which job

| You want to… | Reach for |
| --- | --- |
| Author a mod visually, especially a single mod, a map/scenario, or a quick balance change | **The Pagonia Editor** (EE's, in-game) |
| Author reproducibly — text/YAML, git-diffable, CI-validated, batch/programmatic, a multi-mod project | **`pagonia-patcher`** + this repo's [declarative format](mod-patch-format.md) |
| Look up which GUID to target, what a recipe consumes, what changed between game versions, whether an edit is safe | **The reference catalog + docs** ([Catalog Browser](catalog-browser.md), [Database Overview](../DATABASE_OVERVIEW.md), [engine claims](engine-claims.md)) — useful *whichever* authoring tool you use |
| Install, enable, profile, deploy, and roll back mods on a real install; bundle a reproducible collection | **`pagonia-manager`** |
| Inspect or classify a downloaded mod pak before trusting it | **`pagonia-paker`** |

The reference layer (row 3) is the one every modder needs regardless of authoring tool — it is the companion to the editor, not an alternative to it.

## See also

- [Mod Distribution Patterns](mod-distribution.md) — the Pattern A/B/C model, the cross-pak entity merging primitives, and the distribution channels.
- [What We Know About the Engine](engine-claims.md) — the register of confirmed engine/database facts, including the proxy/inheritance primitives.
- [Multiplayer and Modded Saves](multiplayer-modding.md) — how enabled mods bind to saves and behave in co-op (new in 1.4.0).
