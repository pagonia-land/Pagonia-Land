#requires -Version 5.1
<#
.SYNOPSIS
    Lint the GitHub Actions workflows with actionlint.

.DESCRIPTION
    The local mirror of the lint-workflows.yml CI job. Catches the class of
    workflow bug that only surfaces when a workflow actually runs: invalid
    runner labels (e.g. a retired macOS image), invalid github.* context
    access, expression / YAML errors, and deprecated action versions.

    On first run it downloads a pinned actionlint binary (rhysd/actionlint)
    into a cache under the OS temp dir; later runs reuse it offline. Keep
    $Version in sync with the image tag in .github/workflows/lint-workflows.yml.

    Exit codes: 0 = clean (or actionlint unavailable and -Strict not set),
    1 = lint findings (or download failed under -Strict).

    Note: the CI job additionally runs shellcheck on `run:` blocks via the
    actionlint Docker image; the bare binary here does not unless shellcheck is
    on PATH.

.PARAMETER Version
    actionlint release version to use (no `v` prefix). Default tracks CI.

.PARAMETER Strict
    Treat "couldn't obtain actionlint" (no cache + no network) as a failure
    instead of a skip. Off by default so an offline preflight isn't blocked.

.EXAMPLE
    .\scripts\check-workflows.ps1
#>

[CmdletBinding()]
param(
    [string]$Version = '1.7.12',
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Resolve-Actionlint {
    param([string]$Version)

    $onWindows = $IsWindows
    if ($null -eq $onWindows) { $onWindows = $true }  # Windows PowerShell 5.1 is Windows-only

    $archRaw = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
    $arch = switch ($archRaw) { 'x64' { 'amd64' } 'arm64' { 'arm64' } default { 'amd64' } }

    if ($onWindows)      { $os = 'windows'; $assetExt = 'zip';    $binName = 'actionlint.exe' }
    elseif ($IsMacOS)    { $os = 'darwin';  $assetExt = 'tar.gz'; $binName = 'actionlint' }
    else                 { $os = 'linux';   $assetExt = 'tar.gz'; $binName = 'actionlint' }

    $cacheDir = Join-Path ([System.IO.Path]::GetTempPath()) "pagonia-actionlint/$Version-$os-$arch"
    $binPath  = Join-Path $cacheDir $binName
    if (Test-Path $binPath) { return $binPath }

    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
    $asset = "actionlint_${Version}_${os}_${arch}.$assetExt"
    $url   = "https://github.com/rhysd/actionlint/releases/download/v$Version/$asset"
    $dl    = Join-Path $cacheDir $asset

    Write-Host "Fetching actionlint v$Version ($os/$arch)..." -ForegroundColor DarkGray
    Invoke-WebRequest $url -OutFile $dl -UseBasicParsing
    if ($assetExt -eq 'zip') {
        Expand-Archive -Path $dl -DestinationPath $cacheDir -Force
    } else {
        tar -xzf $dl -C $cacheDir
        if ($LASTEXITCODE -ne 0) { throw "tar failed to extract $dl" }
    }
    if (-not (Test-Path $binPath)) { throw "actionlint binary not found after extracting $asset" }
    if (-not $onWindows) { chmod +x $binPath 2>$null }
    return $binPath
}

try {
    $exe = Resolve-Actionlint -Version $Version
}
catch {
    Write-Host "WARN: could not obtain actionlint ($($_.Exception.Message))." -ForegroundColor Yellow
    Write-Host "      Skipping workflow lint locally — the lint-workflows.yml CI job still covers it." -ForegroundColor Yellow
    if ($Strict) { exit 1 }
    exit 0
}

Push-Location $repoRoot
try {
    & $exe -color
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($code -eq 0) {
    Write-Host "check-workflows: OK — all workflows lint clean." -ForegroundColor Green
} else {
    Write-Host "check-workflows: actionlint reported issues (exit $code)." -ForegroundColor Red
}
exit $code
