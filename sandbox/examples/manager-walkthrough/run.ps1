#requires -Version 5.1
<#
.SYNOPSIS
    End-to-end walkthrough that drives every pagonia-manager command against
    a fixture game tree. Doubles as the manager's preflight harness.

.PARAMETER ManagerExe
    Path to a published pagonia-manager.exe to test instead of the framework
    build. Without this, the script invokes `dotnet run --project ...` against
    the in-repo Cli project.

.PARAMETER Keep
    Don't delete the out/ directory at the end of the run. Useful for
    inspecting the post-run state.

.EXAMPLE
    .\sandbox\examples\manager-walkthrough\run.ps1
    .\sandbox\examples\manager-walkthrough\run.ps1 -Keep
    .\sandbox\examples\manager-walkthrough\run.ps1 -ManagerExe .\path\to\pagonia-manager.exe
#>

[CmdletBinding()]
param(
    [string]$ManagerExe,
    [switch]$Keep
)

$ErrorActionPreference = 'Stop'
$walkRoot = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $walkRoot '..\..\..\')

$outDir = Join-Path $walkRoot 'out'
$storeDir = Join-Path $outDir 'store'
$gameDir = Join-Path $outDir 'game-gdb'
$reportsDir = Join-Path $outDir 'reports'

# live-install fixture: a parallel "game install" tree with a
# pak/core.pak file, used by the live-install round-trip stages below. Its
# own store directory keeps the cache + deploy state isolated from the
# extracted-layout run above (different game fingerprint anyway).
$liveStoreDir = Join-Path $outDir 'store-live'
$liveGameDir = Join-Path $outDir 'game-install'
$liveReportsDir = Join-Path $outDir 'reports-live'

# Always start clean. -Keep only affects the cleanup at the END.
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Path $outDir, $reportsDir | Out-Null

# Stage a working copy of the fixture game-gdb the deploy will write into.
Copy-Item -Recurse (Join-Path $walkRoot 'game-gdb') $gameDir

# Resolve the manager invocation: published exe vs in-repo build.
# PowerShell-on-Windows mangles `dotnet run -- <args>` (the `--` separator is
# eaten before the program sees it), so we build the Cli once up front and
# invoke the produced DLL directly via `dotnet <dll> <args>` from then on.
# Always build the paker CLI — it's the fixture builder for the live-install
# round-trip stages further down (turns the existing game-gdb XML into a
# core.pak that mirrors what shipped paks look like). Cheap dotnet incremental
# build in both modes; we don't ship it as part of -ManagerExe coverage but we
# do need it to construct the fixture deterministically.
$pakerCliProject = Join-Path $repoRoot 'tools\pagonia-paker\src\PagoniaLand.Paker.Cli'
$pakerCliDll = Join-Path $pakerCliProject 'bin\Debug\net10.0\pagonia-paker.dll'
Write-Host '==> (0) build paker CLI (fixture builder)' -ForegroundColor Cyan
dotnet build $pakerCliProject -c Debug --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: paker build (exit $LASTEXITCODE)" -ForegroundColor Red
    exit $LASTEXITCODE
}
function Invoke-Paker {
    param([string[]]$CmdArgs)
    dotnet $pakerCliDll @CmdArgs
}

$useExe = -not [string]::IsNullOrWhiteSpace($ManagerExe)
if ($useExe) {
    $resolvedExe = Resolve-Path $ManagerExe
    function Invoke-Manager {
        param([string[]]$CmdArgs)
        & $resolvedExe @CmdArgs
    }
} else {
    $cliProject = Join-Path $repoRoot 'tools\pagonia-manager\src\PagoniaLand.Manager.Cli'
    $cliDll = Join-Path $cliProject 'bin\Debug\net10.0\pagonia-manager.dll'
    Write-Host '==> (0) build manager CLI' -ForegroundColor Cyan
    dotnet build $cliProject -c Debug --nologo -v quiet | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: build (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    function Invoke-Manager {
        param([string[]]$CmdArgs)
        dotnet $cliDll @CmdArgs
    }
}

$stageNumber = 0
function Stage {
    param([string]$Name, [scriptblock]$Block)
    $script:stageNumber++
    Write-Host "==> ($script:stageNumber) $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: stage $script:stageNumber '$Name' (exit $LASTEXITCODE)" -ForegroundColor Red
        if (-not $Keep) { Remove-Item -Recurse -Force $outDir }
        exit $LASTEXITCODE
    }
}

