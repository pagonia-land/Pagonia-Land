# One-command orchestrator for a game-version update.
#
# When new `.pak` files land in game-paks/ (and optionally a
# `game-paks/patch_notes.txt` next to them), this script runs the full
# refresh pipeline:
#
#   1. extract every *.gd.xml from the new paks into game-gdb/
#   2. validate the XML database
#   3. analyze it (entities.json + references.json)
#   4. regenerate the catalog tree under generated/catalog/
#   5. (optional) freeze the just-installed state under
#      snapshots/<NewVersion>/ so it can be diffed against the next
#      update's state
#
# The previous version stays available via its own snapshot from the
# last update — that's the safety net if anything in this run goes
# wrong. Snapshots are frozen by convention; once written they should
# be treated as read-only.
#
# Diffing against a previous snapshot is a separate manual step (see
# scripts/diff_versions.ps1) — that's where you decide which two
# versions to compare and where to put the report.
#
# Usage:
#   pwsh ./scripts/update-from-paks.ps1                                # auto-reads version from the game-paks/ exe
#   pwsh ./scripts/update-from-paks.ps1 -NewVersion 1.3.1-11826+193733
#
# Without -NewVersion, the version is read from the game executable staged in
# game-paks/ (its ProductVersion — the same string mods declare as
# gameDatabaseVersion). Drop it there yourself or via prepare-update.ps1. If
# neither an explicit version nor a staged exe is present, the pipeline still
# runs but writes no snapshot.
#
# The extraction step always wipes game-gdb/ first (preserving its README).
# Without that, subfolders for paks that no longer ship in the new version
# linger and downstream tools end up mixing two versions' XMLs. The legacy
# -Clean switch is accepted for compatibility but is now a no-op — clean
# is always on.

[CmdletBinding()]
param(
    [string]$NewVersion,
    [switch]$Clean,
    [switch]$SkipAnalysis,
    [switch]$SkipCatalog
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$scripts = Join-Path $repoRoot "scripts"

function Invoke-Step([string]$Label, [string]$ScriptName, [string[]]$ScriptArgs = @()) {
    Write-Host ""
    Write-Host "=== $Label ==="
    $scriptPath = Join-Path $scripts $ScriptName
    & pwsh -NoProfile -ExecutionPolicy Bypass -File $scriptPath @ScriptArgs
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed (exit $LASTEXITCODE)"
    }
}

# When no explicit -NewVersion is given, read it from the game executable
# staged in game-paks/ (where prepare-update.ps1, or a manual copy, places it).
# Its ProductVersion is the real game version — the same string mods declare as
# gameDatabaseVersion. Use ProductVersion, NOT FileVersion (the latter is the
# truncated 1.3.0.0 form). Same read prepare-update.ps1 does; kept inline so the
# two scripts stay independently runnable.
if (-not $NewVersion) {
    $paksDir = Join-Path $repoRoot "game-paks"
    $knownExe = Join-Path $paksDir "Pioneers of Pagonia.exe"
    $stagedExe = $null
    if (Test-Path -LiteralPath $knownExe) {
        $stagedExe = Get-Item -LiteralPath $knownExe
    }
    else {
        $exes = @(Get-ChildItem -LiteralPath $paksDir -Filter *.exe -File -ErrorAction SilentlyContinue)
        if ($exes.Count -eq 1) { $stagedExe = $exes[0] }
    }
    if ($stagedExe) {
        $pv = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($stagedExe.FullName)).ProductVersion
        if ($pv) { $pv = $pv.Trim() }
        if ($pv) {
            $NewVersion = $pv
            Write-Host "Detected version $NewVersion from game-paks/$($stagedExe.Name) (ProductVersion)."
            Write-Host "Pass -NewVersion explicitly to override."
            Write-Host ""
        }
        else {
            Write-Warning "Staged exe $($stagedExe.Name) has an empty ProductVersion; running without a detected version. Pass -NewVersion explicitly."
        }
    }
}

# If a snapshot of <NewVersion> already exists, refuse early so we
# don't run the full pipeline and discover the conflict at the end.
if ($NewVersion) {
    $snapDir = Join-Path $repoRoot "snapshots/$NewVersion"
    if (Test-Path -LiteralPath $snapDir) {
        throw "Snapshot already exists: $snapDir. Remove it first, or pick a different -NewVersion."
    }
}

# 1. Extract the new XMLs. -Clean is unconditional here so a previous
# version's pak subfolders never leak into the new extraction.
Invoke-Step "Extract XMLs from paks" "extract-xmls-from-paks.ps1" @("-Clean")

# 2. Validate the XML database.
Invoke-Step "Validate database" "validate_database.ps1"

# 3. Re-analyze: rebuild entities + references indexes.
if (-not $SkipAnalysis) {
    Invoke-Step "Analyze database" "analyze_database.ps1"
}

# 4. Regenerate the catalog tree.
if (-not $SkipCatalog) {
    Invoke-Step "Regenerate catalog" "generate_catalog.ps1"
}

# 5. Snapshot the just-installed state so the next update has
#    something to diff against. We do this AFTER extract+validate
#    succeed so a broken extraction never becomes a sealed snapshot.
if ($NewVersion) {
    $snapDir = Join-Path $repoRoot "snapshots/$NewVersion"
    Write-Host ""
    Write-Host "=== Snapshot just-installed state -> snapshots/$NewVersion ==="
    New-Item -ItemType Directory -Path $snapDir -Force | Out-Null
    foreach ($d in @("game-gdb", "generated")) {
        $src = Join-Path $repoRoot $d
        if (Test-Path -LiteralPath $src) {
            Copy-Item -LiteralPath $src -Destination $snapDir -Recurse
            Write-Host "  copied $d/ -> $snapDir/$d/"
        }
    }
    # Patch notes that ship next to the new paks travel with the
    # snapshot so the changelog writer can cross-reference them later.
    $notes = Join-Path $repoRoot "game-paks/patch_notes.txt"
    if (Test-Path -LiteralPath $notes) {
        Copy-Item -LiteralPath $notes -Destination $snapDir
        Write-Host "  copied game-paks/patch_notes.txt"
    }
}

Write-Host ""
Write-Host "Done."
Write-Host "Next manual steps:"
if (-not $NewVersion) {
    Write-Host "  - rerun with -NewVersion <version> (or stage the game exe into game-paks/) to freeze this state as a snapshot"
}
Write-Host "  - run scripts/diff_versions.ps1 against the previous snapshot"
Write-Host "  - write a CHANGELOG.md entry summarising the observed changes"
Write-Host "  - cross-reference game-paks/patch_notes.txt if present"
Write-Host "  - update REFERENCE.md and VALIDATION_BASELINE.md counts as needed"
