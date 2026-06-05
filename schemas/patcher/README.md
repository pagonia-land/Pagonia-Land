# Patcher Schemas

This folder documents machine-readable output written by the Pagonia Land Patcher.

The schemas describe the CLI output contract for tools that do not link against `PagoniaLand.Patcher.Core` directly — e.g. a compatibility bot, a website that reads patch reports, or a third-party mod manager not built in .NET. (`pagonia-manager` itself links the library directly and consumes patcher diagnostics inline, but its reports go through this same schema shape for `--json` output.)

## Files

- [patch-plan-report.schema.json](patch-plan-report.schema.json) describes the JSON report written by `plan --json <path>`, including whether the plan source was direct mods, one collection, multiple collections, or a future lockfile.
- [patch-apply-report.schema.json](patch-apply-report.schema.json) describes the JSON report written by `apply --json <path>`. It captures the output game folder, planned and applied write counts, and the apply diagnostics that explain each outcome.

## Stability

These schemas track the implemented patcher commands (`plan` and `apply` for direct mods, single collections, and multi-collection input). They should be treated as the public shape of the patcher output. Add fields instead of renaming or removing them whenever possible. The `lockfile` `PlanSource` value is reserved for the planned lockfile-driven planning step and is allowed in the schemas even though no command emits it yet.

Each schema is verified against the actual report shape by a roundtrip test in [`tools/pagonia-patcher/tests/`](../../tools/pagonia-patcher/tests/) — the test produces a real report through the reporter, validates the JSON against the schema, and fails the build on any drift. Run [`scripts/preflight.ps1`](../../scripts/preflight.ps1) before committing changes that touch a report shape or a schema.

CLI exit codes are documented in [tools/pagonia-patcher/CLI.md](../../tools/pagonia-patcher/CLI.md).
