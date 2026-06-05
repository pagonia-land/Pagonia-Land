# Local Snapshots

This folder is for local version-to-version comparison only. It is the local archive for frozen GameDatabase states.

Snapshot contents are ignored by Git because they can contain extracted proprietary game XML files and generated derived data.

Keep this README in Git so contributors know where local snapshots belong.

For the full modder-facing workflow, see [Local Snapshots](../docs/snapshots.md).

## Short Version

- `game-gdb/` is the active working copy.
- `snapshots/<version>/game-gdb/` is a frozen copy of one extracted version.
- `snapshots/<version>/generated/` contains the generated analysis for that frozen version.
- `snapshots/<version>/notes/` can contain local patch notes or extraction notes.
- Do not commit snapshot contents.
- Use `scripts/diff_versions.ps1` to compare two snapshots.

## Recommended Structure

Use one folder per extracted game database version:

```text
snapshots/
├── 1.3.1-11826+193733/
│   ├── game-gdb/
│   │   ├── core/
│   │   ├── decorations1/
│   │   ├── dlc1/
│   │   └── tools/
│   ├── generated/
│   │   ├── analysis-summary.json
│   │   ├── entities.json
│   │   ├── references.json
│   │   └── catalog/
│   └── notes/
│       └── source.txt
└── 1.2.2-11216+189567/        # an older version keeps fewer subfolders (e.g. pre-Meadowsong has no dlc1/)
    ├── game-gdb/
    ├── generated/
    └── notes/
        └── patch_notes.txt
```

`game-gdb/` at the repository root remains the active working copy.

`snapshots/<version>/game-gdb/` is the frozen copy used for comparison.

Use the exact program version as folder name whenever it is known:

```text
snapshots/1.2.2-11216+189567/
snapshots/1.3.1-11826+193733/
```

Put readable labels, extraction dates, and patch notes under `notes/`.

## Create A Snapshot

From the repository root, after running validation, analysis, and catalog generation:

```powershell
New-Item -ItemType Directory -Force -Path .\snapshots\1.3.1-11826+193733 | Out-Null
Copy-Item -Recurse -Force .\game-gdb .\snapshots\1.3.1-11826+193733\game-gdb
Copy-Item -Recurse -Force .\generated .\snapshots\1.3.1-11826+193733\generated
```

Optionally add local notes:

```powershell
New-Item -ItemType Directory -Force -Path .\snapshots\1.3.1-11826+193733\notes | Out-Null
```

## Compare Two Snapshots

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\diff_versions.ps1 `
  -Old .\snapshots\1.2.2-11216+189567 `
  -New .\snapshots\1.3.1-11826+193733
```

The diff output is written under:

```text
generated/diffs/
```

Those outputs are also ignored by Git because they are derived from local extracted game data.

Use the generated diff summaries to update public documentation such as:

- `CHANGELOG.md`
- `VALIDATION_BASELINE.md`
- `REFERENCE.md`
- system-specific docs when a change affects modding behavior

Do not treat generated diff output as the final conclusion. Use it to find changed areas, then inspect relevant XML and generated catalogs before documenting modding impact.