# SHA-256 over the file tree at $root, files sorted by their forward-slash
# relative path, separator bytes between fields. Same shape the manager's own
# Deploy/Rollback round-trip test uses, so the two answers stay comparable.
function Get-TreeHash {
    param([string]$Root)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = New-Object System.IO.MemoryStream
        $files = Get-ChildItem -Recurse -File -LiteralPath $Root | Sort-Object FullName
        foreach ($file in $files) {
            $rel = ($file.FullName.Substring($Root.Length).TrimStart('\','/')).Replace('\','/')
            $relBytes = [System.Text.Encoding]::UTF8.GetBytes($rel)
            $bytes.Write($relBytes, 0, $relBytes.Length)
            $bytes.WriteByte(0)
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $bytes.Write($fileBytes, 0, $fileBytes.Length)
            $bytes.WriteByte(0)
        }
        $bytes.Position = 0
        ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    } finally {
        $sha.Dispose()
    }
}

$preDeployHash = Get-TreeHash $gameDir

# --- Stages ------------------------------------------------------------------

Stage 'store init' {
    Invoke-Manager @('store', 'init', '--store', $storeDir)
}

Stage 'install cheaper-sawmill (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'install-sawmill.json'
    Invoke-Manager @('install', '--from', (Join-Path $walkRoot 'mods\cheaper-sawmill'), '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'install', '--report', $jsonOut)
}

Stage 'install cheaper-quarry (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'install-quarry.json'
    Invoke-Manager @('install', '--from', (Join-Path $walkRoot 'mods\cheaper-quarry'), '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'install', '--report', $jsonOut)
}

Stage 'enable both + move quarry before sawmill' {
    Invoke-Manager @('enable', 'pagonia-land.walkthrough.cheaper-sawmill', '--store', $storeDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('enable', 'pagonia-land.walkthrough.cheaper-quarry', '--store', $storeDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('move', 'pagonia-land.walkthrough.cheaper-quarry',
        '--before', 'pagonia-land.walkthrough.cheaper-sawmill', '--store', $storeDir)
}

Stage 'status (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'status.json'
    Invoke-Manager @('status', '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'status', '--report', $jsonOut)
}

Stage 'plan' {
    Invoke-Manager @('plan', '--game', $gameDir, '--store', $storeDir)
}

Stage 'deploy --dry-run (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'deploy-dry-run.json'
    Invoke-Manager @('deploy', '--game', $gameDir, '--store', $storeDir, '--dry-run', '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'deploy', '--report', $jsonOut)
}

Stage 'deploy (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'deploy.json'
    Invoke-Manager @('deploy', '--game', $gameDir, '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'deploy', '--report', $jsonOut)
}

Stage 'verify deployed buildings.gd.xml' {
    $deployed = Join-Path $gameDir 'core\gdb\buildings.gd.xml'
    $content = [System.IO.File]::ReadAllText($deployed)
    if ($content -notmatch '<Amount>3</Amount>') {
        Write-Host "expected Sawmill cost to be 3 after deploy" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    if ($content -notmatch '<Amount>5</Amount>') {
        Write-Host "expected Quarry cost to be 5 after deploy" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    Write-Host "  Sawmill: 4 -> 3, Quarry: 6 -> 5"
}

Stage 'deploy-status (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'deploy-status.json'
    Invoke-Manager @('deploy-status', '--game', $gameDir, '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'deployStatus', '--report', $jsonOut)
}

Stage 'rollback (+ schema-validate)' {
    $jsonOut = Join-Path $reportsDir 'rollback.json'
    Invoke-Manager @('rollback', '--game', $gameDir, '--store', $storeDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'rollback', '--report', $jsonOut)
}

Stage 'verify SHA-256 round trip' {
    $postRollbackHash = Get-TreeHash $gameDir
    if ($postRollbackHash -ne $preDeployHash) {
        Write-Host "tree hash differs after rollback:" -ForegroundColor Red
        Write-Host "  pre-deploy:    $preDeployHash" -ForegroundColor Red
        Write-Host "  post-rollback: $postRollbackHash" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    Write-Host "  tree hash matches: $preDeployHash"
}

Stage 'disable + uninstall cleanup' {
    Invoke-Manager @('disable', 'pagonia-land.walkthrough.cheaper-sawmill', '--store', $storeDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('disable', 'pagonia-land.walkthrough.cheaper-quarry', '--store', $storeDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('uninstall', 'pagonia-land.walkthrough.cheaper-sawmill', '--store', $storeDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('uninstall', 'pagonia-land.walkthrough.cheaper-quarry', '--store', $storeDir)
}

# --- Live-install round-trip -------------------------------------
#
# Parallel stages that exercise the live-install deploy path: a fresh
# game-install/pak/core.pak built from the same XML the extracted-layout
# stages above use, against a fresh store so cache + history don't collide.
# Proves the AOT-published manager binary handles the live-install pipeline
# end-to-end: GameLayoutDetector -> PakCacheService.Ensure -> PakRebuilder
# -> RollbackService restores byte-identical from backup.

Stage 'live-install: build core.pak fixture from buildings.gd.xml' {
    # The buildings.gd.xml the live install will hold is the same one the
    # extracted-layout stages used (and just restored after rollback), so we
    # know its content matches what the fixture mod targets.
    $sourceXml = Join-Path $walkRoot 'game-gdb\core\gdb\buildings.gd.xml'
    $xmlSize = (Get-Item -LiteralPath $sourceXml).Length

    # paker pack reads source files relative to the pakinfo's directory, so
    # set up a staging tree where the entry's filename ("core/gdb/buildings.gd.xml")
    # resolves to the actual XML. Begin/End in pakinfo are recomputed by pack;
    # only Compressed + Filename + Size + SizeCompressed need to be correct.
    $stagingDir = Join-Path $outDir 'live-pak-staging'
    $stagingGdb = Join-Path $stagingDir 'core\gdb'
    New-Item -ItemType Directory -Path $stagingGdb -Force | Out-Null
    Copy-Item $sourceXml (Join-Path $stagingGdb 'buildings.gd.xml')

    $pakinfo = @{
        version = 1
        count = 1
        entries = @(
            @{
                index = 0
                pos = 0
                compressed = $false
                filename = 'core/gdb/buildings.gd.xml'
                begin = 0
                end = 0
                size = $xmlSize
                size_compressed = $xmlSize
            }
        )
    }
    $pakinfoPath = Join-Path $stagingDir 'pakinfo.json'
    $pakinfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $pakinfoPath -Encoding utf8

    # Build the live install layout: <gameDir>/pak/core.pak
    New-Item -ItemType Directory -Path (Join-Path $liveGameDir 'pak') -Force | Out-Null
    $outPak = Join-Path $liveGameDir 'pak\core.pak'
    Invoke-Paker @('pack', $pakinfoPath, $outPak)
    if ($LASTEXITCODE -ne 0) { return }

    if (-not (Test-Path -LiteralPath $outPak)) {
        Write-Host "expected $outPak to exist after paker pack" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    Write-Host "  core.pak: $((Get-Item -LiteralPath $outPak).Length) bytes"
}

# Snapshot the live install BEFORE any deploy runs, so the round-trip stage
# at the end can verify byte-identical restoration. core.pak is the only
# moving part; the SHA over its bytes is the smoking-gun check.
$preLiveDeployHash = Get-FileHash -LiteralPath (Join-Path $liveGameDir 'pak\core.pak') -Algorithm SHA256

Stage 'live-install: store init + install cheaper-sawmill + enable' {
    Invoke-Manager @('store', 'init', '--store', $liveStoreDir)
    if ($LASTEXITCODE -ne 0) { return }
    New-Item -ItemType Directory -Path $liveReportsDir -Force | Out-Null
    Invoke-Manager @('install', '--from', (Join-Path $walkRoot 'mods\cheaper-sawmill'), '--store', $liveStoreDir)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('enable', 'pagonia-land.walkthrough.cheaper-sawmill', '--store', $liveStoreDir)
}

Stage 'live-install: deploy (+ schema-validate)' {
    $jsonOut = Join-Path $liveReportsDir 'deploy-live.json'
    Invoke-Manager @('deploy', '--game', $liveGameDir, '--store', $liveStoreDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'deploy', '--report', $jsonOut)
}

Stage 'live-install: verify core.pak was rebuilt (bytes differ from pre-deploy)' {
    $afterDeployHash = Get-FileHash -LiteralPath (Join-Path $liveGameDir 'pak\core.pak') -Algorithm SHA256
    if ($afterDeployHash.Hash -eq $preLiveDeployHash.Hash) {
        Write-Host "expected core.pak SHA to change after deploy:" -ForegroundColor Red
        Write-Host "  pre-deploy:    $($preLiveDeployHash.Hash)" -ForegroundColor Red
        Write-Host "  post-deploy:   $($afterDeployHash.Hash)" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    Write-Host "  core.pak changed: $($preLiveDeployHash.Hash) -> $($afterDeployHash.Hash)"
}

Stage 'live-install: rollback (+ schema-validate)' {
    $jsonOut = Join-Path $liveReportsDir 'rollback-live.json'
    Invoke-Manager @('rollback', '--game', $liveGameDir, '--store', $liveStoreDir, '--json', $jsonOut)
    if ($LASTEXITCODE -ne 0) { return }
    Invoke-Manager @('schema-validate', '--kind', 'rollback', '--report', $jsonOut)
}

Stage 'live-install: verify SHA-256 round trip on core.pak' {
    $postRollbackHash = Get-FileHash -LiteralPath (Join-Path $liveGameDir 'pak\core.pak') -Algorithm SHA256
    if ($postRollbackHash.Hash -ne $preLiveDeployHash.Hash) {
        Write-Host "core.pak SHA differs after rollback:" -ForegroundColor Red
        Write-Host "  pre-deploy:    $($preLiveDeployHash.Hash)" -ForegroundColor Red
        Write-Host "  post-rollback: $($postRollbackHash.Hash)" -ForegroundColor Red
        $script:LASTEXITCODE = 1
        return
    }
    Write-Host "  core.pak restored byte-identical: $($postRollbackHash.Hash)"
}

Write-Host ''
Write-Host "walkthrough: all $stageNumber stages passed." -ForegroundColor Green

if (-not $Keep) {
    Remove-Item -Recurse -Force $outDir
}
