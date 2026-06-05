#requires -Version 5.1
<#
.SYNOPSIS
    Run the docs prebuild + build the static site into ./site/.

.DESCRIPTION
    One-command wrapper for producing the deployable site artifact locally.
    Mirrors docs/ + root + tools + schemas READMEs into docs_build/, then
    runs mkdocs build.

    Output: ./site/ (gitignored).

.EXAMPLE
    pwsh scripts/docs-build.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

Write-Host '==> Prebuild (docs_build/ mirror)' -ForegroundColor Cyan
python scripts/docs-prebuild.py
if ($LASTEXITCODE -ne 0) { throw "prebuild failed (exit $LASTEXITCODE)" }

Write-Host '==> mkdocs build' -ForegroundColor Cyan
mkdocs build
if ($LASTEXITCODE -ne 0) { throw "mkdocs build failed (exit $LASTEXITCODE)" }

# Mirror of the combine step in .github/workflows/pages.yml — keep the two in
# sync (both copy tools/catalog-browser/ to site/catalog/ at the /catalog/ subpath).
Write-Host '==> Combine catalog-browser into site/catalog/' -ForegroundColor Cyan
$catalogDst = Join-Path $repoRoot 'site/catalog'
if (Test-Path -LiteralPath $catalogDst) {
    Remove-Item -LiteralPath $catalogDst -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'tools/catalog-browser') -Destination $catalogDst -Recurse

Write-Host ''
Write-Host "Site built. Docs at /, catalog browser at /catalog/." -ForegroundColor Green
Write-Host "Output: $(Join-Path $repoRoot 'site')"
