# 🗂️ Mod Catalogs

A **catalog** is a curated list of Pagonia Land mod-distribution repos (and optionally other catalogs). Pagonia Land Manager subscribes to as many catalogs as you want, aggregates the lists with cycle + depth + dedup protection, and gives you one `catalog browse` view across the whole community — without forcing every mod through a single central registry.

This document is for **users** discovering mods and for **catalog publishers** curating their own list. For the underlying repo-distribution format (the `mod.yaml` + `index.yaml` that the catalogs point AT), see [Mod Distribution Patterns](mod-distribution.md).

## How It Looks From The User Side

```powershell
# Subscribe to the official catalog plus a community-curated one
pagonia-manager catalog add gh:pagonia-land/Pagonia-Land/catalog/official.yaml
pagonia-manager catalog add gh:someone/hardcore-presets

# Or subscribe to a self-hosted catalog over HTTPS (no GitHub needed)
pagonia-manager catalog add https://mods.example.com/catalog.yaml

# Show what I'm subscribed to
pagonia-manager catalog list

# Aggregated, deduplicated view across both subscriptions
pagonia-manager catalog browse
```

`catalog browse` walks every subscribed catalog AND every catalog they federate to via the `catalogs:` field, recursively. It deduplicates on `(owner, repo)` and surfaces a `vouched by N catalogs` trust signal when multiple catalogs list the same repo. Per-source fetch failures are non-fatal — the aggregator returns everything it CAN see, with diagnostics for what failed.

## Why Multi-Subscription + Federation

A single central registry doesn't scale: it forces every mod author through a single approval queue, gives a single party gatekeeper power, and can't easily specialise by community. Pagonia Land instead treats catalogs as a **first-class data type** anyone can publish:

