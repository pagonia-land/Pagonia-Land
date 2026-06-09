#requires -Version 5.1
<#
.SYNOPSIS
    Guards that every committed doc which presents the CURRENT game snapshot
    agrees on the version string and the headline numbers.

.DESCRIPTION
    The repo restates the current game version + a handful of headline counts
    in many docs (README, DATABASE_OVERVIEW, REFERENCE, per-topic guides, ...).
    When a game update lands, every one of those has to move in lockstep, or the
    repo ships stale numbers — which is fatal for a reference modders rely on.

    This check makes that drift impossible to merge by accident. It runs on the
    COMMITTED markdown only — no game-gdb/, generated/, or snapshots/ needed —
    so it works in CI where the proprietary/generated data is absent. It treats
    two committed docs as the single sources of truth and verifies every other
    "current snapshot" doc agrees with them:

      * VALIDATION_BASELINE.md  -> the current version string + validation
                                   headline counts (entities, GUID refs, ...).
      * REFERENCE.md            -> the generated-catalog counts (localization
                                   rows, asset/icon/mesh rows, ...).

    Checks performed:
      1. Version: every "current snapshot" doc contains the canonical version,
         and no such doc presents a DIFFERENT version as current (historical
         mentions — "previous", "Meadowsong", "1.2.2", deltas — are ignored).
      2. Counts: every doc that restates a headline/catalog number contains the
         canonical value. A doc left on the old value no longer contains the new
         (distinctive) number, so it fails here.

    When you refresh for a new version: update VALIDATION_BASELINE.md and
    REFERENCE.md first (the canonical pair), then run this script — it names
    every doc still carrying a stale version or number. Fix until green.

    The MANAGED file lists below are the machine-readable half of the
    "version currency checklist" in UPDATE_PLAYBOOK.md. Add a doc here when it
    starts citing current-version data.

.EXAMPLE
    pwsh ./scripts/check-doc-currency.ps1
#>

[CmdletBinding()]
param([switch]$Quiet)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Read-Doc([string]$rel) {
    $p = Join-Path $repoRoot $rel
    if (-not (Test-Path -LiteralPath $p)) { throw "Managed doc not found: $rel" }
    return Get-Content -Raw -LiteralPath $p
}

# Pull a "| Label | 1,234 |" table value out of a doc. Returns the matched
# number string verbatim (commas kept) so downstream checks are exact.
function Get-TableValue([string]$text, [string]$label) {
    $rx = '(?m)^\|\s*' + [regex]::Escape($label) + '\s*\|\s*([\d,]+)\s*\|'
    if ($text -match $rx) { return $Matches[1] }
    throw "Could not parse '$label' from the canonical source."
}

$fullVersionRx = '\d+\.\d+\.\d+-\d+\+\d+'

# ---------------------------------------------------------------------------
# 1. Canonical sources of truth.
# ---------------------------------------------------------------------------
$baseline = Read-Doc 'VALIDATION_BASELINE.md'
if ($baseline -notmatch '(?m)^\*\*Baseline:\*\*\s*`([^`]+)`') {
    throw 'Could not parse the canonical version from VALIDATION_BASELINE.md (**Baseline:** `<version>`).'
}
$version = $Matches[1].Trim()

# Validation headline counts (canonical: VALIDATION_BASELINE.md). Only the
# distinctive comma-grouped values are used as currency markers — small values
# like "30" are not unique enough to prove currency by presence.
$cnt = @{
    entities = Get-TableValue $baseline 'Entity definitions'
    guidRefs = Get-TableValue $baseline 'GUID-like references'
    resolved = Get-TableValue $baseline 'Resolved references'
    nullRefs = Get-TableValue $baseline 'Null GUID references'
}

# Generated-catalog counts (canonical: REFERENCE.md).
$reference = Read-Doc 'REFERENCE.md'
$cat = @{
    locaRows     = Get-TableValue $reference 'Localization rows'
    locaUnique   = Get-TableValue $reference 'Unique localization keys'
    locaRef      = Get-TableValue $reference 'Referenced localization-like keys'
    locaNotInTag = Get-TableValue $reference 'Keys not in local tag index'
    assetRows    = Get-TableValue $reference 'Asset reference rows'
    visualRows   = Get-TableValue $reference 'Visual/audio component rows'
    icons        = Get-TableValue $reference 'Icon references'
    meshes       = Get-TableValue $reference 'Mesh references'
}

# ---------------------------------------------------------------------------
# 2. Managed-file manifest (the checklist, in machine-readable form).
# ---------------------------------------------------------------------------

