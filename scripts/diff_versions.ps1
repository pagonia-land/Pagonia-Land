param(
  [Parameter(Mandatory = $true)]
  [string]$Old,

  [Parameter(Mandatory = $true)]
  [string]$New,

  [string]$OutputDir = "",

  # Limit on the number of added or removed references written to the diff
  # reports. References are stable identifiers, so a small change can produce a
  # large reference delta; this cap keeps the report tables readable.
  [int]$ReferenceDiffLimit = 500
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$rootPath = (Resolve-Path -LiteralPath (Get-Location).Path).Path

function Get-RelativePath([string]$BasePath, [string]$Path) {
  return Get-PathRelativeTo $Path $BasePath
}

function Resolve-Snapshot([string]$Path) {
  $resolved = (Resolve-Path -LiteralPath $Path).Path
  $generated = Join-Path $resolved "generated"
  $game = Join-Path $resolved "game-gdb"

  if (Test-Path -LiteralPath (Join-Path $resolved "entities.json")) {
    $generated = $resolved
  }

  if (-not (Test-Path -LiteralPath (Join-Path $generated "entities.json"))) {
    throw "Snapshot is missing generated/entities.json: $Path"
  }

  return [pscustomobject]@{
    Root = $resolved
    Label = Split-Path -Leaf $resolved
    Game = if (Test-Path -LiteralPath $game) { (Resolve-Path -LiteralPath $game).Path } else { "" }
    Generated = (Resolve-Path -LiteralPath $generated).Path
    Entities = Join-Path $generated "entities.json"
    References = Join-Path $generated "references.json"
    Summary = Join-Path $generated "analysis-summary.json"
  }
}

function Read-JsonFile([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) { return $null }
  return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function New-MapByProperty($Rows, [string]$Property) {
  $map = @{}
  foreach ($row in $Rows) {
    $key = [string]$row.$Property
    if ([string]::IsNullOrWhiteSpace($key)) { continue }
    if (-not $map.ContainsKey($key)) {
      $map[$key] = $row
    }
  }
  return $map
}

function Join-ValueTypes($Entity) {
  return (@($Entity.valueTypes) | Sort-Object) -join ", "
}

function Get-FileHashes([string]$GamePath) {
  $rows = New-Object System.Collections.Generic.List[object]
  if ([string]::IsNullOrWhiteSpace($GamePath)) { return @() }

  foreach ($file in Get-ChildItem -LiteralPath $GamePath -Recurse -File -Filter *.xml | Sort-Object FullName) {
    $rows.Add([pscustomobject]@{
      File = Get-RelativePath $GamePath $file.FullName
      Hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
      Length = $file.Length
    })
  }

  return @($rows | ForEach-Object { $_ })
}

function Write-MarkdownTable($Rows, [string[]]$Columns, [string]$Path, [string]$Title, [string]$Intro = "") {
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("# $Title")
  $lines.Add("")
  if (-not [string]::IsNullOrWhiteSpace($Intro)) {
    $lines.Add($Intro)
    $lines.Add("")
  }
  $lines.Add("| " + ($Columns -join " | ") + " |")
  $lines.Add("| " + (($Columns | ForEach-Object { "---" }) -join " | ") + " |")

  foreach ($row in $Rows) {
    $values = foreach ($column in $Columns) {
      ([string]$row.$column).Replace("|", "\|").Replace("`r", " ").Replace("`n", " ").Trim()
    }
    $lines.Add("| " + ($values -join " | ") + " |")
  }

  [System.IO.File]::WriteAllLines([string]$Path, [string[]]@($lines), [System.Text.UTF8Encoding]::new($false))
}

function Export-DiffTable($Rows, [string[]]$Columns, [string]$Directory, [string]$BaseName, [string]$Title, [string]$Intro = "") {
  New-Item -ItemType Directory -Force -Path $Directory | Out-Null
  $rowList = if ($null -eq $Rows) { @() } else { @($Rows | ForEach-Object { $_ }) }
  $csvPath = Join-Path $Directory "$BaseName.csv"
  if ($rowList.Count -gt 0) {
    $rowList | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
  } else {
    $header = ($Columns | ForEach-Object { '"' + $_ + '"' }) -join ","
    [System.IO.File]::WriteAllLines([string]$csvPath, [string[]]@($header), [System.Text.UTF8Encoding]::new($false))
  }
  Write-MarkdownTable $rowList $Columns (Join-Path $Directory "$BaseName.md") $Title $Intro
}

$oldSnapshot = Resolve-Snapshot $Old
$newSnapshot = Resolve-Snapshot $New

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $rootPath "generated\diffs\$($oldSnapshot.Label)_to_$($newSnapshot.Label)"
}
$outPath = Join-Path $rootPath $OutputDir
if ([System.IO.Path]::IsPathRooted($OutputDir)) {
  $outPath = $OutputDir
}
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

$oldEntitiesRaw = Read-JsonFile $oldSnapshot.Entities
$newEntitiesRaw = Read-JsonFile $newSnapshot.Entities
$oldRefsRaw = Read-JsonFile $oldSnapshot.References
$newRefsRaw = Read-JsonFile $newSnapshot.References
$oldEntities = @($oldEntitiesRaw | ForEach-Object { $_ })
$newEntities = @($newEntitiesRaw | ForEach-Object { $_ })
$oldRefs = @($oldRefsRaw | ForEach-Object { $_ })
$newRefs = @($newRefsRaw | ForEach-Object { $_ })
$oldSummary = Read-JsonFile $oldSnapshot.Summary
$newSummary = Read-JsonFile $newSnapshot.Summary

$oldEntityMap = New-MapByProperty $oldEntities "guid"
$newEntityMap = New-MapByProperty $newEntities "guid"

$addedEntities = @($newEntities | Where-Object { -not $oldEntityMap.ContainsKey([string]$_.guid) } | ForEach-Object {
  [pscustomobject]@{
    Package = $_.package
    Name = $_.name
    Components = Join-ValueTypes $_
    Guid = $_.guid
    File = $_.file
  }
})

$removedEntities = @($oldEntities | Where-Object { -not $newEntityMap.ContainsKey([string]$_.guid) } | ForEach-Object {
  [pscustomobject]@{
    Package = $_.package
    Name = $_.name
    Components = Join-ValueTypes $_
    Guid = $_.guid
    File = $_.file
  }
})

$changedEntities = New-Object System.Collections.Generic.List[object]
foreach ($oldEntity in $oldEntities) {
  $guid = [string]$oldEntity.guid
  if (-not $newEntityMap.ContainsKey($guid)) { continue }
  $newEntity = $newEntityMap[$guid]

  $oldComponents = Join-ValueTypes $oldEntity
  $newComponents = Join-ValueTypes $newEntity
  $changes = New-Object System.Collections.Generic.List[string]
  if ([string]$oldEntity.name -ne [string]$newEntity.name) { $changes.Add("name") }
  if ([string]$oldEntity.package -ne [string]$newEntity.package) { $changes.Add("package") }
  if ([string]$oldEntity.file -ne [string]$newEntity.file) { $changes.Add("file") }
  if ($oldComponents -ne $newComponents) { $changes.Add("components") }

  if ($changes.Count -gt 0) {
    $changedEntities.Add([pscustomobject]@{
      Change = ($changes -join ", ")
      OldPackage = $oldEntity.package
      NewPackage = $newEntity.package
      OldName = $oldEntity.name
      NewName = $newEntity.name
      OldComponents = $oldComponents
      NewComponents = $newComponents
      Guid = $guid
      OldFile = $oldEntity.file
      NewFile = $newEntity.file
    })
  }
}

$oldFileMap = New-MapByProperty (Get-FileHashes $oldSnapshot.Game) "File"
$newFileMap = New-MapByProperty (Get-FileHashes $newSnapshot.Game) "File"

$addedFiles = @($newFileMap.Keys | Where-Object { -not $oldFileMap.ContainsKey($_) } | Sort-Object | ForEach-Object {
  [pscustomobject]@{ File = $_; Length = $newFileMap[$_].Length; Hash = $newFileMap[$_].Hash }
})
$removedFiles = @($oldFileMap.Keys | Where-Object { -not $newFileMap.ContainsKey($_) } | Sort-Object | ForEach-Object {
  [pscustomobject]@{ File = $_; Length = $oldFileMap[$_].Length; Hash = $oldFileMap[$_].Hash }
})
$changedFiles = @($newFileMap.Keys | Where-Object { $oldFileMap.ContainsKey($_) -and $oldFileMap[$_].Hash -ne $newFileMap[$_].Hash } | Sort-Object | ForEach-Object {
  [pscustomobject]@{
    File = $_
    OldLength = $oldFileMap[$_].Length
    NewLength = $newFileMap[$_].Length
    OldHash = $oldFileMap[$_].Hash
    NewHash = $newFileMap[$_].Hash
  }
})

function New-ReferenceKey($Ref) {
  return "$($Ref.sourceEntityGuid)|$($Ref.sourcePath)|$($Ref.targetGuid)|$($Ref.guid)"
}

$oldRefMap = @{}
foreach ($ref in $oldRefs) { $oldRefMap[(New-ReferenceKey $ref)] = $ref }
$newRefMap = @{}
foreach ($ref in $newRefs) { $newRefMap[(New-ReferenceKey $ref)] = $ref }

$addedReferences = @($newRefMap.Keys | Where-Object { -not $oldRefMap.ContainsKey($_) } | Select-Object -First $ReferenceDiffLimit | ForEach-Object {
  $ref = $newRefMap[$_]
  [pscustomobject]@{
    SourceFile = $ref.sourceFile
    SourcePackage = $ref.sourcePackage
    SourceEntity = $ref.sourceEntityName
    SourceElement = $ref.sourceElement
    SourcePath = $ref.sourcePath
    Target = $ref.targetName
    TargetPackage = $ref.targetPackage
    Resolved = $ref.resolved
    NullGuid = $ref.nullGuid
    Guid = if ($ref.targetGuid) { $ref.targetGuid } else { $ref.guid }
  }
})

$removedReferences = @($oldRefMap.Keys | Where-Object { -not $newRefMap.ContainsKey($_) } | Select-Object -First $ReferenceDiffLimit | ForEach-Object {
  $ref = $oldRefMap[$_]
  [pscustomobject]@{
    SourceFile = $ref.sourceFile
    SourcePackage = $ref.sourcePackage
    SourceEntity = $ref.sourceEntityName
    SourceElement = $ref.sourceElement
    SourcePath = $ref.sourcePath
    Target = $ref.targetName
    TargetPackage = $ref.targetPackage
    Resolved = $ref.resolved
    NullGuid = $ref.nullGuid
    Guid = if ($ref.targetGuid) { $ref.targetGuid } else { $ref.guid }
  }
})

$summary = [ordered]@{
  old = $oldSnapshot.Label
  new = $newSnapshot.Label
  generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
  oldXmlFiles = if ($oldSummary) { $oldSummary.xmlFiles } else { $oldFileMap.Count }
  newXmlFiles = if ($newSummary) { $newSummary.xmlFiles } else { $newFileMap.Count }
  oldEntities = $oldEntities.Count
  newEntities = $newEntities.Count
  addedEntities = $addedEntities.Count
  removedEntities = $removedEntities.Count
  changedEntityMetadata = $changedEntities.Count
  addedXmlFiles = $addedFiles.Count
  removedXmlFiles = $removedFiles.Count
  changedXmlFiles = $changedFiles.Count
  oldReferences = $oldRefs.Count
  newReferences = $newRefs.Count
  addedReferencesShown = $addedReferences.Count
  removedReferencesShown = $removedReferences.Count
}

[System.IO.File]::WriteAllText(
  (Join-Path $outPath "summary.json"),
  (([pscustomobject]$summary | ConvertTo-Json -Depth 8) + [Environment]::NewLine),
  [System.Text.UTF8Encoding]::new($false)
)

Export-DiffTable $addedEntities @("Package","Name","Components","Guid","File") $outPath "entities-added" "Added Entities"
Export-DiffTable $removedEntities @("Package","Name","Components","Guid","File") $outPath "entities-removed" "Removed Entities"
Export-DiffTable $changedEntities @("Change","OldPackage","NewPackage","OldName","NewName","OldComponents","NewComponents","Guid","OldFile","NewFile") $outPath "entities-changed" "Changed Entity Metadata"
Export-DiffTable $addedFiles @("File","Length","Hash") $outPath "xml-files-added" "Added XML Files"
Export-DiffTable $removedFiles @("File","Length","Hash") $outPath "xml-files-removed" "Removed XML Files"
Export-DiffTable $changedFiles @("File","OldLength","NewLength","OldHash","NewHash") $outPath "xml-files-changed" "Changed XML Files"
Export-DiffTable $addedReferences @("SourceFile","SourcePackage","SourceEntity","SourceElement","SourcePath","Target","TargetPackage","Resolved","NullGuid","Guid") $outPath "references-added-sample" "Added References Sample" "Limited to the first 500 added reference rows."
Export-DiffTable $removedReferences @("SourceFile","SourcePackage","SourceEntity","SourceElement","SourcePath","Target","TargetPackage","Resolved","NullGuid","Guid") $outPath "references-removed-sample" "Removed References Sample" "Limited to the first 500 removed reference rows."

$readme = @(
  "# Version Diff",
  "",
  "Old snapshot: ``$($oldSnapshot.Label)``",
  "",
  "New snapshot: ``$($newSnapshot.Label)``",
  "",
  "Generated: $($summary.generatedAt)",
  "",
  "| Metric | Count |",
  "| --- | ---: |",
  "| Old entities | $($summary.oldEntities) |",
  "| New entities | $($summary.newEntities) |",
  "| Added entities | $($summary.addedEntities) |",
  "| Removed entities | $($summary.removedEntities) |",
  "| Changed entity metadata rows | $($summary.changedEntityMetadata) |",
  "| Added XML files | $($summary.addedXmlFiles) |",
  "| Removed XML files | $($summary.removedXmlFiles) |",
  "| Changed XML files | $($summary.changedXmlFiles) |",
  "| Old references | $($summary.oldReferences) |",
  "| New references | $($summary.newReferences) |",
  "",
  "Files:",
  "",
  '- `summary.json`',
  '- `entities-added.md` / `.csv`',
  '- `entities-removed.md` / `.csv`',
  '- `entities-changed.md` / `.csv`',
  '- `xml-files-added.md` / `.csv`',
  '- `xml-files-removed.md` / `.csv`',
  '- `xml-files-changed.md` / `.csv`',
  '- `references-added-sample.md` / `.csv`',
  '- `references-removed-sample.md` / `.csv`',
  "",
  "This diff is a local research aid. Review changed XML files manually before documenting modding conclusions."
)
[System.IO.File]::WriteAllLines((Join-Path $outPath "README.md"), $readme, [System.Text.UTF8Encoding]::new($false))

[pscustomobject]$summary
