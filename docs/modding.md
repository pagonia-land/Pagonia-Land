# 🧪 Modding

This section covers everything about **authoring mods** and **understanding how the modding ecosystem fits together** — from "I want to change one building cost" all the way to "I'm shipping a Pattern B overlay pak with cross-pak entity merging".

If you're new to Pagonia modding, start with [First Modding Experiments](first-mods.md) for a guided edit. The pages below assume you've already got your local game data set up — see [Local Setup](setup.md) if you haven't yet.

## Practice

Concept guides and reference docs for actually writing and shipping mods:

| Page | What it covers |
|---|---|
| [⛏️ Sandbox](../sandbox/README.md) | The local workshop layout under `sandbox/` for writing your own mods. Three-step workflow (extract → patch → `sandbox/out/`), plus the `sandbox-pack` script to bundle the patched output into a `.pak`. Start here when you're ready to author. |
| [✅ Safe Edits](safe-edits.md) | What changes are low-risk vs. likely to break things. Read this before your first edit. |
| [⚠️ Modding Risk Map](modding-risk-map.md) | Specific high-risk areas: objectives, tech tree, unit attachments, rare components. Practical "be careful here" map. |
| [🚚 Mod Distribution Patterns](mod-distribution.md) | The three observed mod shapes: Pattern A (patched canonical pak), Pattern B (side-by-side overlay), Pattern C (user maps). Plus Meadowsong cross-pak entity merging. |
| [📄 Declarative Patch Format](mod-patch-format.md) | The `mod.yaml` schema, the ten patch operations, target resolution, optional metadata. The reference for hand-authoring patches. |
| [✨ Mod Tweaks](mod-tweaks.md) | User-adjustable mod values — how an author declares them, a curator presets them in a collection, and a player configures them in the manager. The end-to-end map of the feature. |
| [💥 Patch Conflict Model](mod-conflicts.md) | What counts as a conflict between two mods, how the patcher detects it, the resolution model the manager surfaces. |
| [📚 Mod Collections](mod-collections.md) | Curated mod lists, lockfiles, profiles, collection-vs-mod boundary, safety rules. |
| [🗂️ Mod Catalogs](mod-catalogs.md) | Curated lists of mod-distribution repos the manager subscribes to and aggregates into one `catalog browse` view — for users discovering mods and for publishers curating their own list. |
| [🏷️ Mod Tags Vocabulary](mod-tags.md) | The shared tag vocabulary used across `mod.yaml`, collections, catalogs, and repo indexes so users can find — and tooling can filter — mods. |
| [🔗 Patcher and Manager Architecture](mod-manager-architecture.md) | How the three tools (`pagonia-patcher`, `pagonia-paker`, `pagonia-manager`) compose, the JSON-schema + diagnostic-code contracts integrators consume. |

## Worked Examples

Step-by-step traces and small edits to learn by doing — the fastest way to internalise the patterns above:

- [💡 Worked Examples index](examples/README.md) — overview of the example library
- **Traces** — follow a value across files without editing anything (sawmill chain, full production chain, resource deposit to building, DLC production addition, map objective, artifact / treasure / shrine, unit attachments, tech tree unlock, tools-editor sediment)
- **Small Edits** — change a building construction cost, a recipe resource amount, a resource display name or icon, a unit recruitment cost, create a decoration variant
- **Patch Examples** — complete `mod.yaml` files for cheaper-sawmill, optional DLC patch set, resource icon patch, intentional conflict example
- **Collection Examples** — curated `.collection.yaml` manifests with lockfiles
- **Repo & Catalog Layouts** — full reference layouts to fork when publishing: sandbox templates, mod-repo layouts, a multi-mod repo example, a mod catalog example

Every page in this section uses synthetic placeholder values where examples need them; the patterns transfer to real GameDatabase content unchanged.

## Tools You'll Reach For

| Tool | When |
|---|---|
| **`pagonia-manager`** | Installing mods, managing profiles, deploying to a real game install with rollback. Most modders only need this. |
| **`pagonia-patcher`** | Hand-authoring patches, validating `mod.yaml` files, schema-validating before publishing. `validate-mod` also **advises on overlay conflict risk** — prefer additive `Incremental`/`Template` over destructive `Replace`/`Unload` so your mod stacks cleanly with others (see [Patch Conflict Model](mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor)). |
| **`pagonia-paker`** | Inspecting `.pak` archives, building Pattern B overlay paks, classifying downloaded mods. |
| **[Catalog Browser ↗](https://pagonia-land.github.io/Pagonia-Land/catalog/)** | Searching the extracted GameDatabase by entity / building / recipe / GUID — invaluable when figuring out what to patch. |

Downloads on the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest), or via the [Download](downloads.md) tab.

## Where Modding Fits

The Pagonia Editor that ships with the game lets you author all of it — maps, scenarios, GameDatabase changes — and publish it as a `.pak` mod; the [community wiki on wiki.gg](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding) covers using it. Pagonia Land **complements** it, not replaces it: a structured map of the GameDatabase, declarative patches with conflict detection for the text / CI path, and the install / deploy / rollback lifecycle.
