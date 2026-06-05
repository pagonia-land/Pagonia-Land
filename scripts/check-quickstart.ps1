#requires -Version 5.1
<#
.SYNOPSIS
    Verifies the documented quickstart still works for a fresh-clone user.

.DESCRIPTION
    Distinct from scripts/preflight.ps1 (the contributor preflight, which
    builds + tests + schema-validates + runs the manager walkthrough).

    This script is the user-facing preflight: would a new modder following
    docs/setup.md hit a broken link, a missing script, or a wrong path?

    Stages:
      1. Link check  — every relative file/path link in
                       docs/setup.md resolves
      2. Script existence — every script the setup page tells the user to
                       run actually exists and parses without syntax errors
      3. Tool surface — pagonia-paker CLI doc + manager CLI doc exist and
                        are non-empty
      4. Live run (optional, only if game-paks/ has .pak files) — actually
         run the extract → validate → analyze → generate_catalog chain
         and verify expected outputs appear

    The script is read-only against tracked files; it only writes to
    .gitignored output paths under generated/ and game-gdb/ if you have
    paks available.

.PARAMETER SkipLiveRun
    Skip stage 4 even if game-paks/ has paks. Useful in CI or when you
    just want the static check.

.EXAMPLE
    .\scripts\check-quickstart.ps1

.EXAMPLE
    .\scripts\check-quickstart.ps1 -SkipLiveRun
#>

[CmdletBinding()]
param(
    [switch]$SkipLiveRun
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$fail = 0
$pass = 0
$skip = 0

function Pass($msg) { Write-Host "  PASS $msg" -ForegroundColor Green; $script:pass++ }
function Fail($msg) { Write-Host "  FAIL $msg" -ForegroundColor Red;   $script:fail++ }
function Skip($msg) { Write-Host "  SKIP $msg" -ForegroundColor Yellow; $script:skip++ }
function Header($msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }

# ---------------------------------------------------------------------
# Stage 1: Link check
# ---------------------------------------------------------------------

Header 'Stage 1: Links in docs/setup.md'

$quickstart = Join-Path $repoRoot 'docs/setup.md'
if (-not (Test-Path -LiteralPath $quickstart)) {
    Fail "Quickstart file missing: $quickstart"
} else {
    $text = Get-Content -LiteralPath $quickstart -Raw

    # Match every markdown link target — ](path)
    # Skip http(s):// and # anchors only.
    $linkPattern = '\]\(([^)]+)\)'
    $linkMatches = [regex]::Matches($text, $linkPattern)

    foreach ($m in $linkMatches) {
        $target = $m.Groups[1].Value
        # Strip anchor, e.g. ../setup.md#section -> ../setup.md
        $pathOnly = ($target -split '#')[0]
        if (-not $pathOnly) { continue }   # pure anchor
        if ($pathOnly -match '^(https?:|mailto:)') { continue }

        # Resolve relative to the quickstart's location.
        $resolved = Join-Path (Split-Path -Parent $quickstart) $pathOnly
        try {
            $abs = Resolve-Path -LiteralPath $resolved -ErrorAction Stop
            Pass "link -> $target"
        } catch {
            Fail "broken link -> $target  (looked for: $resolved)"
        }
    }
}

# ---------------------------------------------------------------------
# Stage 2: Scripts the quickstart tells the user to run
# ---------------------------------------------------------------------

Header 'Stage 2: Quickstart-referenced scripts exist and parse'

$expectedScripts = @(
    'scripts/validate_database.ps1',
    'scripts/analyze_database.ps1',
    'scripts/generate_catalog.ps1'
)

foreach ($s in $expectedScripts) {
    $p = Join-Path $repoRoot $s
    if (-not (Test-Path -LiteralPath $p)) {
        Fail "missing script: $s"
        continue
    }
    # Parse PowerShell to catch syntax errors without running it.
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors -and $errors.Count -gt 0) {
        Fail "$s parses with $($errors.Count) error(s): $($errors[0].Message)"
    } else {
        Pass "$s exists and parses"
    }
}

