# `schemas/manager/` — Manager JSON Report Schemas

Canonical JSON Schemas for every JSON report `pagonia-manager` emits via `--json <out>`. Downstream tools (a future GUI, CI bots, integration scripts) consume these schemas to know the shape of every report without parsing free-form text.

Each schema is embedded into the AOT-published `pagonia-manager.exe` (see `tools/pagonia-manager/src/PagoniaLand.Manager.Core/PagoniaLand.Manager.Core.csproj`'s `<EmbeddedResource>` block) so `pagonia-manager schema-validate` runs without needing this folder on disk.

## Schemas

| File | Report `kind` | Produced by |
| --- | --- | --- |
| `install-report.schema.json` | `install` | `install --json <out>` |
| `uninstall-report.schema.json` | `uninstall` | `uninstall --json <out>` |
| `deploy-report.schema.json` | `deploy` | `deploy --json <out>` |
| `rollback-report.schema.json` | `rollback` | `rollback --json <out>` |
| `collection-install-report.schema.json` | `collectionInstall` | `collection install --json <out>` |
| `status-report.schema.json` | `status` | `status --json <out>` |
| `deploy-status-report.schema.json` | `deployStatus` | `deploy-status --json <out>` |
| `expansions-list-report.schema.json` | `expansionsList` | `expansions list --json <out>` |
| `expansions-set-report.schema.json` | `expansionsSet` | `expansions set --json <out>` |

The plan report (`plan --json <out>`) carries its own envelope shape (`{ manager, patcher }`) and round-trips through `ManagerPlanReporter` directly; it's not validated by the manager's schema-validate today.

## Stable Contract

- The `reportKind` field is the discriminator — match against it, not the file name.
- `schemaVersion: "0.1"` on every report; bumps when the shape breaks compatibility.
- Diagnostic codes are documented in [`tools/pagonia-manager/CLI.md`](../../tools/pagonia-manager/CLI.md#diagnostic-codes). Match against the `code` field.

## Validating Locally

```powershell
pagonia-manager schema-validate --kind install --report .\install.json
pagonia-manager schema-validate --kind deploy  --report .\deploy.json
```

`schema-validate` exits `Success` (0) on conformance, `Error` (1) on any schema violation.
