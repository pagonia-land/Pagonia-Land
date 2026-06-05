# One-command wrapper around `pagonia-patcher apply` for the sandbox/ workspace.
#
# Defaults:
# - reads every subfolder under sandbox/mods/ as a mod
# - patches against ./game-gdb/
# - writes the patched copy into sandbox/out/
# - emits sandbox/out/apply.md and sandbox/out/apply.json as reports
#
# Useful overrides:
# - -Game <path>      : patch against a different game folder (e.g. the
#                       artificial test fixtures at
#                       .\tools\pagonia-patcher\fixtures\game-gdb-mini)
# - -Collection <yaml>: apply a collection manifest instead of a flat mod
#                       list; the manifest is resolved against
#                       sandbox/mods/

param(
    [string]$Game,
    [string]$Collection
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Game) {
    $Game = Join-Path $repoRoot "game-gdb"
}

$sandbox = Join-Path $repoRoot "sandbox"
$modsRoot = Join-Path $sandbox "mods"
$outDir = Join-Path $sandbox "out"
$cliProject = Join-Path $repoRoot "tools/pagonia-patcher/src/PagoniaLand.Patcher.Cli"

if (-not (Test-Path -LiteralPath $Game)) {
    throw "Game directory not found: $Game. Extract the game database into ./game-gdb/ first, or pass -Game <path>."
}

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$reportPath = Join-Path $outDir "apply.md"
$jsonPath = Join-Path $outDir "apply.json"

if ($Collection) {
    $collectionFull = (Resolve-Path -LiteralPath $Collection).Path
    Write-Host "Applying collection '$collectionFull' to '$outDir' against '$Game'..."
    $cmdArgs = @(
        "apply",
        "--game", $Game,
        "--collection", $collectionFull,
        "--mods-root", $modsRoot,
        "--out", $outDir,
        "--report", $reportPath,
        "--json", $jsonPath
    )
}
else {
    $modDirs = @(
        Get-ChildItem -LiteralPath $modsRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { -not $_.Name.StartsWith(".") }
    )

    if ($modDirs.Count -eq 0) {
        throw "No mod folders found under '$modsRoot'. Add a mod folder there (e.g. copy one from sandbox/examples/) or pass -Collection <path>."
    }

    Write-Host "Applying $($modDirs.Count) mod(s) from '$modsRoot' to '$outDir' against '$Game'..."
    $cmdArgs = @("apply", "--game", $Game, "--mods") +
        ($modDirs | ForEach-Object { $_.FullName }) +
        @("--out", $outDir, "--report", $reportPath, "--json", $jsonPath)
}

dotnet run --project $cliProject -- @cmdArgs
