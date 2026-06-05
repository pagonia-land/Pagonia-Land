# Pagonia Land — Official Mods

This is the **first-party mod-distribution tree** for the Pagonia Land project: a small set of curated, safe quality-of-life mods and presets for [Pioneers of Pagonia](https://mod.io/g/pioneers-of-pagonia), maintained against the current game version. The official catalog points here, so these are what a fresh manager install can browse and install on day one.

It is a real, published mod-distribution repo in the [`mod-distribution`](../docs/mod-distribution.md) shape — `index.yaml` at this folder's root, one folder per mod under `mods/`, presets under `collections/`. It is hosted in a **subdirectory** of this tooling monorepo (rather than a separate repo), reachable via the catalog repo entry's `indexPath` and the install spec `gh:pagonia-land/Pagonia-Land:official-mods/<mod-id>`.

> **Distinct from [`examples/mod-repo-example/`](../examples/mod-repo-example/).** That tree is a *minimal teaching fixture* a modder forks as a starting point (fictional / `pagonia-land.example.*` ids). This tree is *genuine first-party content* (`pagonia-land.mods.*` / `pagonia-land.collections.*` ids) that real users install.

## What's here

| Mod | What it does |
|---|---|
| `pagonia-land.mods.cheaper-sawmill` | Sawmill construction: −1 Softwood Trunk, −1 Stone Block |
| `pagonia-land.mods.cheaper-quarry` | Quarry construction: −1 Softwood Trunk |
| `pagonia-land.mods.bigger-storage` | Storage building's two main ground piles: 32 → 40 |

| Collection | Bundles |
|---|---|
| `pagonia-land.collections.cheaper-buildings` | Cheaper Sawmill + Cheaper Quarry |
| `pagonia-land.collections.starter-qol` | Cheaper Sawmill + Quarry + Bigger Storage |

All mods are Pattern A (XML-only) `replaceValue` edits at `risk: low` — small, reversible numeric tweaks, no new entities, no pak overlays.

## Installing

Once you've subscribed to the official catalog (or it's the default subscription):

```powershell
# A single mod
pagonia-manager install --from gh:pagonia-land/Pagonia-Land:official-mods/pagonia-land.mods.cheaper-sawmill

# A preset
pagonia-manager collection install --from gh:pagonia-land/Pagonia-Land:official-mods/pagonia-land.collections.starter-qol --as-profile starter --activate
```

## Maintenance

Every mod declares the `gameDatabaseVersion` it was authored against (currently `1.3.1-11826+193733`). When the game updates, each mod's patches are re-checked against the new GameDatabase and the `gameDatabaseVersion` strings are bumped — see the project's update playbook. The manifests + patch files are schema-validated on every push (via `scripts/preflight.ps1`), and the patches are checked against the local `game-gdb/` snapshot with `pagonia-patcher plan` before release.

## Related

- [Mod Distribution Patterns](../docs/mod-distribution.md) — the `index.yaml` + `mod.yaml` format this tree follows.
- [Mod Catalogs](../docs/mod-catalogs.md) — how the official catalog references this tree via `indexPath`.
- [Mod Collections](../docs/mod-collections.md) — the preset format used under `collections/`.