# Docs whose PRIMARY "current snapshot / scan / baseline" must be $version.
$versionFiles = @(
    'README.md'
    'DATABASE_OVERVIEW.md'
    'REFERENCE.md'
    'VALIDATION_BASELINE.md'
    'docs/catalog-coverage.md'
    'docs/dlc-patch-override-model.md'
    'docs/package-layering.md'
)

# Docs that restate canonical numbers -> the exact values they must contain.
$valueFiles = [ordered]@{
    'README.md'                          = @($cnt.entities, $cnt.guidRefs)
    'DATABASE_OVERVIEW.md'               = @($cnt.entities, $cnt.guidRefs, $cnt.resolved, $cnt.nullRefs)
    'docs/catalog-coverage.md'           = @($cnt.entities, $cnt.guidRefs, $cnt.resolved, $cnt.nullRefs)
    'docs/dlc-patch-override-model.md'   = @($cnt.entities, $cnt.guidRefs)
    'docs/package-layering.md'           = @($cnt.entities)
    'docs/repository-layout.md'          = @($cnt.entities, $cnt.guidRefs, $cnt.resolved, $cnt.nullRefs)
    'docs/localization.md'               = @($cat.locaRows, $cat.locaUnique, $cat.locaRef, $cat.locaNotInTag)
    'docs/systems/visual-audio-assets.md'= @($cat.assetRows, $cat.visualRows, $cat.icons, $cat.meshes)
}

# Lines that mention a version but in a clearly HISTORICAL context are ignored
# by the stale-version scan (a current-snapshot doc may legitimately name the
# previous version when describing the delta).
$historicalMarkers = 'previous|prior|delta|former|Meadowsong|shipped|public release|->|→|earlier'

$problems = New-Object System.Collections.Generic.List[string]

# ---------------------------------------------------------------------------
# 3. Version checks.
# ---------------------------------------------------------------------------
foreach ($rel in $versionFiles) {
    $text = Read-Doc $rel
    if (-not $text.Contains($version)) {
        $problems.Add("$rel : does not mention the current version '$version' (stale or not yet updated).")
    }
    foreach ($line in ($text -split "`n")) {
        if ($line -notmatch $fullVersionRx) { continue }
        if ($line -match $historicalMarkers) { continue }
        # Only flag lines that frame a version as the current state.
        if ($line -notmatch '(?i)current|snapshot|baseline|as of|latest|regenerated|produced|scan') { continue }
        foreach ($m in [regex]::Matches($line, $fullVersionRx)) {
            if ($m.Value -ne $version) {
                $problems.Add("$rel : presents non-current version '$($m.Value)' as current -> '$($line.Trim())'")
            }
        }
    }
}

# ---------------------------------------------------------------------------
# 4. Count checks.
# ---------------------------------------------------------------------------
foreach ($rel in $valueFiles.Keys) {
    $text = Read-Doc $rel
    foreach ($value in $valueFiles[$rel]) {
        # Digit-boundary match so a count like "811" isn't satisfied by a superstring such as
        # "8110" (a stale-but-larger value Contains() would have happily accepted). Only digit
        # adjacency is guarded, so a version followed by punctuation ("1.3.1.") still matches.
        if ($text -notmatch ('(?<![0-9])' + [regex]::Escape($value) + '(?![0-9])')) {
            $problems.Add("$rel : missing canonical value '$value' (stale or not yet updated).")
        }
    }
}

# ---------------------------------------------------------------------------
# 5. Report.
# ---------------------------------------------------------------------------
if (-not $Quiet) {
    Write-Host "check-doc-currency: canonical version = $version" -ForegroundColor Cyan
    Write-Host ("  validation counts: entities {0}, refs {1}, resolved {2}, null {3}" -f $cnt.entities, $cnt.guidRefs, $cnt.resolved, $cnt.nullRefs)
    Write-Host ("  catalog counts:    loca {0}, assets {1}, icons {2}, meshes {3}" -f $cat.locaRows, $cat.assetRows, $cat.icons, $cat.meshes)
}

if ($problems.Count -gt 0) {
    Write-Host ''
    Write-Host "check-doc-currency: FAIL — $($problems.Count) doc(s) out of sync:" -ForegroundColor Red
    foreach ($p in $problems) { Write-Host "  - $p" -ForegroundColor Red }
    Write-Host ''
    Write-Host "Fix: update VALIDATION_BASELINE.md / REFERENCE.md first (canonical), then bring the files above in line. See UPDATE_PLAYBOOK.md -> Version Currency." -ForegroundColor Yellow
    exit 1
}

Write-Host ''
Write-Host "check-doc-currency: OK — all managed docs are on $version." -ForegroundColor Green
exit 0
