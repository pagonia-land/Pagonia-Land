# Database Update Playbook

This playbook describes how to refresh the repository when new `*.gd.xml` files are extracted from a newer Pioneers of Pagonia game version.

It is written for both human maintainers and AI-assisted update runs.

## Purpose

Use this document when:

- the game was updated
- fresh XML files were extracted from the `.pak` files
- the `core`, `decorations1`, `dlc1`, `tools`, or future package folders were replaced or changed
- the documentation and generated analysis need to be refreshed

## Expected Input Layout

The repository root should contain package folders like:

```text
.
├── game-gdb/
│   ├── core/
│   ├── decorations1/
│   ├── dlc1/
│   └── tools/
├── README.md
├── DATABASE_OVERVIEW.md
└── UPDATE_PLAYBOOK.md
```

Additional future package folders inside `game-gdb/` are fine. The analysis should discover all `*.xml` files recursively below `game-gdb/`, but should treat Markdown files and generated outputs as documentation, not source game data.

The `game-gdb/` directory is ignored by Git. Keep it local unless you have permission to publish the extracted game data.

See [game-gdb/README.md](game-gdb/README.md) for the expected local extraction layout. In short: every `*.gd.xml` lives under `game-gdb/<pak>/gdb/<file>.gd.xml`, matching the canonical layout inside each `.pak` (`<pak>/gdb/<file>.gd.xml`). The known additional case is map-specific gdb files like `<pak>/maps/<...>.gd.xml`.