- **Anyone can host a catalog** (it's a YAML file in a Git repo, or a local file)
- **Users subscribe to multiple catalogs at once** — official + community + private
- **Catalogs can federate** — a catalog can reference other catalogs, building up a curated graph
- **The aggregator dedups across the graph** so a repo listed in 5 catalogs surfaces once with `vouched by 5 catalogs`

The official catalog (`gh:pagonia-land/Pagonia-Land/catalog/official.yaml`) is one default subscription among potentially many — not the only choice. It is **self-hosted in the tooling monorepo** under [`catalog/`](../catalog/), and its first-party repo entry points at the in-repo [`official-mods/`](../official-mods/) tree via the entry's `indexPath` — so the curated list and the mods it advertises ship together. New stores subscribe to it by default (opt-out via `catalog remove`).

## Catalog Format

A catalog is a YAML file (typically `catalog.yaml`) validated against the public [`schemas/mod-patches/catalog.schema.json`](../schemas/mod-patches/catalog.schema.json) (`catalogFormatVersion: "0.1"`):

```yaml
catalogFormatVersion: "0.1"

catalog:
  name: My Curated List
  maintainer: my-handle
  description: One-paragraph summary of what this catalog covers.
  homepage: https://example.com/my-catalog   # optional
  license: CC0                                # optional
  tags: [community, qol]                      # optional

repos:                                        # optional; can be empty
  - owner: someone
    repo: their-pagonia-mods
    summary: QoL mods by someone, updated through Meadowsong.
    tags: [qol, balance]

catalogs:                                     # optional federation
  - source: gh:other-curator/their-catalog
    summary: Hardcore preset community
  - source: file:./catalogs/sub-catalog.yaml   # relative file:// for nesting
    summary: A nested test catalog
```

Either `repos:` or `catalogs:` can be empty / missing — a catalog is allowed to be a pure aggregator (only references other catalogs), a pure leaf (only its own listed repos), or any mix.

A repo entry may carry an optional **`indexPath`** — a repo-relative directory holding that repo's `index.yaml` (and under which its mod folders resolve):

```yaml
repos:
  - owner: pagonia-land
    repo: Pagonia-Land
    indexPath: official-mods          # index.yaml lives in this subfolder, not the repo root
    summary: First-party mods hosted in a subfolder of the tooling repo.
```

Omit `indexPath` (the default) when the `index.yaml` sits at the repo root — that's the common case. It exists so a single repo can both be something else (e.g. a tooling monorepo) *and* host a mod-distribution tree in a subdirectory. The path must be a safe relative directory (no leading `/`, no `\`, no `.`/`..` segments).

`indexPath` flows through to installs as the `:base` segment of a `gh:` source spec. `catalog browse` shows such a repo as `owner/repo:indexPath`, and you install a mod from it by appending the mod id:

```powershell
pagonia-manager install --from gh:pagonia-land/Pagonia-Land:official-mods/<mod-id>
```

The fetcher resolves the repo's `index.yaml`, mod folders, and patch files under the `:base` subdirectory, and records the `:base` in the install provenance so a re-install (or `profile export`) round-trips the subtree. When two aggregated catalogs vouch for the same repo with *different* `indexPath`s, the first catalog visited wins and the manager surfaces `manager.catalogRepoIndexPathConflict` (warning).

In the **interactive shell** you don't assemble that spec by hand: *Browse Community Catalogs* lists each repo's mods + collections, and installing the one you pick runs the same fetch + validate pipeline — a mod then offers to enable it in the active profile, a collection offers to activate its new profile.

Validate your own:

```powershell
pagonia-patcher schema-validate --catalog .\catalog.yaml
```

## Source Types

| Form | Status | Notes |
|---|---|---|
| `gh:<owner>/<repo>[#<ref>][/<path>]` | available | GitHub-hosted. `<ref>` defaults to `HEAD` and is pinned to a commit SHA at fetch time. `<path>` defaults to `catalog.yaml`. The long URL form `https://github.com/...` is NOT accepted for catalog specs today — use the `gh:` short form. |
| `file:<absolute-or-relative-path>` | available | Local file. Subscription specs with relative paths resolve against the working directory at parse time. Federated `file:./...` references inside a catalog resolve against the parent catalog's directory, so the example fixture works regardless of where you launched the CLI. |
| `https://example.com/path/catalog.yaml` | available | Raw HTTPS-hosted catalog — for publishers who self-host outside GitHub. No commit-SHA pinning is possible (the URL itself is the canonical identity); URL normalisation lowercases scheme + host and strips a trailing slash from non-root paths so two equivalent URLs dedup. |
| `http://example.com/...` | available, with warning | Plain HTTP. Surfaces `manager.catalogInsecureHttp` (warning) on every fetch. Set `state.yaml.allowInsecureCatalogSources: true` to silence the warning for LAN / intranet hosts you've vetted. |
| `modio:...` | planned | Lands alongside the broader mod.io adapter. |

Bare local paths without a transport prefix are rejected on purpose — every subscription must name its transport explicitly so the stored subscription list stays grep-able for `file:` / `gh:` / `https:`.

## Federation, Dedup, Cycles, Depth

When `catalog browse` runs:

1. Every subscribed catalog is fetched.
2. Their `catalogs:` references are followed (BFS).
3. Each visited source is added to a visited-set on its **canonical** string. A → B → A bails on the second visit with `manager.catalogCycleDetected` (info; the user still gets repos from the first visit).
4. A **depth cap** (default 5, configurable via `state.yaml.catalogMaxDepth`) limits how deep the BFS descends. At the cap, the catalog is still fetched and its repos collected, but its descendant catalogs are not enqueued. Surfaces `manager.catalogDepthCapped` (warning).
5. Repos are **deduplicated** on `(owner, repo)`. The first vouch contributes the summary + tags; every subsequent vouch appends to a `VouchedBy` list. The CLI shows `vouched by N catalogs` when N > 1.
6. **Per-source fetch failures are non-fatal**. A 404 / network error / parse error on one catalog adds a diagnostic but doesn't abort aggregation. The user sees everything else.

## Catalog Cache

`catalog browse` and `catalog show` cache fetched catalogs on disk under `<store>/cache/catalogs/<sanitised-canonical>/`:

- `catalog.yaml` — the verbatim YAML bytes from the network fetch.
- `cache-meta.yaml` — a small sidecar with `canonical`, `fetchedAt` (ISO-8601 UTC), `commitSha` (for `gh:` sources), and `sourceType`.

Entries fresher than `state.yaml.catalogCacheStalenessHours` (default 24h) serve from disk on the next access — the manager surfaces a `manager.catalogStale` info diagnostic naming the cache age. After the freshness window elapses, the next access re-fetches and rewrites the cache atomically (`manager.catalogCacheWritten`).

`file:` sources bypass the cache entirely; reading them is already as cheap as reading the cache.

To force a fresh view without waiting for the staleness threshold:

```powershell
# Refresh every subscribed catalog:
pagonia-manager catalog refresh

# Refresh just one:
pagonia-manager catalog refresh gh:pagonia-land/Pagonia-Land/catalog/official.yaml
```

Useful when you know upstream changed (e.g. someone PRed a new entry into a catalog you follow) but the cache hasn't expired yet, or when troubleshooting "the manager seems to see an old view of a catalog I just updated".

## Self-Hosting Without GitHub

You don't need a GitHub repo to publish a catalog. Any host that serves a YAML file over HTTPS works:

- **GitLab Pages / Codeberg Pages / static-site host** — push `catalog.yaml` and tell users to `pagonia-manager catalog add https://yoursite.example/catalog.yaml`.
- **S3 / R2 / GCS** — make the bucket object public-read, point users at the object URL.
- **Generic web host** — drop the file under a stable URL on your own server.
- **Corporate intranet / LAN** — host on an internal HTTPS endpoint; users on the LAN subscribe by URL.

Trade-offs vs `gh:` sources:

- **No commit-SHA pinning.** `gh:` catalogs resolve refs to concrete commit SHAs at fetch time; `https://` catalogs trust the host to serve consistent content. If reproducibility matters, version your URL (`catalog-v1.yaml`, `catalog-v2.yaml`) so older subscribers stay on the schema/state they expect.
- **No built-in change history.** Git gives you free history; a flat HTTPS URL doesn't. Keep your catalog in source control and publish each release explicitly.

Plain `http://` is supported (mostly for LAN / intranet cases) but surfaces a `manager.catalogInsecureHttp` warning on every fetch. To silence the warning, flip `state.yaml.allowInsecureCatalogSources: true` after vetting the host — the warning is purely informational, the fetch happens either way.

## Publishing Your Own Catalog

1. Create a `catalog.yaml` with the shape above.
2. Validate locally: `pagonia-patcher schema-validate --catalog catalog.yaml`.
3. Push it to a GitHub repo and tell people to `pagonia-manager catalog add gh:<you>/<your-catalog-repo>`.
4. Optionally: add a CI workflow that re-validates on every push (see [`examples/mod-repo-example/.github/workflows/validate.yml`](../examples/mod-repo-example/.github/workflows/validate.yml) for the pattern).

**Want to be listed in the official catalog instead of (or as well as) self-hosting your own?** Getting a repo or catalog surfaced under `gh:pagonia-land/Pagonia-Land/catalog/official.yaml` — so users who only have the default subscription see it — is a separate, PR-based process: see [`catalog/README.md`](../catalog/README.md) for the repo-entry-vs-federation decision and the submission checklist. Self-hosting (above) and getting listed in the official catalog are independent — you can do either or both.

For a working two-file fixture demonstrating federation, see [`examples/mod-catalog-example/`](../examples/mod-catalog-example/) — its top-level catalog references a nested sub-catalog via `file:./catalogs/sub-catalog.yaml` and shares one repo entry with it (to demonstrate the dedup + vouching trust signal).

## Related Documents

- [Mod Distribution Patterns](mod-distribution.md#distribution-channels) — the channel-status table for everything related (where catalogs sit in the bigger picture).
- [Mod Tags Vocabulary](mod-tags.md) — recommended `tags` values for the catalog-level + per-repo `tags` fields.
- [`examples/mod-catalog-example/`](../examples/mod-catalog-example/) — working reference catalog.
- [`schemas/mod-patches/catalog.schema.json`](../schemas/mod-patches/catalog.schema.json) — JSON Schema contract.
- [Pagonia Land Manager CLI](../tools/pagonia-manager/CLI.md#catalogs) — full verb surface + diagnostic-code registry.
