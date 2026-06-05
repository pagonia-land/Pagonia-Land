#requires -Version 5.1
<#
.SYNOPSIS
    Run the docs prebuild + start `mkdocs serve` for local preview.

.DESCRIPTION
    One-command wrapper for "see the docs site locally". Runs the prebuild
    script that mirrors docs/ + root-level + tools + schemas READMEs into
    docs_build/, then starts mkdocs serve.

    Default URL: http://127.0.0.1:8000

.EXAMPLE
    pwsh scripts/docs-serve.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

Write-Host '==> Prebuild (docs_build/ mirror)' -ForegroundColor Cyan
python scripts/docs-prebuild.py
if ($LASTEXITCODE -ne 0) { throw "prebuild failed (exit $LASTEXITCODE)" }

Write-Host '==> mkdocs serve' -ForegroundColor Cyan
mkdocs serve