# ---------------------------------------------------------------------
# Stage 3: Tool surface
# ---------------------------------------------------------------------

Header 'Stage 3: Tool CLI docs exist'

$expectedDocs = @(
    'tools/pagonia-paker/CLI.md',
    'tools/pagonia-patcher/CLI.md',
    'tools/pagonia-manager/CLI.md'
)

foreach ($d in $expectedDocs) {
    $p = Join-Path $repoRoot $d
    if (-not (Test-Path -LiteralPath $p)) {
        Fail "missing CLI doc: $d"
    } elseif ((Get-Item -LiteralPath $p).Length -lt 200) {
        Fail "$d is suspiciously small (< 200 bytes)"
    } else {
        Pass "$d exists and looks substantive"
    }
}

# ---------------------------------------------------------------------
# Stage 4: Live run (optional)
# ---------------------------------------------------------------------

Header 'Stage 4: Live run against game-paks/ (optional)'

$paksDir = Join-Path $repoRoot 'game-paks'
$paks = if (Test-Path -LiteralPath $paksDir) {
    @(Get-ChildItem -LiteralPath $paksDir -Filter *.pak -File -ErrorAction SilentlyContinue)
} else {
    @()
}

if ($SkipLiveRun) {
    Skip 'Live run skipped by -SkipLiveRun flag'
} elseif ($paks.Count -eq 0) {
    Skip "Live run skipped: no .pak files in game-paks/ (this is normal for fresh clones; drop your install paks there to enable)"
} else {
    Write-Host "  Found $($paks.Count) pak(s) — running the quickstart chain"

    # Step: extract
    try {
        & (Join-Path $repoRoot 'scripts/extract-xmls-from-paks.ps1') -Clean | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "extract exited $LASTEXITCODE" }
        $extractedXmls = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'game-gdb') -Recurse -Filter *.gd.xml -File -ErrorAction SilentlyContinue)
        if ($extractedXmls.Count -lt 1) { throw 'no .gd.xml files appeared under game-gdb/' }
        Pass "extract: $($extractedXmls.Count) .gd.xml files produced"
    } catch {
        Fail "extract: $_"
    }

    # Step: validate
    try {
        & (Join-Path $repoRoot 'scripts/validate_database.ps1') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "validate exited $LASTEXITCODE" }
        Pass 'validate: exit 0'
    } catch {
        Fail "validate: $_"
    }

    # Step: analyze
    try {
        & (Join-Path $repoRoot 'scripts/analyze_database.ps1') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "analyze exited $LASTEXITCODE" }
        $entitiesJson = Join-Path $repoRoot 'generated/entities.json'
        if (-not (Test-Path -LiteralPath $entitiesJson)) { throw 'generated/entities.json did not appear' }
        Pass 'analyze: generated/entities.json produced'
    } catch {
        Fail "analyze: $_"
    }

    # Step: generate_catalog
    try {
        & (Join-Path $repoRoot 'scripts/generate_catalog.ps1') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "generate_catalog exited $LASTEXITCODE" }
        $catalogReadme = Join-Path $repoRoot 'generated/catalog/README.md'
        if (-not (Test-Path -LiteralPath $catalogReadme)) { throw 'generated/catalog/README.md did not appear' }
        Pass 'generate_catalog: catalog README produced'
    } catch {
        Fail "generate_catalog: $_"
    }
}

# ---------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------

Write-Host ""
Write-Host "Summary:" -ForegroundColor White
Write-Host "  Pass:    $pass" -ForegroundColor Green
Write-Host "  Fail:    $fail" -ForegroundColor $(if ($fail -gt 0) { 'Red' } else { 'Gray' })
Write-Host "  Skipped: $skip" -ForegroundColor Yellow

if ($fail -gt 0) {
    Write-Host ""
    Write-Host "Quickstart check FAILED — a fresh-clone user would hit at least one of the issues above." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Quickstart check OK." -ForegroundColor Green
exit 0
