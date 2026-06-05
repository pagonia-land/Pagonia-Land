# Examples

Runnable, schema-validated reference layouts for Pagonia Land modding. Each subdirectory is a working artifact — meant to be forked as a starting point and to live alongside the patcher so CI catches any drift between the canonical schemas and what authors are told to ship.

## Available Today

| Example | What It Shows |
|---|---|
| [`mod-repo-example/`](mod-repo-example/) | Multi-mod Git repository: two mods (one simple, one with an optional DLC patch set) + one collection bundling them, top-level `index.yaml` catalog, a [`.github/workflows/validate.yml`](mod-repo-example/.github/workflows/validate.yml) CI job that schema-validates every file on push. Fork this as the starting point for a personal modding portfolio. |
| [`mod-catalog-example/`](mod-catalog-example/) | Mod catalog (`catalog.yaml`): curated list of mod-distribution repos plus a nested sub-catalog to demo the federation/dedup/cycle-detection semantics. The manager subscribes to catalogs to discover repos across the community. |

## Planned

As remote-install support rolls out, additional reference layouts will land alongside this one so each viable distribution channel has a working example:

| Example | What It Will Show |
|---|---|
| `single-mod-repo/` | Minimal one-mod repository — no `index.yaml`, just `mod.yaml` + `patches/`. The `install --from gh:<owner>/<repo>` path also accepts this shape; useful for "I have one mod, not a whole portfolio" cases. |
| `cross-repo-curator/` | A collection in repo A that pulls mods from repos B and C via the `source:` field. Shows how a curator can ship a "best of" without forking other authors' work. |
| `mod-package-zip/` | The same `index.yaml` + `mods/` + `collections/` layout, but packaged as a single ZIP suitable for mod.io upload or any direct-download link. The format is transport-agnostic; only the fetch protocol differs. |

The distribution-channel overview for end users lives in [`docs/mod-distribution.md`](../docs/mod-distribution.md#distribution-channels).

## Why These Live In The Work Repo

Until the manager's remote-fetch flow ships, the examples need to exist somewhere that the patcher can validate on every CI run. Keeping them inside this repo also means the schema-validation tests (see `tools/pagonia-patcher/tests/`) exercise them directly — schema drift gets caught immediately. When the manager flow lands, the directories here get promoted to stand-alone `pagonia-land/<example-name>` repositories the manager can `install --from gh:...` against.
