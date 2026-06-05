# Pagonia Land Example Catalog

Reference layout for a Pagonia Land mod **catalog** — a curated list of mod-distribution repos plus optional federated references to other catalogs. The manager aggregates across every catalog the user is subscribed to, with cycle + depth + dedup protection, so this format scales beyond one central registry.

> [!NOTE]
> This example currently lives inside the main work repo at [`examples/mod-catalog-example/`](.) so it ships, builds, and gets schema-validated alongside the patcher. Subscribed to via `pagonia-manager catalog add file://<absolute-path>/catalog.yaml` (or `gh:pagonia-land/Pagonia-Land/examples/mod-catalog-example` once a public release ships it).

## What's Here

- [`catalog.yaml`](catalog.yaml) — the top-level catalog listing three repos plus one nested sub-catalog reference.
- [`catalogs/sub-catalog.yaml`](catalogs/sub-catalog.yaml) — a smaller federated catalog the top-level one points at. The aggregator should walk into this file and merge its entries.

The repos listed in both files include the actual [`examples/mod-repo-example/`](../mod-repo-example/) shipped in this work tree (so `catalog browse` produces something real to drill into) plus a couple of fictional curator repos that exist only as catalog entries. The `owner: pagonia-land` / `repo: example-mods` entry names the **future standalone repo** that `examples/mod-repo-example/` is intended to be promoted to (the same forward reference the [mod-repo example README](../mod-repo-example/README.md) makes); today that content lives in this `Pagonia-Land` repo.

## Format Overview

```yaml
catalogFormatVersion: "0.1"

catalog:
  name: My Catalog
  maintainer: my-handle
  description: One-paragraph summary.

repos:                                       # mod-distribution repos this catalog vouches for
  - owner: someone
    repo: their-pagonia-mods
    summary: One-line description.

catalogs:                                    # federation — other catalogs this one trusts
  - source: gh:other-curator/their-catalog
    summary: One-line description.
  - source: file:./catalogs/sub-catalog.yaml  # relative file:// for local nesting (this fixture uses it)
```

Either `repos:` or `catalogs:` can be empty / missing — a catalog is allowed to be a pure aggregator (only references other catalogs) or a pure leaf (only its own listed repos).

## Validate

```powershell
pagonia-patcher schema-validate --catalog .\catalog.yaml
pagonia-patcher schema-validate --catalog .\catalogs\sub-catalog.yaml
```

Both files validate against the public [`schemas/mod-patches/catalog.schema.json`](../../schemas/mod-patches/catalog.schema.json) contract.

## Federation Semantics (Recap)

When the manager aggregates subscribed catalogs:

- **Cycle detection** — A → B → A bails on the second visit and surfaces `manager.catalogCycleDetected` (info; not a hard error).
- **Depth cap** — default 5 hops; beyond that, `manager.catalogDepthCapped` (warning) and stop descending. Configurable via `state.yaml.catalogMaxDepth`.
- **Dedup on `(owner, repo)`** — if multiple catalogs list the same repo, surface it once and record every vouching catalog (`vouched by N catalogs` trust signal).
- **Local cache** — fetched catalogs land under `<store>/cache/catalogs/<sanitized-ref>/`, default 24h freshness; `pagonia-manager catalog refresh` forces re-fetch.

See [`docs/mod-catalogs.md`](../../docs/mod-catalogs.md) for the user-facing walkthrough.
