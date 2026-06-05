# Pagonia Land — Official Catalog

[`official.yaml`](official.yaml) is the **official, curated catalog** of Pagonia Land mod repos — the default subscription in Pagonia Land Manager. A [catalog](../docs/mod-catalogs.md) is a curated list of mod-distribution repos (and optionally other catalogs) that the manager aggregates across every subscription, with cycle + depth + dedup protection.

It is **self-hosted in this monorepo**: the catalog file lives here under `catalog/`, and its one first-party repo entry points at the [`official-mods/`](../official-mods/) tree *in this same repository* via the entry's `indexPath`. So the official mods and the list that advertises them ship together, versioned in one place.

> **This directory ships publicly.** The manager fetches `official.yaml` over `gh:` from public GitHub, so the catalog file — and the [`official-mods/`](../official-mods/) tree it points at — must be present and reachable in the published repository; it can't be a local-only file.

## Subscribing

```powershell
pagonia-manager catalog add gh:pagonia-land/Pagonia-Land/catalog/official.yaml
pagonia-manager catalog browse
```

New stores are subscribed to it by default (opt-out — `catalog remove` drops it like any other subscription).

## What it lists

- **First-party mods** — the [`official-mods/`](../official-mods/) tree (referenced via `indexPath: official-mods`): curated quality-of-life mods + starter presets, maintained against the current game version.
- **Community repos / catalogs** — added over time by PR (see below).

## Getting your repo or catalog listed

The official catalog is a community on-ramp. Two ways in, decided per submission:

- **A single vetted repo** → a `repos:` entry (`owner`, `repo`, optional `indexPath`, `summary`, `tags`). Use this for one mod-distribution repo you maintain.
- **An independently-maintained community list** → a federated `catalogs:` entry (`- source: gh:owner/repo[/path]`). Use this when you curate your own catalog of several repos and want it surfaced under the official one. Federation keeps you in control of your list; the manager dedups and shows a "vouched by N catalogs" trust signal.

To propose an addition, open a PR editing [`official.yaml`](official.yaml) — the [PR template](../.github/PULL_REQUEST_TEMPLATE.md) has a short catalog-submission checklist. On a PR that touches `catalog/`, CI runs `pagonia-patcher schema-validate --catalog` (via [`scripts/preflight.ps1`](../scripts/preflight.ps1)), so a malformed entry fails before review. CI validation is **structure-only** — a maintainer separately checks that the added source is public, reachable, and a good fit, decides repo-entry vs federation, and merges. Every subscriber sees the addition on their next `catalog refresh`.

## Related

- [Mod Catalogs](../docs/mod-catalogs.md) — the catalog format, federation model, and source types.
- [`official-mods/`](../official-mods/) — the first-party tree this catalog points at.
- [`schemas/mod-patches/catalog.schema.json`](../schemas/mod-patches/catalog.schema.json) — the catalog format contract.
