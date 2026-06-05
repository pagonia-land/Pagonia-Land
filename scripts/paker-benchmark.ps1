# Wall-clock benchmark for pagonia-paker.
#
# Measures `list`, `unpack`, and `pack` on a given pak file across a configurable
# set of `--jobs` values. For each combination, runs the command N times and
# reports the median wall-clock time. Used to validate the paker performance
# pass, but useful any time the paker's I/O or compression pipeline changes.
#
# The script assumes `pagonia-paker` is on PATH (override with -PagoniaPaker)
# and that the pak file exists locally. Source files for `pack` are produced by
# a first `unpack` + `list` pass under -WorkDir, so the pak's contents have to
# be readable from disk.
#
# Example:
#   pwsh ./scripts/paker-benchmark.ps1 -Pak ./game-paks/decorations1.pak
#   pwsh ./scripts/paker-benchmark.ps1 -Pak ./game-paks/dlc1.pak -Jobs 1,4,8,16 -Runs 5

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Pak,
    [string]$PagoniaPaker = "pagonia-paker",
    [int[]]$Jobs = @(1, 4, 0),  # 0 means "default" (= processor count)
    [int]$Runs = 3,
    [string]$WorkDir,
    [string[]]$Commands = @("list", "unpack", "pack"),
    [switch]$KeepWorkDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Pak)) {
    Write-Error "Pak file not found: $Pak"
    exit 1
}

$paker = Get-Command $PagoniaPaker -ErrorAction SilentlyContinue
if (-not $paker) {
    Write-Error "Tool '$PagoniaPaker' is not on PATH. Pass -PagoniaPaker with an explicit path."
    exit 1
}

if (-not $WorkDir) {
    $WorkDir = Join-Path $env:TEMP ("paker-bench-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
}
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
Write-Host "Working directory: $WorkDir"

# Pre-stage the unpacked tree + pakinfo.json so `pack` has source files to read.
$stagingDir = Join-Path $WorkDir "stage"
if ($Commands -contains "pack") {
    Write-Host "Staging unpacked tree + pakinfo for pack benchmarks..."
    & $PagoniaPaker unpack $Pak $stagingDir | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Stage unpack failed"; exit 1 }
    & $PagoniaPaker list $Pak $stagingDir | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Error "Stage list failed"; exit 1 }
}

function Format-Jobs([int]$jobs) {
    if ($jobs -le 0) { return "default" }
    return "$jobs"
}

function Median([double[]]$values) {
    $sorted = $values | Sort-Object
    $count = $sorted.Count
    if ($count -eq 0) { return [double]::NaN }
    if ($count % 2 -eq 1) { return $sorted[[Math]::Floor($count / 2)] }
    return ($sorted[$count / 2 - 1] + $sorted[$count / 2]) / 2.0
}

function Invoke-Timed([string[]]$cliArgs) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $PagoniaPaker @cliArgs | Out-Null
    $sw.Stop()
    if ($LASTEXITCODE -ne 0) { Write-Error "Command failed: $($cliArgs -join ' ')"; throw "Command failed" }
    return $sw.Elapsed.TotalSeconds
}

$results = @()

foreach ($cmd in $Commands) {
    foreach ($jobs in $Jobs) {
        # The list command has no -j knob; only measure it once.
        if ($cmd -eq "list" -and $jobs -ne $Jobs[0]) { continue }

        $samples = @()
        for ($run = 0; $run -lt $Runs; $run++) {
            $cliArgs = @($cmd)
            if ($jobs -gt 0 -and $cmd -ne "list") { $cliArgs += "-j"; $cliArgs += "$jobs" }

            switch ($cmd) {
                "list"   {
                    $listDir = Join-Path $WorkDir "list-out"
                    if (Test-Path $listDir) { Remove-Item -Recurse -Force $listDir }
                    $cliArgs += $Pak
                    $cliArgs += $listDir
                }
                "unpack" {
                    $unpackDir = Join-Path $WorkDir "unpack-out"
                    if (Test-Path $unpackDir) { Remove-Item -Recurse -Force $unpackDir }
                    $cliArgs += $Pak
                    $cliArgs += $unpackDir
                }
                "pack"   {
                    $repack = Join-Path $WorkDir "repack.pak"
                    if (Test-Path $repack) { Remove-Item -Force $repack }
                    $cliArgs += (Join-Path $stagingDir "pakinfo.json")
                    $cliArgs += $repack
                }
            }

            $samples += Invoke-Timed $cliArgs
        }

        $median = Median ([double[]]$samples)
        $results += [pscustomobject]@{
            Command  = $cmd
            Jobs     = Format-Jobs $jobs
            MedianMs = [Math]::Round($median * 1000, 1)
            Samples  = ($samples | ForEach-Object { [Math]::Round($_ * 1000, 1) }) -join ", "
        }
    }
}

Write-Host ""
Write-Host "Pak: $Pak"
Write-Host "Runs per cell: $Runs"
Write-Host ""
$results | Format-Table -AutoSize

if (-not $KeepWorkDir) {
    Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
}
