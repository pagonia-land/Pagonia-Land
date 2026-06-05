## Summary

Describe what changed and why.

## Type Of Change

- [ ] Documentation
- [ ] Script/tooling
- [ ] Validation
- [ ] Generated catalog tooling
- [ ] Repository metadata
- [ ] Other:

## Checks

- [ ] I did not commit extracted game files under `game-gdb/`, `game-paks/`, or `game-maps/`
- [ ] I did not commit generated files under `generated/` or `snapshots/`

If your change touches code / schemas / sandbox examples:

- [ ] I ran `scripts\preflight.ps1` (builds + tests all three tools, runs `schema-validate` on every sandbox example, drives the manager walkthrough)

If your change touches `docs/setup.md` or any script the quickstart references:

- [ ] I ran `scripts\check-quickstart.ps1` (verifies linked paths + script existence)

If your PR adds your repo or catalog to the **official catalog** (`catalog/official.yaml`) — see [`catalog/README.md`](../catalog/README.md):

- [ ] I added **either** a `repos:` entry (a single mod-distribution repo I maintain: `owner`, `repo`, optional `indexPath`, `summary`, `tags`) **or** a federated `catalogs:` entry (`- source: gh:owner/repo[/path]` for a community list I curate) — not both for the same thing
- [ ] The repo / catalog I'm adding is public and reachable at the URL given
- [ ] I ran `pagonia-patcher schema-validate --catalog catalog/official.yaml` locally (CI re-runs this)
- [ ] One-line on what it contains + why it belongs in the official catalog (in the Summary above)

If your change touches database analysis or generated outputs:

- [ ] I ran `scripts\validate_database.ps1`
- [ ] I ran `scripts\analyze_database.ps1` if analysis output may be affected
- [ ] I ran `scripts\generate_catalog.ps1` if catalog output may be affected
- [ ] I updated `VALIDATION_BASELINE.md` if validation counts changed

Always:

- [ ] I updated relevant documentation links

## Validation Output

Paste relevant validation summary or explain why validation was not run.

## Notes

Do not attach extracted game XML files, `.pak` files, assets, or generated data dumps.
