# Paker Schemas

This folder documents machine-readable output written by the Pagonia Land Paker.

The schemas describe the CLI output contract for tools that do not link against `PagoniaLand.Paker.Core` directly — e.g. a packaging script, a website that reads pak reports, or a third-party mod manager not built in .NET. (`pagonia-manager` itself links the library directly via `PakBuilder` for Pattern B deploys, but its reports go through this same schema shape for `--json` output.)

## Files

- [pak-list-report.schema.json](pak-list-report.schema.json) — JSON report written by `list --json <path>`. Captures the archive's format version, every index entry, and the path of the `pakinfo.json` sidecar that was also written.
- [pak-unpack-report.schema.json](pak-unpack-report.schema.json) — JSON report written by `unpack --json <path>`. Captures the filter that was applied, per-entry outcomes (`extracted`, `skipped`, `failed`), and the on-disk output paths.
- [pak-pack-report.schema.json](pak-pack-report.schema.json) — JSON report written by `pack --json <path>`. Captures the pakinfo source, the output pak, the filter that was applied, and how many entries actually ended up in the output.
- [pak-patch-report.schema.json](pak-patch-report.schema.json) — JSON report written by `patch --json <path>`. Captures the input pak, the output pak, and the replacement files plus their target entry names.
- [pak-classify-report.schema.json](pak-classify-report.schema.json) — JSON report written by `classify --json <path>`. Captures what the pak contributes as independent signals — `GdbScopes` (`global` / `map-scoped`), `PopmapCount`, `OverridesAtRoot` — plus the module folder, manifest name, and declared dependencies.
- [gdbin-info-report.schema.json](gdbin-info-report.schema.json) — JSON report written by `gdbin info --json <path> <gdbin>`. Captures the seven-byte format header (as hex strings), the entry count, and the in-pak path list the index enumerates.

## Stability

These schemas track the implemented paker commands (`list`, `unpack`, `pack`, `patch`, `classify`, `gdbin info`). They should be treated as the public shape of the paker output. Add fields instead of renaming or removing them whenever possible.

Each schema is verified against the actual report shape by a roundtrip test in [`tools/pagonia-paker/tests/`](../../tools/pagonia-paker/tests/) — the test produces a real report through the reporter, validates the JSON against the schema, and fails the build on any drift. Run [`scripts/preflight.ps1`](../../scripts/preflight.ps1) before committing changes that touch a report shape or a schema.

Diagnostic codes embedded in the `Diagnostics` arrays come from `PagoniaLand.Paker.DiagnosticCodes`; the full enumeration is reproduced in [tools/pagonia-paker/CLI.md](../../tools/pagonia-paker/CLI.md). New codes will be added there before they appear in shipped reports.

CLI exit codes are documented in [tools/pagonia-paker/CLI.md](../../tools/pagonia-paker/CLI.md).
