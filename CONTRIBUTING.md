# Contributing

Thank you for helping improve this community database reference.

This repository is meant to help modders understand the Pioneers of Pagonia game database without publishing extracted proprietary game files.

> **Just evaluating the repo?** If you're here to review or get a feel for the project before committing anything, see [REVIEWING.md](REVIEWING.md) — it tells you which path to follow (try the tools, write a patch, read the docs, integrate against the schemas) and where outside input is most wanted.

## What You Can Contribute

Good contributions include:

- documentation improvements
- new worked examples
- script improvements
- validation checks
- generated-catalog tooling
- corrections to analysis notes
- update notes after a new game version
- issue reports with clear reproduction steps

## What Not To Commit

Do not commit:

- extracted `.pak` contents
- files under `game-gdb/core/gdb/`
- files under `game-gdb/decorations1/gdb/`
- files under `game-gdb/dlc1/gdb/`
- files under `game-gdb/tools/gdb/`
- generated JSON files under `generated/`
- generated catalog files under `generated/catalog/`
- game assets, textures, audio, binaries, or original extracted XML dumps

The public repository should contain documentation, scripts, and metadata only.

## Local Setup

Read [Local Setup](docs/setup.md) first — its **Quick Path** section gives the speedrun, and the rest of the page is the full reference.

Short version:

1. Extract the relevant `.pak` files locally.
2. Copy XML files into the matching package folder under `game-gdb/`.
3. Run validation.
4. Generate local indexes and catalogs.
5. Edit documentation or scripts.
6. Commit only public-safe files.

Useful commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## Pull Request Checklist

Before opening a pull request:

- **If you changed code, schemas, or sandbox examples:** run `scripts\preflight.ps1`. This is the one-stop check — it builds all three tool solutions (patcher + paker + manager), runs every test suite, runs `schema-validate` against every sandbox example, and drives the [manager walkthrough](sandbox/examples/manager-walkthrough/) end-to-end against a fixture game tree. The GitHub Actions workflow at [`.github/workflows/tools.yml`](.github/workflows/tools.yml) runs the same checks on every push and PR plus an AOT publish smoke that re-runs the walkthrough against the published binaries; a local preflight pass means CI will pass too.
- **If you changed a GitHub Actions workflow** (`.github/workflows/`): run `scripts\check-workflows.ps1` (also a stage in `preflight.ps1`). It lints every workflow with actionlint — retired runner labels, invalid `github.*` context access, expression / YAML errors, deprecated actions — the class of bug that otherwise only surfaces when the workflow runs live.
- **If you changed [`docs/setup.md`](docs/setup.md) or any script the quickstart references:** run `scripts\check-quickstart.ps1`. This verifies every link in the setup page resolves, every script it tells the user to run exists and parses, and (optionally, if you have `.pak` files in `game-paks/`) actually runs the extract → validate → analyze → generate-catalog chain end-to-end.
- **If you changed database analysis:** run `scripts\validate_database.ps1`, `scripts\analyze_database.ps1`, and `scripts\generate_catalog.ps1` as needed.
- **If validation warning counts changed:** update [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md).
- Make sure no extracted game data or generated files are staged (see [What Not To Commit](#what-not-to-commit)) — [`.github/workflows/repo-checks.yml`](.github/workflows/repo-checks.yml) hard-rejects PRs that touch off-limits paths.
- Keep documentation in English.
- Explain what changed and why.

### Automated PR checks

These GitHub Actions workflows gate PRs — a pass on each that runs is required to merge:

| Workflow | What it catches |
| --- | --- |
| [`tools.yml`](.github/workflows/tools.yml) | Build + test breakage in the patcher / paker / manager, schema drift, AOT-publish regressions across the published binaries (including a full re-run of the [manager walkthrough](sandbox/examples/manager-walkthrough/) against the AOT exe). Mirrors `scripts/preflight.ps1`. |
| [`lint-workflows.yml`](.github/workflows/lint-workflows.yml) | Invalid GitHub Actions workflow definitions — retired runner labels, invalid `github.*` context access, expression / YAML errors, deprecated action versions, and (via the actionlint image's bundled shellcheck) bugs in `run:` blocks. Runs only when `.github/workflows/**` changes. Mirrors `scripts/check-workflows.ps1`. |
| [`repo-checks.yml`](.github/workflows/repo-checks.yml) — *forbidden-paths* | Commits that touch extracted game data (`game-gdb/*/gdb/`, `game-paks/*.pak`, `game-maps/*.pak`), generated artifacts (`generated/entities.json`, `generated/catalog/`, `generated/diffs/`), or per-version snapshots (`snapshots/<version>/`). The rules are also in [`.gitignore`](.gitignore) and CONTRIBUTING, but this workflow is the gate that cannot be bypassed by `git add -f` or a missing ignore rule. |
| [`repo-checks.yml`](.github/workflows/repo-checks.yml) — *markdown-links* | Broken relative-path and anchor links between Markdown files. Runs `lychee --offline` against every `.md` outside `snapshots/` and `generated/`. Would have caught the kind of regression the `dlc-overlays.md → package-layering.md` rename forced a manual grep sweep over. |

### When You Touch Tools Or Schemas

The C# tools (`pagonia-patcher`, `pagonia-paker`, `pagonia-manager`) and the JSON Schemas under [`schemas/mod-patches/`](schemas/mod-patches/) + [`schemas/manager/`](schemas/manager/) form a tightly coupled contract:

- **The schemas are the public contract** that third-party tools (mod-manager GUIs, IDE plugins, web validators, EE's own tooling) read.
- **Each tool's `schema-validate` subcommand is the reference implementation for its own contract** — `pagonia-patcher schema-validate` validates `mod.yaml` + patch files + collections + lockfiles against [`schemas/mod-patches/`](schemas/mod-patches/); `pagonia-manager schema-validate` validates every `--json <out>` report (install / deploy / rollback / status / collection install / deploy-status) against [`schemas/manager/`](schemas/manager/). Every sandbox example must pass the appropriate `schema-validate`, so if you weaken or change a schema, the failing example tells you exactly what broke.
- **When you add a new field to a C# model**, add the matching property to the schema in the same PR. The CI's `schema-validate` step will fail on a sandbox example that uses the new field if the schema doesn't know it yet — that's how we caught the missing `pak:` block when the schema-validate tool first ran.

This is the rule that prevents the kind of silent drift between code and contract we've already seen and fixed once. Run `scripts\preflight.ps1` before pushing; the workflow on GitHub is your safety net if you forget.

## Documentation Style

Write for curious modders who may not know the database yet.

Prefer:

- concrete examples
- exact file paths
- clear risk notes
- small safe workflows
- commands that can be copied
- explanations of why a field matters

Avoid:

- claiming untested behavior as fact
- publishing large extracted data dumps
- changing unrelated docs in the same PR
- editing generated files manually

## Reporting Issues

When reporting a bug or compatibility issue, include:

- game version, if known
- package folders involved
- command output from validation, if relevant
- what you expected
- what happened
- which documentation page or script is affected

If the issue depends on local extracted game data, describe the relevant entity names and GUIDs only as much as needed.
