#requires -Version 7.0  # invokes pwsh for sub-scripts and the walkthrough
<#
.SYNOPSIS
    Runs every check a contributor should pass before opening a pull request
    that touches code, tools, schemas, or sandbox examples.

.DESCRIPTION
    One command for "did I break anything?". Mirrors what the GitHub Actions
    workflow under .github/workflows/tools.yml runs on every push and PR, so
    a local pass means CI will pass too.

    Stages:
      0. Doc currency: every "current snapshot" doc agrees on the game
         version + headline counts (scripts/check-doc-currency.ps1). Pure
         text, no build — catches a refresh that left some docs stale.
      0b. Workflow lint: actionlint over .github/workflows/
         (scripts/check-workflows.ps1). Mirrors the lint-workflows.yml CI job.
         Skips gracefully (non-fatal) if actionlint can't be fetched offline.
      1. Build all three .NET solutions (patcher + paker + manager).
      2. Run the patcher test suite.
      3. Run the paker test suite.
      4. Run the manager test suite.
      5. Run schema-validate against every mod under sandbox/examples/.
         (No game-gdb needed — sandbox examples are self-contained.)
      6. Run schema-validate against the bundled collection example.
      7. Run the manager end-to-end walkthrough
         (sandbox/examples/manager-walkthrough/run.ps1) which drives every
         pagonia-manager command against a fixture game tree and verifies
         the SHA-256 round trip after deploy + rollback.

    If any stage fails, the script stops and reports the failing stage.

.PARAMETER SkipBuild
    Skip the build step. Useful when iterating on tests and you've already
    built locally.

.PARAMETER SkipSchemaValidate
    Skip the schema-validate stage. Useful when iterating on tests and you
    know your changes don't touch schemas or sandbox examples.

.EXAMPLE
    .\scripts\preflight.ps1

.EXAMPLE
    .\scripts\preflight.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipSchemaValidate
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$patcherSln = 'tools/pagonia-patcher/PagoniaLand.Patcher.slnx'
$pakerSln   = 'tools/pagonia-paker/PagoniaLand.Paker.slnx'
$managerSln = 'tools/pagonia-manager/PagoniaLand.Manager.slnx'
$patcherCli = 'tools/pagonia-patcher/src/PagoniaLand.Patcher.Cli'
$patcherTests = 'tools/pagonia-patcher/tests/PagoniaLand.Patcher.Tests'
$pakerTests   = 'tools/pagonia-paker/tests/PagoniaLand.Paker.Tests'
$managerTests = 'tools/pagonia-manager/tests/PagoniaLand.Manager.Tests'
$managerWalk  = 'sandbox/examples/manager-walkthrough/run.ps1'

function Step($name, $action) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAIL: $name (exit $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Doc currency first — pure text, no build, fails fast. Catches a docs refresh
# that left the current game version or headline counts stale in some files.
Step 'Doc currency (version + headline counts)' {
    pwsh -NoProfile -File (Join-Path $PSScriptRoot 'check-doc-currency.ps1')
}

# Workflow lint — actionlint over .github/workflows/. Pure, no build. Skips
# gracefully (warning, non-fatal) when actionlint can't be fetched offline; the
# lint-workflows.yml CI job is the backstop.
Step 'Workflow lint (actionlint)' {
    pwsh -NoProfile -File (Join-Path $PSScriptRoot 'check-workflows.ps1')
}

# Step-N policy: internal roadmap step references ("Step 56", "Step-32") must
# never appear in shipped source. Gate the C# tree (comments + identifiers);
# tutorial "Step 1/2/3" headings in docs/examples are legitimate and not checked.
Step 'Step-N policy (no roadmap step refs in C#)' {
    $hits = Get-ChildItem -Path 'tools' -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Select-String -Pattern 'Step[- ]?\d+' -CaseSensitive
    if ($hits) {
        Write-Host 'Found roadmap "Step N" references in C# source (remove them — they belong in internal planning notes only):' -ForegroundColor Red
        $hits | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber): $($_.Line.Trim())" -ForegroundColor Red }
        $global:LASTEXITCODE = 1
    }
    else {
        Write-Host 'Step-N policy: clean.' -ForegroundColor Green
        $global:LASTEXITCODE = 0
    }
}

if (-not $SkipBuild) {
    Step 'Build patcher solution' { dotnet build $patcherSln --nologo -v minimal }
    Step 'Build paker solution'   { dotnet build $pakerSln   --nologo -v minimal }
    Step 'Build manager solution' { dotnet build $managerSln --nologo -v minimal }
}

Step 'Patcher tests' { dotnet run --project $patcherTests --no-build:$(-not $SkipBuild) -c Debug }
Step 'Paker tests'   { dotnet run --project $pakerTests   --no-build:$(-not $SkipBuild) -c Debug }
Step 'Manager tests' { dotnet run --project $managerTests --no-build:$(-not $SkipBuild) -c Debug }

if (-not $SkipSchemaValidate) {
    # Every mod under sandbox/examples/ must conform to the canonical schemas
    # under schemas/mod-patches/. If a schema changes, every example that uses
    # the affected shape gets re-checked here.
    $modExamples = @(
        'sandbox/examples/lower-sawmill-cost',
        'sandbox/examples/remove-stone-cost',
        'sandbox/examples/sanctuary-tweak-abilities',
        'sandbox/examples/sanctuary-add-custom-ability',
        'sandbox/examples/add-extra-asset',
        'sandbox/examples/standalone-overlay'
    )

    foreach ($mod in $modExamples) {
        Step "schema-validate $mod" {
            dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --mod $mod
        }
    }

    # The bundled collection example also has to conform.
    Step 'schema-validate beginner-qol collection' {
        dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --collection docs/examples/collections/beginner-qol.collection.yaml
    }

    # The first-party official-mods tree is real published content (the default
    # catalog points at it), so it has to stay schema-valid too: its repo index,
    # every mod, and every collection. Patch-target resolution against game-gdb
    # is verified out of band with `pagonia-patcher plan` (game-gdb is git-ignored
    # and not available in CI), so this gate is structure-only, same as above.
    Step 'schema-validate official-mods index' {
        dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --repo-index official-mods/index.yaml
    }
    foreach ($mod in @('cheaper-sawmill', 'cheaper-quarry', 'bigger-storage')) {
        Step "schema-validate official-mods/$mod" {
            dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --mod "official-mods/mods/$mod"
        }
    }
    foreach ($coll in @('cheaper-buildings', 'starter-qol')) {
        Step "schema-validate official-mods/$coll collection" {
            dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --collection "official-mods/collections/$coll.collection.yaml"
        }
    }

    # The official catalog (the default subscription) is shipped in-repo, so it
    # has to stay schema-valid too.
    Step 'schema-validate official catalog' {
        dotnet run --project $patcherCli --no-build -c Debug -- schema-validate --catalog catalog/official.yaml
    }
}

# The manager walkthrough drives every pagonia-manager command end-to-end
# against a fixture game tree and verifies the SHA-256 round trip. It also
# schema-validates every JSON report the manager produces, so a schema/runtime
# drift fails here loudly.
Step 'Manager walkthrough (end-to-end)' {
    pwsh -NoProfile -File $managerWalk
}

Write-Host ''
Write-Host 'preflight: all checks passed.' -ForegroundColor Green
