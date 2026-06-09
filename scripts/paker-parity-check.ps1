# Cross-check pagonia-paker against plpaker for a given pak file.
#
# Runs `list` and `unpack` through both binaries on the same input, then diffs
# the resulting pakinfo.json files and the extracted directory trees. Any drift
# is reported to stderr and the script exits non-zero. Used while pagonia-paker
# is closing the parity gap with plpaker; after the parity step is ratified
# plpaker can be archived.
#
# Both binaries are expected on PATH. Override with -Plpaker / -PagoniaPaker if
# needed. The script writes its intermediate output under -WorkDir (default:
# a temp directory that gets cleaned up on success).
#
# Example:
#   pwsh ./scripts/paker-parity-check.ps1 -Pak ./game-paks/tools.pak

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Pak,
    [string]$Plpaker = "plpaker",
    [string]$PagoniaPaker = "pagonia-paker",
    [string]$WorkDir,
    [switch]$KeepWorkDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Pak)) {
    Write-Error "Pak file not found: $Pak"
    exit 1
}

foreach ($tool in @($Plpaker, $PagoniaPaker)) {
    $cmd = Get-Command $tool -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Write-Error "Tool '$tool' is not on PATH. Pass -Plpaker / -PagoniaPaker with explicit paths."
        exit 1
    }
}

if (-not $WorkDir) {
    $WorkDir = Join-Path ([System.IO.Path]::GetTempPath()) ("paker-parity-" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
}
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
Write-Host "Working directory: $WorkDir"

$plDir = Join-Path $WorkDir "plpaker"
$paDir = Join-Path $WorkDir "pagonia-paker"

$drift = @()

# --- list ---
Write-Host "Running list with both tools..."
& $Plpaker list $Pak $plDir
if ($LASTEXITCODE -ne 0) { Write-Error "plpaker list failed (exit $LASTEXITCODE)"; exit 1 }
& $PagoniaPaker list $Pak $paDir
if ($LASTEXITCODE -ne 0) { Write-Error "pagonia-paker list failed (exit $LASTEXITCODE)"; exit 1 }

$plJson = Join-Path $plDir "pakinfo.json"
$paJson = Join-Path $paDir "pakinfo.json"

# Whitespace and key order may differ across implementations; parse to a canonical
# representation before diffing.
function Read-Pakinfo([string]$Path) {
    return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth 64)
}

$plInfo = Read-Pakinfo $plJson
$paInfo = Read-Pakinfo $paJson

if ($plInfo.version -ne $paInfo.version)   { $drift += "pakinfo.json: version differs ($($plInfo.version) vs $($paInfo.version))" }
if ($plInfo.count   -ne $paInfo.count)     { $drift += "pakinfo.json: count differs ($($plInfo.count) vs $($paInfo.count))" }

$paByName = @{}
foreach ($entry in $paInfo.entries) { $paByName[$entry.filename] = $entry }

foreach ($entry in $plInfo.entries) {
    $other = $paByName[$entry.filename]
    if (-not $other) {
        $drift += "pakinfo.json: entry missing in pagonia-paker output: $($entry.filename)"
        continue
    }
    foreach ($field in @("compressed", "size", "begin", "end", "size_compressed")) {
        if ($entry.$field -ne $other.$field) {
            $drift += ("pakinfo.json: '{0}' field '{1}' differs: plpaker={2} pagonia-paker={3}" -f $entry.filename, $field, $entry.$field, $other.$field)
        }
    }
}

# --- unpack ---
Write-Host "Running unpack with both tools..."
$plUnpack = Join-Path $WorkDir "plpaker-unpack"
$paUnpack = Join-Path $WorkDir "pagonia-paker-unpack"
& $Plpaker unpack $Pak $plUnpack
if ($LASTEXITCODE -ne 0) { Write-Error "plpaker unpack failed (exit $LASTEXITCODE)"; exit 1 }
& $PagoniaPaker unpack $Pak $paUnpack
if ($LASTEXITCODE -ne 0) { Write-Error "pagonia-paker unpack failed (exit $LASTEXITCODE)"; exit 1 }

# Compare the extracted trees by SHA-256 per relative path.
function Get-RelativeHashes([string]$Root) {
    $hashes = @{}
    Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($Root.Length).TrimStart('\', '/').Replace('\', '/')
        $hashes[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    return $hashes
}

$plHashes = Get-RelativeHashes $plUnpack
$paHashes = Get-RelativeHashes $paUnpack

foreach ($key in $plHashes.Keys) {
    if (-not $paHashes.ContainsKey($key)) { $drift += "unpack: file missing in pagonia-paker output: $key" ; continue }
    if ($plHashes[$key] -ne $paHashes[$key]) { $drift += "unpack: SHA-256 mismatch for $key" }
}
foreach ($key in $paHashes.Keys) {
    if (-not $plHashes.ContainsKey($key)) { $drift += "unpack: extra file in pagonia-paker output: $key" }
}

# --- result ---
if ($drift.Count -gt 0) {
    Write-Host ""
    Write-Host "Drift detected:" -ForegroundColor Yellow
    foreach ($line in $drift) { Write-Host "  - $line" -ForegroundColor Yellow }
    if (-not $KeepWorkDir) { Write-Host "Work dir preserved for inspection: $WorkDir" }
    exit 1
}

Write-Host ""
Write-Host "OK: pakinfo and extracted trees match between plpaker and pagonia-paker." -ForegroundColor Green
if (-not $KeepWorkDir) {
    Remove-Item -LiteralPath $WorkDir -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
