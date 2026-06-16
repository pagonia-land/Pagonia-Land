# Extract every `*.gd.xml` entry from each `*.pak` under game-paks/ into
# game-gdb/ using the pagonia-paker CLI. The result mirrors the canonical
# pak layout (game-gdb/<pak>/gdb/X.gd.xml, plus <pak>/maps/ for any
# map-specific gd.xml shipped by that pak), so mod patches written against
# game-gdb/ also work against a directly unpacked pak.
#
# Replaces the previous "manual copy from the gdb/ folder inside each pak"
# step in the update playbook.
#
# Usage:
#   pwsh ./scripts/extract-xmls-from-paks.ps1 -Clean   # recommended on every refresh
#   pwsh ./scripts/extract-xmls-from-paks.ps1          # additive — leaves stale subfolders behind
#   pwsh ./scripts/extract-xmls-from-paks.ps1 -PaksDir ./game-paks -OutDir ./game-gdb -Clean
#
# Always prefer -Clean when refreshing for a new game version. Without it,
# subfolders for paks that no longer ship in the new version (e.g. a DLC
# that was uninstalled, or a pre-DLC 1.2.2 install over a previous DLC
# extraction) will silently linger in game-gdb/ and mix two versions'
# XMLs together.

[CmdletBinding()]
param(
    [string]$PaksDir = "game-paks",
    [string]$OutDir = "game-gdb",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

$paksFull = if ([System.IO.Path]::IsPathRooted($PaksDir)) { $PaksDir } else { Join-Path $repoRoot $PaksDir }
$outFull = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }
$cliProject = Join-Path $repoRoot "tools/pagonia-paker/src/PagoniaLand.Paker.Cli"

if (-not (Test-Path -LiteralPath $paksFull)) {
    throw "Paks directory not found: $paksFull"
}

$paks = @(Get-ChildItem -LiteralPath $paksFull -Filter *.pak -File)
if ($paks.Count -eq 0) {
    throw "No .pak files in $paksFull. Drop your install paks there first."
}

# Wipe the entire output tree (preserving README.md) before extracting.
# A targeted clean that only removed pak-matching subfolders would leave
# stale per-pak directories behind whenever a version stops shipping a
# pak — e.g. running against pre-DLC 1.2.2 paks would leave a previous
# extraction's `dlc1/` subfolder in place, causing tools downstream to
# mix two versions' XMLs.
if ($Clean -and (Test-Path -LiteralPath $outFull)) {
    Get-ChildItem -LiteralPath $outFull -Force -Exclude "README.md" | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
    Write-Host "Cleaned $outFull (preserved README.md)"
}

New-Item -ItemType Directory -Path $outFull -Force | Out-Null

foreach ($pak in $paks) {
    Write-Host ""
    Write-Host "=== $($pak.Name) ==="
    # -f .gd.xml is a case-sensitive substring filter on the entry filename.
    # Every game-database XML in a pak ends with .gd.xml; nothing else does.
    & dotnet run --project $cliProject -- unpack -f .gd.xml $pak.FullName $outFull
    if ($LASTEXITCODE -ne 0) {
        throw "paker unpack failed for $($pak.Name) (exit $LASTEXITCODE)"
    }
}

Write-Host ""
Write-Host "Done. Inventory under $outFull :"
Get-ChildItem -LiteralPath $outFull -Directory | ForEach-Object {
    $count = (Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter *.gd.xml -File).Count
    Write-Host ("  {0,-16} {1,4} XML" -f $_.Name, $count)
}
$total = (Get-ChildItem -LiteralPath $outFull -Recurse -Filter *.gd.xml -File).Count
Write-Host ("  {0,-16} {1,4} XML" -f "TOTAL", $total)
