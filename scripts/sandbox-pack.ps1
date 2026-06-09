# One-command wrapper around `pagonia-paker pack` (or `patch`) for the
# sandbox/out/ workspace.
#
# Default mode: pack
#   Walks every file under sandbox/out/ (excluding the apply.md / apply.json
#   reports, the .entries-deleted.txt deletion list, and any previously-built
#   .pak files), generates a pakinfo.json inside sandbox/out/, and runs
#   `pagonia-paker pack` against it. Entries whose extension is
#   .xml / .yaml / .yml / .txt / .json are flagged compressed=true; the rest
#   are stored verbatim (see Get-Compressed).
#
# Patch mode (-BasePak <pak>):
#   Runs `pagonia-paker patch` against the given base pak. Every file under
#   sandbox/out/ is passed as a positional path; the paker classifies it as
#   Replace (if the base pak has the entry name) or Add (if not).
#   sandbox/out/.entries-deleted.txt -- written by `pagonia-patcher apply`
#   when the mod manifest's entries.delete list fires -- becomes one
#   `--delete <path>` flag per line. This mode is the right one for producing
#   a real mod .pak the game can load against an installed Pioneers of
#   Pagonia pak.
#
# Examples:
#   .\scripts\sandbox-pack.ps1
#   .\scripts\sandbox-pack.ps1 -Output .\my-mod.pak
#   .\scripts\sandbox-pack.ps1 -BasePak .\game-paks\core.pak -Output .\my-mod.pak

[CmdletBinding()]
param(
    [string]$Source = "sandbox/out",
    [string]$Output = "sandbox/out/sandbox.pak",
    [string]$BasePak,
    [int]$Jobs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-MaybeRelative([string]$path, [string]$root) {
    if ([System.IO.Path]::IsPathRooted($path)) {
        return $path
    }
    return Join-Path $root $path
}

$sourceCandidate = Resolve-MaybeRelative $Source $repoRoot
$sourceFull = (Resolve-Path -LiteralPath $sourceCandidate -ErrorAction SilentlyContinue).Path
if (-not $sourceFull) {
    $sourceFull = $sourceCandidate
}
$outputFull = Resolve-MaybeRelative $Output $repoRoot
$cliProject = Join-Path $repoRoot "tools/pagonia-paker/src/PagoniaLand.Paker.Cli"

if (-not (Test-Path -LiteralPath $sourceFull -PathType Container)) {
    throw "Source directory not found: $sourceFull. Run scripts/sandbox-apply.ps1 first."
}

# Files we never want in the pak: the patcher's apply reports, the deletion
# sidecar, and any previously-built pak (which would otherwise self-include).
$excludeNames = @("apply.md", "apply.json", "pakinfo.json", ".entries-deleted.txt")
$entries = @(
    Get-ChildItem -LiteralPath $sourceFull -Recurse -File |
        Where-Object {
            ($excludeNames -notcontains $_.Name) -and
            ($_.Extension -ne ".pak") -and
            ($_.FullName -ne $outputFull)
        }
)

function Get-EntryName([System.IO.FileInfo]$file, [string]$root) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\', '/')
    return $relative.Replace('\', '/')
}

function Get-Compressed([System.IO.FileInfo]$file) {
    # Match the convention shipped paks use: gzip-pack text-shaped game files
    # plus the per-module JSON metadata (manifest.json / files.json). Shipped
    # paks (verified against core.pak, dlc1.pak, system.pak from mod.io) store
    # both XML and these small JSONs compressed; binary blobs like .image,
    # .audio, .gd.bin, and memory.bin stay verbatim.
    $compressFor = @(".xml", ".yaml", ".yml", ".txt", ".json")
    return $compressFor -contains $file.Extension.ToLowerInvariant()
}

function Read-Deletions([string]$root) {
    $deletionsFile = Join-Path $root ".entries-deleted.txt"
    if (-not (Test-Path -LiteralPath $deletionsFile)) {
        return @()
    }
    return @(
        Get-Content -LiteralPath $deletionsFile |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and -not $_.StartsWith("#") }
    )
}

