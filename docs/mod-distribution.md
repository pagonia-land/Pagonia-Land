# 🚚 Mod Distribution Patterns

Pioneers of Pagonia loads game content from `.pak` archives. Mods are also `.pak` archives, but there is more than one way to build one — and the choice matters for what the mod can do, how it co-exists with other mods, and how it gets distributed.

This page describes the loading model, the three observed mod patterns, and how the tools in this repository fit each pattern.

!!! note
    The loading details below are reverse-engineered from the shipped paks (`core.pak`, `dlc1.pak`, `decorations1.pak`, `tools.pak`) and verified against community mods on mod.io. Sections marked **(empirical)** describe behaviour we observed but haven't yet confirmed against a runtime-debug build or EE documentation — when in doubt, install a mod and verify against your game version. The Meadowsong cross-pak merging primitives (covered in their own section below) are derived from the diff between the pre-Meadowsong `1.2.2-11216+189567` baseline and the public `1.3.0-11768+193445` release.

## The Pak Loading Model

Every shipped pak carries the same standard set of metadata files at known paths:

```text
<pakname>.pak
├── <pakname>/manifest.json     Name, Summary, Author, Image, Dependencies
├── <pakname>/files.json        Key -> Paths map (GameDatabase, Localization, ...)
├── <pakname>/<pakname>.gd.bin  index of *.gd.xml files this module ships
├── <pakname>/memory.bin        opaque 28-byte blob (unused by modding)
└── <pakname>/...               actual content (gdb/, textures, prefabs, audio, gui, ...)
```

`core.pak` additionally ships a global `system.json` at the **pak root** (no `core/` prefix) — that file holds engine-wide settings like camera angles, audio channel counts, and shadow distance.

**The metadata set is not all required.** Two minimum shapes are observed in the wild:

- **Database-contributing paks** (shipped paks, GDB mods) need `manifest.json`, `files.json`, the `<pakname>.gd.bin` index, and `memory.bin`. Without `files.json`+`.gd.bin`, the engine has no path into the module's XML.
- **Non-GDB paks** (user maps under Pattern C, simple Pattern B overlays that only swap a config file) can skip `files.json` and `.gd.bin`. They still need `manifest.json` and `memory.bin` to be loaded as a module. See the Pattern B and C sections below for confirmed examples.

### manifest.json

```json
{
  "Name": "dlc1",
  "Summary": "Meadowsong",
  "Author": "Envision Entertainment GmbH",
  "Image": "",
  "Dependencies": ["core"]
}
```

A pak's `Name` is its **module identity**. `Dependencies` is the list of other module names this pak needs in order to make sense. `core.pak` has an empty dependency list; every other shipped pak depends on `"core"`.

### files.json

```json
{
  "Files": [
    { "Key": "GameDatabase",  "Paths": ["dlc1/dlc1.gd.bin"] },
    { "Key": "Localization", "Paths": ["dlc1/localization"] }
  ]
}
```

`files.json` is the **pointer table** the engine reads at load time. The `GameDatabase` key points at a `.gd.bin` file inside the pak; the `Localization` key points at a folder. Other keys may appear in future packs.

> **The editor writes `Localization` unconditionally.** The 1.4.0 editor emits the `Localization` key whenever it writes `files.json` (i.e. alongside `GameDatabase`), pointing at `<module>/localization` **even when the module ships no compiled `loca_<lang>.bin`** — the folder simply doesn't exist in the pak, and the engine tolerates the dangling pointer. Confirmed against two real editor mods from mod.io (CatDog, "Eye of The Spire"), both GDB-only with a `Localization` key but no `localization/` folder. Our own [pak scaffold](mod-patch-format.md#standalone-overlay-paks-pak-block) deliberately **differs**: it adds the `Localization` key only when a `localization/` folder actually has content, so it never writes a pointer to a missing folder. Both shapes load; ours is the stricter one.

### `<pakname>.gd.bin`

Despite the name, the `.gd.bin` file is **not** compiled game data. It's a small (a few hundred bytes to a few kilobytes) index that lists the absolute in-pak paths of every `*.gd.xml` file this module contributes to the database. The first ~200 bytes of `core.gd.bin` decoded:

```text
03 00 02 2A 00 00 00              header / version
19 00 00 00 c.o.r.e./.g.d.b./...  uint32 length, then UTF-16 LE filename
                                  "core/gdb/abilities.gd.xml"
15 00 00 00 c.o.r.e./.g.d.b./...  "core/gdb/audio.gd.xml"
... (one entry per gdb XML file)
```

The engine reads `files.json -> GameDatabase`, follows it to a `.gd.bin`, walks the path list, and loads each referenced `*.gd.xml` from the pak. That is how the XML files we patch actually reach the game.

**Implication for modding:** if a mod adds a NEW `*.gd.xml`, the matching `.gd.bin` must list it. The XML alone isn't enough — the engine never scans for unlisted files. Replacing an existing XML works because its path is already in `.gd.bin`.

### `localization/loca_<lang>.bin`

The `Localization` key in `files.json` points at a folder (e.g. `dlc1/localization`) holding one compiled blob per language, named `loca_<lang>.bin` (`loca_en_us.bin`, …). The format is inferred empirically from two test paks produced by the 1.4.0 Pagonia Editor ("package gdb with dependency" and "package map with dlc1 and gdb"), so treat it as best-effort. The blob is a **bare sequence of length-prefixed UTF-8 strings, read until end-of-stream** — no header, version, count, or key table:

```text
12 M.Y. .F.e.s.t.i.v.a.l. .G.r.o.u.n.d   7-bit-encoded byte length (0x12=18), then UTF-8
1d W.o.o.k.i.e.e.t.r.e.i.b.e.r.s. ...    0x1d=29, "Wookieetreibers Festiveground"
... (one entry per localized string)
```

