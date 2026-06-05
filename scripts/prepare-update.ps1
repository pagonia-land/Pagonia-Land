# Stage a new game version for a repository refresh by copying the install's
# `.pak` archives and the game executable into game-paks/, so the manual
# "copy the archives here" step from the update playbook is automated.
#
# The game folder is the only thing you point at: the script reads the paks
# from <GameRoot>/pak/ and the version from <GameRoot>/Pioneers of Pagonia.exe.
# It only ever READS from the game install — the sole writes are into the
# repo's game-paks/ folder.
#
# Order of operations is deliberate:
#   1. Resolve the game folder (explicit -GameRoot, else the default Steam path).
#   2. Read the version from the exe's ProductVersion FIRST — before copying
#      gigabytes — so a re-run against an already-captured version aborts cheaply.
#   3. If a snapshot for that version already exists, stop (nothing to do) unless
#      -Force is given.
#   4. Clean stale *.pak / *.exe out of game-paks/ (so a pak the new version no
#      longer ships — e.g. an uninstalled DLC — does not linger), preserving
#      README.md and any hand-edited patch_notes.txt.
#   5. Copy the paks + exe in.
#
# Then the usual flow continues by hand:
#   - add / update game-paks/patch_notes.txt
#   - run scripts/update-from-paks.ps1 (extract -> validate -> analyze ->
#     catalog -> snapshot)
#
# Usage:
#   pwsh ./scripts/prepare-update.ps1                       # default Steam install
#   pwsh ./scripts/prepare-update.ps1 -GameRoot 'D:\Games\Pioneers of Pagonia'
#   pwsh ./scripts/prepare-update.ps1 -DryRun               # show the plan, copy nothing
#   pwsh ./scripts/prepare-update.ps1 -Force                # re-copy even if a snapshot exists

[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$PaksDir = "game-paks",
    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

# 1. Resolve the game folder. An explicit -GameRoot wins; otherwise fall back to
#    the default Steam install path (the same one the manager suggests). The
#    default is Windows-only — Pioneers of Pagonia ships Steam/Windows today.
$defaultGameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Pioneers of Pagonia'
if (-not $GameRoot) {
    if (-not $IsWindows) {
        throw "No -GameRoot given and no platform default is available on this OS. Pass -GameRoot <path>."
    }
    $GameRoot = $defaultGameRoot
    Write-Host "No -GameRoot given; using the default install path:"
    Write-Host "  $GameRoot"
}
if (-not (Test-Path -LiteralPath $GameRoot)) {
    throw "Game folder not found: $GameRoot`nPass -GameRoot <path> if your install lives elsewhere."
}

# pak/ subfolder with at least one *.pak — the live-install shape.
$gamePakDir = Join-Path $GameRoot "pak"
if (-not (Test-Path -LiteralPath $gamePakDir)) {
    throw "No 'pak' subfolder under the game root: $gamePakDir`nIs '$GameRoot' really a Pioneers of Pagonia install?"
}
# Every *.pak in the pak folder, by wildcard — not a fixed list — so a new
# official pak (a future DLC, a renamed or newly-added archive) is copied
# automatically and never silently forgotten. Top-level only, matching how the
# game and the manager's GameLayoutDetector enumerate this folder.
$srcPaks = @(Get-ChildItem -LiteralPath $gamePakDir -Filter *.pak -File)
if ($srcPaks.Count -eq 0) {
    throw "No .pak files under $gamePakDir."
}

# Locate the game executable. Prefer the known name; fall back to the single
# *.exe at the game root so an upstream rename doesn't hard-break this.
$knownExe = Join-Path $GameRoot "Pioneers of Pagonia.exe"
if (Test-Path -LiteralPath $knownExe) {
    $srcExe = Get-Item -LiteralPath $knownExe
}
else {
    $exes = @(Get-ChildItem -LiteralPath $GameRoot -Filter *.exe -File)
    if ($exes.Count -eq 1) {
        $srcExe = $exes[0]
        Write-Host "Note: 'Pioneers of Pagonia.exe' not found; using the only .exe at the game root: $($srcExe.Name)"
    }
    elseif ($exes.Count -eq 0) {
        throw "No .exe at the game root ($GameRoot); cannot read the game version."
    }
    else {
        throw "Multiple .exe files at the game root; cannot pick one automatically. Expected 'Pioneers of Pagonia.exe'."
    }
}

# 2. Read the version from the exe FIRST. Use ProductVersion, NOT FileVersion:
#    FileVersion is the 4-part numeric form (e.g. 1.3.0.0) with the build /
#    revision lost, while ProductVersion carries the full string that matches a
#    mod manifest's gameDatabaseVersion (e.g. 1.3.1-11826+193733).
$version = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($srcExe.FullName)).ProductVersion
if ($version) { $version = $version.Trim() }
if (-not $version) {
    throw "Could not read ProductVersion from $($srcExe.Name); the file may be missing its version resource."
}
Write-Host ""
Write-Host "Detected game version: $version"
Write-Host "  from $($srcExe.Name) (ProductVersion)"

