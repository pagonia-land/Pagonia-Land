# Local Snapshots

Snapshots are local, frozen copies of extracted GameDatabase XMLs and generated analysis output.

They are useful when the game receives an update and you want to answer a simple but important question:

```text
What actually changed between version A and version B?
```

The repository does not publish extracted game files. Snapshot contents stay local and are ignored by Git.

## Why Snapshots Exist

Pioneers of Pagonia updates can change many XML files at once. Some changes are obvious from patch notes, but many modding-relevant changes are hidden in database details:

- new entities
- removed entities
- changed component sets
- changed production inputs or outputs
- changed objective requirements
- changed notification or narration references
- changed DLC additions or overrides
- changed map-specific data
- changed validation warnings

If you only overwrite `game-gdb/` with the newest XMLs, the old version is gone. You can still analyze the new state, but you cannot reliably compare it against the previous state.

Snapshots solve that. They preserve a version so it can be compared later.

## `game-gdb/` Versus `snapshots/`

Use the folders for different jobs:

| Folder | Purpose |
| --- | --- |
| `game-gdb/` | Active working copy. Put the newest extracted XMLs here and run validation/generation against it. |
| `snapshots/<version>/game-gdb/` | Frozen copy of a known version. Do not edit it after creating it. |
| `snapshots/<version>/generated/` | Frozen generated analysis for that version. Used by diff scripts. |
| `snapshots/<version>/notes/` | Optional local notes, patch notes, extraction notes, or test observations. |

Think of `game-gdb/` as the workbench and `snapshots/` as the archive.

## What Snapshots Can Be Used For

Snapshots are useful for modders, maintainers, and AI-assisted update passes.

Common uses:

- compare a new release with the previous public baseline
- check whether a mod broke because a referenced GUID changed or disappeared
- detect new resources, buildings, units, objectives, notifications, or recipes
- document patch-note claims with database evidence
- update generated counts in `REFERENCE.md`
- update `CHANGELOG.md`
- review DLC changes separately from core changes
- keep a local rollback reference while testing mods

They are also useful when a game update looks small. Small XML changes can still affect production chains, objective flow, or map events.

## Recommended Folder Names

Use the exact program version for snapshot folder names whenever it is known.

Current local examples:

```text
snapshots/1.2.2-11216+189567/
snapshots/1.3.1-11826+193733/
```

Avoid vague names:

```text
snapshots/new/
snapshots/old/
snapshots/test/
```

The exact program version is better than a descriptive label such as "Meadowsong release" because it is unique to the extracted GameDatabase files. Put human-readable labels, dates, patch notes, and extraction notes under `notes/`.

## Create A Snapshot

First update `game-gdb/`, then run the normal analysis workflow:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then copy the current active state into a snapshot:

```powershell
$snapshot = ".\snapshots\1.3.1-11826+193733"
New-Item -ItemType Directory -Force -Path $snapshot | Out-Null
Copy-Item -Recurse -Force .\game (Join-Path $snapshot "game-gdb")
Copy-Item -Recurse -Force .\generated (Join-Path $snapshot "generated")
New-Item -ItemType Directory -Force -Path (Join-Path $snapshot "notes") | Out-Null
```

If you have patch notes, extraction notes, or test notes, place them under:

```text
snapshots/<version>/notes/
```

## Compare Two Snapshots

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\diff_versions.ps1 `
  -Old ".\snapshots\1.2.2-11216+189567" `
  -New ".\snapshots\1.3.1-11826+193733"
```

The diff script writes reports to:

```text
generated/diffs/<old>_to_<new>/
```

Important files:

| File | Purpose |
| --- | --- |
| `README.md` | Human-readable diff summary |
| `summary.json` | Machine-readable diff summary |
| `entities-added.md/.csv` | New entity GUIDs |
| `entities-removed.md/.csv` | Removed entity GUIDs |
| `entities-changed.md/.csv` | Entity name/package/file/component metadata changes |
| `xml-files-changed.md/.csv` | Changed XML file hashes and sizes |
| `references-added-sample.md/.csv` | Sample of newly detected references |
| `references-removed-sample.md/.csv` | Sample of removed references |

The reference reports are samples, currently limited to the first 500 changed rows. They are meant for triage, not as the only source of truth.

## How To Read A Diff

Start with:

```text
generated/diffs/<old>_to_<new>/README.md
generated/diffs/<old>_to_<new>/summary.json
```

Then inspect in this order:

1. `xml-files-changed.md`
2. `entities-added.md`
3. `entities-removed.md`
4. `entities-changed.md`
5. `references-added-sample.csv`
6. `references-removed-sample.csv`

Useful questions:

- Did the number of XML files change?
- Did a new package file appear?
- Were any entities removed?
- Did a known entity change components?
- Did a recipe gain or lose resource references?
- Did an objective or notification gain new references?
- Do patch-note claims match database evidence?
- Did validation warnings increase?

For deeper review, open the changed XML files directly and compare the exact entities.

## What To Document After A Diff

Do not copy large XML content into public docs.

Instead, document derived observations:

- changed counts
- changed file names
- added/removed entity names and GUIDs
- changed component names
- high-level production, resource, objective, notification, or DLC behavior
- validation warning changes
- modding impact and risk

Good places to update:

```text
CHANGELOG.md
VALIDATION_BASELINE.md
REFERENCE.md
docs/catalog-coverage.md
docs/systems/*.md
```

## What Not To Commit

Do not commit:

- `snapshots/<version>/game-gdb/`
- `snapshots/<version>/generated/`
- `generated/diffs/`
- extracted XML files
- large copied XML snippets

These files are ignored because they are derived from locally extracted game data.

It is fine to commit:

- snapshot workflow documentation
- scripts
- hand-written observations
- derived counts
- small identifiers such as names, GUIDs, component names, and file names

## Common Mistakes

### Overwriting `game-gdb/` Before Taking A Snapshot

If the old `game-gdb/` folder is overwritten before a snapshot is created, the old version cannot be compared unless you extract it again.

Create a snapshot before replacing a known-good local version.

### Editing A Snapshot

Snapshots should be frozen. If you edit a snapshot, future diffs become less trustworthy.

If you need to test changes, edit `game-gdb/` or create a separate test snapshot with a clear name.

### Comparing Without Generated Indexes

`scripts/diff_versions.ps1` needs at least:

```text
snapshots/<version>/generated/entities.json
snapshots/<version>/generated/references.json
snapshots/<version>/generated/analysis-summary.json
```

Run `scripts\analyze_database.ps1` before creating the snapshot.

### Treating Diff Output As A Final Conclusion

Diff reports are a map, not the destination.

Use them to find changed areas quickly, then inspect relevant XML and generated catalogs before writing modding conclusions.

## Suggested Update Workflow

For a normal game update:

1. If the current `game-gdb/` state is important, snapshot it first.
2. Replace XMLs under `game-gdb/` with the newly extracted version.
3. Run validation, analysis, and catalog generation.
4. Create a snapshot for the new version.
5. Run `scripts\diff_versions.ps1` against the previous snapshot.
6. Review changed files, entities, references, and validation counts.
7. Update public docs with derived observations.
8. Commit only docs and scripts, not local snapshot contents.

This gives the repository a repeatable update rhythm without publishing extracted game files.
