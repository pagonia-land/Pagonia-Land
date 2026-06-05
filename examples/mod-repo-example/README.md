# Pagonia Land Example Mod Repo

Reference layout for a Git-hosted Pagonia Land mod repository. Two small example mods plus a collection that ties them into a ready-to-play preset — designed for modders to fork as a starting point when publishing their own work.

> [!NOTE]
> This example currently lives inside the main work repo at [`examples/mod-repo-example/`](.) so it ships, builds, and gets schema-validated alongside the patcher. Once the manager's remote-install flow ships, it will be promoted into a stand-alone `pagonia-land/example-mods` repository the manager can install from.

> [!TIP]
> The same `index.yaml` + `mods/` + `collections/` layout works for any transport: clone-able Git repos, ZIPs uploaded to mod.io, direct download links, or local folders. The manager treats the format as stable and the transport as pluggable — see [`docs/mod-distribution.md`](../../docs/mod-distribution.md#distribution-channels) for the full picture.

## What's Here

| Mod | Version | Description |
|---|---|---|
| [Cheaper Sawmill](mods/cheaper-sawmill/) | 0.1.0 | Reduces the Sawmill's Softwood Trunk construction cost by one. |
| [Apple Icon Test](mods/apple-icon-test/) | 0.1.0 | Cosmetic DLC pack icon swap; demonstrates an optional `dlc1`-only patch set. |

### Presets / Modpacks (Collections)

A **Collection** is a ready-to-play preset — a curated mod list with pinned versions and a load order. One command installs everything and activates a profile.

| Preset | Description | Try It |
|---|---|---|
| [Beginner QoL Pack](collections/beginner-qol.collection.yaml) | Both example mods bundled into one starter setup. | `pagonia-manager collection install --from gh:pagonia-land/example-mods/pagonia-land.example.beginner-qol --as-profile beginner-qol --activate` |

> **Why a catalog?** The [`index.yaml`](index.yaml) at the repo root is the machine-readable version of this table — `pagonia-manager` reads it to discover what this repo offers without crawling folders.

## Install With Pagonia Land Manager

> Requires [Pagonia Land Manager](https://github.com/pagonia-land/Pagonia-Land/releases/latest). Run `pagonia-manager --version` to check you have it. The remote-install commands below ship with the manager's upcoming remote-source release; until then, see *Install Manually*.

Install one mod:

```powershell
pagonia-manager install --from gh:pagonia-land/example-mods/pagonia-land.example.cheaper-sawmill
```

Install a whole collection as a ready-to-play preset (creates a profile + activates it):

```powershell
pagonia-manager collection install --from gh:pagonia-land/example-mods/pagonia-land.example.beginner-qol --as-profile beginner-qol --activate
```

Or install without activating (you switch profiles later):

```powershell
pagonia-manager collection install --from gh:pagonia-land/example-mods/pagonia-land.example.beginner-qol
```

Pin to a specific commit / tag / branch:

```powershell
pagonia-manager install --from gh:pagonia-land/example-mods#v0.1.0/pagonia-land.example.cheaper-sawmill
```

## Install Manually

The remote-fetch commands above ship with the manager's upcoming remote-source release. Until that lands you can already use this repo today:

```powershell
git clone https://github.com/pagonia-land/example-mods
pagonia-manager install --from .\example-mods\mods\cheaper-sawmill
pagonia-manager collection install --from .\example-mods\collections\beginner-qol.collection.yaml --mods-root .\example-mods\mods
```

## Validate

Every push runs [`pagonia-patcher schema-validate`](.github/workflows/validate.yml) against `index.yaml`, every `mod.yaml`, and every collection in this repo. To validate locally:

```powershell
pagonia-patcher schema-validate --repo-index .\index.yaml
pagonia-patcher schema-validate --mod .\mods\cheaper-sawmill
pagonia-patcher schema-validate --mod .\mods\apple-icon-test
pagonia-patcher schema-validate --collection .\collections\beginner-qol.collection.yaml
```

## Authoring Notes

- Every mod has a [`mod.yaml`](https://github.com/pagonia-land/Pagonia-Land/blob/main/docs/mod-patch-format.md) inside its folder.
- The patch operations are documented in the [Pagonia Land docs](https://github.com/pagonia-land/Pagonia-Land/blob/main/docs/safe-edits.md).
- This repo's [`index.yaml`](index.yaml) is what the manager fetches first; keep it in sync when you add or rename mods.
- The repo-layout contract is published as a JSON Schema at [`schemas/mod-patches/repo-index.schema.json`](https://github.com/pagonia-land/Pagonia-Land/blob/main/schemas/mod-patches/repo-index.schema.json); the [Mod Distribution Patterns](https://github.com/pagonia-land/Pagonia-Land/blob/main/docs/mod-distribution.md#distribution-channels) doc covers the channel-status table.

## License

MIT — a `LICENSE` file ships at the repo root once this example lands as a standalone repository.