[`scripts/extract-xmls-from-paks.ps1`](scripts/extract-xmls-from-paks.ps1) does this extraction automatically — see the [One-Command Refresh](#one-command-refresh) section below. The extraction always wipes `game-gdb/` first (preserving the README), so per-pak subfolders for paks that no longer ship in the new version don't linger across the refresh.

## Local Snapshot Layout

For version diffs, keep old and new extracted data side by side under `snapshots/`.

Snapshot contents are ignored by Git. They may contain extracted game XMLs and generated derived data.

For the modder-facing explanation of why snapshots matter and how to read diff output, see [Local Snapshots](docs/snapshots.md).

Recommended local structure:

```text
snapshots/
├── 1.2.2-11216+189567/
│   ├── game-gdb/
│   ├── generated/
│   └── notes/
└── 1.3.1-11826+193733/
    ├── game-gdb/
    ├── generated/
    └── notes/
```

The repository root `game-gdb/` folder is the active working copy. Snapshot folders are frozen comparison inputs.

Create snapshots before overwriting a known-good `game-gdb/` state. Once a snapshot exists, treat it as read-only. Prefer the exact program version, such as `1.3.1-11826+193733`, as the snapshot folder name.

## One-Command Refresh

When a new game version drops, the typical flow is:

1. Stage the new install's `*.pak` files (and the exe) into [`game-paks/`](game-paks/README.md). Either copy them by hand, or — since the install folder is known — let the staging script do it:

   ```powershell
   pwsh ./scripts/prepare-update.ps1                     # default Steam install
   pwsh ./scripts/prepare-update.ps1 -GameRoot '<path>'  # install lives elsewhere
   ```

   `prepare-update.ps1` reads the version from the exe's `ProductVersion` first, stops early if you already have a snapshot for that version, cleans stale paks out of `game-paks/` (keeping `README.md` and a hand-edited `patch_notes.txt`), then copies the paks + exe in. It only ever **reads** from the game install. Pass `-DryRun` to preview, `-Force` to re-copy a version you already captured.
2. Drop the official patch notes next to them as `game-paks/patch_notes.txt` (or update the one prepare kept). The orchestrator copies that file into the snapshot at the end so the changelog writer can cross-reference it later.
3. Run the orchestrator with the version you just installed as the snapshot name:

   ```powershell
   pwsh ./scripts/update-from-paks.ps1 -NewVersion 1.3.1-11826+193733
   ```

   `prepare-update.ps1` prints that exact version string (read from the exe); you can also read it from the official patch notes or the in-game version display.

The orchestrator runs five steps in order, stopping on the first error:

| Step | Script | What it produces |
| --- | --- | --- |
| 1 | [`extract-xmls-from-paks.ps1`](scripts/extract-xmls-from-paks.ps1) `-Clean` | Wiped + refreshed `game-gdb/<pak>/{gdb,maps}/*.gd.xml`. The orchestrator always passes `-Clean` so stale per-pak subfolders from a previous version (e.g. a previously-installed DLC that the new version doesn't ship) are removed first. |
| 2 | [`validate_database.ps1`](scripts/validate_database.ps1) | XML well-formedness + cross-reference checks |
| 3 | [`analyze_database.ps1`](scripts/analyze_database.ps1) | `generated/entities.json`, `generated/references.json`, `generated/analysis-summary.json` |
| 4 | [`generate_catalog.ps1`](scripts/generate_catalog.ps1) | `generated/catalog/` — Markdown, CSV, Mermaid catalogs and filters |
| 5 | (built into the orchestrator) | `snapshots/<NewVersion>/{game-gdb,generated,patch_notes.txt}` — frozen record of what was just installed |

The snapshot happens **after** extract+validate succeed, so a broken extraction never becomes a sealed snapshot. The previous version's snapshot — written the same way at the previous update — stays untouched and is your safety net plus the natural left-hand input for the next diff.

If you skip `-NewVersion`, the orchestrator reads the version from the game exe staged in `game-paks/` (its `ProductVersion`) and snapshots under that. Only when no `-NewVersion` is given **and** no exe is staged does it run without writing a snapshot — useful when iterating on the analysis scripts, but not what you want for a real version bump. Pass `-NewVersion` to override a staged exe.

After the orchestrator finishes, **the following are still manual**:

- `pwsh ./scripts/diff_versions.ps1 -Old snapshots/<old> -New snapshots/<NewVersion>` — produces `generated/diffs/<old>_to_<new>/` with the structural diff (entities added/removed/changed, reference deltas).
- Write a [CHANGELOG.md](CHANGELOG.md) entry summarising the changes, cross-referencing `game-paks/patch_notes.txt` if present.
- Update [REFERENCE.md](REFERENCE.md) and [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md) counts as needed.
- Commit. Only Markdown changes and any small text updates are tracked; `game-gdb/`, `generated/`, and `snapshots/` stay local.

If you want to run the steps one at a time (the historical workflow), the section [Update Workflow](#update-workflow) below walks each one.

> **See also — end-user side of an update.** This playbook is about refreshing the *repo's* GameDatabase analysis. Separately, the **manager** ([`tools/pagonia-manager/CLI.md`](tools/pagonia-manager/CLI.md)) is update-aware on the *deploy* side: after a real game update it surfaces the new version in `deploy-status`, warns `manager.canonicalPakChangedExternally` if a canonical pak changed at cache time, and warns `manager.modGameVersionMismatch` when a previously-deployed mod was built for the old `gameDatabaseVersion`. That's the right place to point users who deploy mods onto a freshly-updated install.

## Version Currency

The repo is a reference modders rely on, so it must always present **one** current game version and **one** set of headline numbers — stale data in any corner is a real failure. The version and its counts are restated in many docs, so the refresh has to move all of them together. Two things keep that reliable: a small set of canonical sources, and an automated check that fails the build if any managed doc drifts.

### Canonical sources (update these first)

| Source | Owns |
| --- | --- |
| [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md) | the **current version string** + the **validation headline counts** (entities, unique GUIDs, GUID-like references, resolved, null, other-unresolved, ref/recipe counts) |
| [REFERENCE.md](REFERENCE.md) | the **generated-catalog counts** (every catalog row total — resources, localization, assets/icons/meshes, search index, …) |

Refresh these two from the regenerated `generated/analysis-summary.json` + `generated/catalog/` first. Everything else is brought into line with them.

### The automated guard

```powershell
pwsh ./scripts/check-doc-currency.ps1
```

[`scripts/check-doc-currency.ps1`](scripts/check-doc-currency.ps1) reads the canonical version + counts from the two files above, then verifies every managed doc agrees — same current version, same distinctive numbers. It runs on committed Markdown only (no `game-gdb/`/`generated/` needed), so it works in CI. It is wired into [`scripts/preflight.ps1`](scripts/preflight.ps1) (stage 0) and into a dedicated [`docs-currency`](.github/workflows/docs-currency.yml) workflow that gates documentation-only PRs. **Bump the canonical pair, run the check, fix every file it names, repeat until green.** The managed-file lists inside the script are the machine-readable copy of the checklist below — add a doc to both when it starts citing current-version data.

### The full checklist (every place current-version data lives)

Auto-checked by `check-doc-currency.ps1`:

- **Version string** — [README.md](README.md), [DATABASE_OVERVIEW.md](DATABASE_OVERVIEW.md), [REFERENCE.md](REFERENCE.md), [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md), [docs/catalog-coverage.md](docs/catalog-coverage.md), [docs/dlc-patch-override-model.md](docs/dlc-patch-override-model.md), [docs/package-layering.md](docs/package-layering.md).
- **Headline counts** — README, DATABASE_OVERVIEW, catalog-coverage, dlc-patch-override-model, package-layering, [docs/repository-layout.md](docs/repository-layout.md).
- **Catalog counts** — [docs/localization.md](docs/localization.md), [docs/systems/visual-audio-assets.md](docs/systems/visual-audio-assets.md) (against REFERENCE.md).

Manual (not auto-checked — verify by hand against the new diff/catalog):

- **DATABASE_OVERVIEW.md** — per-file entity counts (e.g. `resources.gd.xml`), the most-common-reference-field table (e.g. the `Unit` row), and the cross-package reference matrix (e.g. `dlc1`→`dlc1`). The `InheritanceMode` "Uses in <version>" table is part of the cross-doc item below.
- **InheritanceMode + `<InheritedIndex>` counts (cross-doc — the easy-to-miss one).** Any update that adds or removes a `Template` / `Replace` / `Incremental` entity shifts these, and they are restated across **six** docs. They are *not* version-tagged headline numbers, so `check-doc-currency.ps1` does **not** catch them — regenerate from the new `game-gdb/` and fix every occurrence by hand:

  ```powershell
  $xml = Get-ChildItem game-gdb -Recurse -File -Filter *.xml
  # per-mode counts (Template / Replace / Incremental)
  $xml | Select-String -Pattern 'InheritanceMode="(\w+)"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
    Group-Object | Select-Object Name, Count
  # total uses + list-merge markers
  ($xml | Select-String 'InheritanceMode="' -AllMatches | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum
  ($xml | Select-String '<InheritedIndex>'   -AllMatches | ForEach-Object { $_.Matches.Count } | Measure-Object -Sum).Sum
  # distinct InheritedGuid targets (backs the "each use targets a distinct InheritedGuid" claim)
  ($xml | Select-String 'InheritanceMode="\w+"[^>]*InheritedGuid="([0-9a-f-]+)"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique).Count
  ```

  Update the *current* count in each of these — but **keep** the `1.2.2` / `1.3.0` historical figures in mod-distribution's "1.2.2 → 1.3.0 diff" section as-is:
  - [DATABASE_OVERVIEW.md](DATABASE_OVERVIEW.md) — the "Uses in `<version>`" table
  - [docs/dlc-patch-override-model.md](docs/dlc-patch-override-model.md) — the Current-Evidence table, the "Meadowsong (N uses)" prose cells, **and** the closing "full picture as of `<version>`" paragraph
  - [docs/package-layering.md](docs/package-layering.md) — the `<Entity InheritanceMode="…">` total and the `<InheritedIndex>` marker count
  - [docs/mod-distribution.md](docs/mod-distribution.md) — the "Hits in 1.3.x" column of the Confirmed-primitives table
  - [docs/engine-claims.md](docs/engine-claims.md) — the "N Template / N Replace / N Incremental" line and the "each of the N uses targets a distinct `InheritedGuid`" claim
  - [docs/quirks-and-anomalies.md](docs/quirks-and-anomalies.md) — the "three flavours — `Template` (N), `Replace` (N), `Incremental` (N)" sentence
- **Patch-notes review — read *both* sections.** `game-paks/patch_notes.txt` usually splits into a **DLC** block and a **Core Game** block. Skim *both* for changes that affect modders even when they don't move the database counts — pak / package naming and casing, mod-map or mod loading, load order, `.gd.bin` / pak format, file-name rules. (1.3.2's core-game "Fixed loading of mod maps when the package name is not written entirely in lower case" is the worked example — captured in [docs/mod-distribution.md](docs/mod-distribution.md) and the CHANGELOG entry.) Record anything actionable in the relevant modding doc, not only the changelog.
- **docs/quirks-and-anomalies.md** — **add** a new row to the reference-resolution-rate history table (don't overwrite the old version's row).
- **CHANGELOG.md** — add a new per-version entry with the validation delta (old→new). The previous entry stays.
- **Example manifests + illustrative versions** — bump `gameDatabaseVersion` in example mods/collections and "e.g. `<version>`" strings to the new version. Policy: update example and illustrative versions too, but **keep** genuine historical statements (e.g. "Meadowsong shipped as 1.3.0", version diffs), version-drift teaching examples that need two versions, and test fixtures / test code / C# version constants (tests must not be pinned to a specific version unless the test is specifically about versioning).

### Annotation convention

Tag every "current state" block with the version it describes — a line like `Current snapshot: \`<version>\``, `Current scan (\`<version>\`, regenerated <date>)`, or `as of \`<version>\``. This makes the blocks greppable and lets the checker tell a current claim apart from a historical mention. When you state a count for the current version, put it under such a tagged block, never as a bare number with no version nearby.

## Update Workflow

### 1. Inspect the Repository

Check which package folders and XML files exist:

```powershell
Get-ChildItem -Force
Get-ChildItem -Force -LiteralPath .\game-gdb
rg --no-ignore --files .\game-gdb -g "*.xml"
Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | Group-Object Extension
```

Record the number of XML files.

### 2. Validate XML Parsing

Every XML file should parse as XML and should normally use `EntityGroup` as the root element:

```powershell
$rootPath = (Get-Location).Path

Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | ForEach-Object {
  try {
    [xml]$x = Get-Content -LiteralPath $_.FullName -Raw
    [pscustomobject]@{
      Path = $_.FullName.Substring($rootPath.Length + 1)
      Root = $x.DocumentElement.Name
      Children = $x.DocumentElement.ChildNodes.Count
    }
  } catch {
    [pscustomobject]@{
      Path = $_.FullName.Substring($rootPath.Length + 1)
      Root = "PARSE_ERROR"
      Children = 0
    }
  }
} | Sort-Object Path | Format-Table -AutoSize
```

If any file has `PARSE_ERROR`, stop and inspect it before updating the docs.

You can also run the repository validation script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Use stricter handling for unresolved non-null GUID references:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1 -TreatUnresolvedAsError
```

### 3. Regenerate Analysis Indexes And Catalogs

Generate the machine-readable entity/reference indexes:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Generate the local human-readable building, production, recipe, unit, and graph catalogs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

The catalog is written to `generated/catalog/` and is ignored by Git. It is meant to answer current-database questions such as building dependency matrices, building costs, production links, recipe inputs/outputs, worker assignments, timing hints, production chains, unit equipment, unit recruitment costs, localization/text key usage, and objective/unlock flow.

After validation, review the additional modding-sensitive counts in the validation summary:

- building recipe links
- recipe resource references
- building cost resource references
- employment unit references
- recruitment resource references

New errors in these checks usually indicate a broken modding reference or a database structure change that needs documentation.

Compare the result with [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md). If warning counts or samples change, update the baseline or investigate the change before committing documentation updates.

### 3a. Create A Local Snapshot

After validation, analysis, and catalog generation, copy the active local data into a snapshot folder:

```powershell
$snapshot = ".\snapshots\1.3.1-11826+193733"
New-Item -ItemType Directory -Force -Path $snapshot | Out-Null
Copy-Item -Recurse -Force .\game-gdb (Join-Path $snapshot "game-gdb")
Copy-Item -Recurse -Force .\generated (Join-Path $snapshot "generated")
```

If patch notes or extraction notes exist, keep them in `notes/`:

```powershell
New-Item -ItemType Directory -Force -Path (Join-Path $snapshot "notes") | Out-Null
```

### 3b. Compare Two Local Snapshots

When both old and new snapshots exist, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\diff_versions.ps1 `
  -Old .\snapshots\1.2.2-11216+189567 `
  -New .\snapshots\1.3.1-11826+193733
```

The script writes local diff reports under `generated/diffs/`.

Use those reports to update:

- [CHANGELOG.md](CHANGELOG.md)
- [VALIDATION_BASELINE.md](VALIDATION_BASELINE.md)
- [REFERENCE.md](REFERENCE.md)
- affected system docs

Do not commit snapshot contents or generated diff outputs unless explicit permission exists to publish derived data.

### 4. Count Entities Per Package and File

Use this to count all `Entity` nodes with GUIDs:

```powershell
$rootPath = (Get-Location).Path
$gamePath = (Resolve-Path -LiteralPath (Join-Path $rootPath "game-gdb")).Path
$entities = @()

Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | ForEach-Object {
  [xml]$x = Get-Content -LiteralPath $_.FullName -Raw
  $rel = $_.FullName.Substring($rootPath.Length + 1)
  $gameRel = $_.FullName.Substring($gamePath.Length + 1)
  $pkg = $gameRel.Split('\')[0]

  foreach ($e in $x.SelectNodes('//Entity[@Guid]')) {
    $entities += [pscustomobject]@{
      Guid = [string]$e.Guid
      Name = [string]$e.Name
      File = $rel
      Package = $pkg
      Abstract = [string]$e.IsAbstract
      ValueTypes = (($e.Values.ChildNodes | ForEach-Object Name | Where-Object { $_ }) -join ',')
    }
  }
}

"PACKAGES"
$entities | Group-Object Package | Select-Object Name,Count | Format-Table -AutoSize

"FILES"
$entities | Group-Object File | Sort-Object Name | Select-Object Name,Count | Format-Table -AutoSize

"TOTALS"
[pscustomobject]@{
  Entities = $entities.Count
  UniqueGuids = ($entities.Guid | Sort-Object -Unique).Count
} | Format-Table -AutoSize
```

Important checks:

- `Entities` should equal `UniqueGuids`.
- If not, investigate duplicate GUIDs before updating documentation.

### 5. Count Component/Value Types

This shows which components exist and how common they are:

```powershell
$rootPath = (Get-Location).Path

Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | ForEach-Object {
  [xml]$x = Get-Content -LiteralPath $_.FullName -Raw
  foreach ($e in $x.SelectNodes('//Entity')) {
    foreach ($v in $e.Values.ChildNodes) {
      if ($v.Name) {
        [pscustomobject]@{
          Type = $v.Name
          File = $_.FullName.Substring($rootPath.Length + 1)
        }
      }
    }
  }
} | Group-Object Type | Sort-Object Count -Descending | Select-Object -First 100 Name,Count | Format-Table -AutoSize
```

Use this to detect new systems, renamed components, or major changes after a game update.

### 6. Resolve GUID References

This scan collects GUID definitions, GUID-like references, resolved references, null GUIDs, and unresolved non-null references:

```powershell
$rootPath = (Get-Location).Path
$gamePath = (Resolve-Path -LiteralPath (Join-Path $rootPath "game-gdb")).Path
$defs = @{}
$entities = @()

Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | ForEach-Object {
  [xml]$x = Get-Content -LiteralPath $_.FullName -Raw
  $rel = $_.FullName.Substring($rootPath.Length + 1)
  $gameRel = $_.FullName.Substring($gamePath.Length + 1)
  $pkg = $gameRel.Split('\')[0]

  foreach ($e in $x.SelectNodes('//Entity[@Guid]')) {
    $obj = [pscustomobject]@{
      Guid = [string]$e.Guid
      Name = [string]$e.Name
      File = $rel
      Package = $pkg
      Abstract = [string]$e.IsAbstract
    }

    $entities += $obj
    if (-not $defs.ContainsKey($obj.Guid)) {
      $defs[$obj.Guid] = $obj
    }
  }
}

$guidRegex = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
$refs = @()

Get-ChildItem -LiteralPath .\game-gdb -Recurse -File -Filter *.xml | ForEach-Object {
  [xml]$x = Get-Content -LiteralPath $_.FullName -Raw
  $rel = $_.FullName.Substring($rootPath.Length + 1)
  $gameRel = $_.FullName.Substring($gamePath.Length + 1)
  $srcPkg = $gameRel.Split('\')[0]

  foreach ($n in $x.SelectNodes('//*[not(self::Entity)]')) {
    $txt = ($n.InnerText).Trim()
    if ($txt -match $guidRegex) {
      $target = $defs[$txt]
      $refs += [pscustomobject]@{
        SourceFile = $rel
        SourcePackage = $srcPkg
        RefElement = $n.Name
        Guid = $txt
        TargetName = if ($target) { $target.Name } else { '' }
        TargetFile = if ($target) { $target.File } else { '' }
        TargetPackage = if ($target) { $target.Package } else { '' }
        Resolved = [bool]$target
        Null = ($txt -eq '00000000-0000-0000-0000-000000000000')
      }
    }
  }
}

"REFERENCE ELEMENTS TOP"
$refs | Group-Object RefElement | Sort-Object Count -Descending | Select-Object -First 50 Name,Count | Format-Table -AutoSize

"REFERENCE PACKAGES"
$refs | Group-Object SourcePackage,TargetPackage | Sort-Object Count -Descending | Select-Object Name,Count | Format-Table -AutoSize

"TOTALS"
[pscustomobject]@{
  AllRefs = $refs.Count
  Resolved = ($refs | Where-Object Resolved).Count
  NullRefs = ($refs | Where-Object Null).Count
  OtherUnresolved = ($refs | Where-Object { -not $_.Resolved -and -not $_.Null }).Count
} | Format-Table -AutoSize

"OTHER UNRESOLVED BY ELEMENT"
$refs | Where-Object { -not $_.Resolved -and -not $_.Null } |
  Group-Object RefElement |
  Sort-Object Count -Descending |
  Select-Object -First 30 Name,Count |
  Format-Table -AutoSize
```

Update the `Key Analysis Numbers` section in [DATABASE_OVERVIEW.md](DATABASE_OVERVIEW.md) and the `What's Inside` snapshot table in [README.md](README.md) from this output — then run `scripts/check-doc-currency.ps1` to confirm every other doc matches (see [Version Currency](#version-currency)).

### 7. Sanity-Check The Extracted Files

The structural diff from **step 3b (Compare Two Local Snapshots)** — written under
`generated/diffs/<old>_to_<new>/` — is the authoritative record of *what changed*:
entities added / removed / changed and reference deltas. That is where new
components and changed reference patterns actually surface, so it drives the review
checklist below.

This step is just a quick smell test that extraction itself produced sane files.
Peek at the head of a few representative ones and confirm each looks like well-formed
GameDatabase XML — expected root element, readable entities, no obvious truncation.
(`-TotalCount` only reads the first N lines; the exact number is arbitrary — you just
want the header plus the first entities, not a full review.)

```powershell
Get-Content -LiteralPath .\game-gdb\core\gdb\resources.gd.xml -TotalCount 50
Get-Content -LiteralPath .\game-gdb\core\gdb\buildings.gd.xml -TotalCount 50
Get-Content -LiteralPath .\game-gdb\core\gdb\productionrecipes.gd.xml -TotalCount 50
```

If DLC or package folders changed, glance at their main files the same way:

```powershell
Get-Content -LiteralPath .\game-gdb\dlc1\gdb\resources.gd.xml -TotalCount 50
Get-Content -LiteralPath .\game-gdb\dlc1\gdb\buildings.gd.xml -TotalCount 50
Get-Content -LiteralPath .\game-gdb\dlc1\gdb\productionrecipes.gd.xml -TotalCount 50
```

Then, from the **diff** (not the head-peek), look for:

- new components
- removed or renamed files
- new package folders
- changed reference patterns
- new DLC overlay behavior
- new objective or tech tree systems

### 8. Update Documentation

At minimum, update:

- [README.md](README.md)
- [DATABASE_OVERVIEW.md](DATABASE_OVERVIEW.md)
- future generated/reference docs, once they exist

Keep documentation in English for GitHub readability.

### 9. Suggested AI Update Checklist

When an AI assistant refreshes the repo, it should:

1. scan files and packages
2. parse XML roots
3. count entities per package and file
4. check duplicate GUIDs
5. count component types
6. resolve GUID references
7. compare key numbers to the previous docs
8. update Markdown files
9. mention any unusual changes or unresolved references
10. avoid modifying extracted XML files unless explicitly asked

## What Not To Do By Default

- Do not rewrite or normalize the extracted XML files.
- Do not change GUIDs.
- Do not delete old documentation unless replacing it with an updated version.
- Do not treat null GUIDs as broken references.
- Do not assume a package is unused just because it has few references.

## Future Automation

The PowerShell snippets above are wrapped as scripts:

```text
scripts/
├── prepare-update.ps1
├── extract-xmls-from-paks.ps1
├── update-from-paks.ps1
├── analyze_database.ps1
├── generate_catalog.ps1
├── validate_database.ps1
└── diff_versions.ps1
```

Run validation from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Run analysis from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\analyze_database.ps1
```

Run the generated catalog from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

It produces:

```text
generated/
├── entities.json
├── references.json
├── analysis-summary.json
└── catalog/
```

These generated files are also ignored by Git by default because they are derived from local game data. Once they exist locally, Markdown updates can become much more reliable and less manual.
