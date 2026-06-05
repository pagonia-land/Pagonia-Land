# Pagonia Land Schemas

Stable JSON Schemas that form the **public contract** between Pagonia Land's tools and anything that integrates against them — a future GUI, IDE plugins, web validators, CI bots, EE's own tooling, third-party mod managers.

If you're building something that reads or writes Pagonia Land data, this is the directory you read against. Every CLI command in the three tools either consumes one of these schemas (input) or emits a schema-validated report (output).

## Layout

| Subfolder | What it covers | Used by | Read this if you're… |
| --- | --- | --- | --- |
| [`mod-patches/`](mod-patches/) | The **patch DSL**: `mod.yaml`, patch files, collections, collection lockfiles | Patcher (input), Manager (passes through) | …writing a mod, building an editor, validating a mod archive before install |
| [`manager/`](manager/) | Every JSON report shape the **manager CLI** emits via `--json <out>` | Manager (output) | …building a GUI / CI bot that drives the manager and reads its reports |
| [`patcher/`](patcher/) | Every JSON report shape the **patcher CLI** emits via `--json <out>` | Patcher (output) | …building tooling that drives the patcher directly (rare; usually go via manager) |
| [`paker/`](paker/) | Every JSON report shape the **paker CLI** emits (list, unpack, pack, patch, classify, gd.bin info) | Paker (output) | …building tooling that drives the paker directly, or processing `pakinfo.json` |

Each subfolder has its own README with the per-schema table and the diagnostic-code conventions for that surface.

## Verification: `schema-validate`

Every tool ships a `schema-validate` subcommand that is the **reference implementation** of its own contract:

```powershell
pagonia-patcher schema-validate <mod.yaml | patch.yaml | collection.yaml | lockfile>
pagonia-manager schema-validate --kind <kind> --report <report.json>
pagonia-paker   schema-validate --kind <kind> --report <report.json>
```

If your integration produces or consumes a report that disagrees with `schema-validate`, the schema is the source of truth — please open an issue.

## Drift Protection

The schemas embedded in each tool's binary are the same files in this folder (via `<EmbeddedResource>`). Two safeguards keep schemas, C# models, and sandbox examples in sync:

- [`scripts/preflight.ps1`](../scripts/preflight.ps1) runs `schema-validate` against every sandbox example before any commit
- [`.github/workflows/tools.yml`](../.github/workflows/tools.yml) runs the same on every push and PR

See [CONTRIBUTING.md → When You Touch Tools Or Schemas](../CONTRIBUTING.md#when-you-touch-tools-or-schemas) for the maintenance rule.

## Versioning

Today: every release ships the same schemas in lockstep. Each report carries a `schemaVersion` field (currently `"0.1"`) that will bump on breaking shape changes.

Open question: whether to introduce `$id`-based URL versioning (e.g. `https://pagonia.land/schemas/manager/1/install-report.schema.json`) before there's anything depending on it externally. Feedback welcome via the [issue tracker](https://github.com/pagonia-land/Pagonia-Land/issues).

## Getting The Schemas

- **Inside the tools:** already embedded — `schema-validate` works without this folder on disk
- **As source of truth:** read directly from this folder
- **As a release artefact:** the `pagonia-schemas-<version>.zip` archive is published alongside every release (see [the latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest))
