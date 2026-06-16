---
title: Pagonia Land
hide:
  - navigation
---

<div class="hero" markdown>

# Community Modding Workshop {: .hero__title }

<div class="hero__row" markdown>

<img class="hero__icon" src="assets/icon-128.png" alt="">

**Pagonia Land** is a workshop for modding [Pioneers of Pagonia](https://pioneersofpagonia.com/) — specifically **GameDatabase modding**: changing existing buildings, units, recipes, costs, and objectives.
{: .hero__subtitle }

</div>

<div class="hero__buttons" markdown>
[🚀 Get Started](setup.md){: .md-button .md-button--primary }
[⬇ Download](downloads.md){: .md-button }
[🔍 Catalog Browser](https://pagonia-land.github.io/Pagonia-Land/catalog/){: .md-button target="_blank" rel="noopener" }
</div>

</div>

!!! note "Looking for map editing instead?"
    Designing a map's **terrain, layout, and placement** in the editor — plus asset/texture file formats and the editor's own project format — is the [community wiki on wiki.gg](https://pioneersofpagonia.wiki.gg/wiki/Category:Modding)'s slice. The **GameDatabase changes** the 1.4.0 Pagonia Editor authors *are* covered here — it publishes them as ordinary `.pak` mods our tools handle.

## What You Get

Pagonia Land brings together the things a modder actually reaches for:

- **A structured map of the game's XML database** — entity model, GUID references, system guides.
- **Three command-line tools** covering the full mod lifecycle: editing → installing → deploying.
- **JSON Schemas** that any third-party tool can read against — the integration contract for GUIs, IDE plugins, CI scripts.
- **A growing library of worked examples** — from "change one construction cost" walkthroughs to building a custom Sanctuary ability end-to-end.

The goal is to shorten the path from "I have an idea" to "it works in-game" — whether you're writing your first patch, installing someone else's mod, or building a GUI on top of the contracts we ship.

## Start Here

| Your goal | Where to go |
|---|---|
| First orientation + get your game data set up | [Local Setup](setup.md) |
| Make your first small edits | [First Modding Experiments](first-mods.md) |
| Try a fuller walkthrough | [Worked Examples](examples/README.md) |
| Browse the data interactively | [Catalog Browser](catalog-browser.md) |
| Common questions | [FAQ](faq.md) |

!!! info "Two paths through this site"
    **Just want to install mods?** Grab the [`pagonia-manager` binary](downloads.md) — no clone needed.
    **Want to write mods or explore the database?** Clone or [download the repository](https://github.com/pagonia-land/Pagonia-Land) first; see the [FAQ](faq.md#how-do-i-get-a-local-copy-of-this-repository) for the two ways to get it.

!!! warning "Modding is at your own risk"
    Mods change official Pioneers of Pagonia files. Envision Entertainment cannot provide technical support for modded installs. Changes can introduce bugs, instability, or save corruption. If something goes wrong (black screens, crashes), the EE support team cannot help until all modifications are reverted.

## Tools

Three command-line tools cover the full mod lifecycle. Most modders only use the **manager**.

| Tool | What it does | When you'd use it |
|---|---|---|
| **pagonia-manager** | install / enable / profile / plan / deploy / rollback mods | You want to install or manage mods on a real game install |
| **pagonia-patcher** | declarative `mod.yaml` patch DSL with conflict detection | You're writing a mod by hand at the XML level |
| **pagonia-paker** | list / unpack / pack / patch `.pak` archives | You need to inspect, extract, or repack `.pak` files directly |

Downloads as Native AOT single-file binaries (no .NET runtime needed) on the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest) page.

## What's Inside

Browse the full content via the site navigation. The major tabs:

- **Get Started** — Local Setup, First Modding Experiments, FAQ
- **Database** — Entity model, GUIDs, common fields, package layering; gameplay data (Resources, Buildings, Production Recipes, Units, Objectives); cross-file systems (Artifacts, Combat Boosts, Map/POI Objectives, Shrines)
- **Modding** — Mod Conflicts, Distribution, Patch Format, Tweaks, Collections, Catalogs, Safe Edits
- **Tools** — The `pagonia-manager`, `pagonia-patcher`, and `pagonia-paker` CLIs
- **Schemas** — The JSON-Schema contracts for the mod-distribution formats
- **Reference** — Database Overview, Glossary, Catalog Browser, snapshots, Game Database Changelog

## About This Site

Generated from the [pagonia-land/Pagonia-Land](https://github.com/pagonia-land/Pagonia-Land) repository on GitHub. To contribute see [CONTRIBUTING.md](https://github.com/pagonia-land/Pagonia-Land/blob/main/CONTRIBUTING.md); to report issues use the [issue tracker](https://github.com/pagonia-land/Pagonia-Land/issues).

Pioneers of Pagonia is a trademark of [Envision Entertainment GmbH](https://www.envision-entertainment.de/). This is an independent community project — not affiliated with, endorsed by, or supported by EE.