$invokeArgs = @()
if ($PSBoundParameters.ContainsKey("Jobs")) {
    $invokeArgs += @("-j", "$Jobs")
}

if ($BasePak) {
    # --- patch mode ---
    if (-not (Test-Path -LiteralPath $BasePak)) {
        throw "Base pak not found: $BasePak"
    }
    $baseFull = (Resolve-Path -LiteralPath $BasePak).Path

    $candidates = @($entries | ForEach-Object { Get-EntryName $_ $sourceFull })
    $deletions = Read-Deletions $sourceFull

    if ($candidates.Count -eq 0 -and $deletions.Count -eq 0) {
        throw "Nothing to patch: no files under '$sourceFull' and no .entries-deleted.txt. Did sandbox-apply.ps1 write anything?"
    }

    Write-Host ("Patching '" + $baseFull + "' with " + $candidates.Count + " replacement/add file(s) and " + $deletions.Count + " deletion(s) -> '" + $outputFull + "'")

    # The paker resolves replacement paths from the current working
    # directory, so chdir into sandbox/out/ for the invocation.
    Push-Location $sourceFull
    try {
        $cmdArgs = @("patch") + $invokeArgs
        foreach ($d in $deletions) { $cmdArgs += @("--delete", $d) }
        $cmdArgs += @($baseFull, $outputFull) + $candidates
        dotnet run --project $cliProject -- @cmdArgs
        if ($LASTEXITCODE -ne 0) { throw "pagonia-paker patch failed (exit $LASTEXITCODE)" }
    }
    finally {
        Pop-Location
    }
}
else {
    # --- pack mode ---
    if ($entries.Count -eq 0) {
        throw "No files found under '$sourceFull' to pack. Did sandbox-apply.ps1 write anything?"
    }

    # Build a pakinfo.json describing every selected file. begin/end/size_compressed
    # are recomputed by the packer, so we leave them at zero. `size` carries the
    # uncompressed payload size (= file length on disk).
    $entryRows = @()
    for ($i = 0; $i -lt $entries.Count; $i++) {
        $file = $entries[$i]
        $entryRows += [pscustomobject]@{
            index           = $i
            pos             = $i
            compressed      = Get-Compressed $file
            filename        = Get-EntryName $file $sourceFull
            begin           = 0
            end             = 0
            size            = $file.Length
            size_compressed = 0
        }
    }

    $pakInfo = [pscustomobject]@{
        # Index version 2 matches the shipped game paks (core.pak etc.) and the
        # manager's own overlay-pak builder (PagoniaLand.Manager.Core PakBuilder,
        # which also writes version 2). The paker test fixtures use version 1 as an
        # arbitrary round-trip value; that is not the on-disk game convention.
        version = 2
        count   = $entryRows.Count
        entries = $entryRows
    }

    $pakInfoPath = Join-Path $sourceFull "pakinfo.json"
    $pakInfoJson = $pakInfo | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $pakInfoPath -Value $pakInfoJson -Encoding UTF8

    try {
        $cmdArgs = @("pack") + $invokeArgs + @($pakInfoPath, $outputFull)
        Write-Host "Packing $($entries.Count) file(s) from '$sourceFull' -> '$outputFull'"
        dotnet run --project $cliProject -- @cmdArgs
        if ($LASTEXITCODE -ne 0) { throw "pagonia-paker pack failed (exit $LASTEXITCODE)" }
    }
    finally {
        # The intermediate pakinfo.json was just a paker input; the canonical
        # one is the report you get from `pagonia-paker list` against the
        # produced .pak.
        Remove-Item -LiteralPath $pakInfoPath -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Output: $outputFull"
$bytes = (Get-Item -LiteralPath $outputFull).Length
Write-Host ("Size:   {0:N0} bytes" -f $bytes)
