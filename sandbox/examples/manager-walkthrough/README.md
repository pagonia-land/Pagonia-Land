# Manager End-to-End Walkthrough

Self-contained sandbox example that drives every `pagonia-manager` command in sequence against a fixture game tree. Doubles as the manager's preflight harness: `scripts/preflight.ps1` runs this walkthrough on every contributor check and on every CI run.

> **No game install needed — it runs against a tiny bundled fixture.** The walkthrough ships two fixture mods (`cheaper-sawmill` + `cheaper-quarry`, touching different entities so they don't conflict) and a minimal `game-gdb/core/gdb/buildings.gd.xml` holding just a Sawmill + Quarry entity. The file structure and cost amounts are fixture-only, but the Sawmill entity and the Softwood Trunk resource reuse their real shipped GUIDs (`c732cb26-…`, `c22b4997-…`) so the example resolves the same way a real patch would; the Quarry GUID (`ab999999-…`) is synthetic. Everything runs in a throwaway `out/` directory next to this README.

## What The Walkthrough Covers

| Stage | Commands |
|---|---|
| Store setup | `store init` |
| Install lifecycle | `install` (×2), `list` |
| Profile lifecycle | `profile create`, `profile use`, `profile list` |
| Active-profile mod selection | `enable` (×2), `move --before`, `status` |
| Planning | `plan --json` + `schema-validate --kind plan` (via patcher schema) |
| Deploy + rollback | `deploy --dry-run`, `deploy`, `schema-validate --kind deploy`, `rollback` |
| SHA-256 round trip | `game-gdb/` hash before deploy === hash after rollback |
| Cleanup | switch back to default, `profile delete`, `uninstall` |

Every state-changing command emits a JSON report; the script validates each through `schema-validate` so the schemas under `schemas/manager/` stay in sync with the runtime.

## Running The Walkthrough

```powershell
.\sandbox\examples\manager-walkthrough\run.ps1
```

Default mode runs against the framework build (`dotnet run --project tools\pagonia-manager\src\PagoniaLand.Manager.Cli`). Pass `-ManagerExe <path>` to point at a published `pagonia-manager.exe` instead — CI uses this to smoke the AOT binary.

`-Keep` leaves the `out/` directory in place after the run for inspection. Without it, the script cleans up.

## Files

```text
manager-walkthrough/
  README.md              # this file
  run.ps1                # full lifecycle driver
  mods/
    cheaper-sawmill/     # Sawmill softwood cost 4 -> 3
      mod.yaml
      patches/sawmill.yaml
    cheaper-quarry/      # Quarry softwood cost 6 -> 5
      mod.yaml
      patches/quarry.yaml
  game-gdb/
    core/gdb/
      buildings.gd.xml   # fixture game tree (Sawmill + Quarry)
  out/                   # created at run time; deleted at end (unless -Keep)
    store/               # the manager's store root for this run
    game-gdb/            # working copy of the fixture; deploy writes here
    reports/             # JSON reports per command
```

## What Success Looks Like

```
==> store init
==> install cheaper-sawmill + schema-validate
==> install cheaper-quarry + schema-validate
==> enable both + move quarry before sawmill
==> status
  1. pagonia-land.walkthrough.cheaper-quarry@0.1.0
  2. pagonia-land.walkthrough.cheaper-sawmill@0.1.0
==> plan --json + schema-validate
==> deploy --dry-run --json + schema-validate
==> deploy --json + schema-validate
==> verify deployed buildings.gd.xml (Sawmill 4->3, Quarry 6->5)
==> rollback + schema-validate
==> verify SHA-256(game-gdb) matches pre-deploy
walkthrough: all 14 stages passed.
```