Each string is exactly what `System.IO.BinaryWriter.Write(string)` emits: a 7-bit-encoded (LEB128-style) **byte**-length prefix followed by the UTF-8 payload. The strings are value-only and positionally ordered — no keys are embedded, so the engine presumably resolves them by index from the GameDatabase side. Decode one with [`pagonia-paker loca info <loca>`](../tools/pagonia-paker/CLI.md#loca-info-json-report-loca); the byte layout is documented in the paker's [`loca_<lang>.bin` Localization Format](../tools/pagonia-paker/CLI.md#loca_langbin-localization-format) section. (Authoring/compiling these blobs is the editor's job — the paker only reads them.)

### Load order and overrides (empirical)

`core.pak` is loaded first because every other module declares it as a dependency. After that, modules with no remaining unmet dependencies load in some order (alphabetic, manifest-declared, or filesystem-order are all plausible — verify by experiment for the version you target). When two paks contribute a file at the same in-pak path, the later one wins — **EE confirmed this last-loaded-wins rule on 2026-06-06** (see [Dev statement 4](#dev-statement-4)); it had previously been empirical. Mods exploit this by shipping a pak whose `Name` is "after" core in the load order and which carries a replacement at the same path — for example the camera mod on mod.io that ships its own `system.json` at the pak root. (For `*.gd.xml` GameDatabase content the merge model takes over from crude file-level last-write-wins — see [Loading model update](#loading-model-update).)

## Pattern A: Patch The Canonical Pak

> **The pattern our `pagonia-patcher` + `pagonia-paker` tooling primarily supports.**

The modder takes the shipped pak (e.g. `core.pak`), substitutes some bytes inside, optionally adds or removes entries, and produces a **modified version of the same pak**. They (or their installer) drop it back where the original lived, overwriting it.

```text
my-mod.pak           = modified core.pak with XML rule tweaks + a few new assets
~~ replaces ~~
core.pak
```

**When to use:**

- the change is wide (many XML rules across many files)
- the change touches engine-wide globals where overlay semantics get confusing
- the modder is OK with the maintenance cost of staying in sync with patch updates

**Pros:**

- guaranteed to win — there's no override semantics to fight with
- the full power of declarative XML patching (`replaceValue`, `addEntity`, `mergeComponent`, etc.) is available
- conflicts between mods are detected up front by `pagonia-patcher`

**Cons:**

- destructive: every new game version requires re-applying the mod against the fresh pak
- two Pattern A mods that touch the same file fight; only one can win without merging by hand
- bigger artefact (the whole pak is shipped, not just the delta)
- some mod hosts (mod.io) may not accept replacements of shipped game files

**Tooling today:** [`pagonia-patcher apply --game <unpacked-game-gdb>`](../tools/pagonia-patcher/CLI.md) writes patched XMLs into `sandbox/out/`. [`sandbox-pack -BasePak core.pak`](../scripts/sandbox-pack.ps1) reads that tree and `.entries-deleted.txt`, runs `pagonia-paker patch`, and produces the modified pak.

## Pattern B: Side-By-Side Overlay Pak

> **The pattern most mod.io mods use, including the "more zoom" camera mod.**

The modder ships a **new, standalone pak** with a name the game has never seen. Inside, it carries its own `manifest.json` (declaring `Dependencies: ["core"]`) plus only the files it actually wants to override or add. The override files are placed at the **same in-pak path the original lives at** — so a `system.json` override goes at the pak root, because that's where `core.pak` keeps it. When the engine loads modules, it discovers this pak, sees it depends on core, loads it after core, and the **last-write-wins** semantics overlay the mod's files over core's.

A real example — the "System Test" / camera-zoom mod on mod.io. The pak's module name is `system`:

```text
core.pak                          shipped, unchanged
system.pak                        side-by-side overlay
├── system.json                   pak ROOT — overrides core's system.json
├── system.copy.txt               pak ROOT — content "system.json" (purpose unclear, see note)
├── system/manifest.json          { Name: "System", Dependencies: ["core"] }
├── system/memory.bin             standard 28-byte blob
└── system/images/cat_small.image preview image referenced by manifest.Image
```

The override is the single 6 KB `system.json` file at the pak root. Compared with core's defaults, it bumps `CameraMaxDistance` from 130 to 330 (massive zoom-out), `CameraMaxViewAngleNear` from 65 to 85, and a handful of related camera/audio knobs. Everything else in `core.pak`'s `system.json` is preserved by the modder copying the whole file and editing the values they care about — the engine takes the mod's bytes wholesale, there's no per-key merge.

> **Note on `*.copy.txt`.** The System pak ships an 11-byte `system.copy.txt` next to the override, with the literal content `system.json`. Its role isn't confirmed — it could be a build-tool artefact, a hint the author left for themselves, or something the engine reads. Until we can verify by inspecting the loader, treat it as optional.
>
> **Note on `files.json` and `.gd.bin`.** Modules that contribute to the game database (`core`, `dlc1`, …) need both. A Pattern B pak that only overrides static config like `system.json` doesn't add new XML rules, so it can skip both — `system.pak` does, and the engine accepts it.

**When to use:**

- the change is small and self-contained (one config file, one icon, one ability balance)
- the modder wants to ship something that drops in without overwriting shipped files
- multiple coexisting mods are expected
- mod.io distribution is the goal — mod.io's review process is friendlier to "new pak" submissions than to "replacement pak" ones

**Pros:**

- multiple Pattern B mods can coexist if they touch different files
- uninstall is trivial — delete the pak
- survives game updates well unless the override target itself changes shape
- mod.io distribution model fits naturally (one pak per mod, identified by its `Name`)

**Cons:**

- the engine's override semantics are coarse — there is no "merge XML rules", only "this file's bytes win wholesale"
- two Pattern B mods that both override the same file: the later-loaded pak wins wholesale and the other's bytes are silently dropped (the wins rule itself is settled — *later pak wins*; only which of the two loads later is empirical, see [Load order and overrides](#load-order-and-overrides-empirical))
- declaring XML rule additions (`replaceValue`, `addListItem`, etc.) is not directly supported — the modder ships the full overridden XML and accepts that they now own its evolution against future game patches

**Tooling today:** declare a `pak:` block in `mod.yaml` and the patcher's apply step writes the four-file module skeleton (`manifest.json` + `files.json` + `<name>/<name>.gd.bin` + `memory.bin`) under `<pak.name>/`. Run [`sandbox-pack`](../scripts/sandbox-pack.ps1) without `-BasePak` to bundle everything into a standalone engine-loadable overlay pak. The `pak:` block shape is documented in [docs/mod-patch-format.md](mod-patch-format.md#standalone-overlay-paks-pak-block); [`sandbox/examples/standalone-overlay/`](../sandbox/examples/standalone-overlay/) is the worked example.

## Pattern C — User Maps (Editor Output)

!!! important
    **Updated in 1.4.0 (Pagonia Editor Update, beta).** A map published by the 1.4.0 Pagonia Editor is an ordinary GameDatabase-contributing pak — it ships `files.json` + `<mod>.gd.bin` + compiled `localization/loca_<lang>.bin` **and** the map under `usermaps/` (`.popmap` + a map-scoped `*.gd.xml`), so [`classify`](../tools/pagonia-paker/CLI.md) reports `GdbScopes: map-scoped` (its GameDatabase changes apply only when playing that map, not your normal games) alongside a `PopmapCount`. Our tools handle it like any other pak; the editor's authoring UI and project format are EE's domain (see the [community wiki](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding)). The popmap-only description below still fits maps from the **pre-1.4.0** editor (no GameDatabase changes).

The Pagonia Editor lets a player author a map and exports it as a self-contained pak. Inside, the pak follows the **same module layout** as `core.pak` or `dlc1.pak` (a `<modulename>/` folder with `manifest.json` and `memory.bin`), but it skips `files.json` / `.gd.bin` and instead drops the actual map under `<modulename>/usermaps/<mapname>.popmap`. The engine surfaces these in the custom-map browser rather than wiring them into the database.

```text
black forest islands.pak
├── black forest islands/manifest.json
│      { Name: "Black Forest Islands", Author: "NTL",
│        Summary: "Scenarios set on islands inspired by the Black Forest.",
│        Image: "black forest islands/images/black_forst_mp.image",
│        Dependencies: ["core"] }
├── black forest islands/memory.bin                            standard 28-byte blob
├── black forest islands/images/black_forst_m.image            small preview
├── black forest islands/images/black_forst_mp.image           large preview (referenced by manifest.Image)
└── black forest islands/usermaps/black forest islands - village in need.popmap
                                                                the actual map data
```

A few things to notice from a sample of real mod.io user-map paks (`4r70_DnD`, `Black Forest Islands`, `Tales of Pagonia`, `flex17`):

- the module folder name is **always lowercase**, even when the manifest's `Name` is mixed-case ("Black Forest Islands" ↔ `black forest islands/`). This was not just convention: **before game version 1.3.2, the engine failed to load a mod map whose package name was not entirely lowercase.** The 1.3.2 "Free Beer" core update fixed that ("Fixed loading of mod maps when the package name is not written entirely in lower case"), so mixed-case map packages now load — but lowercase remains the safe choice, since installs still on 1.3.1 or earlier reject anything else.
- `Dependencies` is always `["core"]`; DLC dependencies on user maps are possible in principle but none of the sampled maps declared one
- the `Image` field of `manifest.json` is an in-pak path to a `.image` file — the editor stores previews under `<modulename>/images/`, but path conventions vary by author (some use `map_thumbnail.image` at module root, others put everything under `images/`)
- **one user-map pak can ship multiple maps.** `Tales of Pagonia` ships two popmaps in the same pak (`tales of a creeper.popmap` and `tales of swampy hills.popmap`) — each appears as its own entry in the map browser
- `files.json` and `.gd.bin` are absent — the engine doesn't try to read GDB out of a user-map pak
- **older community maps load/play more reliably since 1.3.3.** The 1.3.3 stable hotfix (2026-06-15) fixed *enemy raids not starting correctly on older community maps* and *a rare crash when browsing community maps/packages* — both engine/loader fixes with no GameDatabase change. 1.3.3 is a stable-branch hotfix built *after* the 1.4.0 "Pagonia Editor" beta this repo tracks, so these fixes should fold into a later 1.4.0 beta build rather than being present in the current beta data.

The map can reference units, buildings, recipes, and POI types that exist in `core.pak` and any enabled DLC — but it cannot add new ones. Maps that need new buildings or units have to combine Pattern C (the map) with Pattern B (an overlay pak that adds the new content).

**When to use:**

- the deliverable is a scenario or freeform map for an existing rule set
- the modder uses the editor and wants the official author experience

**Pros:**

- the official, supported authoring path; no engineering needed
- mod.io distribution is first-class
- map content is portable across game updates as long as the referenced GUIDs survive

**Cons:**

- cannot add new entities, recipes, abilities, etc. — only place existing ones
- the editor is the only producer; there's no "build a popmap from scratch in YAML" workflow

**Tooling today:** none. This repository focuses on database-level modding (Patterns A and B). Map authoring belongs to the editor.

## Comparison

| Aspect | A: Patch canonical pak | B: Overlay pak | C: User map |
| --- | --- | --- | --- |
| Modifies shipped files on disk | yes | no | no |
| Can add new entities / rules | yes | yes | no |
| Can rebalance with declarative patches | yes | partial (override-whole-file only) | no |
| Survives game updates | needs re-apply | survives unless override target changes | survives if referenced GUIDs survive |
| Multiple coexisting mods | hard (conflicts on same pak) | fine (different paks) | fine |
| mod.io-friendly | depends | yes | yes |
| Authored by | this repo's tooling | partial — overlay scaffolding wanted | the Pagonia Editor |

A practical rule of thumb:

- **Pattern A** when you want fine-grained XML edits across many shipped rules (the patcher's strength).
- **Pattern B** when you ship a single self-contained override or a small set of new files.
- **Pattern C** for scenarios and freeform maps.

Real-world mods often combine them — e.g. a campaign mod that uses Pattern B to add new buildings and abilities plus Pattern C user maps that place them.

## Tooling Gaps

What the current tooling does NOT cover yet. **Done** items are already shipped — see the linked CLI / schema reference for the surface they expose.

| Gap | Status | What it unblocks |
| --- | --- | --- |
| Encode/decode the `.gd.bin` index format | **Done** | Reading and writing the index is a library + CLI surface (`pagonia-paker gdbin info`). |
| `pagonia-paker patch` updates `<m>/<m>.gd.bin` when a new `*.gd.xml` is added under `<m>/` | **Done** | Pattern A close-the-loop — modders can ship new XML rules in `core.pak` (or any module) and the engine actually sees them, without touching `.gd.bin` by hand. |
| Generate a `<modname>/manifest.json` + `<modname>/files.json` + `<modname>/<modname>.gd.bin` + `memory.bin` automatically from `mod.yaml` | **Done** | A `pak:` block in `mod.yaml` triggers the patcher's scaffold step. `sandbox-pack` without `-BasePak` produces a proper Pattern B overlay pak. See [docs/mod-patch-format.md](mod-patch-format.md#standalone-overlay-paks-pak-block) for the block shape; [`sandbox/examples/standalone-overlay/`](../sandbox/examples/standalone-overlay/) is the worked example. |
| Load-order / override-precedence validation | open (now deterministic) | warn when two Pattern B mods would silently fight over the same in-pak path. EE confirmed last-loaded-wins (2026-06-06), so this can be a precise warning, not a heuristic. |
| Pak-contribution sniffer for downloaded mods | **Done** | `pagonia-paker classify <pak>` reports what a pak contributes as independent signals — `GdbScopes` (GameDatabase reach: **`global`** = affects all your games vs **`map-scoped`** = only that map, both, or empty = none), `PopmapCount` (bundled maps), `OverridesAtRoot` (root file overlays) — plus the module's declared `Name` and `Dependencies`. No single "kind" label: one pak can do several at once (a published editor map ships a GameDatabase *and* a map). JSON report shape pinned by [`schemas/paker/pak-classify-report.schema.json`](../schemas/paker/pak-classify-report.schema.json). |
| Native user-map authoring | far future | the Pagonia Editor owns this today and is unlikely to be displaced. |

## Cross-Pak Entity Merging (Meadowsong)

The pak shapes described above (A / B / C) treat each pak as a unit whose files either patch shipped bytes or overlay other paks file-by-file. **Meadowsong** (shipped as the `dlc1` module on 2026-05-27) introduces a parallel mechanism that operates one level up: across paks, the engine merges every loaded module's GameDatabase XMLs into one logical entity set, and the XML schema itself carries primitives for declaring relations between entities across that boundary.

The information below is collected from two **public statements by an Envision Entertainment developer** describing the new Meadowsong modding capabilities, plus one short **dev reply to a question from a player using the currently-released (pre-Meadowsong) Pagonia Editor**.

**Dev statement 1** (paraphrased and translated from German):
{: #dev-statement-1 }

> With Meadowsong, gdb files can live in different paks; the game loads them all and produces the joint dataset for a game session. Maps can have their own map-specific gdb too. You will discover new ways for entities to depend on each other, beyond the usual hierarchical inheritance: you can selectively **unload** other entities (to play without lumberjacks at all), **replace** other entities (give the base-game mines a longer range), use other entities as a **template** (you still build the regular sawmill, but you can also build a New Great Sawmill that is an improved variant of it), or **extend** other entities (if a mod adds Moonsickle as a resource, the base-game toolmaker should perhaps be able to craft Moonsickle from silver bars too).

**Dev statement 2** clarifies the rollout shape:
{: #dev-statement-2 }

> Behind all of the game is a system called game database. It defines what a building is, or what a unit is. Visuals, features, gameplay balancing, you name it. This does not only apply to units & buildings but to almost everything in the game, such as objectives or dialogs or how the terrain looks. With this change, modders can modify this database by adding new entities or changing existing ones. Initially this is done **per map**, i.e. the changes are only active for a specific map created in the Pagonia Editor. But we also prepare a system where these changes can be applied **globally**, i.e. as long as the mod is active, those changes apply to all maps and game modes.

**Dev reply to a player question.** A player using the currently-released Pagonia Editor (which authors maps, not GameDatabase content) asked whether modders will be able to modify "almost all attributes, values, etc., and even add new ones — for example, new buildings, resources, units" and whether all of that will be editable via the editor or only via files/scripts. The dev's reply:
{: #dev-reply }

> You can [add] new buildings, resources etc. There is an editor for the game database. But importing new meshes into the game is not possible yet.

**How to read this (updated for the 1.4.0 official release):** "There is an editor for the game database" referred to **EE's internal authoring tool** — the same tool the dev team uses to author the shipped GameDatabase content. EE has now surfaced that capability to modders by extending the Pagonia Editor. As of the **1.4.0 official release (2026-06-24)** the editor authors GameDatabase mods both **map-scoped** *and* **globally-active** through a UI — no hand-written `*.gd.xml` required. (EE's May 2026 roadmap had framed this as a two-wave rollout — per-map first, globally-active later in Q3 2026 — but the official 1.4.0 release delivered the global capability early, alongside single-language localization modding.) Hand-writing `*.gd.xml` (Pattern A patches against shipped paks, or Pattern B overlay paks with their own gdb) is no longer the *only* path to global GDB content — it is now the **text / CI / automation / batch-authoring** path alongside the editor's visual one, plus the fallback for pre-1.4.0 game versions and for non-entity assets the editor can't express.

Two concrete consequences for this repository's audience:

- **The globally-active GDB modder UI has shipped (1.4.0 official).** The Pagonia Editor now authors both per-map and globally-active GDB mods directly, via "create / replace / unload / enhance … by using proxies" (the same `InheritedGuid` inheritance model documented under [Confirmed primitives](#confirmed-primitives); "proxy" is the user-facing name, not a new primitive). This repository's patcher + paker remain the practical tools for the **text-based / reproducible / CI** authoring path, for non-entity assets, and for older game versions — they are now a *complement* to the editor, not a stand-in for a missing one.
- **No custom-mesh import (yet).** New entities can reference any existing mesh, texture, audio, or prefab that ships in `core.pak` / `dlc1.pak` / etc., but a mod cannot ship its own 3D model and have the game render it. Practical implication: new buildings/units must reuse the visuals of an existing one (a "Cool New Sawmill" looks like the regular sawmill); custom recipes, balance changes, and entity-shape variations are fully in scope, but anything that needs novel art is blocked until mesh import opens up.

### Dev review follow-up (2026-06-06)

A later round of feedback came from an Envision Entertainment developer who reviewed this documentation site directly. It corrected two assumptions we'd recorded as open. Both are paraphrased and translated from German. (For a one-page register of every engine claim and its confidence level — confirmed / data-derived / inferred — see [What We Know About the Engine](engine-claims.md).)

**Dev statement 3 — `InheritanceMode="Unload"` is real, just unused in shipped content.**
{: #dev-statement-3 }

> `InheritanceMode="Unload"` has been implemented since Meadowsong, but it is unused. A proxy entity A can unload another entity B, with the effect that the final database simply does not contain B — as if it had never existed. This works well only when no other entity C still holds a reference to B (B's GUID appears nowhere else). An effective unload therefore usually means unloading several entities together, so that all of B's dependencies are removed with it. Some parts of the game validate references and others don't, so today the only way to find out what can be unloaded safely — without a crash — is by trial and error.

This **corrects** the inference we'd drawn from the shipped-data scan. The scan was right on the facts: zero entities use `InheritanceMode="Unload"` in 1.3.x. But we wrongly read that absence as "the entity-level Unload mode doesn't ship." It does — EE simply doesn't exercise it in their own content. A pure data scan is structurally blind to an engine feature with no data uses, which is exactly the gap a dev statement fills. The corrected table is in [Confirmed primitives](#confirmed-primitives); the methodology lesson is logged in [quirks-and-anomalies.md → `InheritanceMode="Unload"` ships but is unused](quirks-and-anomalies.md#inheritancemodeunload-ships-but-is-unused).

**Dev statement 4 — the intended model is load-time pak overlays, not external pak manipulation; plus the multi-mod conflict rule.**
{: #dev-statement-4 }

> External manipulation of pak files is not something we intend. Instead our approach is that other paks are able to replace files from the core/dlc paks at load time. Conflicts between several files with the same path (inside different paks) are resolved via the mod load order — the last-loaded mod wins. A newly introduced `.gd.xml` file loads without problems and lands in the merged game database. When it comes to changing core or dlc gdb entities, you should work with `Replace` or `Unload` as little as possible, and use `Incremental` and `Template` instead. If several mods replace the same entity via `Replace`, the entity from the last-loaded mod wins. An entity can, by contrast, be upgraded multiple times via `Incremental`. Once there are several competing gdb mods, it is most pleasant for players if there are as few conflicts among the mods as possible.

Three things this settles:

- **External pak rewriting is a non-sanctioned path.** EE's intended distribution shape is a *separate* overlay pak that the engine merges at load time — exactly **Pattern B** below — not a rewritten copy of `core.pak` / `dlc1.pak`. Our **Pattern A** (byte-level pak patching) stays useful for non-entity assets the merge model can't reach (icons, audio, `system.json`) and for pre-Meadowsong versions, but it is explicitly **not** the path EE plans around for GameDatabase content. We had already de-prioritised it (our standing guidance is "do not extend the byte-level op surface"); this confirms the call rather than changing it.
- **The multi-mod conflict-resolution rule is now known, not open.** For same-path file conflicts and for two mods that `Replace` the same entity, **the last-loaded mod wins** (load order decides). `Incremental` is the exception — multiple mods can each stack additions onto the same inherited entity without clobbering one another. This answers what we'd flagged as "unobserved" in [quirks-and-anomalies.md](quirks-and-anomalies.md#each-inheritedguid-target-is-hit-exactly-once) and "still empirical" elsewhere.
- **Conflict-minimising authoring guidance, straight from EE.** Prefer `Incremental` / `Template` (additive, stackable) over `Replace` / `Unload` (destructive, last-loaded-wins) when editing core/dlc entities, because the destructive modes are precisely where competing mods collide. The fewer entities a mod `Replace`s or `Unload`s, the better it co-exists with the rest of a player's mod set.

### Rollout: planned in two waves, delivered together in 1.4.0

The second dev statement separated the feature into two scopes; EE's May 2026 post-release roadmap framed them as two waves (per-map first, globally-active in Q3 2026). In the event, the **official 1.4.0 release (2026-06-24) delivered both**: the Pagonia Editor now authors map-scoped *and* globally-active GDB mods, and single-language localization modding shipped alongside. The table below maps each scope to the closest pattern in this doc.

| Scope | Status | What it does | Authoring path | Closest pattern in this doc |
| --- | --- | --- | --- | --- |
| **Map-scoped** | **Shipped in 1.4.0** (first as a 1.4.0 beta, then the official release) | Entity changes only active while playing this specific map | The Pagonia Editor exports a map pak that bundles per-map GDB entity changes (see the Pattern C box above) | **Pattern C with own gdb** — a user-map pak that also ships entity changes, applied only for the duration of that map |
| **Globally-active** | **Shipped in the 1.4.0 official release** ("modifications are no longer limited to the package's map"; balancing data — population growth, production ratios, combat, raids, plant growth — now editable) | Entity changes apply across every map and game mode while the mod is active | The Pagonia Editor, **or** a hand-authored standalone overlay pak with its own gdb using the engine's entity-relation primitives | **Pattern B (overlay) with merged gdb** — exactly what the bundled paker's scaffold produces (see [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md)) |

**Localization modding** also shipped in 1.4.0 ("Mods can now contain localization in one language, e.g. adding a new building with a custom name"). The compiled per-language blob an editor mod ships — `localization/loca_<lang>.bin` — is decoded in [`localization/loca_<lang>.bin`](#localizationloca_langbin) above (a bare sequence of length-prefixed UTF-8 strings). What an authoring-side, localization-*only* mod pak looks like — and whether keys move into the blob or stay positional — isn't fully documented yet; this repository's paker can *read* the compiled blob today, but `mod.yaml` has no declarative `localization:` authoring block yet.

The Pattern B scaffold this repository ships is, by construction, the **right shape** for a globally-active GDB mod: the four-file module skeleton plus a `<modname>/gdb/*.gd.xml` that uses the four entity-relation primitives the devs named (unload / replace / template / extend). With the editor now authoring these mods visually, hand-writing that XML is the **power-user / CI / batch** path rather than the only path.

For map-scoped content, the 1.4.0 Pagonia Editor is the intended producer of per-map entity changes rather than hand-authored map gdb. Our [classifier](../tools/pagonia-paker/CLI.md) reports such a published editor map's per-map GameDatabase as `GdbScopes: map-scoped` (distinct from a `global` mod) alongside its `PopmapCount`, so the dual nature is surfaced today.

### Confirmed primitives

The public Meadowsong release (version `1.3.0-11768+193445`, shipped 2026-05-27) exercises three entity-level inheritance primitives in its shipped data. The fourth dev-announced concept — entity-level **Unload** — is **implemented in the engine since Meadowsong but used zero times in shipped content** (confirmed by EE, see [Dev statement 3](#dev-statement-3)). Separately, a pre-existing encounter-component pattern achieves a removal at the encounter-slot level. Both "remove" mechanisms are listed below.

| Dev concept | XML form | Hits in 1.3.x | Inherited entity's fate |
| --- | --- | ---: | --- |
| Template | `<Entity InheritanceMode="Template" InheritedGuid="...">` | 18 | Stays in the merged dataset alongside the new derived entity. |
| Replace | `<Entity InheritanceMode="Replace" InheritedGuid="...">` | 14 | Removed (replaced wholesale by the new entity). With two mods, the last-loaded `Replace` wins. |
| Extend | `<Entity InheritanceMode="Incremental" InheritedGuid="...">` | 19 | Stays; the new entity contributes additional list items, referencing inherited list positions via `<InheritedIndex>N</InheritedIndex>` markers. Stackable — several mods can each `Incremental` the same entity. |
| Unload (entity level) | `<Entity InheritanceMode="Unload" InheritedGuid="...">` (exact attribute shape unverified — **0 shipped examples**) | 0 | A proxy entity removes the inherited entity from the merged dataset entirely. Safe only when nothing else references the target; usually requires unloading dependents together. Engine-confirmed by EE, never used in shipped data. |
| Unload (encounter level) | `<ReplaceSelf><ReplaceWithEntity>00000000-0000-0000-0000-000000000000</ReplaceWithEntity></ReplaceSelf>` inside an encounter component | 17 | The encounter slot resolves to the null GUID and is effectively removed. A distinct, pre-existing mechanism — not the entity-level Unload mode above. |

Worked examples from the shipped `game-gdb/` tree:

- **Template** — `game-gdb/dlc1/gdb/buildings.gd.xml` line 4: `<Entity Name="BuildingBase" Guid="07a9d5fe-..." InheritanceMode="Template" InheritedGuid="21b0ad81-...">`. DLC1 clones core's building base and overrides DLC-specific fields.
- **Replace** — `game-gdb/dlc1/maps/meadowsong map database.gd.xml` line 8827: `<Entity Name="TechTreeUnlockedT4StoneBlocks DLC1CM1 replace" InheritanceMode="Replace" InheritedGuid="cbd1b817-...">`. The Meadowsong campaign's first map swaps in a modified tech-tree-unlock node for stone blocks.
- **Extend** — `game-gdb/dlc1/gdb/buildings.gd.xml` line 5671: `<Entity Name="Bakery DLC1 extension" InheritanceMode="Incremental" InheritedGuid="68ce0420-...">`. DLC1 grows the base-game Bakery's list-shaped components (Costs, BuildupLocators, etc.) with new items, referencing the inherited entries by `<InheritedIndex>N</InheritedIndex>`.
- **Unload** — `game-gdb/core/gdb/campaign map 1 - tutorial.gd.xml` line 4158: `<ReplaceSelf><ReplaceWithEntity>00000000-0000-0000-0000-000000000000</ReplaceWithEntity></ReplaceSelf>`. Campaign-map setup removes a specific encounter slot from the merged dataset.

### Format notes derived from the 1.2.2 → 1.3.0 diff

- **The `InheritanceMode` attribute machinery is pre-Meadowsong**, but barely used. 1.2.2 (the pre-DLC public release) has exactly **one** `InheritanceMode="Template"` use (the abstract `DecorativeBuilding` in `decorations1/gdb/decorations.gd.xml`). Meadowsong's contribution is **wide-scale adoption** — the three primitives jump from 1 use total to **49** across `core`, `decorations1`, and especially `dlc1`.
- **The list-inheritance mechanism is also pre-Meadowsong.** `<InheritedIndex>N</InheritedIndex>` children appear 3,148 times in 1.2.2 and 4,366 times in 1.3.0. Meadowsong leverages this existing tag for the new `InheritanceMode="Incremental"` semantics.
- **The `ReplaceSelf` null-GUID "unload" pattern is pre-Meadowsong** (13 occurrences in 1.2.2, 17 in 1.3.0). It's used by core's campaign maps to remove encounter slots conditionally — not a Meadowsong introduction. It is **distinct** from the entity-level `InheritanceMode="Unload"` mode, which EE has confirmed ships in the engine (since Meadowsong) but is used zero times in shipped content — see [Dev statement 3](#dev-statement-3).
- **The `.gd.bin` index format is stable across the 1.2.2 → Meadowsong jump.** Seven-byte v3 header (`03 XX 02 YY 00 00 00`), where byte[3] tracks `entries.Count - 1`. Our existing `GdBinReader` reads shipped paks and any Meadowsong-era mod indexes without changes — see [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md).
- **The single 1.2.2 `InheritanceMode="Template"` use (`DecorativeBuilding`) survived unchanged** into 1.3.0 — same GUID (`9edc01cd-...`), same `InheritedGuid`, same file. Meadowsong didn't redefine the attribute, it just opened the floodgates on its usage.

The full diff between the 1.2.2 and Meadowsong snapshots is in `generated/diffs/1.2.2-11216+189567_to_1.3.0-11768+193445/` after running `scripts/diff_versions.ps1`. Headline numbers: 44 → 59 XML files, 4,099 → 4,714 entities (+625 added, 10 removed, 68 with changed metadata), 26,162 → 31,776 GUID-like references.

### Impact on the patterns above

- **Pattern A (patch the canonical pak)** loses most of its raison d'être for GameDatabase content: a Meadowsong-aware mod ships its own pak with relation-declaring entities that reference core's GUIDs, and the engine merges them at load time. Pattern A stays relevant for byte-level overrides that aren't entity-shaped (icons, audio, etc.) and for pre-Meadowsong game versions.
- **Pattern B (overlay pak)** becomes the natural shape for modern mods. The four-file scaffold the patcher already writes plus the modder's own `<modname>/gdb/*.gd.xml` using the engine's relation primitives is the complete pre-flight for a Meadowsong mod.
- **Pattern C (user maps)** can now also carry its own map-specific gdb — i.e. a user-map pak can additionally contribute entities. Our [classifier](../tools/pagonia-paker/CLI.md) reports such a pak's per-map GameDatabase as `GdbScopes: map-scoped` together with its `PopmapCount`, so a map-with-gdb is fully described by its signals.

### Loading model update

Pre-Meadowsong, our description of the engine's loading was "later pak wins on same in-pak path (last-write-wins)". For GameDatabase content under Meadowsong this is **no longer the whole picture** — the engine doesn't do crude file-level last-write-wins for `*.gd.xml`; it builds a single merged entity set where every loaded module's XMLs contribute through the inheritance primitives confirmed above (additive / exclusive / destructive). The file-level last-write-wins description still holds for non-GDB assets (config files like `system.json`, textures, audio); for `*.gd.xml`, the merge model takes over.

Where two mods *do* collide on the same entity, EE confirmed (2026-06-06, [Dev statement 4](#dev-statement-4)) that load order still decides — but **at the entity-relation level, not the file level**:

| Collision | Resolution |
| --- | --- |
| Two paks ship a file at the same in-pak path (non-GDB) | Last-loaded pak's file wins (file-level last-write-wins). |
| Two mods `Replace` the same inherited entity | Last-loaded mod's `Replace` wins. |
| Two mods `Incremental` the same inherited entity | **Both apply** — additions stack, no clobber. |
| A mod `Unload`s an entity another mod still references | Unsafe; the reference dangles. EE's guidance: unload the dependents too, or avoid `Unload`. |

The practical takeaway EE stressed: **prefer `Incremental` / `Template` over `Replace` / `Unload`** when editing core/dlc entities, so competing gdb mods produce as few hard conflicts as possible. `pagonia-patcher validate-mod` turns this into authoring-time lint — its [conflict-minimising advisor](mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor) flags destructive modes, warns when an `Unload` would dangle, and (with a game root) spots a `Replace` that could be `Incremental`. The manager runs it on install too.

### Status of "what we built" against this

- The patcher's existing operation set (`replaceValue`, `multiplyValue`, `addValue`, `mergeComponent`, `addEntity`, `removeEntity`, `addListItem`, `removeListItem`, `replaceAttribute`, `replaceNode`) is **byte-level** — it edits the XML of the shipped paks. Meadowsong's relation mechanism is **declarative inside a fresh XML file**: the mod ships its own entity that references the inherited one. These are complementary, not competing — a Meadowsong-aware mod often writes the new XML directly rather than patching the shipped one. **EE has confirmed (2026-06-06) that externally rewriting the shipped paks is not their intended path** ([Dev statement 4](#dev-statement-4)); the sanctioned shape is a separate overlay pak (Pattern B) merged at load time. The byte-level ops remain a legitimate fallback for non-entity assets and pre-Meadowsong versions, but they are not the canonical GameDatabase-modding path and the op surface is intentionally frozen.
- The Pattern B scaffold already produces the right module shape for a global GDB mod. No tooling work is needed there to support the new primitives. With the **1.4.0 official release**, modders can author both map-scoped and globally-active GDB mods directly in the Pagonia Editor (see "Rollout" above) — hand-writing the XML is now the power-user / CI / batch path, and our scaffold wraps it into an engine-loadable pak.
- A future patcher iteration could add declarative `entity:` operations to `mod.yaml` so the patch format describes the engine's merge primitives without the modder authoring the XML by hand. The primitives are confirmed against the public release. **Priority note:** now that the editor authors these mods visually, this is lower-value as a primary authoring surface — its remaining justification is specifically the CI / automation / batch-generation flows the visual editor doesn't serve, so it is gated on concrete demand from that audience rather than on EE's schema settling.


## Distribution Channels

The three patterns above describe how a mod is *built* and *applied locally*. This section is the orthogonal question: once a mod exists on disk, **how does it reach other players?**

Pagonia Land treats the **packaging format** (`index.yaml` + `mods/<id>/mod.yaml` + `collections/*.collection.yaml`) as a stable contract and the **transport** as pluggable. Any source that can deliver that file layout — a clone-able Git repo, a ZIP on mod.io, a direct-download link, a folder on disk — can host a mod. The manager will grow source-adapters per transport over time; the install pipeline behind them is the same.

### What Works Today, What's Coming

| Source | Status | Manager Invocation | Notes |
|---|---|---|---|
| **Local folder** | available | `pagonia-manager install --from <folder>` | The mod sits on disk; user points the manager at it. |
| **Local ZIP** | available | `pagonia-manager install --from <mod.zip>` | Single-mod ZIP; manager unpacks and installs. |
| **Local collection file** | available | `pagonia-manager collection install --from <coll.yaml> --mods-root <dir>` | A `*.collection.yaml` plus a local mods root. |
| **GitHub repository** | available | `pagonia-manager install --from gh:<owner>/<repo>[#<ref>]/<mod-id>` | Raw-URL fetch against a repo that ships an [`index.yaml`](../examples/mod-repo-example/index.yaml). Pin to a tag, branch, or commit SHA via `#<ref>`; the resolved commit SHA lands in the install sidecar so re-running months later names the exact code. The long URL form `https://github.com/<owner>/<repo>/tree/<ref>[/<path>]` (copied from the GitHub web UI) works too. |
| **GitHub collection (with cross-repo refs)** | available | `pagonia-manager collection install --from gh:<owner>/<repo>/<id> --as-profile <name> --activate` | One command from "saw a link in Discord" to "playing the publisher's exact setup". Cross-repo mod refs allowed via the per-mod `source: gh:` field. The lockfile pins each mod entry to a commit SHA + resolved timestamp so a re-install reproduces byte-identical. |
| **Catalog browse (federated)** | available | `pagonia-manager catalog list / add / remove / browse / show` | Subscribe to one or many catalogs (each a curated list of repos). `catalog browse` aggregates across every subscription + every catalog they federate to via the `catalogs:` field, with cycle + depth + dedup protection. Source types: `gh:owner/repo`, `https://…/catalog.yaml` (raw HTTP self-hosting), and `file:path` are all available today. (mod.io is an *install* transport, not a catalog source type — you subscribe to a catalog that lists mods, not to mod.io itself.) See [Mod Catalogs](mod-catalogs.md). |
| **mod.io** | available (Map-only for PoP today) | `pagonia-manager install --from modio:<game>/<mod-id>[#<version>]` | mod.io API resolves the mod to a pre-signed download URL, then the direct-URL ZIP pipeline takes over (streaming download, SHA-256 hashing, zip-slip-guarded extract). Auth is a free read-only API key — embedded in the binary or supplied via `PAGONIA_MODIO_API_KEY` env var; **no personal OAuth token required**. **Pioneers of Pagonia is Map-only on mod.io today** — those map mods are managed by the game's in-game UGC subscription list, not by `pagonia-manager install`. The adapter detects Map-type and aborts cleanly. A non-Map type (gameplay / overlay / localization) requires EE adding it to the mod.io taxonomy — tracked as a future EE outreach. |
| **Direct download URL** | available | `pagonia-manager install --from https://example.com/mod.zip` | Streaming download into a temp file (multi-GB-safe), SHA-256 hashed on the fly, zip-slip-guarded extraction, sidecar records `url:<url>#<sha256>` so re-installs detect drift. The URL is the source identity — no auth, no rate limit, no registry lookup. `http://` is opt-in via `state.yaml.allowInsecureSources`. |

The shape `index.yaml` + `mods/` + `collections/` is the same across **every** future row in this table — only the fetch protocol changes per source. That means a modder who builds a working repo today can move it between transports later (host on GitHub, also publish a ZIP to mod.io, also drop a copy on a fan site) without restructuring anything.

### Validating Any Repo / Package

Whatever transport you ship through, validate the package against the canonical schemas before publishing:

```powershell
pagonia-patcher schema-validate --repo-index path\to\index.yaml
pagonia-patcher schema-validate --mod path\to\mods\<mod-id>
pagonia-patcher schema-validate --collection path\to\collections\<name>.collection.yaml
```

The schemas live at [`schemas/mod-patches/`](../schemas/mod-patches/) and are the **public contract** — any package that passes `schema-validate` is guaranteed to parse cleanly in mod managers, IDE plugins, and future tooling.

### Format compatibility

Every distribution file carries a `MAJOR.MINOR` **format version** — `patchFormatVersion` (a mod), `collectionFormatVersion`, `indexFormatVersion`, `catalogFormatVersion`, and the resolved-lockfile's `collectionLockVersion`. It describes the *shape of the file*, not the mod's own `version` and not the `gameDatabaseVersion`; it moves on its own clock, and only when the file's structure changes. They all sit at `0.1` today.

When a tool reads a file, it compares that version to the latest it knows for that format and behaves predictably across versions — the same rule in the patcher and in the manager:

| File vs. the tool (same format) | What happens | Signal |
|---|---|---|
| same major, same or older minor | reads normally | — |
| same major, **newer** minor | reads anyway, ignoring any newer optional fields | `formatMinorAhead` (info) — "update recommended" |
| **newer** major | refused — a breaking shape this build can't read | `formatMajorUnsupported` (error) — names where to get a newer release |
| **older, retired** major | refused — re-export the file with a current tool | `formatMajorRetired` (error) |
| a value that isn't `MAJOR.MINOR` | refused | `formatVersionMalformed` (error) |

So a package authored against a slightly newer toolchain still opens in an older one (you just get a nudge to update), while a genuinely incompatible one tells you exactly what to do instead of failing cryptically. (One caveat: on the manager's remote-**install** path — which aborts on any reject rather than carrying a diagnostics channel — a tolerated newer-minor reads fine but the `formatMinorAhead` info note is not surfaced; the browse / `catalog show` paths do show it.) Two rules follow for authors:

- **Bump the minor only for an additive, backward-compatible change** (a new *optional* field). Bump the major only for a breaking one (a field removed, renamed, re-typed, or re-meant). Never bump on a game update or a content change.
- **You never hand-migrate an additive bump.** When a tool writes a managed file (a collection export, a lockfile, an `index build`), it stamps the current version; `index build` also raises an older same-major `index.yaml` to the current minor in place (`formatMigratedInPlace`). The file rises the next time a tool touches it.

### The index mirror contract

A repo's `index.yaml` lists a curated entry per mod so the manager can browse a catalog — show every mod's name, version, and safety — **without fetching each mod folder**. That means each entry *duplicates* a subset of the mod's own `mod.yaml`, which is the authoritative manifest. The copy is a deliberate cache, but a cache can drift: edit `safeToRemove` in a `mod.yaml` and forget the index, and the browse list now advertises the wrong thing. `schema-validate` checks the index's *shape*, not whether those copies still match.

`pagonia-patcher index-check <repo-root>` closes that gap by cross-checking the index against each `mod.yaml`. The fields fall into three classes:

| `index.yaml` field | `mod.yaml` source | Class | Drift is… |
|---|---|---|---|
| `id` | `id` | join key | the matching key (and checked: an entry's `id` must equal its mod's) |
| `displayName` | `name` | **mirror** | a bug — flagged |
| `version` | `version` | **mirror** | a bug — flagged |
| `gameDatabaseVersion` | `gameDatabaseVersion` | **mirror** | a bug — flagged |
| `safetyFlags.{requiresNewGame, safeToRemove, multiplayerSafe, campaignSafe}` | the flat fields of the same name | **mirror** (flat ↔ nested) | a bug — flagged (safety-critical) |
| `contentHash` | *computed* (sha256 of `mod.yaml` + referenced patches) | **computed** | a bug — flagged (same-version content drift); `index build` inserts/refreshes it |
| `description` | `description` | **curated** | allowed — the index is a short catalog blurb, the manifest the full text |
| `tags`, `screenshots` | `tags` (+ none) | **curated** | allowed — the catalog may curate its own |
| `path` | — | index-only | n/a |

Because the index is a curated *subset*, an entry may **omit** a mirror field entirely (the catalog simply won't surface it) — that's a curation choice, not drift. Only a field the index *carries* whose value disagrees with the manifest is flagged, since a present-but-wrong copy is what misleads a browsing user.

`index-check` also flags **orphaned** entries (an index entry whose `mod.yaml` is missing on disk) and **missing** entries (a mod folder present but not listed — invisible to the catalog). `pagonia-patcher index build <repo-root>` re-syncs the mirror values from the manifests in place — surgically, preserving formatting — and `index build --check` is the write-nothing CI gate. Wire one of them next to `schema-validate` in your validate job; the first-party [`official-mods/`](../official-mods/) tree runs `index-check` in [preflight](../scripts/preflight.ps1). Full field reference: [patcher CLI → index-check / index build](../tools/pagonia-patcher/CLI.md#keeping-indexyaml-in-sync-with-each-modyaml-index-check-index-build).

That author-side check has a **consumer-side complement** in the manager: when it installs a mod from a repo with an `index.yaml`, it cross-checks what the catalog advertised against the `mod.yaml` it actually downloads and warns (`manager.repoIndexMetadataMismatch`) if they disagree — so even a catalog that skipped the CI gate (or is deliberately misleading) can't quietly hand you something other than what you browsed. See [Mod Catalogs](mod-catalogs.md#how-it-looks-from-the-user-side).

## Mod dependencies & load order

A `mod.yaml` can declare how it relates to other mods — `dependencies`, `incompatibleWith`, `loadAfter`, and `loadBefore` (all lists of mod ids). The manager reads and acts on these:

- **Dependencies.** When you enable a mod (or `plan` / `deploy` / `doctor`), a declared dependency that isn't enabled is flagged (`manager.modDependencyMissing`, saying whether it's installed-but-disabled or not installed). Installing with `pagonia-manager install --with-deps` (or the interactive "install its dependencies?" prompt) pulls the missing ones — and theirs, transitively — from the **same repo** the mod came from first, then your **subscribed catalogs**; an unresolvable one warns (`manager.modDependencyUnresolved`) without failing the rest. Disabling or uninstalling a mod others still depend on warns first (`manager.modDependedUponByOthers`).
- **Incompatibilities.** Two enabled mods that declare each other in `incompatibleWith` are flagged (`manager.modIncompatibleEnabled`) — advisory, disable one.
- **Load order.** At `plan` / `deploy` the enabled set is topologically ordered to honour `loadAfter` / `loadBefore`, with your manual profile order as the **stable tiebreaker** — so a mod that must load after another lands correctly without hand-sorting. A reorder is reported (`manager.loadOrderAdjusted`), never silent; a constraint *cycle* is flagged (`manager.loadOrderCycle`) and those mods are left in your manual order. The interactive *Reorder load order* screen marks which positions are dependency-pinned. This is the cross-mod companion to the [overlay-conflict checks](mod-conflicts.md): load order decides which mod wins a destructive overlay collision.

All of these are **advisory** — they inform and assist, they never block an install, enable, or deploy. Full diagnostic reference: [manager CLI → diagnostics](../tools/pagonia-manager/CLI.md#diagnostic-codes).

### Reference Examples

Working layouts ship under [`examples/`](../examples/):

- **[`examples/mod-repo-example/`](../examples/mod-repo-example/)** — Multi-mod Git repository: three mods + one collection + top-level `index.yaml` + a `.github/workflows/validate.yml` CI job. Fork it as a starting point.
- More examples (single-mod-only, cross-repo curator, mod.io-ZIP variant) will land alongside the corresponding manager capabilities. The [`examples/README.md`](../examples/README.md) index stays current.

## Where Things Live In This Repo

| Document | Covers |
| --- | --- |
| [`docs/mod-patch-format.md`](mod-patch-format.md) | The `mod.yaml` shape used by Pattern A and by the Pattern B side of `entries:`. |
| [`docs/first-mods.md`](first-mods.md) | Worked example of Pattern A end-to-end: write rules, apply, pack, install. |
| [`docs/mod-manager-architecture.md`](mod-manager-architecture.md) | Architecture reference: how `pagonia-patcher`, `pagonia-paker`, and `pagonia-manager` compose, what each tool owns, and the JSON-schema + diagnostic-code contracts integrators consume. |
| [`tools/pagonia-patcher/CLI.md`](../tools/pagonia-patcher/CLI.md) | Patcher CLI contract: commands, exit codes, JSON reports. |
| [`tools/pagonia-paker/CLI.md`](../tools/pagonia-paker/CLI.md) | Paker CLI contract: list / unpack / pack / patch / compress / decompress. |
| [`sandbox/README.md`](../sandbox/README.md) | One-command workflow tying the above together against `sandbox/out/`. |

This document slots in above them as the conceptual map: which of the three patterns am I building, and which tools do I reach for from there.