# 3. Early guard: an existing snapshot for this version means the repo already
#    reflects it, so there's nothing to prepare. Checked BEFORE the copy so a
#    redundant re-run costs nothing instead of churning gigabytes.
$snapDir = Join-Path $repoRoot "snapshots/$version"
if ((Test-Path -LiteralPath $snapDir) -and -not $Force) {
    Write-Host ""
    Write-Host "A snapshot for $version already exists:"
    Write-Host "  $snapDir"
    Write-Host "Your local data already reflects this version — nothing to prepare."
    Write-Host "Pass -Force to re-copy the paks + exe anyway."
    return
}

# Resolve the destination paks dir (repo-relative unless an absolute path is given).
$paksFull = if ([System.IO.Path]::IsPathRooted($PaksDir)) { $PaksDir } else { Join-Path $repoRoot $PaksDir }

# Build + print the plan.
$plan = @($srcPaks) + @($srcExe)
$totalBytes = ($plan | Measure-Object -Property Length -Sum).Sum

# Existing *.pak / *.exe the clean step will remove (README.md + patch_notes.txt kept).
$staleToRemove = @()
if (Test-Path -LiteralPath $paksFull) {
    $staleToRemove = @(Get-ChildItem -LiteralPath $paksFull -File | Where-Object { $_.Extension -in ".pak", ".exe" })
}

Write-Host ""
Write-Host "Plan:"
Write-Host "  Game root : $GameRoot"
Write-Host "  Dest      : $paksFull"
Write-Host ("  Copy      : {0} pak(s) + 1 exe  ({1:N0} MB total)" -f $srcPaks.Count, ($totalBytes / 1MB))
foreach ($p in $srcPaks) { Write-Host ("    {0,-20} {1,9:N1} MB" -f $p.Name, ($p.Length / 1MB)) }
Write-Host ("    {0,-20} {1,9:N1} MB" -f $srcExe.Name, ($srcExe.Length / 1MB))
if ($staleToRemove.Count -gt 0) {
    Write-Host ("  Clean     : remove {0} existing pak/exe file(s) first (README.md + patch_notes.txt kept)" -f $staleToRemove.Count)
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run — nothing copied. Re-run without -DryRun to do it."
    return
}

# 4. Clean. Remove only *.pak / *.exe so a pak the new version no longer ships
#    doesn't linger; README.md and a hand-edited patch_notes.txt are preserved.
New-Item -ItemType Directory -Path $paksFull -Force | Out-Null
foreach ($f in $staleToRemove) {
    Remove-Item -LiteralPath $f.FullName -Force
}
if ($staleToRemove.Count -gt 0) {
    Write-Host ""
    Write-Host ("Cleaned {0} old pak/exe file(s) from {1} (kept README.md + patch_notes.txt)." -f $staleToRemove.Count, $paksFull)
}

# 5. Copy the paks + exe in.
Write-Host ""
$i = 0
foreach ($item in $plan) {
    $i++
    $dest = Join-Path $paksFull $item.Name
    Write-Host ("  [{0}/{1}] {2} ({3:N1} MB)" -f $i, $plan.Count, $item.Name, ($item.Length / 1MB))
    Copy-Item -LiteralPath $item.FullName -Destination $dest -Force
}

Write-Host ""
Write-Host "Prepared $version into $paksFull."
Write-Host ""
Write-Host "Next steps:"
$notes = Join-Path $paksFull "patch_notes.txt"
if (Test-Path -LiteralPath $notes) {
    Write-Host "  - review / update game-paks/patch_notes.txt (a previous file is still there)"
}
else {
    Write-Host "  - add the official patch notes as game-paks/patch_notes.txt"
}
Write-Host "  - run: pwsh ./scripts/update-from-paks.ps1 -NewVersion $version"
