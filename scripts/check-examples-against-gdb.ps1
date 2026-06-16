#requires -Version 7.0
<#
.SYNOPSIS
    Dry-run every shipped example/official mod against a real game database and
    fail if any patch target no longer resolves (drift after a game update).

.DESCRIPTION
    Schema validation (scripts/preflight.ps1) only checks that example mods are
    structurally well-formed; it never touches the real game data, because the
    extracted game-gdb is proprietary and not committed (so CI can't see it).
    That leaves a gap: when a game update renames an asset, moves an entity, or
    changes a value, an example's `expectedOldValue` / `expectedOldXml` / target
    GUID silently goes stale and only surfaces when someone actually deploys it.

    This script closes that gap locally. For every example and official mod it
    runs `pagonia-patcher plan --game <game-gdb> --mods <mod>` (a pure dry-run,
    nothing is written) and treats a non-zero exit — target-missing, expected-
    value mismatch, conflict — as a failure. Run it after every game update, and
    before committing changes to the example mods.

    Game database resolution order (first hit wins):
      1. -GameRoot <path>
      2. $env:PAGONIA_GAME_GDB
      3. <repo>/game-gdb            (the working extraction)
      4. newest <repo>/snapshots/*/game-gdb

    If none is found the script prints a notice and exits 0 — the gate is
    local-only by design, so its absence (e.g. in CI) is a skip, not a failure.

.PARAMETER GameRoot
    Path to an extracted game database root (the folder that contains core/,
    dlc1/, ...). Overrides every other source.

.PARAMETER NoBuild
    Skip building the patcher CLI. Use when you've already built it this session.

.EXAMPLE
    .\scripts\check-examples-against-gdb.ps1

.EXAMPLE
    .\scripts\check-examples-against-gdb.ps1 -GameRoot D:\extracted\game-gdb
#>

[CmdletBinding()]
param(
    [string]$GameRoot,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$patcherCli = 'tools/pagonia-patcher/src/PagoniaLand.Patcher.Cli'

function Test-GameGdb([string]$path) {
    # A usable game root has at least core/gdb under it. Stay defensive: a bad
    # -GameRoot (e.g. a non-existent drive) must read as "not a game root", not
    # throw under $ErrorActionPreference = 'Stop'.
    if (-not $path) { return $false }
    try { return Test-Path -LiteralPath (Join-Path $path 'core/gdb') -ErrorAction Stop }
    catch { return $false }
}

# --- Resolve the game database --------------------------------------------
$resolved = $null
$source = $null

if ($GameRoot) {
    $resolved = $GameRoot; $source = '-GameRoot'
}
elseif ($env:PAGONIA_GAME_GDB) {
    $resolved = $env:PAGONIA_GAME_GDB; $source = '$env:PAGONIA_GAME_GDB'
}
elseif (Test-GameGdb (Join-Path $repoRoot 'game-gdb')) {
    $resolved = Join-Path $repoRoot 'game-gdb'; $source = 'repo game-gdb/'
}
else {
    $snapshot = Get-ChildItem -Path (Join-Path $repoRoot 'snapshots') -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'game-gdb' } |
        Where-Object { Test-GameGdb $_ } |
        Select-Object -First 1
    if ($snapshot) { $resolved = $snapshot; $source = "snapshot $(Split-Path (Split-Path $snapshot -Parent) -Leaf)" }
}

if (-not (Test-GameGdb $resolved)) {
    Write-Host 'check-examples-against-gdb: no local game-gdb found — skipping.' -ForegroundColor Yellow
    Write-Host '  (provide -GameRoot, set $env:PAGONIA_GAME_GDB, extract to game-gdb/, or add a snapshot.)' -ForegroundColor DarkGray
    exit 0
}

Write-Host "check-examples-against-gdb: using $source -> $resolved" -ForegroundColor Cyan

# --- Build the CLI once ----------------------------------------------------
if (-not $NoBuild) {
    Write-Host '==> Build patcher CLI' -ForegroundColor Cyan
    dotnet build $patcherCli --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { Write-Host 'FAIL: build' -ForegroundColor Red; exit $LASTEXITCODE }
}

# --- Discover example mods -------------------------------------------------
# Every directory with a mod.yaml under these roots is a real, shipped example.
# manager-walkthrough is excluded: it ships its own synthetic game-gdb fixture
# (with made-up GUIDs) and is exercised end-to-end by its own run.ps1, so it is
# not meant to resolve against the real game database.
$roots = @(
    'sandbox/examples',
    'examples/mod-repo-example/mods',
    'official-mods/mods'
)

$mods = foreach ($root in $roots) {
    $full = Join-Path $repoRoot $root
    if (-not (Test-Path $full)) { continue }
    Get-ChildItem -Path $full -Recurse -Filter 'mod.yaml' -File |
        Where-Object { $_.FullName -notmatch '[\\/]manager-walkthrough[\\/]' } |
        ForEach-Object { Split-Path $_.FullName -Parent }
}
$mods = $mods | Sort-Object -Unique

if (-not $mods) { Write-Host 'No example mods found.' -ForegroundColor Yellow; exit 0 }

# --- Dry-run each mod ------------------------------------------------------
$failures = @()
foreach ($mod in $mods) {
    $rel = [IO.Path]::GetRelativePath($repoRoot, $mod).Replace('\', '/')
    $output = & dotnet run --project $patcherCli --no-build -- plan --game $resolved --mods $mod 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAIL] $rel" -ForegroundColor Red
        $detail = $output | Select-String -Pattern 'Error:|Conflict|mismatch|missing' | Select-Object -First 4
        $detail | ForEach-Object { Write-Host "         $($_.Line.Trim())" -ForegroundColor Red }
        $failures += $rel
    }
    else {
        Write-Host "  [ ok ] $rel" -ForegroundColor Green
    }
}

Write-Host ''
if ($failures.Count -gt 0) {
    Write-Host "check-examples-against-gdb: $($failures.Count) example mod(s) no longer resolve against $source." -ForegroundColor Red
    Write-Host '  These reference game data that has drifted — update their targets/expected values.' -ForegroundColor Red
    exit 1
}

Write-Host "check-examples-against-gdb: all $($mods.Count) example mods resolve cleanly." -ForegroundColor Green
exit 0
