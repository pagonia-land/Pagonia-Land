param(
  [string]$Root = (Get-Location).Path,
  [string]$GameDir = "game-gdb",
  [string]$GeneratedDir = "generated"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$gamePath = Join-Path $rootPath $GameDir
if (-not (Test-Path -LiteralPath $gamePath)) {
  throw "Game data directory not found: $gamePath"
}
$gamePath = (Resolve-Path -LiteralPath $gamePath).Path
$outputDir = Join-Path $rootPath $GeneratedDir
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$xmlFiles = Get-ChildItem -LiteralPath $gamePath -Recurse -File -Filter *.xml |
  Sort-Object FullName

function Get-RelativePath([string]$Path) {
  return Get-PathRelativeTo $Path $rootPath
}

function Get-GameRelativePath([string]$Path) {
  return Get-PathRelativeTo $Path $gamePath
}

function Get-GroupPath($Node) {
  $groups = New-Object System.Collections.Generic.List[string]
  $current = $Node.ParentNode

  while ($null -ne $current) {
    if ($current.Name -eq "EntityGroup" -and $current.Attributes["Name"]) {
      $groups.Insert(0, [string]$current.Attributes["Name"].Value)
    }
    $current = $current.ParentNode
  }

  return ($groups -join "/")
}

function Get-ParentEntity($Node) {
  $current = $Node.ParentNode

  while ($null -ne $current) {
    if ($current.Name -eq "Entity" -and $current.Attributes["Guid"]) {
      return $current
    }
    $current = $current.ParentNode
  }

  return $null
}

function Get-ElementPath($Node) {
  $parts = New-Object System.Collections.Generic.List[string]
  $current = $Node

  while ($null -ne $current -and $current.NodeType -eq [System.Xml.XmlNodeType]::Element) {
    $label = $current.Name
    if ($current.Attributes["Name"]) {
      $label = "$label[@Name='$($current.Attributes["Name"].Value)']"
    }
    $parts.Insert(0, $label)
    $current = $current.ParentNode
  }

  return "/" + ($parts -join "/")
}

$definitions = @{}
$entities = New-Object System.Collections.Generic.List[object]
$parsedDocs = New-Object System.Collections.Generic.List[object]

foreach ($file in $xmlFiles) {
  [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
  $relative = Get-RelativePath $file.FullName
  $gameRelative = Get-GameRelativePath $file.FullName
  $package = $gameRelative.Split([char]'\')[0]

  $parsedDocs.Add([pscustomobject]@{
    File = $relative
    Package = $package
    Document = $doc
  })

  foreach ($entityNode in $doc.SelectNodes("//Entity[@Guid]")) {
    $valueTypes = @()
    if ($entityNode.Values) {
      $valueTypes = @($entityNode.Values.ChildNodes | ForEach-Object { $_.LocalName } | Where-Object { $_ })
    }

    $parentEntity = Get-ParentEntity $entityNode

    $entity = [pscustomobject]@{
      guid = [string]$entityNode.Guid
      name = [string]$entityNode.Name
      package = $package
      file = $relative
      groupPath = Get-GroupPath $entityNode
      isAbstract = ([string]$entityNode.IsAbstract -eq "true")
      parentEntityGuid = if ($parentEntity) { [string]$parentEntity.Guid } else { $null }
      parentEntityName = if ($parentEntity) { [string]$parentEntity.Name } else { $null }
      childEntityCount = @($entityNode.Children.Entity).Count
      valueTypes = $valueTypes
    }

    $entities.Add($entity)
    if (-not $definitions.ContainsKey($entity.guid)) {
      $definitions[$entity.guid] = $entity
    }
  }
}

$guidRegex = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
$references = New-Object System.Collections.Generic.List[object]

foreach ($parsed in $parsedDocs) {
  foreach ($node in $parsed.Document.SelectNodes("//*[not(self::Entity)]")) {
    $text = ($node.InnerText).Trim()
    if ($text -match $guidRegex) {
      $target = $definitions[$text]
      $sourceEntity = Get-ParentEntity $node

      $references.Add([pscustomobject]@{
        sourceFile = $parsed.File
        sourcePackage = $parsed.Package
        sourceEntityGuid = if ($sourceEntity) { [string]$sourceEntity.Guid } else { $null }
        sourceEntityName = if ($sourceEntity) { [string]$sourceEntity.Name } else { $null }
        sourceElement = $node.Name
        sourcePath = Get-ElementPath $node
        guid = $text
        resolved = [bool]$target
        nullGuid = ($text -eq "00000000-0000-0000-0000-000000000000")
        targetGuid = if ($target) { $target.guid } else { $null }
        targetName = if ($target) { $target.name } else { $null }
        targetPackage = if ($target) { $target.package } else { $null }
        targetFile = if ($target) { $target.file } else { $null }
        targetValueTypes = if ($target) { $target.valueTypes } else { @() }
      })
    }
  }
}

$summary = [pscustomobject]@{
  generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
  xmlFiles = $xmlFiles.Count
  totalEntities = $entities.Count
  uniqueGuids = @($entities | Select-Object -ExpandProperty guid -Unique).Count
  guidLikeReferences = $references.Count
  resolvedReferences = @($references | Where-Object { $_.resolved }).Count
  nullGuidReferences = @($references | Where-Object { $_.nullGuid }).Count
  otherUnresolvedReferences = @($references | Where-Object { -not $_.resolved -and -not $_.nullGuid }).Count
  packages = @($entities | Group-Object package | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{
      package = $_.Name
      entities = $_.Count
    }
  })
}

$jsonDepth = 20
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

[System.IO.File]::WriteAllText(
  (Join-Path $outputDir "entities.json"),
  (($entities | Sort-Object package,file,groupPath,name,guid | ConvertTo-Json -Depth $jsonDepth) + [Environment]::NewLine),
  $utf8NoBom
)

[System.IO.File]::WriteAllText(
  (Join-Path $outputDir "references.json"),
  (($references | Sort-Object sourcePackage,sourceFile,sourceEntityName,sourceElement,guid | ConvertTo-Json -Depth $jsonDepth) + [Environment]::NewLine),
  $utf8NoBom
)

[System.IO.File]::WriteAllText(
  (Join-Path $outputDir "analysis-summary.json"),
  (($summary | ConvertTo-Json -Depth $jsonDepth) + [Environment]::NewLine),
  $utf8NoBom
)

$summary
