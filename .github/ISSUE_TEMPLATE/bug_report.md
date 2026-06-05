---
name: Bug report
about: Report a tool, documentation, script, or compatibility problem
title: "[Bug]: "
labels: bug
assignees: ""
---

## Summary

Describe the problem clearly.

## Affected Area

- Tool (`pagonia-manager` / `pagonia-patcher` / `pagonia-paker`):
- Tool version (`<tool> --version`) + release tag or commit:
- Platform / RID (e.g. `win-x64`, `linux-x64`, `osx-arm64`):
- Documentation page:
- Script:
- Generated catalog:
- Game database package:

## Steps To Reproduce

1.
2.
3.

## Expected Result

What did you expect to happen?

## Actual Result

What happened instead?

## Validation Output

For a **tool** bug, paste the `<tool> --version` line and the relevant JSON report
(run the failing command with `--json <out>` and paste the report).

For a **database / docs / script** bug, paste relevant output from:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

## Notes

Do not attach extracted game XML files, `.pak` files, assets, or generated data dumps.
