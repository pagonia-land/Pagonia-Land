param(
  [string]$Root = (Get-Location).Path,
  [string]$GameDir = "game-gdb",
  [string]$OutputDir = "generated\catalog",
  [int]$GraphEdgeLimit = 450
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$gamePath = Join-Path $rootPath $GameDir
if (-not (Test-Path -LiteralPath $gamePath)) {
  throw "Game data directory not found: $gamePath"
}

$gamePath = (Resolve-Path -LiteralPath $gamePath).Path
$outPath = Join-Path $rootPath $OutputDir
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

function Get-RelativePath([string]$Path) {
  return Get-PathRelativeTo $Path $rootPath
}

function Get-GamePackage([string]$Path) {
  return Get-GameRelativePackage $Path $gamePath
}

function Get-Text($Node, [string]$ChildName) {
  return Get-NodeText $Node $ChildName
}

function Get-ChildNode($Node, [string]$XPath) {
  return Get-NodeChild $Node $XPath
}

function Format-Md([string]$Text) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
  return $Text.Replace("|", "\|").Replace("`r", " ").Replace("`n", " ").Trim()
}

function Format-MermaidId([string]$Text) {
  return "n_" + ($Text -replace "[^A-Za-z0-9_]", "_")
}

function Format-MermaidLabel([string]$Text) {
  if ([string]::IsNullOrWhiteSpace($Text)) { return "Unknown" }
  return $Text.Replace('"', "'").Replace("[", "(").Replace("]", ")")
}

function Format-AmountName([string]$Amount, [string]$Name) {
  if ([string]::IsNullOrWhiteSpace($Name)) { return "" }
  if ([string]::IsNullOrWhiteSpace($Amount)) { return $Name }
  return "$Amount $Name"
}

function Join-Unique($Items) {
  return (($Items | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } | Sort-Object -Unique) -join "; ")
}

function Join-OrderedUnique($Items) {
  $seen = @{}
  $ordered = New-Object System.Collections.Generic.List[string]
  foreach ($item in $Items) {
    $value = [string]$item
    if ([string]::IsNullOrWhiteSpace($value)) { continue }
    if ($seen.ContainsKey($value)) { continue }
    $seen[$value] = $true
    $ordered.Add($value)
  }
  return ($ordered -join "; ")
}

function Test-ValueType($Entity, [string]$ValueType) {
  return @($Entity.ValueTypes) -contains $ValueType
}

function Get-ResourceDescription($Entity) {
  return Get-ChildNode $Entity.Node.Values "ResourceDescription"
}

function Resolve-ResourceCategoryName($ResourceDescription) {
  return Resolve-Name (Get-Text $ResourceDescription "ResourceCategory")
}

function Get-ResourceTags($ResourceDescription) {
  $tags = New-Object System.Collections.Generic.List[string]
  if ($null -eq $ResourceDescription) { return @() }

  foreach ($tagNode in $ResourceDescription.SelectNodes("Tags/Item/Content/Tag")) {
    $tagGuid = ([string]$tagNode.InnerText).Trim()
    if (-not [string]::IsNullOrWhiteSpace($tagGuid)) {
      $tags.Add((Resolve-Name $tagGuid))
    }
  }

  return @($tags)
}

function Get-RecipeInputResources($RecipeNode) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $RecipeNode) { return @() }

  foreach ($resourceNode in $RecipeNode.SelectNodes("ProductionSteps/Item/Content/Input/Resource | ProductionSteps/Item/Content/InputOutput/Resource")) {
    $resourceGuid = ([string]$resourceNode.InnerText).Trim()
    if (-not [string]::IsNullOrWhiteSpace($resourceGuid)) {
      $amount = Get-Text $resourceNode.ParentNode "Amount"
      $items.Add((Format-AmountName $amount (Resolve-Name $resourceGuid)))
    }
  }

  return @($items)
}

function Get-ResourceReferences($Node, [string]$XPath) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return @() }

  foreach ($resourceNode in $Node.SelectNodes($XPath)) {
    $resourceGuid = ([string]$resourceNode.InnerText).Trim()
    if (-not [string]::IsNullOrWhiteSpace($resourceGuid)) {
      $items.Add((Resolve-Name $resourceGuid))
    }
  }

  return @($items)
}

function Add-ResourceUsage(
  [System.Collections.Generic.List[object]]$Rows,
  [string]$Package,
  [string]$ResourceGuid,
  [string]$UsageType,
  [string]$Amount,
  [string]$Building,
  [string]$Recipe,
  [string]$Unit,
  [string]$Context,
  [string]$Details,
  [string]$SourceGuid,
  [string]$File
) {
  if ([string]::IsNullOrWhiteSpace($ResourceGuid)) { return }
  if ($ResourceGuid -eq "00000000-0000-0000-0000-000000000000") { return }

  $Rows.Add([pscustomobject]@{
    Package = $Package
    Resource = Resolve-Name $ResourceGuid
    ResourceGuid = $ResourceGuid
    UsageType = $UsageType
    Amount = $Amount
    Building = $Building
    Recipe = $Recipe
    Unit = $Unit
    Context = $Context
    Details = $Details
    SourceGuid = $SourceGuid
    File = $File
  })
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
    $values = foreach ($col in $Columns) {
      Format-Md ([string]$row.$col)
    }
    $lines.Add("| " + ($values -join " | ") + " |")
  }

  [System.IO.File]::WriteAllLines($Path, $lines, [System.Text.UTF8Encoding]::new($false))
}

function Export-CatalogTable([object[]]$Rows, [string[]]$Columns, [string]$Directory, [string]$BaseName, [string]$Title, [string]$Intro = "") {
  New-Item -ItemType Directory -Force -Path $Directory | Out-Null
  $rowList = if ($null -eq $Rows) { @() } else { @($Rows) }
  $csvPath = Join-Path $Directory "$BaseName.csv"
  $rowCount = ($rowList | Measure-Object).Count
  if ($rowCount -gt 0) {
    $rowList | Select-Object $Columns | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
  } else {
    [System.IO.File]::WriteAllLines($csvPath, @(($Columns | ForEach-Object { '"' + $_ + '"' }) -join ","), [System.Text.UTF8Encoding]::new($false))
  }
  Write-MarkdownTable $rowList $Columns (Join-Path $Directory "$BaseName.md") $Title $Intro
}

function Add-SearchIndexItems(
  [System.Collections.Generic.List[object]]$SearchItems,
  [object[]]$Rows,
  [string]$Type,
  [string]$TitleColumn,
  [string[]]$SubtitleColumns,
  [string[]]$FieldColumns
) {
  foreach ($row in $Rows) {
    $fields = [ordered]@{}
    $terms = New-Object System.Collections.Generic.List[string]

    foreach ($column in $FieldColumns) {
      $value = [string]$row.$column
      $fields[$column] = $value
      if (-not [string]::IsNullOrWhiteSpace($value)) {
        $terms.Add($value)
      }
    }

    $subtitleParts = New-Object System.Collections.Generic.List[string]
    foreach ($column in $SubtitleColumns) {
      $value = [string]$row.$column
      if (-not [string]::IsNullOrWhiteSpace($value)) {
        $subtitleParts.Add($value)
      }
    }

    $title = [string]$row.$TitleColumn
    if ([string]::IsNullOrWhiteSpace($title)) {
      $title = [string]$row.EntityName
    }
    if ([string]::IsNullOrWhiteSpace($title)) {
      $title = [string]$row.Guid
    }

    $SearchItems.Add([pscustomobject]@{
      type = $Type
      title = $title
      subtitle = ($subtitleParts -join " | ")
      package = [string]$row.Package
      guid = [string]$row.Guid
      file = [string]$row.File
      terms = (Join-Unique $terms)
      fields = [pscustomobject]$fields
    })
  }
}

function Write-SearchIndex([System.Collections.Generic.List[object]]$SearchItems, [string]$Path) {
  $index = [pscustomobject]@{
    generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssK")
    itemCount = $SearchItems.Count
    items = @($SearchItems)
  }

  [System.IO.File]::WriteAllText(
    $Path,
    (($index | ConvertTo-Json -Depth 12) + [Environment]::NewLine),
    [System.Text.UTF8Encoding]::new($false)
  )
}

function New-ProductionGraphLines($Rows, [int]$Limit) {
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("flowchart LR")
  $edgeCount = 0
  $seenEdges = @{}

  foreach ($row in $Rows) {
    if ($edgeCount -ge $Limit) { break }
    $buildingId = Format-MermaidId "building_$($row.BuildingGuid)"
    $recipeId = Format-MermaidId "recipe_$($row.RecipeGuid)"
    $edge = "$buildingId|$recipeId"
    if (-not $seenEdges.ContainsKey($edge)) {
      $lines.Add("  $buildingId[""$((Format-MermaidLabel $row.Building))""] --> $recipeId{{""$((Format-MermaidLabel $row.Recipe))""}}")
      $seenEdges[$edge] = $true
      $edgeCount++
    }

    foreach ($inputName in (($row.Inputs -split "; ") | Where-Object { $_ })) {
      if ($edgeCount -ge $Limit) { break }
      $inputId = Format-MermaidId "input_$inputName"
      $edge = "$inputId|$recipeId"
      if (-not $seenEdges.ContainsKey($edge)) {
        $lines.Add("  $inputId[""$((Format-MermaidLabel $inputName))""] --> $recipeId")
        $seenEdges[$edge] = $true
        $edgeCount++
      }
    }

    foreach ($outputName in (($row.Outputs -split "; ") | Where-Object { $_ })) {
      if ($edgeCount -ge $Limit) { break }
      $outputId = Format-MermaidId "output_$outputName"
      $edge = "$recipeId|$outputId"
      if (-not $seenEdges.ContainsKey($edge)) {
        $lines.Add("  $recipeId --> $outputId[""$((Format-MermaidLabel $outputName))""]")
        $seenEdges[$edge] = $true
        $edgeCount++
      }
    }
  }

  return [pscustomobject]@{
    Lines = $lines
    EdgeCount = $edgeCount
  }
}

function Write-ProductionGraph($Rows, [string]$Directory, [string]$BaseName, [string]$Title, [string]$Intro, [int]$Limit) {
  New-Item -ItemType Directory -Force -Path $Directory | Out-Null
  $graph = New-ProductionGraphLines $Rows $Limit
  [System.IO.File]::WriteAllLines((Join-Path $Directory "$BaseName.mmd"), $graph.Lines, [System.Text.UTF8Encoding]::new($false))

  $graphMd = New-Object System.Collections.Generic.List[string]
  $graphMd.Add("# $Title")
  $graphMd.Add("")
  $graphMd.Add($Intro)
  $graphMd.Add("")
  if ($graph.EdgeCount -ge $Limit) {
    $graphMd.Add("This graph was limited to $Limit edges. Increase ``-GraphEdgeLimit`` when running ``scripts\generate_catalog.ps1`` if needed.")
    $graphMd.Add("")
  }
  $graphMd.Add('```mermaid')
  $graphMd.AddRange($graph.Lines)
  $graphMd.Add('```')
  [System.IO.File]::WriteAllLines((Join-Path $Directory "$BaseName.md"), $graphMd, [System.Text.UTF8Encoding]::new($false))

  return $graph.EdgeCount
}

$xmlFiles = @(Get-ChildItem -LiteralPath $gamePath -Recurse -File -Filter *.xml | Sort-Object FullName)
$defs = @{}
$entities = New-Object System.Collections.Generic.List[object]

foreach ($file in $xmlFiles) {
  [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
  $relative = Get-RelativePath $file.FullName
  $package = Get-GamePackage $file.FullName

  foreach ($entity in $doc.SelectNodes("//Entity[@Guid]")) {
    $values = $entity.Values
    $kind = "Entity"
    $displayName = [string]$entity.Name

    $resourceDescription = Get-ChildNode $values "ResourceDescription"
    $buildingComponent = Get-ChildNode $values "Building"
    $unitComponent = Get-ChildNode $values "Unit"
    $recipeComponent = Get-ChildNode $values "ProductionRecipe"

    if ($resourceDescription) {
      $kind = "Resource"
      $displayName = Get-Text $resourceDescription "Name"
    } elseif ($buildingComponent) {
      $kind = "Building"
      $displayName = Get-Text $buildingComponent "Name"
    } elseif ($unitComponent) {
      $kind = "Unit"
      $displayName = Get-Text $unitComponent "Name"
    } elseif ($recipeComponent) {
      $kind = "Recipe"
      $displayName = [string]$entity.Name
    }

    if ([string]::IsNullOrWhiteSpace($displayName)) {
      $displayName = [string]$entity.Name
    }

    $valueTypes = @()
    if ($values) {
      $valueTypes = @($values.ChildNodes | ForEach-Object { $_.LocalName } | Where-Object { $_ })
    }

    $row = [pscustomobject]@{
      Guid = [string]$entity.Guid
      EntityName = [string]$entity.Name
      DisplayName = $displayName
      Kind = $kind
      Package = $package
      File = $relative
      ValueTypes = $valueTypes
      Node = $entity
    }

    $entities.Add($row)
    if (-not $defs.ContainsKey($row.Guid)) {
      $defs[$row.Guid] = $row
    }
  }
}

function Resolve-Name([string]$Guid) {
  if ([string]::IsNullOrWhiteSpace($Guid)) { return "" }
  if ($defs.ContainsKey($Guid)) { return $defs[$Guid].DisplayName }
  if ($Guid -eq "00000000-0000-0000-0000-000000000000") { return "(none)" }
  return "Unresolved:$Guid"
}

function Test-GuidString([string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
  return ($Value.Trim() -match "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
}

function Get-ResolvedLeafReferences($Node, [string[]]$NamePatterns) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return @() }

  foreach ($leaf in $Node.SelectNodes(".//*[not(*)]")) {
    $name = [string]$leaf.LocalName
    $matched = $false
    foreach ($pattern in $NamePatterns) {
      if ($name -match $pattern) {
        $matched = $true
        break
      }
    }
    if (-not $matched) { continue }

    $guid = ([string]$leaf.InnerText).Trim()
    if (-not (Test-GuidString $guid)) { continue }
    if ($guid -eq "00000000-0000-0000-0000-000000000000") { continue }
    $items.Add("$name=$(Resolve-Name $guid)")
  }

  return @($items | Sort-Object -Unique)
}

function Get-ResolvedLeafReferencesForXPath($Node, [string]$XPath) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return @() }

  foreach ($leaf in $Node.SelectNodes($XPath)) {
    $name = [string]$leaf.LocalName
    $guid = ([string]$leaf.InnerText).Trim()
    if (-not (Test-GuidString $guid)) { continue }
    if ($guid -eq "00000000-0000-0000-0000-000000000000") { continue }
    $items.Add("$name=$(Resolve-Name $guid)")
  }

  return @($items | Sort-Object -Unique)
}

function Get-ScalarSummary($Node, [string[]]$SkipNames = @(), [int]$Limit = 30) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return "" }

  foreach ($leaf in $Node.SelectNodes(".//*[not(*)]")) {
    if ($items.Count -ge $Limit) { break }
    $name = [string]$leaf.LocalName
    if ($SkipNames -contains $name) { continue }
    $value = ([string]$leaf.InnerText).Trim()
    if ([string]::IsNullOrWhiteSpace($value)) { continue }
    if (Test-GuidString $value) { continue }
    if ($value.Length -gt 80) { $value = $value.Substring(0, 80) + "..." }
    $items.Add("$name=$value")
  }

  return Join-OrderedUnique $items
}

function Get-ComponentGuidValue($Node, [string]$ChildName = "") {
  if ($null -eq $Node) { return "" }
  if (-not [string]::IsNullOrWhiteSpace($ChildName)) {
    $childValue = Get-Text $Node $ChildName
    if (Test-GuidString $childValue) { return $childValue }
  }
  $directValue = ([string]$Node.InnerText).Trim()
  if (Test-GuidString $directValue) { return $directValue }
  return ""
}

function Get-ValueTypeNames($Entity, [string[]]$Patterns) {
  return @($Entity.ValueTypes | Where-Object {
    $name = [string]$_
    $matched = $false
    foreach ($pattern in $Patterns) {
      if ($name -match $pattern) {
        $matched = $true
        break
      }
    }
    $matched
  })
}

function Get-EntityReferenceSources([string]$TargetGuid) {
  if (-not (Test-GuidString $TargetGuid)) { return @() }
  if ($TargetGuid -eq "00000000-0000-0000-0000-000000000000") { return @() }
  if ($null -eq $script:entityReferenceSourcesByTarget) { return @() }
  if (-not $script:entityReferenceSourcesByTarget.ContainsKey($TargetGuid)) { return @() }
  return @($script:entityReferenceSourcesByTarget[$TargetGuid] | Sort-Object -Unique)
}

function New-EntityReferenceSourceMap {
  $map = @{}
  foreach ($source in $entities) {
    $values = $source.Node.SelectSingleNode("Values")
    if ($null -eq $values) { continue }

    foreach ($leaf in $values.SelectNodes(".//*[not(*)]")) {
      $value = ([string]$leaf.InnerText).Trim()
      if (-not (Test-GuidString $value)) { continue }
      if ($value -eq "00000000-0000-0000-0000-000000000000") { continue }
      if ($value -eq [string]$source.Guid) { continue }
      if (-not $map.ContainsKey($value)) {
        $map[$value] = New-Object System.Collections.Generic.List[string]
      }
      $map[$value].Add("$($leaf.LocalName)=$($source.DisplayName)")
    }
  }

  return $map
}

function Get-LocalizationLikeValues($Node) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return @() }

  foreach ($leaf in $Node.SelectNodes(".//*[not(*)]")) {
    if (-not (Test-LocalizationFieldName $leaf.LocalName)) { continue }
    $value = ([string]$leaf.InnerText).Trim()
    if (-not (Test-LooksLikeLocalizationKey $value)) { continue }
    $items.Add("$($leaf.LocalName)=$value")
  }

  return @($items | Sort-Object -Unique)
}

function Get-ObjectiveSpecificComponents($Entity) {
  return @($Entity.ValueTypes | Where-Object {
    $_ -match "^(Objective|VictoryObjective)" -or $_ -in @("CommodityObjective","TradeObjective","FactionObjective","TechTreeTierGroup")
  })
}

function Test-HasAncestorNamed($Node, [string]$Name) {
  $current = $Node.ParentNode
  while ($null -ne $current) {
    if ($current.LocalName -eq $Name) { return $true }
    $current = $current.ParentNode
  }
  return $false
}

function Get-NodePathFromValues($Node) {
  $parts = New-Object System.Collections.Generic.List[string]
  $current = $Node
  while ($null -ne $current -and $current.LocalName -ne "Values") {
    if (-not [string]::IsNullOrWhiteSpace($current.LocalName)) {
      $parts.Insert(0, $current.LocalName)
    }
    $current = $current.ParentNode
  }
  return ($parts -join "/")
}

function Test-LocalizationFieldName([string]$Name) {
  if ([string]::IsNullOrWhiteSpace($Name)) { return $false }
  $exact = @(
    "Name",
    "NamePlural",
    "Description",
    "ActiveBuildingDescription",
    "ShortDescription",
    "Title",
    "Subtitle",
    "Text",
    "Tooltip",
    "Hint",
    "Message",
    "Notification"
  )
  if ($exact -contains $Name) { return $true }
  if (@("FirstName","LastName") -contains $Name) { return $false }
  return ($Name -match "(Name|Description|Title|Subtitle|Text|Tooltip|Hint|Message|Notification|Label)$")
}

function Test-LooksLikeLocalizationKey([string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
  $text = $Value.Trim()
  if ($text -match "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$") { return $false }
  if ($text -match "^(true|false|null|none)$") { return $false }
  if ($text -match "^[+-]?\d+([.,]\d+)?s?$") { return $false }
  if ($text -match "^[A-Z]?:?[\\/]" -or $text -match "[\\/]" -or $text -match "://") { return $false }
  if ($text -match "\.(png|jpg|jpeg|tga|dds|prefab|fbx|wav|ogg|mp3|bank|asset|mat|mesh)$") { return $false }
  if ($text.Length -gt 240) { return $false }
  return $true
}

function Get-AssetType([string]$FieldName, [string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
  $text = $Value.Trim()
  $field = [string]$FieldName
  if ($text -match "^event:/") { return "AudioEvent" }
  if ($field -match "Audio|Sound|Music|Ambience|Announcement") { return "Audio" }
  if ($field -match "Icon" -or $text -match "/icons?/") { return "Icon" }
  if ($field -match "CharacterKit" -or $text -match "\.characterkit\.json$") { return "CharacterKit" }
  if ($field -match "Prefab" -or $text -match "\.prefab\.json$") { return "Prefab" }
  if ($field -match "Mesh" -or $text -match "\.mesh\.json$") { return "Mesh" }
  if ($field -match "Texture|Mask|Foam|Caustic" -or $text -match "\.(png|jpg|jpeg|tga|dds)$") { return "Texture" }
  if ($field -match "Material" -or $text -match "\.(mat|material)(\.json)?$") { return "Material" }
  if ($field -match "Vfx|Effect|Particle" -or $text -match "/vfx/") { return "VFX" }
  if ($text -match "\.json$") { return "JsonAsset" }
  if ($text -match "\.(asset|bank|wav|ogg|mp3)$") { return "Asset" }
  return ""
}

function Test-LooksLikeAssetReference([string]$FieldName, [string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
  $text = $Value.Trim()
  if (Test-GuidString $text) { return $false }
  if ($text -match "^event:/") { return $true }
  if ($text -match "^[a-zA-Z0-9_./ -]+\.(png|jpg|jpeg|tga|dds|prefab\.json|mesh\.json|characterkit\.json|json|asset|mat|material|bank|wav|ogg|mp3)$") { return $true }
  $field = [string]$FieldName
  if ($field -match "Icon|Mesh|Prefab|Texture|Material|CharacterKit|Vfx|Effect|Audio|Sound|Music|Ambience|Announcement" -and $text -match "[/\\]") { return $true }
  return $false
}

function Get-AssetRoot([string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
  $text = $Value.Trim()
  if ($text -match "^event:/") { return "event:" }
  if ($text -match "^([^/\\]+)[/\\]") { return $matches[1] }
  return ""
}

function Get-AssetExtension([string]$Value) {
  if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
  $text = $Value.Trim()
  if ($text -match "\.([A-Za-z0-9]+)(\.json)?$") {
    if ($matches[2]) { return "$($matches[1]).json" }
    return $matches[1]
  }
  return ""
}

function Get-AssetReferencePairs($Node) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $Node) { return @() }

  foreach ($leaf in $Node.SelectNodes(".//*[not(*)]")) {
    $value = ([string]$leaf.InnerText).Trim()
    if (-not (Test-LooksLikeAssetReference $leaf.LocalName $value)) { continue }
    $items.Add("$($leaf.LocalName)=$value")
  }

  return @($items | Sort-Object -Unique)
}

function Get-EntityGroupPath($EntityNode) {
  $groups = New-Object System.Collections.Generic.List[string]
  $node = $EntityNode.ParentNode
  while ($null -ne $node) {
    if ($node.LocalName -eq "EntityGroup" -and $node.Attributes["Name"]) {
      $groups.Add([string]$node.Attributes["Name"].Value)
    }
    $node = $node.ParentNode
  }

  if ($groups.Count -eq 0) { return "" }
  [array]::Reverse($groups)
  return ($groups -join "/")
}

$localizationRows = New-Object System.Collections.Generic.List[object]
$declaredLocalizationKeys = @{}

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "LocaParkplatz" }) {
  $loca = Get-ChildNode $entity.Node.Values "LocaParkplatz"
  foreach ($content in $loca.SelectNodes("Tags/Item/Content")) {
    $key = Get-Text $content "Tag"
    if ([string]::IsNullOrWhiteSpace($key)) { continue }
    $declaredLocalizationKeys[$key] = $true

    $localizationRows.Add([pscustomobject]@{
      Package = $entity.Package
      Key = $key
      Declared = "True"
      UsageType = "DeclaredTag"
      Entity = $entity.EntityName
      DisplayName = $entity.DisplayName
      Kind = $entity.Kind
      FieldPath = "LocaParkplatz/Tags/Item/Content/Tag"
      Comment = Get-Text $content "Comment"
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

foreach ($entity in $entities) {
  $valuesNode = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $valuesNode) { continue }
  foreach ($node in $valuesNode.SelectNodes(".//*[not(*)]")) {
    if (Test-HasAncestorNamed $node "LocaParkplatz") { continue }
    if (Test-HasAncestorNamed $node "CreditsCategory") { continue }
    if (-not (Test-LocalizationFieldName $node.LocalName)) { continue }

    $key = ([string]$node.InnerText).Trim()
    if (-not (Test-LooksLikeLocalizationKey $key)) { continue }

    $localizationRows.Add([pscustomobject]@{
      Package = $entity.Package
      Key = $key
      Declared = if ($declaredLocalizationKeys.ContainsKey($key)) { "True" } else { "False" }
      UsageType = "ReferencedKey"
      Entity = $entity.EntityName
      DisplayName = $entity.DisplayName
      Kind = $entity.Kind
      FieldPath = Get-NodePathFromValues $node
      Comment = ""
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

$localizationRows = @($localizationRows | Sort-Object Key, UsageType, Package, Entity, FieldPath)

$objectiveFlowRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities) {
  $specificComponents = @(Get-ObjectiveSpecificComponents $entity)
  if (-not (Test-ValueType $entity "GeneralObjective") -and $specificComponents.Count -eq 0) { continue }

  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }
  $general = Get-ChildNode $values "GeneralObjective"
  $startRewards = Get-ChildNode $values "ObjectiveStartRewards"
  $rewards = Get-ChildNode $values "ObjectiveRewards"
  $notifications = Get-ChildNode $values "ObjectiveNotifications"
  $completionTriggers = Get-ChildNode $values "ObjectiveCompletionTriggers"
  $unlockConditions = Get-ChildNode $values "TechTreeTierGroup/UnlockConditions"

  $titleKeys = New-Object System.Collections.Generic.List[string]
  $descriptionKeys = New-Object System.Collections.Generic.List[string]
  $amounts = New-Object System.Collections.Generic.List[string]
  foreach ($componentName in $specificComponents) {
    $component = Get-ChildNode $values $componentName
    if ($null -eq $component) { continue }

    foreach ($field in @("Title","ToolTip","AdditionalHint")) {
      $value = Get-Text $component $field
      if (-not [string]::IsNullOrWhiteSpace($value)) { $titleKeys.Add("$field=$value") }
    }
    foreach ($field in @("Description","ShortDescription","ActiveBuildingDescription")) {
      $value = Get-Text $component $field
      if (-not [string]::IsNullOrWhiteSpace($value)) { $descriptionKeys.Add("$field=$value") }
    }
    foreach ($field in @("Amount","AmountMin","AmountMiddle","AmountMax","ResourceAmount","TargetAmount")) {
      $value = Get-Text $component $field
      if (-not [string]::IsNullOrWhiteSpace($value)) { $amounts.Add("$field=$value") }
    }
  }

  $preconditionObjectives = Get-ResolvedLeafReferences (Get-ChildNode $general "PreCondition") @("^GeneralObjective$","Objective$")
  $preconditionObjectives += Get-ResolvedLeafReferences $unlockConditions @("Objective$")
  $skipObjectives = Get-ResolvedLeafReferences (Get-ChildNode $general "SkipCondition") @("^GeneralObjective$","Objective$")
  $failObjectives = @()
  $failObjectives += Get-ResolvedLeafReferences (Get-ChildNode $general "FailConditionsObjectives") @("^GeneralObjective$","Objective$")
  $resourceRefs = Get-ResolvedLeafReferences $values @("Resource$","Commodity$","DepositResource$","GatherResource$","ResourceTag$","DepositTag$")
  $buildingRefs = Get-ResolvedLeafReferences $values @("Building$","AllowedBuilding$","GathererBuilding$")
  $unitRefs = Get-ResolvedLeafReferences $values @("Unit$","AllowedUnit$","Recruitment$")
  $pointOfInterestRefs = Get-ResolvedLeafReferences $values @("PointOfInterest$","PointsOfInterest$","Camp$","Encounter$")
  $startRewardRefs = Get-ResolvedLeafReferences $startRewards @(".+")
  $rewardRefs = Get-ResolvedLeafReferences $rewards @(".+")
  $notificationRefs = Get-ResolvedLeafReferences $notifications @(".+")
  $completionTriggerRefs = Get-ResolvedLeafReferences $completionTriggers @(".+")
  $onUnlockedNotification = Resolve-Name (Get-Text $values "TechTreeTierGroup/OnUnlockedNotification")
  $localizationKeys = Get-LocalizationLikeValues $values

  $objectiveFlowRows.Add([pscustomobject]@{
    Package = $entity.Package
    Objective = $entity.DisplayName
    Entity = $entity.EntityName
    Type = if (Test-ValueType $entity "TechTreeTierGroup") { "TechTreeTierGroup" } else { Get-Text $general "Type" }
    Hidden = Get-Text $general "Hidden"
    Category = if (-not [string]::IsNullOrWhiteSpace((Get-Text $general "Category"))) { Resolve-Name (Get-Text $general "Category") } else { Resolve-Name (Get-Text $unlockConditions "Category") }
    SortOrder = Get-Text $general "SortOrder"
    ObjectiveComponents = Join-Unique $specificComponents
    TitleKeys = Join-Unique $titleKeys
    DescriptionKeys = Join-Unique $descriptionKeys
    Amounts = Join-Unique $amounts
    PreconditionObjectives = Join-Unique $preconditionObjectives
    SkipObjectives = Join-Unique $skipObjectives
    FailObjectives = Join-Unique $failObjectives
    ResourceRefs = Join-Unique $resourceRefs
    BuildingRefs = Join-Unique $buildingRefs
    UnitRefs = Join-Unique $unitRefs
    PointOfInterestRefs = Join-Unique $pointOfInterestRefs
    StartRewards = Join-Unique $startRewardRefs
    Rewards = Join-Unique $rewardRefs
    Notifications = Join-Unique $notificationRefs
    CompletionTriggers = Join-Unique $completionTriggerRefs
    OnUnlockedNotification = $onUnlockedNotification
    LocalizationKeys = Join-Unique $localizationKeys
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$objectiveFlowRows = @($objectiveFlowRows | Sort-Object Package,File,Objective)

$techTreeRows = New-Object System.Collections.Generic.List[object]
$unlockGateRows = New-Object System.Collections.Generic.List[object]
$unlockRewardRows = New-Object System.Collections.Generic.List[object]
$seasonalRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities) {
  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }

  $techTree = Get-ChildNode $values "TechTreeTierGroup"
  if ($techTree) {
    $unlockConditions = Get-ChildNode $techTree "UnlockConditions"
    $techTreeRows.Add([pscustomobject]@{
      Package = $entity.Package
      TierGroup = $entity.DisplayName
      Entity = $entity.EntityName
      Tier = Get-Text $techTree "Tier"
      SortOrder = Get-Text $techTree "SortOrder"
      TitleKey = Get-Text $techTree "Title"
      DescriptionKey = Get-Text $techTree "Description"
      TooltipKey = Get-Text $techTree "ToolTip"
      Icon = Get-Text $techTree "Icon"
      Buildings = Join-Unique (Get-ResolvedLeafReferencesForXPath $techTree "Buildings/Item/Content/Building")
      Units = Join-Unique (Get-ResolvedLeafReferencesForXPath $techTree "Units/Item/Content/Unit")
      Category = Resolve-Name (Get-Text $unlockConditions "Category")
      PrimaryObjective = Resolve-Name (Get-Text $unlockConditions "PrimaryObjective")
      AlternativeObjectives = Join-Unique (Get-ResolvedLeafReferencesForXPath $unlockConditions "AlternativeObjectives/Item/Content/AlternativeObjective")
      OnUnlockedNotification = Resolve-Name (Get-Text $techTree "OnUnlockedNotification")
      ShowNextTierObjectives = Get-Text $techTree "UiShowNextTierObjectivesOnCompletion"
      Values = Get-ScalarSummary $techTree @("Title","Description","ToolTip","Icon") 30
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  $needsUnlock = Get-ChildNode $values "NeedsUnlock"
  if ($needsUnlock) {
    $unlockGateRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Kind = $entity.Kind
      GateType = "NeedsUnlock"
      DLC = Resolve-Name (Get-Text $needsUnlock "DLC")
      DLCNeedsOwn = Get-Text $needsUnlock "DLCNeedsOwn"
      TechTreeTierGroups = Join-Unique (Get-ResolvedLeafReferencesForXPath $needsUnlock ".//TechTreeTierGroup")
      Objectives = Join-Unique (Get-ResolvedLeafReferencesForXPath $needsUnlock ".//Objective | .//GeneralObjective")
      Buildings = Join-Unique (Get-ResolvedLeafReferencesForXPath $needsUnlock ".//Building")
      Units = Join-Unique (Get-ResolvedLeafReferencesForXPath $needsUnlock ".//Unit")
      Resources = Join-Unique (Get-ResolvedLeafReferencesForXPath $needsUnlock ".//Resource | .//DepositResource")
      References = Join-Unique (Get-ResolvedLeafReferences $needsUnlock @(".*"))
      Values = Get-ScalarSummary $needsUnlock @() 20
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  $seasonal = Get-ChildNode $values "Seasonal"
  if ($seasonal) {
    $seasonalRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Kind = $entity.Kind
      AllowedSeasons = Get-Text $seasonal "AllowedSeasons"
      References = Join-Unique (Get-ResolvedLeafReferences $seasonal @(".*"))
      Values = Get-ScalarSummary $seasonal @("AllowedSeasons") 20
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  foreach ($rewardComponentName in @("ObjectiveStartRewards","ObjectiveRewards")) {
    $rewardComponent = Get-ChildNode $values $rewardComponentName
    if ($null -eq $rewardComponent) { continue }

    $unlockedBuildings = Get-ResolvedLeafReferencesForXPath $rewardComponent "UnlockedBuildings/Item/Content/Building"
    $unlockedRecipes = Get-ResolvedLeafReferencesForXPath $rewardComponent "UnlockedProductionRecipes/Item/Content/Recipe | UnlockedProductionRecipes/Item/Content/ProductionRecipe"
    $unlockedRecruitments = Get-ResolvedLeafReferencesForXPath $rewardComponent "UnlockedRecruitments/Item/Content/Unit | UnlockedRecruitments/Item/Content/Recruitment"
    $unlockedShrineAbilities = Get-ResolvedLeafReferencesForXPath $rewardComponent "UnlockShrineAbilities/Item/Content/Ability | UnlockShrineAbilities/Item/Content/ShrineAbility"
    $unlockedTechTreeGroups = Get-ResolvedLeafReferencesForXPath $rewardComponent "UnlockTechTreeTierGroup/Item/Content/TechTreeTierGroup"
    $farmRecipeIdentifiers = @($rewardComponent.SelectNodes("UnlockGathererFarmRecipes/Item/Content/RecipeIdentifier") | ForEach-Object { ([string]$_.InnerText).Trim() } | Where-Object { $_ } | Sort-Object -Unique)

    $unlockRewardRows.Add([pscustomobject]@{
      Package = $entity.Package
      Objective = $entity.DisplayName
      Entity = $entity.EntityName
      RewardComponent = $rewardComponentName
      UnlockedBuildings = Join-Unique $unlockedBuildings
      UnlockGathererFarmRecipes = Join-Unique $farmRecipeIdentifiers
      UnlockedProductionRecipes = Join-Unique $unlockedRecipes
      UnlockedRecruitments = Join-Unique $unlockedRecruitments
      UnlockShrineAbilities = Join-Unique $unlockedShrineAbilities
      UnlockTechTreeTierGroups = Join-Unique $unlockedTechTreeGroups
      ModifiedNPCBases = Join-Unique (Get-ResolvedLeafReferencesForXPath $rewardComponent "ModifiedNPCBases/Item/Content/*")
      ModifiedCustomRaids = Join-Unique (Get-ResolvedLeafReferencesForXPath $rewardComponent "ModifiedNPCCustomRaids/Item/Content/*")
      KilledUnits = Join-Unique (Get-ResolvedLeafReferencesForXPath $rewardComponent "KilledUnits/Item/Content/*")
      OtherReferences = Join-Unique (Get-ResolvedLeafReferences $rewardComponent @(".*"))
      Values = Get-ScalarSummary $rewardComponent @() 25
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

$techTreeRows = @($techTreeRows | Sort-Object Package,Tier,SortOrder,TierGroup)
$unlockGateRows = @($unlockGateRows | Sort-Object Package,Kind,Name)
$unlockRewardRows = @($unlockRewardRows | Sort-Object Package,File,Objective,RewardComponent)
$seasonalRows = @($seasonalRows | Sort-Object Package,File,Name)

$assetReferenceRows = New-Object System.Collections.Generic.List[object]
$visualAudioComponentRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities) {
  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }

  foreach ($leaf in $values.SelectNodes(".//*[not(*)]")) {
    $value = ([string]$leaf.InnerText).Trim()
    if (-not (Test-LooksLikeAssetReference $leaf.LocalName $value)) { continue }
    $componentNode = $leaf
    while ($null -ne $componentNode.ParentNode -and $componentNode.ParentNode.LocalName -ne "Values") {
      $componentNode = $componentNode.ParentNode
    }

    $assetReferenceRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Kind = $entity.Kind
      Component = $componentNode.LocalName
      FieldPath = Get-NodePathFromValues $leaf
      FieldName = $leaf.LocalName
      AssetType = Get-AssetType $leaf.LocalName $value
      AssetRoot = Get-AssetRoot $value
      Extension = Get-AssetExtension $value
      Asset = $value
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  $visualComponents = @($values.ChildNodes | Where-Object {
    $_.LocalName -match "^(Vis|UiBuildingStatesOverwrite|UnitAnimations|UnitAttachment|UnitActiveAttachmentSet|UnitStatefulAttachment|UnitAttachableEffect)" -or
    $_.LocalName -in @("Building","Unit","ResourceDescription","TerrainProp","Deposit","DepositResourceType","Notification","ShrineAbility","TechTreeTierGroup")
  })

  foreach ($component in $visualComponents) {
    $componentAssets = New-Object System.Collections.Generic.List[string]
    $componentAudio = New-Object System.Collections.Generic.List[string]
    foreach ($leaf in $component.SelectNodes(".//*[not(*)]")) {
      $value = ([string]$leaf.InnerText).Trim()
      if (-not (Test-LooksLikeAssetReference $leaf.LocalName $value)) { continue }
      if ((Get-AssetType $leaf.LocalName $value) -match "Audio") {
        $componentAudio.Add("$($leaf.LocalName)=$value")
      } else {
        $componentAssets.Add("$($leaf.LocalName)=$value")
      }
    }

    if ($componentAssets.Count -eq 0 -and $componentAudio.Count -eq 0 -and $component.LocalName -notmatch "^(Vis|UiBuildingStatesOverwrite|UnitAnimations|UnitAttachment)") { continue }

    $visualAudioComponentRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Kind = $entity.Kind
      Component = $component.LocalName
      AssetReferences = Join-OrderedUnique $componentAssets
      AudioEvents = Join-OrderedUnique $componentAudio
      Values = Get-ScalarSummary $component @() 30
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

$assetReferenceRows = @($assetReferenceRows | Sort-Object Package,AssetType,Asset,Name)
$visualAudioComponentRows = @($visualAudioComponentRows | Sort-Object Package,File,Name,Component)

$toolEditorRows = New-Object System.Collections.Generic.List[object]
$toolEditorComponents = @("VisTerrain","VisFluid","VisRoad","VisTerritory","EditorBrushes","TerrainSediment","VisVegetationGroup","VisTexture","VisVegetation")

foreach ($entity in $entities | Where-Object { $_.Package -eq "tools" }) {
  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }

  foreach ($componentName in $toolEditorComponents) {
    $component = Get-ChildNode $values $componentName
    if ($null -eq $component) { continue }

    $linkedTextures = @()
    if ($componentName -in @("TerrainSediment","EditorBrushes")) {
      $linkedTextures = Get-ResolvedLeafReferencesForXPath $component ".//TopTerrainTexture | .//SideTerrainTexture | .//RoadTexture | .//BulldozerBrushTexture"
    }

    $toolEditorRows.Add([pscustomobject]@{
      Package = $entity.Package
      Area = $componentName
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      GroupPath = Get-EntityGroupPath $entity.Node
      IsAbstract = [string]$entity.Node.GetAttribute("IsAbstract")
      LinkedTextures = Join-Unique $linkedTextures
      VegetationGroup = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//VegetationGroup")
      VegetationItems = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Vegetation")
      ClusterGroups = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Group")
      AssetReferences = Join-Unique (Get-AssetReferencePairs $component)
      Values = Get-ScalarSummary $component @("DiffuseMap","NormalMap","ParameterMap","ColorTexture","GroundColorTexture","VegetationLimiter","ShapingBorderMasks","GrassRemovalMaskTexture","Map","Mesh","NormalMap","CausticsTexture","FoamTexture","FlowMap") 35
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

$toolEditorRows = @($toolEditorRows | Sort-Object GroupPath,Area,Name)

$script:entityReferenceSourcesByTarget = New-EntityReferenceSourceMap

$notificationNarrationRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities) {
  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }

  $notification = Get-ChildNode $values "Notification"
  $filterCategory = Get-ChildNode $values "NotificationFilterCategory"
  $narrationDialog = Get-ChildNode $values "UiNarrationDialog"
  $narrationCamera = Get-ChildNode $values "UiNarrationDialogCamera"
  $globalParameters = Get-ChildNode $values "NotificationParameters"

  if ($null -eq $notification -and $null -eq $filterCategory -and $null -eq $narrationDialog -and $null -eq $narrationCamera -and $null -eq $globalParameters) {
    continue
  }

  $kind = "NotificationRelated"
  if ($narrationDialog) {
    $kind = "NarrationDialog"
  } elseif ($notification -and $filterCategory) {
    $kind = "NotificationWithFilter"
  } elseif ($notification) {
    $kind = "Notification"
  } elseif ($globalParameters) {
    $kind = "NotificationParameters"
  } elseif ($filterCategory) {
    $kind = "NotificationFilterCategory"
  }

  $lineTexts = New-Object System.Collections.Generic.List[string]
  $lineSpeakers = New-Object System.Collections.Generic.List[string]
  $lineIndex = 0
  if ($narrationDialog) {
    foreach ($line in $narrationDialog.SelectNodes("Lines/Item/Content")) {
      $lineIndex++
      $textKey = Get-Text $line "Text"
      $speakerGuid = Get-Text $line "Speaker"
      $speakerName = Resolve-Name $speakerGuid
      if (-not [string]::IsNullOrWhiteSpace($textKey)) {
        if ((Test-GuidString $speakerGuid) -and $speakerGuid -ne "00000000-0000-0000-0000-000000000000" -and -not [string]::IsNullOrWhiteSpace($speakerName)) {
          $lineTexts.Add("$lineIndex=$textKey [$speakerName]")
        } else {
          $lineTexts.Add("$lineIndex=$textKey")
        }
      }
      if (-not [string]::IsNullOrWhiteSpace($speakerGuid) -and $speakerGuid -ne "00000000-0000-0000-0000-000000000000") {
        $lineSpeakers.Add($speakerName)
      }
    }
  }

  $defaultSpeakerGuid = Get-Text $narrationDialog "DefaultSpeaker"
  $announcementGuid = Get-Text $notification "Announcement"
  $filterGuid = Get-Text $notification "FilterCategory"
  $references = Get-EntityReferenceSources $entity.Guid
  $textKeys = Get-LocalizationLikeValues $values

  $notificationNarrationRows.Add([pscustomobject]@{
    Package = $entity.Package
    Kind = $kind
    Name = $entity.DisplayName
    Entity = $entity.EntityName
    MessageKey = Get-Text $notification "Message"
    TooltipKey = Get-Text $notification "Tooltip"
    CoopMessageKey = Get-Text $notification "CoopMessage"
    DialogType = Get-Text $narrationDialog "Type"
    DialogLines = Join-OrderedUnique $lineTexts
    DefaultSpeaker = Resolve-Name $defaultSpeakerGuid
    LineSpeakers = Join-Unique $lineSpeakers
    Flags = Get-Text $notification "Flags"
    Icon = Get-Text $notification "Icon"
    LifeTime = Get-Text $notification "LifeTime"
    Sound = Get-Text $notification "Sound"
    Music = Get-Text $notification "Music"
    Announcement = Resolve-Name $announcementGuid
    FilterCategory = Resolve-Name $filterGuid
    FilterDuration = Get-Text $filterCategory "Duration"
    FilterRadius = Get-Text $filterCategory "Radius"
    Camera = if ($narrationCamera) { "True" } else { "False" }
    ReferenceCount = $references.Count
    ReferencedBy = Join-Unique $references
    LocalizationKeys = Join-Unique $textKeys
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$notificationNarrationRows = @($notificationNarrationRows | Sort-Object Package,File,Kind,Name)

$resourceRows = New-Object System.Collections.Generic.List[object]
$resourceUsageRows = New-Object System.Collections.Generic.List[object]
$recipeResourceUsageRows = New-Object System.Collections.Generic.List[object]

$terrainPropRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "TerrainProp" }) {
  $terrainProp = Get-ChildNode $entity.Node.Values "TerrainProp"
  $categoryTags = Get-ResolvedLeafReferencesForXPath $terrainProp "PropCategoryTags/Item/Content/CategoryTag"
  $allowedSediments = Get-ResolvedLeafReferencesForXPath $terrainProp "AllowedSedimentTags/Item/Content/SedimentTag"
  $forbiddenSediments = Get-ResolvedLeafReferencesForXPath $terrainProp "ForbiddenSedimentTags/Item/Content/SedimentTag"

  $terrainPropRows.Add([pscustomobject]@{
    Package = $entity.Package
    TerrainProp = $entity.DisplayName
    Entity = $entity.EntityName
    Prefab = Get-Text $terrainProp "Prefab"
    BlockingType = Get-Text $terrainProp "BlockingType"
    UsePrefabBlocking = Get-Text $terrainProp "UsePrefabBlocking"
    CategoryTags = Join-Unique $categoryTags
    AllowedSediments = Join-Unique $allowedSediments
    ForbiddenSediments = Join-Unique $forbiddenSediments
    SpacingRadius = Get-Text $terrainProp "SpacingRadius"
    MinMaxScale = Get-Text $terrainProp "MinMaxScale"
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$depositResourceTypeRows = New-Object System.Collections.Generic.List[object]
$depositRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "DepositResourceType" }) {
  $depositType = Get-ChildNode $entity.Node.Values "DepositResourceType"
  $generated = Get-ChildNode $entity.Node.Values "GeneratedDeposit"
  $subTypes = Get-ResolvedLeafReferencesForXPath $depositType "SubDepositResourceTypes/Item/Content/DepositResourceType"
  $forestWeights = Get-ResolvedLeafReferencesForXPath $generated "ForestWeights/Item/Content/ForestTag"
  $sedimentWeights = Get-ResolvedLeafReferencesForXPath $generated "SedimentWeights/Item/Content/SedimentTag"

  $depositResourceTypeRows.Add([pscustomobject]@{
    Package = $entity.Package
    DepositResourceType = $entity.DisplayName
    Entity = $entity.EntityName
    NameKey = Get-Text $depositType "Name"
    DescriptionKey = Get-Text $depositType "Description"
    Category = Resolve-Name (Get-Text $depositType "Category")
    Icon = Get-Text $depositType "Icon"
    MinimapColor = Get-Text $depositType "MinimapColor"
    DiscoveredNotification = Resolve-Name (Get-Text $depositType "DiscoveredNotification")
    ExplorerDiscoverEvent = Resolve-Name (Get-Text $depositType "ExplorerDiscoverEvent")
    SubDepositTypes = Join-Unique $subTypes
    SortOrder = Get-Text $depositType "SortOrder"
    GeneratedChance = Get-Text $generated "Chance"
    MapGenChanceMin = Get-Text $generated "MapGenChanceMin"
    MapGenChanceMax = Get-Text $generated "MapGenChanceMax"
    DefaultForestWeight = Get-Text $generated "DefaultForestWeight"
    ForestWeights = Join-Unique $forestWeights
    SedimentWeights = Join-Unique $sedimentWeights
    NpcTerritoryAmount = Get-Text $generated "NpcTerritoryAmount"
    NpcCharacteristic = Get-Text $generated "NpcCharacteristic"
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "Deposit" }) {
  $deposit = Get-ChildNode $entity.Node.Values "Deposit"
  $terrainDeposit = Get-ChildNode $entity.Node.Values "TerrainDeposit"
  $harvestResources = New-Object System.Collections.Generic.List[string]
  foreach ($content in $deposit.SelectNodes("HarvestResources/Item/Content")) {
    $resourceGuid = Get-Text $content "Resource"
    if ([string]::IsNullOrWhiteSpace($resourceGuid)) { continue }
    $amount = Get-Text $content "AmountPerSizeFactor"
    $resourceName = Resolve-Name $resourceGuid
    $harvestResources.Add((Format-AmountName $amount $resourceName))
    Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "DepositHarvestOutput" $amount "" "" "" $entity.DisplayName "Deposit harvest resource" $entity.Guid $entity.File
  }

  $harvestDeposits = Get-ResolvedLeafReferencesForXPath $deposit "HarvestDeposits/Item/Content/Deposit"
  $depositTags = Get-ResolvedLeafReferencesForXPath $deposit "DepositTags/Item/Content/DepositTag"
  $allowedSediments = Get-ResolvedLeafReferencesForXPath $terrainDeposit "AllowedSediments/Item/Content/SedimentTag"

  $depositRows.Add([pscustomobject]@{
    Package = $entity.Package
    Deposit = $entity.DisplayName
    Entity = $entity.EntityName
    DepositResourceType = Resolve-Name (Get-Text $deposit "DepositResourceType")
    HarvestResources = Join-OrderedUnique $harvestResources
    HarvestDeposits = Join-Unique $harvestDeposits
    Prefab = Get-Text $deposit "Prefab"
    VisibleOnMinimap = Get-Text $deposit "VisibleOnMinimap"
    DepositTags = Join-Unique $depositTags
    AllowedSediments = Join-Unique $allowedSediments
    HasTerrainDeposit = [string](Test-ValueType $entity "TerrainDeposit")
    HasGrowing = [string](Test-ValueType $entity "GrowingDeposit")
    HasRegrowing = [string](Test-ValueType $entity "RegrowingDeposit")
    HasFixedAmount = [string](Test-ValueType $entity "FixedAmountDeposit")
    HasObstacle = [string](Test-ValueType $entity "ObstacleDeposit")
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$mapGenRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities) {
  $entityValues = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $entityValues) { continue }
  $mapGenComponents = @($entityValues.ChildNodes | ForEach-Object { $_.LocalName } | Where-Object { $_ -match "^(MapGen|UiMapGen).*|^ForestTag$" })
  if ($mapGenComponents.Count -eq 0) { continue }

  foreach ($componentName in $mapGenComponents) {
    $component = Get-ChildNode $entity.Node.Values $componentName
    if ($null -eq $component) { continue }
    $depositRefs = Get-ResolvedLeafReferencesForXPath $component ".//Deposit"
    $resourceRefs = Get-ResolvedLeafReferencesForXPath $component ".//Resource | .//DepositResource | .//DepositResourceType"
    $buildingRefs = Get-ResolvedLeafReferencesForXPath $component ".//Building | .//NpcBuilding | .//Buildable"
    $tagRefs = Get-ResolvedLeafReferencesForXPath $component ".//Tag | .//ForestTag | .//SedimentTag | .//DepositTag | .//ResourceTag"

    $mapGenRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Component = $componentName
      LocaTag = Get-Text $component "LocaTag"
      Deposits = Join-Unique $depositRefs
      Resources = Join-Unique $resourceRefs
      Buildings = Join-Unique $buildingRefs
      Tags = Join-Unique $tagRefs
      Values = Get-ScalarSummary $component @("LocaTag")
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

$npcUnitRows = New-Object System.Collections.Generic.List[object]
$npcBaseRows = New-Object System.Collections.Generic.List[object]
$encounterCombatRows = New-Object System.Collections.Generic.List[object]
$factionRows = New-Object System.Collections.Generic.List[object]

$encounterComponentPatterns = @(
  "^AspectEncounter",
  "^UnitEncounter",
  "^BuildingEncounter",
  "^UnitRaid",
  "^UnitDrop",
  "^Encounter",
  "^SpawnUnitsAbility$",
  "^EffectEncounter",
  "^LinkedToSummonerEffects$",
  "^AspectPatrollingNPCBoss$",
  "^AspectNPCCustomRaid$",
  "^Infection",
  "^EffectNPCBoss",
  "^TerritoryDefender",
  "^AspectWildAnimal",
  "^AspectGuardAnimal",
  "^AspectFarmAnimal",
  "^UniqueUnitSpawn$"
)

foreach ($entity in $entities) {
  $values = $entity.Node.SelectSingleNode("Values")
  if ($null -eq $values) { continue }

  $unit = Get-ChildNode $values "Unit"
  $building = Get-ChildNode $values "Building"
  $encounterComponents = Get-ValueTypeNames $entity $encounterComponentPatterns

  if ($unit -and ($entity.File -match "npcunits\.gd\.xml$" -or $encounterComponents.Count -gt 0)) {
    $npcUnitRows.Add([pscustomobject]@{
      Package = $entity.Package
      Unit = $entity.DisplayName
      Entity = $entity.EntityName
      NameKey = Get-Text $unit "Name"
      DescriptionKey = Get-Text $unit "Description"
      MaxSpeed = Get-Text $unit "MaxSpeed"
      Tags = Join-Unique (Get-ResolvedLeafReferencesForXPath $values "TaggedUnit/Tags/Item/Content/Tag | UnitTags/Item/Content/Tag")
      UnitClassTags = Join-Unique (Get-ResolvedLeafReferencesForXPath $values ".//UnitClassTag")
      EncounterTargetTags = Join-Unique (Get-ResolvedLeafReferencesForXPath $values ".//EncounterTargetTag")
      FactionOverride = Resolve-Name (Get-ComponentGuidValue (Get-ChildNode $values "UiFactionOverride | Unit/UiFactionOverride") "Faction")
      EncounterRefs = Join-Unique (Get-ResolvedLeafReferences $values @("Encounter|Target|Enemy|Faction|SubFaction|Ability|Notification|Deposit|Resource"))
      RaidValues = Get-ScalarSummary (Get-ChildNode $values "UnitRaidParameters") @() 20
      EncounterValues = Get-ScalarSummary (Get-ChildNode $values "UnitEncounterParameters") @() 20
      DropValues = Get-ScalarSummary (Get-ChildNode $values "UnitDropParameters") @() 20
      BossValues = Get-ScalarSummary (Get-ChildNode $values "AspectPatrollingNPCBoss") @() 20
      InfectionValues = Get-ScalarSummary (Get-ChildNode $values "InfectionChanceParameters") @() 20
      MinimapVisible = [string](Test-ValueType $entity "UiMinimapVisibleUnit")
      EncounterInitiator = [string](Test-ValueType $entity "AspectEncounterInitiator")
      EncounterNPCUnit = [string](Test-ValueType $entity "AspectEncounterNPCUnit")
      RaidCapable = [string](Test-ValueType $entity "UnitRaidParameters")
      Boss = [string](Test-ValueType $entity "AspectPatrollingNPCBoss")
      Drops = [string](Test-ValueType $entity "UnitDropParameters")
      Infection = [string](Test-ValueType $entity "InfectionChanceParameters")
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  if ($building -and ($entity.File -match "npcbases\.gd\.xml$" -or $encounterComponents.Count -gt 0)) {
    $npcBaseRows.Add([pscustomobject]@{
      Package = $entity.Package
      Base = $entity.DisplayName
      Entity = $entity.EntityName
      NameKey = Get-Text $building "Name"
      BuildingTags = Join-Unique (Get-ResolvedLeafReferencesForXPath $values "BuildingTag | Building/Tags/Item/Content/Tag")
      Faction = Resolve-Name (Get-ComponentGuidValue (Get-ChildNode $values "Faction") "Faction")
      SubFaction = Resolve-Name (Get-ComponentGuidValue (Get-ChildNode $values "SubFaction") "Faction")
      EncounterTargetTags = Join-Unique (Get-ResolvedLeafReferencesForXPath $values ".//EncounterTargetTag")
      SpawnUnits = Join-Unique (Get-ResolvedLeafReferencesForXPath $values ".//SpawnUnit//Unit | .//UniqueUnitSpawn//Unit | .//Unit")
      MapGenRefs = Join-Unique (Get-ResolvedLeafReferencesForXPath $values ".//SpawnLocations//Tag | .//AllowedSediment//SedimentTag | .//DepositResource | .//DepositResourceType")
      EncounterRefs = Join-Unique (Get-ResolvedLeafReferences $values @("Encounter|Target|Faction|SubFaction|Notification|Unit|Deposit|Resource|Building"))
      EncounterValues = Get-ScalarSummary (Get-ChildNode $values "BuildingEncounterParameters") @() 20
      WildAnimalValues = Get-ScalarSummary (Get-ChildNode $values "AspectWildAnimalNPCBase") @() 20
      InfectionValues = Join-Unique @(
        Get-ScalarSummary (Get-ChildNode $values "AspectInfectedAreaObject") @() 12
        Get-ScalarSummary (Get-ChildNode $values "AspectInfectedAreaSource") @() 12
      )
      EncounterInitiator = [string](Test-ValueType $entity "AspectEncounterInitiator")
      EncounterNPCBase = [string](Test-ValueType $entity "AspectEncounterNPCBase")
      CustomRaid = [string](Test-ValueType $entity "AspectNPCCustomRaid")
      WildAnimalBase = [string](Test-ValueType $entity "AspectWildAnimalNPCBase")
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  foreach ($componentName in $encounterComponents) {
    $component = Get-ChildNode $values $componentName
    if ($null -eq $component) { continue }
    $encounterCombatRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Kind = if ($unit) { "Unit" } elseif ($building) { "Building" } else { $entity.Kind }
      Component = $componentName
      References = Join-Unique (Get-ResolvedLeafReferences $component @(".*"))
      Units = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Unit | .//SpawnUnit//Unit")
      Buildings = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Building | .//NpcBuilding | .//Buildable")
      Resources = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Resource | .//DepositResource | .//DepositResourceType")
      Tags = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Tag | .//UnitTag | .//UnitClassTag | .//EncounterTargetTag")
      Notifications = Join-Unique (Get-ResolvedLeafReferencesForXPath $component ".//Notification | .//ObjectiveNotification")
      Values = Get-ScalarSummary $component @() 30
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }

  $factionComponents = @($values.ChildNodes | ForEach-Object { $_.LocalName } | Where-Object { $_ -in @("Faction","SubFaction","UiNpcFactionName","VisFactionColor","UiFactionOverride") })
  foreach ($componentName in $factionComponents) {
    $component = Get-ChildNode $values $componentName
    if ($null -eq $component) { continue }
    $factionRows.Add([pscustomobject]@{
      Package = $entity.Package
      Name = $entity.DisplayName
      Entity = $entity.EntityName
      Component = $componentName
      NameKey = Get-Text $component "Name"
      Faction = Resolve-Name (Get-ComponentGuidValue $component "Faction")
      SubFaction = Resolve-Name (Get-ComponentGuidValue $component "SubFaction")
      Color = Join-Unique @((Get-Text $component "Color"), (Get-Text $component "MinimapColor"))
      References = Join-Unique (Get-ResolvedLeafReferences $component @("Faction|SubFaction|Color|Tag"))
      Values = Get-ScalarSummary $component @("Name") 20
      Components = ($entity.ValueTypes -join ", ")
      Guid = $entity.Guid
      File = $entity.File
    })
  }
}

foreach ($entity in $entities | Where-Object { Get-ChildNode $_.Node.Values "ResourceDescription" }) {
  $resourceDescription = Get-ResourceDescription $entity

  $resourceRows.Add([pscustomobject]@{
    Package = $entity.Package
    Resource = $entity.DisplayName
    Entity = $entity.EntityName
    Category = Resolve-ResourceCategoryName $resourceDescription
    NameKey = Get-Text $resourceDescription "Name"
    NamePluralKey = Get-Text $resourceDescription "NamePlural"
    DescriptionKey = Get-Text $resourceDescription "Description"
    Icon = Get-Text $resourceDescription "Icon"
    CarryType = Get-Text $resourceDescription "CarryType"
    Mesh = Get-Text $resourceDescription "Mesh"
    UiDisplay = Get-Text $resourceDescription "UiDisplay"
    SortOrder = Get-Text $resourceDescription "SortOrder"
    WealthValue = Get-Text $resourceDescription "WealthValue"
    StealValue = Get-Text $resourceDescription "StealValue"
    Tags = Join-Unique (Get-ResourceTags $resourceDescription)
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

function Get-ResourceAmountList($ParentNode, [string]$ContainerXPath) {
  $items = New-Object System.Collections.Generic.List[string]
  if ($null -eq $ParentNode) { return @() }

  foreach ($content in $ParentNode.SelectNodes($ContainerXPath)) {
    $resource = Get-Text $content "Resource"
    if ([string]::IsNullOrWhiteSpace($resource)) {
      $resource = Get-Text $content "Description"
    }
    if ([string]::IsNullOrWhiteSpace($resource)) { continue }

    $amount = Get-Text $content "Amount"
    $items.Add((Format-AmountName $amount (Resolve-Name $resource)))
  }

  return @($items)
}

$recipeRows = New-Object System.Collections.Generic.List[object]
$recipeMap = @{}

foreach ($entity in $entities | Where-Object { Get-ChildNode $_.Node.Values "ProductionRecipe" }) {
  $recipe = Get-ChildNode $entity.Node.Values "ProductionRecipe"
  $inputs = New-Object System.Collections.Generic.List[string]
  $outputs = New-Object System.Collections.Generic.List[string]
  $workLoops = 0
  $workSteps = 0
  $stepTypes = New-Object System.Collections.Generic.List[string]

  foreach ($step in $recipe.SelectNodes("ProductionSteps/Item/Content")) {
    $type = Get-Text $step "Type"
    if (-not [string]::IsNullOrWhiteSpace($type)) {
      $stepTypes.Add($type)
    }

    $resourceGuid = Get-Text $step "InputOutput/Resource"
    $amount = Get-Text $step "InputOutput/Amount"
    if (-not [string]::IsNullOrWhiteSpace($resourceGuid)) {
      $label = Format-AmountName $amount (Resolve-Name $resourceGuid)
      if ($type -eq "Input") {
        $inputs.Add($label)
        Add-ResourceUsage $recipeResourceUsageRows $entity.Package $resourceGuid "RecipeInput" $amount "" $entity.DisplayName "" (Get-Text $recipe "RecipeIdentifier") "ProductionRecipe input step" $entity.Guid $entity.File
      } elseif ($type -eq "Output") {
        $outputs.Add($label)
        Add-ResourceUsage $recipeResourceUsageRows $entity.Package $resourceGuid "RecipeOutput" $amount "" $entity.DisplayName "" (Get-Text $recipe "RecipeIdentifier") "ProductionRecipe output step" $entity.Guid $entity.File
      }
    }

    if ($type -eq "Work") {
      $workSteps++
      $loopText = Get-Text $step "Work/LoopAnimationAmount"
      if ($loopText -match "^-?\d+(\.\d+)?$") {
        $workLoops += [double]$loopText
      }
    }
  }

  $row = [pscustomobject]@{
    Package = $entity.Package
    Recipe = $entity.DisplayName
    Entity = $entity.EntityName
    Identifier = Get-Text $recipe "RecipeIdentifier"
    DefaultState = Get-Text $recipe "DefaultState"
    Inputs = (($inputs | Sort-Object -Unique) -join "; ")
    Outputs = (($outputs | Sort-Object -Unique) -join "; ")
    WorkSteps = [string]$workSteps
    WorkLoops = [string]$workLoops
    StepTypes = (($stepTypes | Sort-Object -Unique) -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  }

  $recipeRows.Add($row)
  $recipeMap[$entity.Guid] = $row
}

$buildingRows = New-Object System.Collections.Generic.List[object]
$buildingProductionRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Get-ChildNode $_.Node.Values "Building" }) {
  $values = $entity.Node.Values
  $building = Get-ChildNode $values "Building"
  $buildable = Get-ChildNode $values "Buildable"
  $buildup = Get-ChildNode $values "AspectBuildup"
  $production = Get-ChildNode $values "AspectProduction"

  $constructionCosts = Get-ResourceAmountList $buildup "Costs/Item/Content"
  $builderUnit = Resolve-Name (Get-Text $buildup "Employment/Unit")
  $builderAmount = Get-Text $buildup "Employment/Amount"

  $recipeLabels = New-Object System.Collections.Generic.List[string]
  if ($production) {
    foreach ($recipeGuidNode in $production.SelectNodes("Recipes/Item/Content/Recipe")) {
      $recipeGuid = ([string]$recipeGuidNode.InnerText).Trim()
      $recipeName = Resolve-Name $recipeGuid
      $recipeLabels.Add($recipeName)

      $recipeRow = $recipeMap[$recipeGuid]
      $prodWorker = Resolve-Name (Get-Text $production "Employment/Unit")
      $prodAmount = Get-Text $production "Employment/Amount"
      $time = Get-Text $production "Efficiency/TimeOfOptimalWorkStep"

      $buildingProductionRows.Add([pscustomobject]@{
        Package = $entity.Package
        Building = $entity.DisplayName
        ProductionWorker = (Format-AmountName $prodAmount $prodWorker)
        OptimalWorkStep = $time
        Recipe = $recipeName
        RecipeIdentifier = if ($recipeRow) { $recipeRow.Identifier } else { "" }
        Inputs = if ($recipeRow) { $recipeRow.Inputs } else { "" }
        Outputs = if ($recipeRow) { $recipeRow.Outputs } else { "" }
        RecipeWorkLoops = if ($recipeRow) { $recipeRow.WorkLoops } else { "" }
        BuildingGuid = $entity.Guid
        RecipeGuid = $recipeGuid
      })
    }
  }

  $gatherOutputs = New-Object System.Collections.Generic.List[string]
  foreach ($gather in $values.SelectNodes("AspectGatherer/ResourceToGather/Item/Content")) {
    $gatherResource = Get-Text $gather "GatherResource"
    if (-not [string]::IsNullOrWhiteSpace($gatherResource)) {
      $gatherOutputs.Add((Resolve-Name $gatherResource))
      Add-ResourceUsage $resourceUsageRows $entity.Package $gatherResource "GatherOutput" "" $entity.DisplayName "" "" "AspectGatherer" "GatherResource" $entity.Guid $entity.File
    }
  }

  if ($buildup) {
    foreach ($cost in $buildup.SelectNodes("Costs/Item/Content")) {
      $resourceGuid = Get-Text $cost "Resource"
      $amount = Get-Text $cost "Amount"
      $locator = Get-Text $cost "PileLocator"
      Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "ConstructionCost" $amount $entity.DisplayName "" "" "AspectBuildup" $locator $entity.Guid $entity.File
    }
  }

  foreach ($pile in $values.SelectNodes("AspectPiles/Piles/Item/Content")) {
    $usage = Get-Text $pile "Usage"
    $positionType = Get-Text $pile "PositionType"
    $maxCapacity = Get-Text $pile "MaxCapacity"
    $pileLocator = Get-Text $pile "PileLocator"
    $isObjectiveStorage = Get-Text $pile "IsObjectiveStorage"
    $details = Join-Unique @(
      if ($usage) { "Usage=$usage" }
      if ($positionType) { "Position=$positionType" }
      if ($maxCapacity) { "MaxCapacity=$maxCapacity" }
      if ($pileLocator) { "PileLocator=$pileLocator" }
      if ($isObjectiveStorage) { "ObjectiveStorage=$isObjectiveStorage" }
    )

    foreach ($resourceNode in $pile.SelectNodes("Resources/Item/Content/Description | Resources/Item/Content/Resource")) {
      $resourceGuid = ([string]$resourceNode.InnerText).Trim()
      Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "StoragePile" "" $entity.DisplayName "" "" "AspectPiles" $details $entity.Guid $entity.File
    }
  }

  $buildingRows.Add([pscustomobject]@{
    Package = $entity.Package
    Building = $entity.DisplayName
    Category = Resolve-Name (Get-Text $buildable "Category")
    UiGroup = Get-Text $buildable "UiBuildingGroup"
    ConstructionCosts = (($constructionCosts | Sort-Object -Unique) -join "; ")
    Builder = (Format-AmountName $builderAmount $builderUnit)
    ProductionRecipes = (($recipeLabels | Sort-Object -Unique) -join "; ")
    GatherOutputs = (($gatherOutputs | Sort-Object -Unique) -join "; ")
    OptimalWorkStep = Get-Text $production "Efficiency/TimeOfOptimalWorkStep"
    Guid = $entity.Guid
    File = $entity.File
  })
}

$unitRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Get-ChildNode $_.Node.Values "Unit" }) {
  $values = $entity.Node.Values
  $recruitmentCost = Get-ChildNode $values "RecruitmentCost"
  $costs = Get-ResourceAmountList $recruitmentCost "ResourceCosts/Item/Content"
  $tags = New-Object System.Collections.Generic.List[string]
  foreach ($tagNode in $values.SelectNodes("TaggedUnit/Tags/Item/Content/Tag | UnitTags/Item/Content/Tag")) {
    $tagGuid = ([string]$tagNode.InnerText).Trim()
    if (-not [string]::IsNullOrWhiteSpace($tagGuid)) {
      $tags.Add((Resolve-Name $tagGuid))
    }
  }

  if ($recruitmentCost) {
    foreach ($cost in $recruitmentCost.SelectNodes("ResourceCosts/Item/Content")) {
      $resourceGuid = Get-Text $cost "Resource"
      $amount = Get-Text $cost "Amount"
      Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "RecruitmentCost" $amount "" "" $entity.DisplayName "RecruitmentCost" "" $entity.Guid $entity.File
    }
  }

  $unitRows.Add([pscustomobject]@{
    Package = $entity.Package
    Unit = $entity.DisplayName
    RecruitmentCosts = (($costs | Sort-Object -Unique) -join "; ")
    NeedsManualRecruitment = Get-Text $recruitmentCost "NeedsManualRecruitment"
    SourceRecruitableUnit = Resolve-Name (Get-Text $recruitmentCost "SourceRecruitableUnit")
    Tags = (($tags | Sort-Object -Unique) -join "; ")
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$artifactRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Get-ChildNode $_.Node.Values "ResourceDescription" }) {
  $resourceDescription = Get-ResourceDescription $entity
  $combatBoost = Get-ChildNode $entity.Node.Values "CombatBoostArtifact"
  $category = Resolve-ResourceCategoryName $resourceDescription
  $isArtifactCategory = $category -eq "Artifacts"
  $isArtifactLike = $isArtifactCategory -or (Test-ValueType $entity "CombatBoostArtifact") -or $entity.EntityName -match "Artifact"

  if (-not $isArtifactLike) { continue }

  $artifactRows.Add([pscustomobject]@{
    Package = $entity.Package
    Artifact = $entity.DisplayName
    Entity = $entity.EntityName
    Category = $category
    NameKey = Get-Text $resourceDescription "Name"
    DescriptionKey = Get-Text $resourceDescription "Description"
    Icon = Get-Text $resourceDescription "Icon"
    CarryType = Get-Text $resourceDescription "CarryType"
    WealthValue = Get-Text $resourceDescription "WealthValue"
    StealValue = Get-Text $resourceDescription "StealValue"
    TreasureAudio = Get-Text $resourceDescription "TreasureHunterAudioDiscoverEvent"
    TreasureTier = Get-Text $resourceDescription "TreasureHunterNotificationTier"
    CombatBoost = Get-Text $combatBoost "Boost"
    CombatWeight = Get-Text $combatBoost "Weight"
    Tags = Join-Unique (Get-ResourceTags $resourceDescription)
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$treasureHunterRecipeRows = New-Object System.Collections.Generic.List[object]
$treasureHunterTargetRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "TreasureHunterRecipe" }) {
  $recipe = Get-ChildNode $entity.Node.Values "TreasureHunterRecipe"
  $targets = New-Object System.Collections.Generic.List[string]
  $targetCount = 0
  $combatBoostTargets = 0
  $artifactTargets = 0

  foreach ($resourceNode in $recipe.SelectNodes("TargetResources/Item/Content/Resource")) {
    $resourceGuid = ([string]$resourceNode.InnerText).Trim()
    if ([string]::IsNullOrWhiteSpace($resourceGuid)) { continue }
    $target = if ($defs.ContainsKey($resourceGuid)) { $defs[$resourceGuid] } else { $null }
    $targetName = Resolve-Name $resourceGuid
    $targetCategory = if ($target) { Resolve-ResourceCategoryName (Get-ResourceDescription $target) } else { "" }
    $isCombatBoost = if ($target) { Test-ValueType $target "CombatBoostArtifact" } else { $false }
    $isArtifactCategory = $targetCategory -eq "Artifacts"

    $targetCount++
    if ($isCombatBoost) { $combatBoostTargets++ }
    if ($isArtifactCategory) { $artifactTargets++ }
    $targets.Add($targetName)

    $treasureHunterTargetRows.Add([pscustomobject]@{
      Package = $entity.Package
      Recipe = $entity.DisplayName
      RecipeNameKey = Get-Text $recipe "Name"
      Target = $targetName
      TargetCategory = $targetCategory
      IsArtifactCategory = [string]$isArtifactCategory
      IsCombatBoostArtifact = [string]$isCombatBoost
      TargetGuid = $resourceGuid
      RecipeGuid = $entity.Guid
      File = $entity.File
    })

    Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "TreasureHunterTarget" "" "" $entity.DisplayName "" (Get-Text $recipe "Name") "TreasureHunterRecipe target resource" $entity.Guid $entity.File
  }

  $treasureHunterRecipeRows.Add([pscustomobject]@{
    Package = $entity.Package
    Recipe = $entity.DisplayName
    NameKey = Get-Text $recipe "Name"
    SortOrder = Get-Text $recipe "SortOrder"
    DefaultEnabled = Get-Text $recipe "DefaultEnabled"
    AllowSubSelection = Get-Text $recipe "AllowSubSelection"
    TargetCount = [string]$targetCount
    ArtifactTargets = [string]$artifactTargets
    CombatBoostTargets = [string]$combatBoostTargets
    Targets = Join-Unique $targets
    Guid = $entity.Guid
    File = $entity.File
  })
}

$treasureAreaRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "AspectTreasureArea" }) {
  $values = $entity.Node.Values
  $building = Get-ChildNode $values "Building"

  $treasureAreaRows.Add([pscustomobject]@{
    Package = $entity.Package
    TreasureArea = $entity.DisplayName
    Entity = $entity.EntityName
    BuildingNameKey = Get-Text $building "Name"
    Icon = Get-Text $building "Icon"
    Category = Resolve-Name (Get-Text (Get-ChildNode $values "Buildable") "Category")
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$shrineAbilityRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "ShrineAbility" }) {
  $ability = Get-ChildNode $entity.Node.Values "ShrineAbility"
  $specificTypes = @($entity.ValueTypes | Where-Object { $_ -ne "ShrineAbility" })
  $resourceCosts = Get-ResourceAmountList $ability "ResourceCosts/Item/Content"

  $shrineAbilityRows.Add([pscustomobject]@{
    Package = $entity.Package
    Ability = $entity.DisplayName
    NameKey = Get-Text $ability "Name"
    DescriptionKey = Get-Text $ability "Description"
    Type = Get-Text $ability "Type"
    UsageType = Get-Text $ability "UsageType"
    ManaCost = Get-Text $ability "ManaCost"
    Cooldown = Get-Text $ability "Cooldown"
    TargetRange = Get-Text $ability "TargetRange"
    ResourceCosts = Join-Unique $resourceCosts
    SpecificComponents = ($specificTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

$shrineRecipeRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "ShrineRecipe" }) {
  $recipe = Get-ChildNode $entity.Node.Values "ShrineRecipe"
  $inputs = Get-RecipeInputResources $recipe
  $stepTypes = @($recipe.SelectNodes("ProductionSteps/Item/Content/*") | ForEach-Object { $_.LocalName } | Where-Object { $_ } | Sort-Object -Unique)
  $workerSlots = @($recipe.SelectNodes("ProductionSteps/Item/Content/AssignedWorker") | ForEach-Object { ([string]$_.InnerText).Trim() } | Where-Object { $_ } | Sort-Object -Unique)
  $locators = @($recipe.SelectNodes("ProductionSteps/Item/Content/TargetLocator") | ForEach-Object { ([string]$_.InnerText).Trim() } | Where-Object { $_ } | Sort-Object -Unique)

  $shrineRecipeRows.Add([pscustomobject]@{
    Package = $entity.Package
    Recipe = $entity.DisplayName
    Identifier = Get-Text $recipe "RecipeIdentifier"
    Inputs = Join-Unique $inputs
    StepTypes = ($stepTypes -join ", ")
    WorkerSlots = ($workerSlots -join ", ")
    Locators = ($locators -join ", ")
    IsAbstract = [string](([string]$entity.Node.IsAbstract) -eq "true")
    Guid = $entity.Guid
    File = $entity.File
  })

  foreach ($resourceNode in $recipe.SelectNodes("ProductionSteps/Item/Content/Input/Resource")) {
    $resourceGuid = ([string]$resourceNode.InnerText).Trim()
    Add-ResourceUsage $resourceUsageRows $entity.Package $resourceGuid "ShrineRecipeInput" "" "" $entity.DisplayName "" (Get-Text $recipe "RecipeIdentifier") "ShrineRecipe input resource" $entity.Guid $entity.File
  }
}

$shrineBuildingRows = New-Object System.Collections.Generic.List[object]

foreach ($entity in $entities | Where-Object { Test-ValueType $_ "AspectShrine" }) {
  $values = $entity.Node.Values
  $building = Get-ChildNode $values "Building"
  $shrine = Get-ChildNode $values "AspectShrine"
  $abilities = Get-ResourceReferences $shrine "Abilities/Item/Content/Ability"
  $recipes = Get-ResourceReferences $shrine "ManaProduction/Recipes/Item/Content/Recipe"
  $manaResources = Get-ResourceReferences $shrine "ManaProduction/ManaPiles/Item/Content/Resource | ManaProduction/ManaPiles/Item/Content/Refund/Resource"
  $worker = Resolve-Name (Get-Text $shrine "Employment/Unit")
  $workerAmount = Get-Text $shrine "Employment/Amount"
  $secondaryWorker = Resolve-Name (Get-Text $shrine "Employment/SecondaryUnit")

  $shrineBuildingRows.Add([pscustomobject]@{
    Package = $entity.Package
    Shrine = $entity.DisplayName
    BuildingNameKey = Get-Text $building "Name"
    Worker = Format-AmountName $workerAmount $worker
    SecondaryWorker = $secondaryWorker
    Abilities = Join-Unique $abilities
    ManaRecipes = Join-Unique $recipes
    ManaResources = Join-Unique $manaResources
    Components = ($entity.ValueTypes -join ", ")
    Guid = $entity.Guid
    File = $entity.File
  })
}

function New-SystemGraphLines($TreasureRecipes, $TreasureTargets, $ShrineBuildings) {
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("flowchart LR")
  $lines.Add('  TreasureHunter["Treasure Hunter"]')
  $lines.Add('  ShrineSystem["Shrine / Sanctuary"]')

  foreach ($recipe in $TreasureRecipes | Sort-Object Recipe) {
    $recipeId = Format-MermaidId "treasure_recipe_$($recipe.Guid)"
    $lines.Add("  TreasureHunter --> $recipeId{{""$((Format-MermaidLabel $recipe.Recipe))""}}")
  }

  foreach ($target in $TreasureTargets | Where-Object { $_.IsArtifactCategory -eq "True" -or $_.IsCombatBoostArtifact -eq "True" } | Sort-Object Recipe,Target) {
    $recipeId = Format-MermaidId "treasure_recipe_$($target.RecipeGuid)"
    $targetId = Format-MermaidId "treasure_target_$($target.TargetGuid)"
    $lines.Add("  $recipeId --> $targetId[""$((Format-MermaidLabel $target.Target))""]")
  }

  foreach ($shrine in $ShrineBuildings | Sort-Object Shrine) {
    $shrineId = Format-MermaidId "shrine_$($shrine.Guid)"
    $lines.Add("  ShrineSystem --> $shrineId[""$((Format-MermaidLabel $shrine.Shrine))""]")
    foreach ($abilityName in (($shrine.Abilities -split "; ") | Where-Object { $_ })) {
      $abilityId = Format-MermaidId "ability_$abilityName"
      $lines.Add("  $shrineId --> $abilityId{{""$((Format-MermaidLabel $abilityName))""}}")
    }
  }

  return $lines
}

$buildingRows = @($buildingRows | Sort-Object Package,Building)
$buildingProductionRows = @($buildingProductionRows | Sort-Object Package,Building,Recipe)
$recipeRows = @($recipeRows | Sort-Object Package,Recipe)
$unitRows = @($unitRows | Sort-Object Package,Unit)
$resourceRows = @($resourceRows | Sort-Object Package,Resource)
$artifactRows = @($artifactRows | Sort-Object Package,Artifact)
$treasureHunterRecipeRows = @($treasureHunterRecipeRows | Sort-Object Package,Recipe)
$treasureHunterTargetRows = @($treasureHunterTargetRows | Sort-Object Package,Recipe,Target)
$treasureAreaRows = @($treasureAreaRows | Sort-Object Package,TreasureArea)
$shrineAbilityRows = @($shrineAbilityRows | Sort-Object Package,Ability)
$shrineRecipeRows = @($shrineRecipeRows | Sort-Object Package,Recipe)
$shrineBuildingRows = @($shrineBuildingRows | Sort-Object Package,Shrine)
$terrainPropRows = @($terrainPropRows | Sort-Object Package,TerrainProp)
$depositResourceTypeRows = @($depositResourceTypeRows | Sort-Object Package,DepositResourceType)
$depositRows = @($depositRows | Sort-Object Package,Deposit)
$mapGenRows = @($mapGenRows | Sort-Object Package,File,Component,Name)
$npcUnitRows = @($npcUnitRows | Sort-Object Package,Unit)
$npcBaseRows = @($npcBaseRows | Sort-Object Package,Base)
$encounterCombatRows = @($encounterCombatRows | Sort-Object Package,File,Name,Component)
$factionRows = @($factionRows | Sort-Object Package,Name,Component)

foreach ($usage in $recipeResourceUsageRows) {
  $linkedBuildings = @($buildingProductionRows | Where-Object { $_.Recipe -eq $usage.Recipe -or $_.RecipeGuid -eq $usage.SourceGuid })
  if ($linkedBuildings.Count -eq 0) {
    $resourceUsageRows.Add($usage)
    continue
  }

  foreach ($link in $linkedBuildings) {
    Add-ResourceUsage $resourceUsageRows $usage.Package $usage.ResourceGuid $usage.UsageType $usage.Amount $link.Building $usage.Recipe "" $usage.Context $usage.Details $usage.SourceGuid $usage.File
  }
}

$resourceUsageRows = @($resourceUsageRows | Sort-Object Resource,UsageType,Building,Recipe,Unit)

$resourceFlowRows = New-Object System.Collections.Generic.List[object]
foreach ($resource in $resourceRows) {
  $uses = @($resourceUsageRows | Where-Object { $_.ResourceGuid -eq $resource.Guid })

  $resourceFlowRows.Add([pscustomobject]@{
    Package = $resource.Package
    Resource = $resource.Resource
    Category = $resource.Category
    CarryType = $resource.CarryType
    ProducedBy = Join-Unique @($uses | Where-Object { $_.UsageType -in @("RecipeOutput","GatherOutput","DepositHarvestOutput") } | ForEach-Object {
      if ($_.Building -and $_.Recipe) { "$($_.Building) -> $($_.Recipe)" }
      elseif ($_.Building) { $_.Building }
      elseif ($_.Recipe) { $_.Recipe }
      elseif ($_.Context) { "Deposit: $($_.Context)" }
    })
    ConsumedBy = Join-Unique @($uses | Where-Object { $_.UsageType -in @("RecipeInput","ShrineRecipeInput") } | ForEach-Object {
      if ($_.Building -and $_.Recipe) { "$($_.Building) -> $($_.Recipe)" }
      elseif ($_.Recipe) { $_.Recipe }
    })
    ConstructionCostFor = Join-Unique @($uses | Where-Object { $_.UsageType -eq "ConstructionCost" } | ForEach-Object { $_.Building })
    RecruitmentCostFor = Join-Unique @($uses | Where-Object { $_.UsageType -eq "RecruitmentCost" } | ForEach-Object { $_.Unit })
    StoredIn = Join-Unique @($uses | Where-Object { $_.UsageType -eq "StoragePile" } | ForEach-Object { $_.Building })
    TreasureTargets = Join-Unique @($uses | Where-Object { $_.UsageType -eq "TreasureHunterTarget" } | ForEach-Object { $_.Recipe })
    UsageCount = [string]$uses.Count
    Guid = $resource.Guid
    File = $resource.File
  })
}
$resourceFlowRows = @($resourceFlowRows | Sort-Object Package,Resource)

function New-BuildingDependencyMatrixRows() {
  $rows = New-Object System.Collections.Generic.List[object]

  foreach ($building in $buildingRows) {
    $productionLinks = @($buildingProductionRows | Where-Object { $_.BuildingGuid -eq $building.Guid -or ($_.Building -eq $building.Building -and $_.Package -eq $building.Package) })
    $usageRows = @($resourceUsageRows | Where-Object { $_.Building -eq $building.Building -and $_.Package -eq $building.Package })
    $storageResources = Join-Unique @($usageRows | Where-Object { $_.UsageType -eq "StoragePile" } | ForEach-Object { $_.Resource })
    $constructionResources = Join-Unique @($usageRows | Where-Object { $_.UsageType -eq "ConstructionCost" } | ForEach-Object {
      if ([string]::IsNullOrWhiteSpace($_.Amount)) { $_.Resource } else { "$($_.Amount) $($_.Resource)" }
    })
    $recipeInputs = Join-Unique @($productionLinks | ForEach-Object { $_.Inputs -split "; " })
    $recipeOutputs = Join-Unique @($productionLinks | ForEach-Object { $_.Outputs -split "; " })
    $recipeNames = Join-Unique @($productionLinks | ForEach-Object { $_.Recipe })
    $recipeIdentifiers = Join-Unique @($productionLinks | ForEach-Object { $_.RecipeIdentifier })
    $productionWorkers = Join-Unique @($productionLinks | ForEach-Object { $_.ProductionWorker })
    $workLoops = Join-Unique @($productionLinks | ForEach-Object {
      if (-not [string]::IsNullOrWhiteSpace($_.RecipeWorkLoops) -and -not [string]::IsNullOrWhiteSpace($_.Recipe)) {
        "$($_.Recipe)=$($_.RecipeWorkLoops)"
      }
    })

    $inputResourceNames = @($recipeInputs -split "; " | ForEach-Object { ($_ -replace "^\s*\d+(\.\d+)?\s+", "").Trim() } | Where-Object { $_ })
    $outputResourceNames = @($recipeOutputs -split "; " | ForEach-Object { ($_ -replace "^\s*\d+(\.\d+)?\s+", "").Trim() } | Where-Object { $_ })
    $constructionResourceNames = @($constructionResources -split "; " | ForEach-Object { ($_ -replace "^\s*\d+(\.\d+)?\s+", "").Trim() } | Where-Object { $_ })

    $dependencyResources = Join-Unique @($constructionResourceNames + $inputResourceNames)
    $providedResources = Join-Unique @($outputResourceNames + @($building.GatherOutputs -split "; " | Where-Object { $_ }))

    $rows.Add([pscustomobject]@{
      Package = $building.Package
      Building = $building.Building
      Category = $building.Category
      UiGroup = $building.UiGroup
      ConstructionCosts = if (-not [string]::IsNullOrWhiteSpace($constructionResources)) { $constructionResources } else { $building.ConstructionCosts }
      Builder = $building.Builder
      ProductionWorker = $productionWorkers
      Recipes = $recipeNames
      RecipeIdentifiers = $recipeIdentifiers
      RecipeInputs = $recipeInputs
      RecipeOutputs = $recipeOutputs
      GatherOutputs = $building.GatherOutputs
      StorageResources = $storageResources
      DependencyResources = $dependencyResources
      ProvidedResources = $providedResources
      OptimalWorkStep = $building.OptimalWorkStep
      RecipeWorkLoops = $workLoops
      HasProduction = [string]($productionLinks.Count -gt 0)
      HasGathering = [string](-not [string]::IsNullOrWhiteSpace($building.GatherOutputs))
      HasStorage = [string](-not [string]::IsNullOrWhiteSpace($storageResources))
      Guid = $building.Guid
      File = $building.File
    })
  }

  return @($rows | Sort-Object Package,Building)
}

function New-UnitEquipmentMatrixRows() {
  $rows = New-Object System.Collections.Generic.List[object]
  $recruitmentRows = @($resourceUsageRows | Where-Object { $_.UsageType -eq "RecruitmentCost" })

  foreach ($usage in $recruitmentRows) {
    $unit = @($unitRows | Where-Object { $_.Guid -eq $usage.SourceGuid } | Select-Object -First 1)
    if ($unit.Count -eq 0) {
      $unit = @($unitRows | Where-Object { $_.Unit -eq $usage.Unit -and $_.Package -eq $usage.Package } | Select-Object -First 1)
    }
    $unitRow = if ($unit.Count -gt 0) { $unit[0] } else { $null }
    $resource = if ($defs.ContainsKey($usage.ResourceGuid)) { $defs[$usage.ResourceGuid] } else { $null }
    $resourceDescription = if ($resource) { Get-ResourceDescription $resource } else { $null }
    $producers = @($resourceUsageRows | Where-Object { $_.ResourceGuid -eq $usage.ResourceGuid -and $_.UsageType -eq "RecipeOutput" })

    $producerLinks = Join-Unique @($producers | ForEach-Object {
      if ($_.Building -and $_.Recipe) { "$($_.Building) -> $($_.Recipe)" }
      elseif ($_.Recipe) { $_.Recipe }
    })
    $producerBuildings = Join-Unique @($producers | ForEach-Object { $_.Building })
    $producerRecipes = Join-Unique @($producers | ForEach-Object { $_.Recipe })
    $producerInputs = Join-Unique @($producers | ForEach-Object {
      $recipe = $recipeMap[$_.SourceGuid]
      if ($recipe) { $recipe.Inputs -split "; " }
    })
    $producerOutputs = Join-Unique @($producers | ForEach-Object {
      $recipe = $recipeMap[$_.SourceGuid]
      if ($recipe) { $recipe.Outputs -split "; " }
    })
    $chainSamples = Join-Unique @($productionChainRows | Where-Object {
      $_.EndType -eq "RecruitmentCost" -and
      $_.EndTarget -eq $usage.Unit -and
      ($_.Resources -split "; ") -contains $usage.Resource
    } | Select-Object -First 5 | ForEach-Object { $_.Chain })

    $rows.Add([pscustomobject]@{
      Package = $usage.Package
      Unit = $usage.Unit
      Equipment = $usage.Resource
      Amount = $usage.Amount
      EquipmentCategory = if ($resourceDescription) { Resolve-ResourceCategoryName $resourceDescription } else { "" }
      EquipmentCarryType = if ($resourceDescription) { Get-Text $resourceDescription "CarryType" } else { "" }
      ProducedByBuildings = $producerBuildings
      ProducedByRecipes = $producerRecipes
      ProducerLinks = $producerLinks
      ProducerInputs = $producerInputs
      ProducerOutputs = $producerOutputs
      ChainSamples = $chainSamples
      SourceRecruitableUnit = if ($unitRow) { $unitRow.SourceRecruitableUnit } else { "" }
      UnitTags = if ($unitRow) { $unitRow.Tags } else { "" }
      UnitGuid = if ($unitRow) { $unitRow.Guid } else { $usage.SourceGuid }
      EquipmentGuid = $usage.ResourceGuid
      File = $usage.File
    })
  }

  return @($rows | Sort-Object Package,Unit,Equipment)
}

function Add-MapListItem($Map, [string]$Key, $Value) {
  if ([string]::IsNullOrWhiteSpace($Key)) { return }
  if (-not $Map.ContainsKey($Key)) {
    $Map[$Key] = New-Object System.Collections.Generic.List[object]
  }
  $Map[$Key].Add($Value)
}

function Get-ProductionChainRecipeLabel([string]$RecipeGuid, [string]$RecipeName) {
  $buildingNames = Join-Unique @($buildingProductionRows | Where-Object { $_.RecipeGuid -eq $RecipeGuid } | ForEach-Object { $_.Building })
  if (-not [string]::IsNullOrWhiteSpace($buildingNames)) {
    return "$RecipeName [$buildingNames]"
  }
  return $RecipeName
}

function Add-ProductionChainRow(
  [System.Collections.Generic.List[object]]$Rows,
  [string]$Package,
  [string]$StartResource,
  [string]$EndType,
  [string]$EndTarget,
  [string]$EndAmount,
  [string[]]$Parts,
  [string[]]$ResourceNames,
  [string[]]$RecipeNames,
  [string[]]$BuildingNames,
  [string]$Notes
) {
  if ([string]::IsNullOrWhiteSpace($EndTarget)) { $EndTarget = $EndType }

  $Rows.Add([pscustomobject]@{
    Package = $Package
    StartResource = $StartResource
    EndType = $EndType
    EndTarget = $EndTarget
    EndAmount = $EndAmount
    Chain = ($Parts -join " -> ")
    ResourceCount = [string]@($ResourceNames | Sort-Object -Unique).Count
    RecipeCount = [string]@($RecipeNames | Sort-Object -Unique).Count
    Resources = Join-Unique $ResourceNames
    Recipes = Join-Unique $RecipeNames
    Buildings = Join-Unique $BuildingNames
    Notes = $Notes
  })
}

function New-ProductionChainRows([int]$MaxDepth = 10, [int]$MaxRows = 5000) {
  $rows = New-Object System.Collections.Generic.List[object]
  $resourceInputs = @{}
  $recipeOutputs = @{}
  $resourceSinks = @{}
  $resourceSources = @{}
  $seenRows = @{}

  foreach ($usage in $recipeResourceUsageRows) {
    if ($usage.UsageType -eq "RecipeInput") {
      Add-MapListItem $resourceInputs $usage.ResourceGuid $usage
    } elseif ($usage.UsageType -eq "RecipeOutput") {
      Add-MapListItem $recipeOutputs $usage.SourceGuid $usage
      Add-MapListItem $resourceSources $usage.ResourceGuid $usage
    }
  }

  foreach ($usage in $resourceUsageRows) {
    if ($usage.UsageType -eq "GatherOutput") {
      Add-MapListItem $resourceSources $usage.ResourceGuid $usage
    }

    if ($usage.UsageType -in @("ConstructionCost","RecruitmentCost","ShrineRecipeInput")) {
      Add-MapListItem $resourceSinks $usage.ResourceGuid $usage
    }
  }

  $startResources = @($resourceRows | Where-Object {
    $isInputOrSink = $resourceInputs.ContainsKey($_.Guid) -or $resourceSinks.ContainsKey($_.Guid)
    $isGathered = $false
    if ($resourceSources.ContainsKey($_.Guid)) {
      $isGathered = @($resourceSources[$_.Guid] | Where-Object { $_.UsageType -eq "GatherOutput" }).Count -gt 0
    }
    $hasRecipeProducer = $false
    if ($resourceSources.ContainsKey($_.Guid)) {
      $hasRecipeProducer = @($resourceSources[$_.Guid] | Where-Object { $_.UsageType -eq "RecipeOutput" }).Count -gt 0
    }

    $isInputOrSink -and ($isGathered -or -not $hasRecipeProducer)
  })

  function Trace-Resource(
    [object]$Resource,
    [string[]]$Parts,
    [string[]]$ResourceNames,
    [string[]]$RecipeNames,
    [string[]]$BuildingNames,
    [string[]]$VisitedRecipeGuids,
    [int]$Depth,
    [string]$StartResource,
    [string]$StartPackage
  ) {
    if ($rows.Count -ge $MaxRows) { return }
    if ($Depth -gt $MaxDepth) { return }

    if ($resourceSinks.ContainsKey($Resource.Guid)) {
      foreach ($sink in $resourceSinks[$Resource.Guid]) {
        $endTarget = ""
        if ($sink.UsageType -eq "ConstructionCost") { $endTarget = $sink.Building }
        elseif ($sink.UsageType -eq "RecruitmentCost") { $endTarget = $sink.Unit }
        elseif ($sink.UsageType -eq "ShrineRecipeInput") { $endTarget = $sink.Recipe }

        $sinkLabel = if ([string]::IsNullOrWhiteSpace($sink.Amount)) {
          "$($sink.UsageType): $endTarget"
        } else {
          "$($sink.UsageType): $endTarget ($($sink.Amount))"
        }
        $rowParts = @($Parts + $sinkLabel)
        $rowBuildings = @($BuildingNames)
        if (-not [string]::IsNullOrWhiteSpace($sink.Building)) { $rowBuildings += $sink.Building }

        $key = "$($StartResource)|$($sink.UsageType)|$endTarget|$($rowParts -join ' -> ')"
        if (-not $seenRows.ContainsKey($key)) {
          $seenRows[$key] = $true
          Add-ProductionChainRow $rows $StartPackage $StartResource $sink.UsageType $endTarget $sink.Amount $rowParts $ResourceNames $RecipeNames $rowBuildings ""
        }
      }
    }

    if (-not $resourceInputs.ContainsKey($Resource.Guid)) { return }

    foreach ($inputRef in $resourceInputs[$Resource.Guid]) {
      if ($VisitedRecipeGuids -contains $inputRef.SourceGuid) { continue }
      if ($rows.Count -ge $MaxRows) { return }

      $recipeLabel = Get-ProductionChainRecipeLabel $inputRef.SourceGuid $inputRef.Recipe
      $recipeBuildings = @($buildingProductionRows | Where-Object { $_.RecipeGuid -eq $inputRef.SourceGuid } | ForEach-Object { $_.Building })
      $nextParts = @($Parts + $recipeLabel)
      $nextRecipes = @($RecipeNames + $inputRef.Recipe)
      $nextBuildings = @($BuildingNames + $recipeBuildings)
      $nextVisited = @($VisitedRecipeGuids + $inputRef.SourceGuid)

      if (-not $recipeOutputs.ContainsKey($inputRef.SourceGuid)) { continue }

      foreach ($output in $recipeOutputs[$inputRef.SourceGuid]) {
        $outputResource = $defs[$output.ResourceGuid]
        if ($null -eq $outputResource) { continue }
        $outputName = Resolve-Name $output.ResourceGuid
        $amountLabel = if ([string]::IsNullOrWhiteSpace($output.Amount)) { $outputName } else { "$($output.Amount) $outputName" }
        Trace-Resource $outputResource @($nextParts + $amountLabel) @($ResourceNames + $outputName) $nextRecipes $nextBuildings $nextVisited ($Depth + 1) $StartResource $StartPackage
      }
    }
  }

  foreach ($resource in $startResources) {
    if ($rows.Count -ge $MaxRows) { break }
    Trace-Resource $resource @($resource.Resource) @($resource.Resource) @() @() @() 0 $resource.Resource $resource.Package
  }

  return @($rows | Sort-Object StartResource,EndType,EndTarget,Chain)
}

function New-ProductionChainGraphLines($ChainRows, [int]$Limit) {
  $lines = New-Object System.Collections.Generic.List[string]
  $lines.Add("flowchart LR")
  $edgeCount = 0
  $seenEdges = @{}

  foreach ($row in $ChainRows) {
    if ($edgeCount -ge $Limit) { break }
    $parts = @($row.Chain -split " -> " | Where-Object { $_ })
    for ($i = 0; $i -lt ($parts.Count - 1); $i++) {
      if ($edgeCount -ge $Limit) { break }
      $from = [string]$parts[$i]
      $to = [string]$parts[$i + 1]
      $fromId = Format-MermaidId $from
      $toId = Format-MermaidId $to
      $edge = "$fromId|$toId"
      if ($seenEdges.ContainsKey($edge)) { continue }

      $lines.Add("  $fromId[""$((Format-MermaidLabel $from))""] --> $toId[""$((Format-MermaidLabel $to))""]")
      $seenEdges[$edge] = $true
      $edgeCount++
    }
  }

  return [pscustomobject]@{
    Lines = $lines
    EdgeCount = $edgeCount
  }
}

function Write-ProductionChainGraph($ChainRows, [string]$Directory, [string]$BaseName, [string]$Title, [string]$Intro, [int]$Limit) {
  New-Item -ItemType Directory -Force -Path $Directory | Out-Null
  $graph = New-ProductionChainGraphLines $ChainRows $Limit
  [System.IO.File]::WriteAllLines((Join-Path $Directory "$BaseName.mmd"), $graph.Lines, [System.Text.UTF8Encoding]::new($false))

  $graphMd = New-Object System.Collections.Generic.List[string]
  $graphMd.Add("# $Title")
  $graphMd.Add("")
  $graphMd.Add($Intro)
  $graphMd.Add("")
  if ($graph.EdgeCount -ge $Limit) {
    $graphMd.Add("This graph was limited to $Limit edges. Increase ``-GraphEdgeLimit`` when running ``scripts\generate_catalog.ps1`` if needed.")
    $graphMd.Add("")
  }
  $graphMd.Add('```mermaid')
  $graphMd.AddRange($graph.Lines)
  $graphMd.Add('```')
  [System.IO.File]::WriteAllLines((Join-Path $Directory "$BaseName.md"), $graphMd, [System.Text.UTF8Encoding]::new($false))

  return $graph.EdgeCount
}

$productionChainRows = New-ProductionChainRows
$productionChainColumns = @("Package","StartResource","EndType","EndTarget","EndAmount","Chain","ResourceCount","RecipeCount","Resources","Recipes","Buildings","Notes")
$buildingDependencyRows = New-BuildingDependencyMatrixRows
$unitEquipmentRows = New-UnitEquipmentMatrixRows

$buildingColumns = @("Package","Building","Category","UiGroup","ConstructionCosts","Builder","ProductionRecipes","GatherOutputs","OptimalWorkStep","Guid")
$buildingDependencyColumns = @("Package","Building","Category","UiGroup","ConstructionCosts","Builder","ProductionWorker","Recipes","RecipeIdentifiers","RecipeInputs","RecipeOutputs","GatherOutputs","StorageResources","DependencyResources","ProvidedResources","OptimalWorkStep","RecipeWorkLoops","HasProduction","HasGathering","HasStorage","Guid")
$buildingProductionColumns = @("Package","Building","ProductionWorker","OptimalWorkStep","Recipe","RecipeIdentifier","Inputs","Outputs","RecipeWorkLoops","BuildingGuid","RecipeGuid")
$recipeColumns = @("Package","Recipe","Identifier","DefaultState","Inputs","Outputs","WorkSteps","WorkLoops","StepTypes","Guid")
$unitColumns = @("Package","Unit","RecruitmentCosts","NeedsManualRecruitment","SourceRecruitableUnit","Tags","Components","Guid")
$unitEquipmentColumns = @("Package","Unit","Equipment","Amount","EquipmentCategory","EquipmentCarryType","ProducedByBuildings","ProducedByRecipes","ProducerLinks","ProducerInputs","ProducerOutputs","ChainSamples","SourceRecruitableUnit","UnitTags","UnitGuid","EquipmentGuid")
$resourceColumns = @("Package","Resource","Entity","Category","NameKey","NamePluralKey","DescriptionKey","Icon","CarryType","Mesh","UiDisplay","SortOrder","WealthValue","StealValue","Tags","Components","Guid")
$resourceUsageColumns = @("Package","Resource","UsageType","Amount","Building","Recipe","Unit","Context","Details","ResourceGuid","SourceGuid")
$resourceFlowColumns = @("Package","Resource","Category","CarryType","ProducedBy","ConsumedBy","ConstructionCostFor","RecruitmentCostFor","StoredIn","TreasureTargets","UsageCount","Guid")
$localizationColumns = @("Package","Key","Declared","UsageType","Entity","DisplayName","Kind","FieldPath","Comment","Guid","File")
$objectiveFlowColumns = @("Package","Objective","Entity","Type","Hidden","Category","SortOrder","ObjectiveComponents","TitleKeys","DescriptionKeys","Amounts","PreconditionObjectives","SkipObjectives","FailObjectives","ResourceRefs","BuildingRefs","UnitRefs","PointOfInterestRefs","StartRewards","Rewards","Notifications","CompletionTriggers","OnUnlockedNotification","LocalizationKeys","Components","Guid","File")
$techTreeColumns = @("Package","TierGroup","Entity","Tier","SortOrder","TitleKey","DescriptionKey","TooltipKey","Icon","Buildings","Units","Category","PrimaryObjective","AlternativeObjectives","OnUnlockedNotification","ShowNextTierObjectives","Values","Guid","File")
$unlockGateColumns = @("Package","Name","Entity","Kind","GateType","DLC","DLCNeedsOwn","TechTreeTierGroups","Objectives","Buildings","Units","Resources","References","Values","Components","Guid","File")
$unlockRewardColumns = @("Package","Objective","Entity","RewardComponent","UnlockedBuildings","UnlockGathererFarmRecipes","UnlockedProductionRecipes","UnlockedRecruitments","UnlockShrineAbilities","UnlockTechTreeTierGroups","ModifiedNPCBases","ModifiedCustomRaids","KilledUnits","OtherReferences","Values","Guid","File")
$seasonalColumns = @("Package","Name","Entity","Kind","AllowedSeasons","References","Values","Components","Guid","File")
$assetReferenceColumns = @("Package","Name","Entity","Kind","Component","FieldPath","FieldName","AssetType","AssetRoot","Extension","Asset","Components","Guid","File")
$visualAudioComponentColumns = @("Package","Name","Entity","Kind","Component","AssetReferences","AudioEvents","Values","Components","Guid","File")
$toolEditorColumns = @("Package","Area","Name","Entity","GroupPath","IsAbstract","LinkedTextures","VegetationGroup","VegetationItems","ClusterGroups","AssetReferences","Values","Components","Guid","File")
$notificationNarrationColumns = @("Package","Kind","Name","Entity","MessageKey","TooltipKey","CoopMessageKey","DialogType","DialogLines","DefaultSpeaker","LineSpeakers","Flags","Icon","LifeTime","Sound","Music","Announcement","FilterCategory","FilterDuration","FilterRadius","Camera","ReferenceCount","ReferencedBy","LocalizationKeys","Components","Guid","File")
$artifactColumns = @("Package","Artifact","Entity","Category","NameKey","DescriptionKey","Icon","CarryType","WealthValue","StealValue","TreasureAudio","TreasureTier","CombatBoost","CombatWeight","Tags","Components","Guid")
$treasureHunterRecipeColumns = @("Package","Recipe","NameKey","SortOrder","DefaultEnabled","AllowSubSelection","TargetCount","ArtifactTargets","CombatBoostTargets","Targets","Guid")
$treasureHunterTargetColumns = @("Package","Recipe","RecipeNameKey","Target","TargetCategory","IsArtifactCategory","IsCombatBoostArtifact","TargetGuid","RecipeGuid")
$treasureAreaColumns = @("Package","TreasureArea","Entity","BuildingNameKey","Icon","Category","Components","Guid")
$shrineAbilityColumns = @("Package","Ability","NameKey","DescriptionKey","Type","UsageType","ManaCost","Cooldown","TargetRange","ResourceCosts","SpecificComponents","Guid")
$shrineRecipeColumns = @("Package","Recipe","Identifier","Inputs","StepTypes","WorkerSlots","Locators","IsAbstract","Guid")
$shrineBuildingColumns = @("Package","Shrine","BuildingNameKey","Worker","SecondaryWorker","Abilities","ManaRecipes","ManaResources","Components","Guid")
$terrainPropColumns = @("Package","TerrainProp","Entity","Prefab","BlockingType","UsePrefabBlocking","CategoryTags","AllowedSediments","ForbiddenSediments","SpacingRadius","MinMaxScale","Components","Guid","File")
$depositResourceTypeColumns = @("Package","DepositResourceType","Entity","NameKey","DescriptionKey","Category","Icon","MinimapColor","DiscoveredNotification","ExplorerDiscoverEvent","SubDepositTypes","SortOrder","GeneratedChance","MapGenChanceMin","MapGenChanceMax","DefaultForestWeight","ForestWeights","SedimentWeights","NpcTerritoryAmount","NpcCharacteristic","Components","Guid","File")
$depositColumns = @("Package","Deposit","Entity","DepositResourceType","HarvestResources","HarvestDeposits","Prefab","VisibleOnMinimap","DepositTags","AllowedSediments","HasTerrainDeposit","HasGrowing","HasRegrowing","HasFixedAmount","HasObstacle","Components","Guid","File")
$mapGenColumns = @("Package","Name","Entity","Component","LocaTag","Deposits","Resources","Buildings","Tags","Values","Components","Guid","File")
$npcUnitColumns = @("Package","Unit","Entity","NameKey","DescriptionKey","MaxSpeed","Tags","UnitClassTags","EncounterTargetTags","FactionOverride","EncounterRefs","RaidValues","EncounterValues","DropValues","BossValues","InfectionValues","MinimapVisible","EncounterInitiator","EncounterNPCUnit","RaidCapable","Boss","Drops","Infection","Components","Guid","File")
$npcBaseColumns = @("Package","Base","Entity","NameKey","BuildingTags","Faction","SubFaction","EncounterTargetTags","SpawnUnits","MapGenRefs","EncounterRefs","EncounterValues","WildAnimalValues","InfectionValues","EncounterInitiator","EncounterNPCBase","CustomRaid","WildAnimalBase","Components","Guid","File")
$encounterCombatColumns = @("Package","Name","Entity","Kind","Component","References","Units","Buildings","Resources","Tags","Notifications","Values","Components","Guid","File")
$factionColumns = @("Package","Name","Entity","Component","NameKey","Faction","SubFaction","Color","References","Values","Components","Guid","File")

Export-CatalogTable $buildingRows $buildingColumns $outPath "buildings" "Buildings" "Current local building catalog. Generated from local XML files."
Export-CatalogTable $buildingDependencyRows $buildingDependencyColumns $outPath "building-dependency-matrix" "Building Dependency Matrix" "One row per building summarizing construction costs, workers, production recipes, recipe inputs and outputs, gather outputs, and explicit storage resources."
Export-CatalogTable $buildingProductionRows $buildingProductionColumns $outPath "building-production" "Building Production" 'Production links between buildings and recipes. `OptimalWorkStep` is read from the building production aspect when available.'
Export-CatalogTable $recipeRows $recipeColumns $outPath "recipes" "Recipes" "Recipe inputs, outputs, states, and work-loop hints generated from local XML files."
Export-CatalogTable $unitRows $unitColumns $outPath "units" "Units" "Unit recruitment costs, tags, and component summaries generated from local XML files."
Export-CatalogTable $unitEquipmentRows $unitEquipmentColumns $outPath "unit-equipment-matrix" "Unit Equipment Matrix" "One row per unit equipment requirement, linking recruitment costs to equipment resources, producer buildings, producer recipes, and recipe inputs."
Export-CatalogTable $resourceRows $resourceColumns $outPath "resources" "Resources" "Resource definitions, categories, carry types, UI hints, asset paths, and value hints generated from local XML files."
Export-CatalogTable $resourceFlowRows $resourceFlowColumns $outPath "resource-flow" "Resource Flow" "Resource-centric summary of production, consumption, construction, recruitment, storage, and treasure hunter target links."
Export-CatalogTable $resourceUsageRows $resourceUsageColumns $outPath "resource-usage" "Resource Usage" "Expanded resource usage rows across production recipes, gathering, construction costs, recruitment costs, storage piles, shrine recipes, and treasure hunter targets."
Export-CatalogTable $localizationRows $localizationColumns $outPath "localization-index" "Localization Index" "Declared localization tags and likely UI text key references generated from local XML files. This maps technical keys to entities, components, and source files."
Export-CatalogTable $objectiveFlowRows $objectiveFlowColumns $outPath "objective-flow" "Objective Flow" "Objective, unlock, reward, notification, and requirement relationships generated from local XML files."
Export-CatalogTable $techTreeRows $techTreeColumns $outPath "tech-tree" "Tech Tree" "Tech tree tier groups with unlocked buildings, units, objectives, alternatives, and unlock notifications."
Export-CatalogTable $unlockGateRows $unlockGateColumns $outPath "unlock-gates" "Unlock Gates" "Entities with explicit NeedsUnlock gates, including DLC and other detected gate references."
Export-CatalogTable $unlockRewardRows $unlockRewardColumns $outPath "unlock-rewards" "Unlock Rewards" "Objective reward rows that unlock buildings, gatherer farm recipes, production recipes, recruitments, shrine abilities, or tech tree groups."
Export-CatalogTable $seasonalRows $seasonalColumns $outPath "seasonal-gates" "Seasonal Gates" "Entities with Seasonal components and detected allowed-season values."
Export-CatalogTable $assetReferenceRows $assetReferenceColumns $outPath "asset-references" "Asset References" "Detected icon, mesh, prefab, texture, material, character kit, VFX, JSON asset, and audio event references generated from local XML files."
Export-CatalogTable $visualAudioComponentRows $visualAudioComponentColumns $outPath "visual-audio-components" "Visual And Audio Components" "Visual, audio, attachment, animation, UI state, and asset-bearing component rows generated from local XML files."
Export-CatalogTable $toolEditorRows $toolEditorColumns $outPath "tools-editor" "Tools And Editor Data" "Editor/Magmaview terrain, fluid, road, territory, brush, sediment, vegetation group, texture, and vegetation rows generated from local XML files."
Export-CatalogTable $notificationNarrationRows $notificationNarrationColumns $outPath "notification-narration" "Notification And Narration Catalog" "Notifications, narration dialogs, notification filter categories, text keys, audio hints, and reverse references generated from local XML files."
Export-CatalogTable $productionChainRows $productionChainColumns $outPath "production-chains" "Production Chains" "Derived chain paths from starting resources through production recipes to construction costs, recruitment costs, and shrine recipe inputs."
Export-CatalogTable $terrainPropRows $terrainPropColumns $outPath "terrain-props" "Terrain Props" "Terrain prop entities with prefab paths, blocking hints, category tags, sediment constraints, spacing, and scale hints."
Export-CatalogTable $depositResourceTypeRows $depositResourceTypeColumns $outPath "deposit-resource-types" "Deposit Resource Types" "Deposit resource type definitions, discovery notifications, generated deposit hints, forest weights, and sediment weights."
Export-CatalogTable $depositRows $depositColumns $outPath "deposits" "Deposits" "Deposit entities with harvest outputs, prefab paths, terrain placement hints, growth/regrowth flags, and minimap hints."
Export-CatalogTable $mapGenRows $mapGenColumns $outPath "mapgen" "Map Generation" "Map generation parameters, landscape templates, deposit distribution groups, treasure categories, difficulty templates, and related references."
Export-CatalogTable $npcUnitRows $npcUnitColumns $outPath "npc-units" "NPC Units" "NPC, animal, hostile, boss, raid-capable, and encounter-related unit rows generated from local XML files."
Export-CatalogTable $npcBaseRows $npcBaseColumns $outPath "npc-bases" "NPC Bases" "NPC camps, dens, infected-area objects, encounter bases, wild animal bases, and related spawn/mapgen rows generated from local XML files."
Export-CatalogTable $encounterCombatRows $encounterCombatColumns $outPath "encounter-combat" "Encounter And Combat Components" "One row per detected encounter, combat, raid, boss, drop, infection, guard animal, or spawn component."
Export-CatalogTable $factionRows $factionColumns $outPath "factions" "NPC Factions" "Faction, sub-faction, faction UI name, faction color, and faction override rows generated from local XML files."

$edgeCount = Write-ProductionGraph $buildingProductionRows $outPath "production-graph" "Production Graph" "Generated Mermaid graph showing building -> recipe -> resource relationships." $GraphEdgeLimit
$chainEdgeCount = Write-ProductionChainGraph $productionChainRows $outPath "production-chain-graph" "Production Chain Graph" "Generated Mermaid graph showing resource -> recipe -> resource -> end-use chain paths." $GraphEdgeLimit

$filtersPath = Join-Path $outPath "filters"
$packagesPath = Join-Path $filtersPath "packages"
$productionPath = Join-Path $filtersPath "production"
$localizationPath = Join-Path $filtersPath "localization"
$objectivePath = Join-Path $filtersPath "objectives"
$notificationPath = Join-Path $filtersPath "notifications"
$worldPath = Join-Path $filtersPath "world"
$combatPath = Join-Path $filtersPath "combat"
$progressionPath = Join-Path $filtersPath "progression"
$assetsPath = Join-Path $filtersPath "assets"
$toolsPath = Join-Path $filtersPath "tools"
$systemsPath = Join-Path $outPath "systems"
New-Item -ItemType Directory -Force -Path $filtersPath | Out-Null
New-Item -ItemType Directory -Force -Path $packagesPath | Out-Null
New-Item -ItemType Directory -Force -Path $productionPath | Out-Null
New-Item -ItemType Directory -Force -Path $localizationPath | Out-Null
New-Item -ItemType Directory -Force -Path $objectivePath | Out-Null
New-Item -ItemType Directory -Force -Path $notificationPath | Out-Null
New-Item -ItemType Directory -Force -Path $worldPath | Out-Null
New-Item -ItemType Directory -Force -Path $combatPath | Out-Null
New-Item -ItemType Directory -Force -Path $progressionPath | Out-Null
New-Item -ItemType Directory -Force -Path $assetsPath | Out-Null
New-Item -ItemType Directory -Force -Path $toolsPath | Out-Null
New-Item -ItemType Directory -Force -Path $systemsPath | Out-Null

Export-CatalogTable $artifactRows $artifactColumns $systemsPath "artifacts" "Artifacts And Artifact-Like Resources" "Artifact resources, combat-boost artifacts, and campaign artifact resources generated from local XML files."
Export-CatalogTable $treasureHunterRecipeRows $treasureHunterRecipeColumns $systemsPath "treasure-hunter-recipes" "Treasure Hunter Recipes" "Treasure hunter target categories and target counts."
Export-CatalogTable $treasureHunterTargetRows $treasureHunterTargetColumns $systemsPath "treasure-hunter-targets" "Treasure Hunter Targets" "Expanded treasure hunter recipe target resources."
Export-CatalogTable $treasureAreaRows $treasureAreaColumns $systemsPath "treasure-areas" "Treasure Areas" "Treasure area entities and point-of-interest style treasure locations."
Export-CatalogTable $shrineBuildingRows $shrineBuildingColumns $systemsPath "shrine-buildings" "Shrine Buildings" "Buildings and point-of-interest entities that expose shrine behavior."
Export-CatalogTable $shrineAbilityRows $shrineAbilityColumns $systemsPath "shrine-abilities" "Shrine Abilities" "Shrine and sanctuary abilities, including mana costs, cooldowns, ranges, and ability-specific components."
Export-CatalogTable $shrineRecipeRows $shrineRecipeColumns $systemsPath "shrine-recipes" "Shrine Recipes" "Shrine and sanctuary offering recipes generated from local XML files."

$systemGraphLines = New-SystemGraphLines $treasureHunterRecipeRows $treasureHunterTargetRows $shrineBuildingRows
[System.IO.File]::WriteAllLines((Join-Path $systemsPath "artifact-treasure-shrine-graph.mmd"), $systemGraphLines, [System.Text.UTF8Encoding]::new($false))
$systemGraphMd = New-Object System.Collections.Generic.List[string]
$systemGraphMd.Add("# Artifact, Treasure, And Shrine Graph")
$systemGraphMd.Add("")
$systemGraphMd.Add("Generated Mermaid graph for the local artifact, treasure hunter, and shrine systems.")
$systemGraphMd.Add("")
$systemGraphMd.Add('```mermaid')
foreach ($line in $systemGraphLines) {
  $systemGraphMd.Add([string]$line)
}
$systemGraphMd.Add('```')
[System.IO.File]::WriteAllLines((Join-Path $systemsPath "artifact-treasure-shrine-graph.md"), $systemGraphMd, [System.Text.UTF8Encoding]::new($false))

$packageSummaries = New-Object System.Collections.Generic.List[object]
$packages = @($entities | Select-Object -ExpandProperty Package -Unique | Sort-Object)
foreach ($package in $packages) {
  $packagePath = Join-Path $packagesPath $package
  $packageBuildings = @($buildingRows | Where-Object { $_.Package -eq $package })
  $packageBuildingDependencies = @($buildingDependencyRows | Where-Object { $_.Package -eq $package })
  $packageBuildingProduction = @($buildingProductionRows | Where-Object { $_.Package -eq $package })
  $packageRecipes = @($recipeRows | Where-Object { $_.Package -eq $package })
  $packageUnits = @($unitRows | Where-Object { $_.Package -eq $package })
  $packageUnitEquipment = @($unitEquipmentRows | Where-Object { $_.Package -eq $package })
  $packageResources = @($resourceRows | Where-Object { $_.Package -eq $package })
  $packageResourceFlow = @($resourceFlowRows | Where-Object { $_.Package -eq $package })
  $packageResourceUsage = @($resourceUsageRows | Where-Object { $_.Package -eq $package })
  $packageLocalization = @($localizationRows | Where-Object { $_.Package -eq $package })
  $packageObjectiveFlow = @($objectiveFlowRows | Where-Object { $_.Package -eq $package })
  $packageTechTree = @($techTreeRows | Where-Object { $_.Package -eq $package })
  $packageUnlockGates = @($unlockGateRows | Where-Object { $_.Package -eq $package })
  $packageUnlockRewards = @($unlockRewardRows | Where-Object { $_.Package -eq $package })
  $packageSeasonal = @($seasonalRows | Where-Object { $_.Package -eq $package })
  $packageAssetReferences = @($assetReferenceRows | Where-Object { $_.Package -eq $package })
  $packageVisualAudioComponents = @($visualAudioComponentRows | Where-Object { $_.Package -eq $package })
  $packageToolEditorRows = @($toolEditorRows | Where-Object { $_.Package -eq $package })
  $packageNotificationNarration = @($notificationNarrationRows | Where-Object { $_.Package -eq $package })
  $packageTerrainProps = @($terrainPropRows | Where-Object { $_.Package -eq $package })
  $packageDepositResourceTypes = @($depositResourceTypeRows | Where-Object { $_.Package -eq $package })
  $packageDeposits = @($depositRows | Where-Object { $_.Package -eq $package })
  $packageMapGen = @($mapGenRows | Where-Object { $_.Package -eq $package })
  $packageNpcUnits = @($npcUnitRows | Where-Object { $_.Package -eq $package })
  $packageNpcBases = @($npcBaseRows | Where-Object { $_.Package -eq $package })
  $packageEncounterCombat = @($encounterCombatRows | Where-Object { $_.Package -eq $package })
  $packageFactions = @($factionRows | Where-Object { $_.Package -eq $package })
  $packageProductionChains = @($productionChainRows | Where-Object { $_.Package -eq $package })
  $packageArtifacts = @($artifactRows | Where-Object { $_.Package -eq $package })
  $packageTreasureHunterRecipes = @($treasureHunterRecipeRows | Where-Object { $_.Package -eq $package })
  $packageShrineAbilities = @($shrineAbilityRows | Where-Object { $_.Package -eq $package })
  $packageShrineRecipes = @($shrineRecipeRows | Where-Object { $_.Package -eq $package })

  Export-CatalogTable $packageBuildings $buildingColumns $packagePath "buildings" "$package Buildings" "Building catalog filtered to package `$package`."
  Export-CatalogTable $packageBuildingDependencies $buildingDependencyColumns $packagePath "building-dependency-matrix" "$package Building Dependency Matrix" "Building dependency matrix filtered to package `$package`."
  Export-CatalogTable $packageBuildingProduction $buildingProductionColumns $packagePath "building-production" "$package Building Production" "Production links filtered to package `$package`."
  Export-CatalogTable $packageRecipes $recipeColumns $packagePath "recipes" "$package Recipes" "Recipes filtered to package `$package`."
  Export-CatalogTable $packageUnits $unitColumns $packagePath "units" "$package Units" "Units filtered to package `$package`."
  Export-CatalogTable $packageUnitEquipment $unitEquipmentColumns $packagePath "unit-equipment-matrix" "$package Unit Equipment Matrix" "Unit equipment matrix filtered to package `$package`."
  Export-CatalogTable $packageResources $resourceColumns $packagePath "resources" "$package Resources" "Resources filtered to package `$package`."
  Export-CatalogTable $packageResourceFlow $resourceFlowColumns $packagePath "resource-flow" "$package Resource Flow" "Resource flow summary filtered to package `$package`."
  Export-CatalogTable $packageResourceUsage $resourceUsageColumns $packagePath "resource-usage" "$package Resource Usage" "Expanded resource usage rows filtered to package `$package`."
  Export-CatalogTable $packageLocalization $localizationColumns $packagePath "localization-index" "$package Localization Index" "Localization rows filtered to package `$package`."
  Export-CatalogTable $packageObjectiveFlow $objectiveFlowColumns $packagePath "objective-flow" "$package Objective Flow" "Objective flow rows filtered to package `$package`."
  Export-CatalogTable $packageTechTree $techTreeColumns $packagePath "tech-tree" "$package Tech Tree" "Tech tree rows filtered to package `$package`."
  Export-CatalogTable $packageUnlockGates $unlockGateColumns $packagePath "unlock-gates" "$package Unlock Gates" "Unlock gate rows filtered to package `$package`."
  Export-CatalogTable $packageUnlockRewards $unlockRewardColumns $packagePath "unlock-rewards" "$package Unlock Rewards" "Unlock reward rows filtered to package `$package`."
  Export-CatalogTable $packageSeasonal $seasonalColumns $packagePath "seasonal-gates" "$package Seasonal Gates" "Seasonal gate rows filtered to package `$package`."
  Export-CatalogTable $packageAssetReferences $assetReferenceColumns $packagePath "asset-references" "$package Asset References" "Asset reference rows filtered to package `$package`."
  Export-CatalogTable $packageVisualAudioComponents $visualAudioComponentColumns $packagePath "visual-audio-components" "$package Visual And Audio Components" "Visual/audio component rows filtered to package `$package`."
  Export-CatalogTable $packageToolEditorRows $toolEditorColumns $packagePath "tools-editor" "$package Tools And Editor Data" "Tools/editor rows filtered to package `$package`."
  Export-CatalogTable $packageNotificationNarration $notificationNarrationColumns $packagePath "notification-narration" "$package Notification And Narration Catalog" "Notification and narration rows filtered to package `$package`."
  Export-CatalogTable $packageTerrainProps $terrainPropColumns $packagePath "terrain-props" "$package Terrain Props" "Terrain prop rows filtered to package `$package`."
  Export-CatalogTable $packageDepositResourceTypes $depositResourceTypeColumns $packagePath "deposit-resource-types" "$package Deposit Resource Types" "Deposit resource type rows filtered to package `$package`."
  Export-CatalogTable $packageDeposits $depositColumns $packagePath "deposits" "$package Deposits" "Deposit rows filtered to package `$package`."
  Export-CatalogTable $packageMapGen $mapGenColumns $packagePath "mapgen" "$package Map Generation" "Map generation rows filtered to package `$package`."
  Export-CatalogTable $packageNpcUnits $npcUnitColumns $packagePath "npc-units" "$package NPC Units" "NPC unit rows filtered to package `$package`."
  Export-CatalogTable $packageNpcBases $npcBaseColumns $packagePath "npc-bases" "$package NPC Bases" "NPC base rows filtered to package `$package`."
  Export-CatalogTable $packageEncounterCombat $encounterCombatColumns $packagePath "encounter-combat" "$package Encounter And Combat Components" "Encounter and combat component rows filtered to package `$package`."
  Export-CatalogTable $packageFactions $factionColumns $packagePath "factions" "$package NPC Factions" "Faction rows filtered to package `$package`."
  Export-CatalogTable $packageProductionChains $productionChainColumns $packagePath "production-chains" "$package Production Chains" "Derived production chains filtered to starting resources from package `$package`."
  Export-CatalogTable $packageArtifacts $artifactColumns $packagePath "artifacts" "$package Artifacts" "Artifacts and artifact-like resources filtered to package `$package`."
  Export-CatalogTable $packageTreasureHunterRecipes $treasureHunterRecipeColumns $packagePath "treasure-hunter-recipes" "$package Treasure Hunter Recipes" "Treasure hunter recipes filtered to package `$package`."
  Export-CatalogTable $packageShrineAbilities $shrineAbilityColumns $packagePath "shrine-abilities" "$package Shrine Abilities" "Shrine abilities filtered to package `$package`."
  Export-CatalogTable $packageShrineRecipes $shrineRecipeColumns $packagePath "shrine-recipes" "$package Shrine Recipes" "Shrine recipes filtered to package `$package`."
  $packageEdges = Write-ProductionGraph $packageBuildingProduction $packagePath "production-graph" "$package Production Graph" "Generated Mermaid graph for package `$package`." $GraphEdgeLimit

  $packageSummaries.Add([pscustomobject]@{
    Package = $package
    Buildings = $packageBuildings.Count
    BuildingDependencyRows = $packageBuildingDependencies.Count
    BuildingProductionLinks = $packageBuildingProduction.Count
    Recipes = $packageRecipes.Count
    Units = $packageUnits.Count
    UnitEquipmentRows = $packageUnitEquipment.Count
    Resources = $packageResources.Count
    ResourceUsageRows = $packageResourceUsage.Count
    LocalizationRows = $packageLocalization.Count
    ObjectiveFlowRows = $packageObjectiveFlow.Count
    TechTreeRows = $packageTechTree.Count
    UnlockGateRows = $packageUnlockGates.Count
    UnlockRewardRows = $packageUnlockRewards.Count
    SeasonalRows = $packageSeasonal.Count
    AssetReferenceRows = $packageAssetReferences.Count
    VisualAudioComponentRows = $packageVisualAudioComponents.Count
    ToolEditorRows = $packageToolEditorRows.Count
    NotificationNarrationRows = $packageNotificationNarration.Count
    TerrainProps = $packageTerrainProps.Count
    DepositResourceTypes = $packageDepositResourceTypes.Count
    Deposits = $packageDeposits.Count
    MapGenRows = $packageMapGen.Count
    NpcUnits = $packageNpcUnits.Count
    NpcBases = $packageNpcBases.Count
    EncounterCombatRows = $packageEncounterCombat.Count
    FactionRows = $packageFactions.Count
    ProductionChains = $packageProductionChains.Count
    Artifacts = $packageArtifacts.Count
    TreasureHunterRecipes = $packageTreasureHunterRecipes.Count
    ShrineAbilities = $packageShrineAbilities.Count
    ShrineRecipes = $packageShrineRecipes.Count
    ProductionGraphEdges = $packageEdges
    Folder = "filters/packages/$package"
  })
}

$buildingsWithRecipes = @($buildingRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ProductionRecipes) })
$buildingsWithGatherOutputs = @($buildingRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.GatherOutputs) })
$buildingsWithStorage = @($buildingDependencyRows | Where-Object { $_.HasStorage -eq "True" })
$buildingsWithDependencies = @($buildingDependencyRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.DependencyResources) })
$recipesWithInputOrOutput = @($recipeRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Inputs) -or -not [string]::IsNullOrWhiteSpace($_.Outputs) })
$unitsWithRecruitmentCost = @($unitRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.RecruitmentCosts) })
$unitEquipmentWithProducer = @($unitEquipmentRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ProducedByRecipes) })
$unitEquipmentWithoutProducer = @($unitEquipmentRows | Where-Object { [string]::IsNullOrWhiteSpace($_.ProducedByRecipes) })
$resourcesProduced = @($resourceFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ProducedBy) })
$resourcesConsumed = @($resourceFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.ConsumedBy) })
$resourcesStored = @($resourceFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.StoredIn) })
$localizationDeclaredTags = @($localizationRows | Where-Object { $_.UsageType -eq "DeclaredTag" })
$localizationReferencedKeys = @($localizationRows | Where-Object { $_.UsageType -eq "ReferencedKey" })
$localizationNotInLocalTagIndex = @($localizationReferencedKeys | Where-Object { $_.Declared -ne "True" })
$localizationUniqueKeys = @($localizationRows | Select-Object -ExpandProperty Key -Unique)
$localizationUniqueDeclaredTags = @($localizationDeclaredTags | Select-Object -ExpandProperty Key -Unique)
$localizationUniqueReferencedKeys = @($localizationReferencedKeys | Select-Object -ExpandProperty Key -Unique)
$localizationUniqueNotInLocalTagIndex = @($localizationNotInLocalTagIndex | Select-Object -ExpandProperty Key -Unique)
$objectiveFlowWithPreconditions = @($objectiveFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.PreconditionObjectives) })
$objectiveFlowWithRewards = @($objectiveFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.StartRewards) -or -not [string]::IsNullOrWhiteSpace($_.Rewards) })
$objectiveFlowWithNotifications = @($objectiveFlowRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Notifications) -or -not [string]::IsNullOrWhiteSpace($_.OnUnlockedNotification) })
$objectiveFlowTechTree = @($objectiveFlowRows | Where-Object { $_.Type -eq "TechTreeTierGroup" -or $_.ObjectiveComponents -match "TechTreeTierGroup" })
$techTreeWithObjectives = @($techTreeRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.PrimaryObjective) -or -not [string]::IsNullOrWhiteSpace($_.AlternativeObjectives) })
$unlockGatesDlc = @($unlockGateRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.DLC) })
$unlockRewardsBuildings = @($unlockRewardRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.UnlockedBuildings) })
$unlockRewardsRecipes = @($unlockRewardRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.UnlockGathererFarmRecipes) -or -not [string]::IsNullOrWhiteSpace($_.UnlockedProductionRecipes) })
$unlockRewardsRecruitments = @($unlockRewardRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.UnlockedRecruitments) })
$unlockRewardsTechTree = @($unlockRewardRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.UnlockTechTreeTierGroups) })
$seasonalWithAllowedSeasons = @($seasonalRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.AllowedSeasons) })
$assetIcons = @($assetReferenceRows | Where-Object { $_.AssetType -eq "Icon" })
$assetPrefabs = @($assetReferenceRows | Where-Object { $_.AssetType -eq "Prefab" })
$assetMeshes = @($assetReferenceRows | Where-Object { $_.AssetType -eq "Mesh" })
$assetTextures = @($assetReferenceRows | Where-Object { $_.AssetType -eq "Texture" })
$assetAudioEvents = @($assetReferenceRows | Where-Object { $_.AssetType -match "Audio" })
$assetCharacterKits = @($assetReferenceRows | Where-Object { $_.AssetType -eq "CharacterKit" })
$visualComponentsWithAssets = @($visualAudioComponentRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.AssetReferences) })
$visualComponentsWithAudio = @($visualAudioComponentRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.AudioEvents) })
$toolTerrainSediments = @($toolEditorRows | Where-Object { $_.Area -eq "TerrainSediment" })
$toolVegetationGroups = @($toolEditorRows | Where-Object { $_.Area -eq "VisVegetationGroup" })
$toolVegetation = @($toolEditorRows | Where-Object { $_.Area -eq "VisVegetation" })
$toolTextures = @($toolEditorRows | Where-Object { $_.Area -eq "VisTexture" })
$toolGlobalsAndBrushes = @($toolEditorRows | Where-Object { $_.Area -in @("VisTerrain","VisFluid","VisRoad","VisTerritory","EditorBrushes") })
$notificationRows = @($notificationNarrationRows | Where-Object { $_.Kind -eq "Notification" -or $_.Kind -eq "NotificationWithFilter" })
$narrationDialogRows = @($notificationNarrationRows | Where-Object { $_.Kind -eq "NarrationDialog" })
$referencedNotificationNarrationRows = @($notificationNarrationRows | Where-Object { [int]$_.ReferenceCount -gt 0 })
$unreferencedNotificationNarrationRows = @($notificationNarrationRows | Where-Object { [int]$_.ReferenceCount -eq 0 -and $_.Kind -ne "NotificationParameters" })
$terrainPropsWithBlocking = @($terrainPropRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.BlockingType) -or $_.UsePrefabBlocking -eq "True" })
$terrainPropsWithSedimentRules = @($terrainPropRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.AllowedSediments) -or -not [string]::IsNullOrWhiteSpace($_.ForbiddenSediments) })
$depositsWithHarvestResources = @($depositRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.HarvestResources) })
$depositsGrowingOrRegrowing = @($depositRows | Where-Object { $_.HasGrowing -eq "True" -or $_.HasRegrowing -eq "True" })
$generatedDepositTypes = @($depositResourceTypeRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.GeneratedChance) -or -not [string]::IsNullOrWhiteSpace($_.MapGenChanceMin) -or -not [string]::IsNullOrWhiteSpace($_.MapGenChanceMax) })
$mapGenDepositRows = @($mapGenRows | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Deposits) })
$npcBossRows = @($npcUnitRows | Where-Object { $_.Boss -eq "True" })
$raidCapableNpcUnitRows = @($npcUnitRows | Where-Object { $_.RaidCapable -eq "True" })
$customRaidNpcBaseRows = @($npcBaseRows | Where-Object { $_.CustomRaid -eq "True" })
$npcUnitDropRows = @($npcUnitRows | Where-Object { $_.Drops -eq "True" })
$npcInfectionRows = @($encounterCombatRows | Where-Object { $_.Component -match "^Infection|^AspectInfectedArea|^EffectNPCBoss" -or $_.Values -match "Infection" })
$encounterTargetTagRows = @($encounterCombatRows | Where-Object { $_.Component -eq "EncounterTargetTag" -or -not [string]::IsNullOrWhiteSpace($_.Tags) })
$productionChainsToBuildings = @($productionChainRows | Where-Object { $_.EndType -eq "ConstructionCost" })
$productionChainsToUnits = @($productionChainRows | Where-Object { $_.EndType -eq "RecruitmentCost" })
$productionChainsToShrines = @($productionChainRows | Where-Object { $_.EndType -eq "ShrineRecipeInput" })

Export-CatalogTable $buildingsWithRecipes $buildingColumns $productionPath "buildings-with-recipes" "Buildings With Recipes" "Buildings that reference at least one production recipe."
Export-CatalogTable $buildingsWithGatherOutputs $buildingColumns $productionPath "buildings-with-gather-outputs" "Buildings With Gather Outputs" "Buildings that gather or expose resource outputs through gatherer data."
Export-CatalogTable $buildingsWithStorage $buildingDependencyColumns $productionPath "buildings-with-storage" "Buildings With Storage" "Buildings with at least one explicit storage pile resource reference."
Export-CatalogTable $buildingsWithDependencies $buildingDependencyColumns $productionPath "buildings-with-dependencies" "Buildings With Dependencies" "Buildings with construction cost resources or production input resources."
Export-CatalogTable $recipesWithInputOrOutput $recipeColumns $productionPath "recipes-with-input-output" "Recipes With Inputs Or Outputs" "Recipes that declare at least one input or output resource."
Export-CatalogTable $unitsWithRecruitmentCost $unitColumns $productionPath "units-with-recruitment-cost" "Units With Recruitment Cost" "Units that declare at least one recruitment cost."
Export-CatalogTable $unitEquipmentWithProducer $unitEquipmentColumns $productionPath "unit-equipment-with-producers" "Unit Equipment With Producers" "Unit equipment requirements with at least one known producer recipe."
Export-CatalogTable $unitEquipmentWithoutProducer $unitEquipmentColumns $productionPath "unit-equipment-without-producers" "Unit Equipment Without Producers" "Unit equipment requirements without a known producer recipe in the current generated catalog."
Export-CatalogTable $resourcesProduced $resourceFlowColumns $productionPath "resources-produced" "Resources Produced" "Resources with at least one known recipe or gather output producer."
Export-CatalogTable $resourcesConsumed $resourceFlowColumns $productionPath "resources-consumed" "Resources Consumed" "Resources with at least one known recipe or shrine recipe consumer."
Export-CatalogTable $resourcesStored $resourceFlowColumns $productionPath "resources-stored" "Resources Stored" "Resources with at least one explicit building pile/storage reference."
Export-CatalogTable $localizationDeclaredTags $localizationColumns $localizationPath "declared-tags" "Localization Declared Tags" "Declared localization tags found in LocaParkplatz entities."
Export-CatalogTable $localizationReferencedKeys $localizationColumns $localizationPath "referenced-keys" "Localization Referenced Keys" "Likely UI text key references found outside LocaParkplatz declarations."
Export-CatalogTable $localizationNotInLocalTagIndex $localizationColumns $localizationPath "not-in-local-tag-index" "Localization Keys Not In Local Tag Index" "Referenced localization-like keys that are not declared in the local LocaParkplatz tag index. These rows are research hints, not validation errors."
Export-CatalogTable $objectiveFlowWithPreconditions $objectiveFlowColumns $objectivePath "with-preconditions" "Objective Flow With Preconditions" "Objective flow rows with prerequisite objective references."
Export-CatalogTable $objectiveFlowWithRewards $objectiveFlowColumns $objectivePath "with-rewards" "Objective Flow With Rewards" "Objective flow rows with start or completion reward references."
Export-CatalogTable $objectiveFlowWithNotifications $objectiveFlowColumns $objectivePath "with-notifications" "Objective Flow With Notifications" "Objective flow rows with notification references."
Export-CatalogTable $objectiveFlowTechTree $objectiveFlowColumns $objectivePath "tech-tree" "Objective Flow Tech Tree Rows" "Objective flow rows for tech tree tier groups and unlock-like data."
Export-CatalogTable @($techTreeWithObjectives) $techTreeColumns $progressionPath "tech-tree-with-objectives" "Tech Tree Rows With Objectives" "Tech tree rows with primary or alternative unlock objectives."
Export-CatalogTable @($unlockGatesDlc) $unlockGateColumns $progressionPath "dlc-unlock-gates" "DLC Unlock Gates" "NeedsUnlock rows that reference a DLC entity."
Export-CatalogTable @($unlockRewardsBuildings) $unlockRewardColumns $progressionPath "unlock-rewards-buildings" "Unlock Rewards For Buildings" "Objective reward rows that unlock buildings."
Export-CatalogTable @($unlockRewardsRecipes) $unlockRewardColumns $progressionPath "unlock-rewards-recipes" "Unlock Rewards For Recipes" "Objective reward rows that unlock gatherer farm recipes or production recipes."
Export-CatalogTable @($unlockRewardsRecruitments) $unlockRewardColumns $progressionPath "unlock-rewards-recruitments" "Unlock Rewards For Recruitments" "Objective reward rows that unlock recruitments or units."
Export-CatalogTable @($unlockRewardsTechTree) $unlockRewardColumns $progressionPath "unlock-rewards-tech-tree" "Unlock Rewards For Tech Tree Groups" "Objective reward rows that unlock tech tree tier groups."
Export-CatalogTable @($seasonalWithAllowedSeasons) $seasonalColumns $progressionPath "seasonal-with-allowed-seasons" "Seasonal Rows With Allowed Seasons" "Seasonal rows that explicitly list allowed seasons."
Export-CatalogTable $assetIcons $assetReferenceColumns $assetsPath "icons" "Icon References" "Detected icon path references."
Export-CatalogTable $assetPrefabs $assetReferenceColumns $assetsPath "prefabs" "Prefab References" "Detected prefab path references."
Export-CatalogTable $assetMeshes $assetReferenceColumns $assetsPath "meshes" "Mesh References" "Detected mesh path references."
Export-CatalogTable $assetTextures $assetReferenceColumns $assetsPath "textures" "Texture References" "Detected texture path references."
Export-CatalogTable $assetAudioEvents $assetReferenceColumns $assetsPath "audio-events" "Audio Event References" "Detected audio event and audio-like references."
Export-CatalogTable $assetCharacterKits $assetReferenceColumns $assetsPath "character-kits" "Character Kit References" "Detected character kit references."
Export-CatalogTable $visualComponentsWithAssets $visualAudioComponentColumns $assetsPath "visual-components-with-assets" "Visual Components With Assets" "Visual/audio component rows with at least one non-audio asset reference."
Export-CatalogTable $visualComponentsWithAudio $visualAudioComponentColumns $assetsPath "visual-components-with-audio" "Visual Components With Audio" "Visual/audio component rows with at least one audio reference."
Export-CatalogTable $toolTerrainSediments $toolEditorColumns $toolsPath "terrain-sediments" "Tools Terrain Sediments" "Tools/editor TerrainSediment rows with linked textures, vegetation groups, scalar tuning, and asset references."
Export-CatalogTable $toolVegetationGroups $toolEditorColumns $toolsPath "vegetation-groups" "Tools Vegetation Groups" "Tools/editor VisVegetationGroup rows with single vegetation entries and cluster groups."
Export-CatalogTable $toolVegetation $toolEditorColumns $toolsPath "vegetation" "Tools Vegetation" "Tools/editor VisVegetation rows with plant texture references and visual tuning values."
Export-CatalogTable $toolTextures $toolEditorColumns $toolsPath "textures" "Tools Textures" "Tools/editor VisTexture rows with diffuse, normal, parameter, and height-scale data."
Export-CatalogTable $toolGlobalsAndBrushes $toolEditorColumns $toolsPath "globals-and-brushes" "Tools Globals And Brushes" "Tools/editor global terrain, fluid, road, territory, and brush rows."
Export-CatalogTable $notificationRows $notificationNarrationColumns $notificationPath "notifications" "Notifications" "Rows with notification components, including message keys, flags, icons, sounds, filter categories, and reverse references."
Export-CatalogTable $narrationDialogRows $notificationNarrationColumns $notificationPath "narration-dialogs" "Narration Dialogs" "Rows with UiNarrationDialog components, including dialog type, line text keys, speakers, notification wrapper data, and reverse references."
Export-CatalogTable $referencedNotificationNarrationRows $notificationNarrationColumns $notificationPath "referenced" "Referenced Notifications And Narration" "Notification and narration rows referenced by at least one other entity in the current local catalog."
Export-CatalogTable $unreferencedNotificationNarrationRows $notificationNarrationColumns $notificationPath "unreferenced" "Unreferenced Notifications And Narration" "Notification and narration rows with no detected reverse reference. These are research hints, not automatic errors."
Export-CatalogTable $terrainPropsWithBlocking $terrainPropColumns $worldPath "terrain-props-with-blocking" "Terrain Props With Blocking" "Terrain props with explicit blocking hints or prefab blocking enabled."
Export-CatalogTable $terrainPropsWithSedimentRules $terrainPropColumns $worldPath "terrain-props-with-sediment-rules" "Terrain Props With Sediment Rules" "Terrain props with detected allowed or forbidden sediment rules."
Export-CatalogTable $depositsWithHarvestResources $depositColumns $worldPath "deposits-with-harvest-resources" "Deposits With Harvest Resources" "Deposit rows with one or more harvest output resources."
Export-CatalogTable $depositsGrowingOrRegrowing $depositColumns $worldPath "deposits-growing-or-regrowing" "Growing Or Regrowing Deposits" "Deposit rows with GrowingDeposit or RegrowingDeposit components."
Export-CatalogTable $generatedDepositTypes $depositResourceTypeColumns $worldPath "generated-deposit-types" "Generated Deposit Types" "Deposit resource types with generated/mapgen chance hints."
Export-CatalogTable $mapGenDepositRows $mapGenColumns $worldPath "mapgen-with-deposits" "MapGen Rows With Deposits" "Map generation rows that reference deposit entities."
Export-CatalogTable $npcUnitRows $npcUnitColumns $combatPath "npc-units" "NPC Units" "NPC unit catalog rows."
Export-CatalogTable $npcBaseRows $npcBaseColumns $combatPath "npc-bases" "NPC Bases" "NPC base catalog rows."
Export-CatalogTable $npcBossRows $npcUnitColumns $combatPath "bosses" "NPC Bosses" "NPC units with AspectPatrollingNPCBoss."
Export-CatalogTable $raidCapableNpcUnitRows $npcUnitColumns $combatPath "raid-capable-units" "Raid-Capable NPC Units" "NPC units with UnitRaidParameters."
Export-CatalogTable $customRaidNpcBaseRows $npcBaseColumns $combatPath "custom-raid-bases" "Custom Raid NPC Bases" "NPC bases with AspectNPCCustomRaid."
Export-CatalogTable $npcUnitDropRows $npcUnitColumns $combatPath "drop-parameters" "NPC Unit Drop Parameters" "NPC units with UnitDropParameters."
Export-CatalogTable $npcInfectionRows $encounterCombatColumns $combatPath "infection" "Infection-Related Encounter Rows" "Encounter and combat rows with infection-related components or scalar values."
Export-CatalogTable $encounterTargetTagRows $encounterCombatColumns $combatPath "encounter-target-tags" "Encounter Target Tag Rows" "Encounter and combat rows that declare or reference encounter target tags."
Export-CatalogTable $productionChainsToBuildings $productionChainColumns $productionPath "production-chains-to-buildings" "Production Chains To Buildings" "Production chains that end in building construction costs."
Export-CatalogTable $productionChainsToUnits $productionChainColumns $productionPath "production-chains-to-units" "Production Chains To Units" "Production chains that end in unit recruitment costs."
Export-CatalogTable $productionChainsToShrines $productionChainColumns $productionPath "production-chains-to-shrines" "Production Chains To Shrines" "Production chains that end in shrine recipe inputs."
Export-CatalogTable $packageSummaries @("Package","Buildings","BuildingDependencyRows","BuildingProductionLinks","Recipes","Units","UnitEquipmentRows","Resources","ResourceUsageRows","LocalizationRows","ObjectiveFlowRows","TechTreeRows","UnlockGateRows","UnlockRewardRows","SeasonalRows","AssetReferenceRows","VisualAudioComponentRows","ToolEditorRows","NotificationNarrationRows","TerrainProps","DepositResourceTypes","Deposits","MapGenRows","NpcUnits","NpcBases","EncounterCombatRows","FactionRows","ProductionChains","Artifacts","TreasureHunterRecipes","ShrineAbilities","ShrineRecipes","ProductionGraphEdges","Folder") $filtersPath "package-summary" "Package Summary" "Counts for the generated package-filtered catalogs."

$searchItems = New-Object System.Collections.Generic.List[object]
$entitySearchRows = @($entities | ForEach-Object {
  [pscustomobject]@{
    Package = $_.Package
    EntityName = $_.EntityName
    DisplayName = $_.DisplayName
    Kind = $_.Kind
    Components = ($_.ValueTypes -join ", ")
    Guid = $_.Guid
    File = $_.File
  }
})
$entityColumns = @("Package","EntityName","DisplayName","Kind","Components","Guid","File")

Add-SearchIndexItems $searchItems $entitySearchRows "entity" "DisplayName" @("Kind","EntityName") $entityColumns
Add-SearchIndexItems $searchItems $buildingRows "building" "Building" @("Category","UiGroup") $buildingColumns
Add-SearchIndexItems $searchItems $buildingDependencyRows "building-dependency" "Building" @("DependencyResources","ProvidedResources","Recipes") $buildingDependencyColumns
Add-SearchIndexItems $searchItems $buildingProductionRows "building-production" "Building" @("Recipe","Inputs","Outputs") $buildingProductionColumns
Add-SearchIndexItems $searchItems $recipeRows "recipe" "Recipe" @("Identifier","Inputs","Outputs") $recipeColumns
Add-SearchIndexItems $searchItems $unitRows "unit" "Unit" @("RecruitmentCosts","Tags") $unitColumns
Add-SearchIndexItems $searchItems $unitEquipmentRows "unit-equipment" "Unit" @("Equipment","ProducerLinks","ChainSamples") $unitEquipmentColumns
Add-SearchIndexItems $searchItems $resourceRows "resource" "Resource" @("Category","CarryType") $resourceColumns
Add-SearchIndexItems $searchItems $resourceFlowRows "resource-flow" "Resource" @("ProducedBy","ConsumedBy","StoredIn") $resourceFlowColumns
Add-SearchIndexItems $searchItems $resourceUsageRows "resource-usage" "Resource" @("UsageType","Building","Recipe","Unit") $resourceUsageColumns
Add-SearchIndexItems $searchItems $localizationRows "localization" "Key" @("UsageType","Entity","FieldPath") $localizationColumns
Add-SearchIndexItems $searchItems $objectiveFlowRows "objective-flow" "Objective" @("Type","ObjectiveComponents","PreconditionObjectives") $objectiveFlowColumns
Add-SearchIndexItems $searchItems $techTreeRows "tech-tree" "TierGroup" @("Tier","PrimaryObjective","AlternativeObjectives") $techTreeColumns
Add-SearchIndexItems $searchItems $unlockGateRows "unlock-gate" "Name" @("GateType","DLC","References") $unlockGateColumns
Add-SearchIndexItems $searchItems $unlockRewardRows "unlock-reward" "Objective" @("RewardComponent","UnlockedBuildings","UnlockedProductionRecipes","UnlockedRecruitments") $unlockRewardColumns
Add-SearchIndexItems $searchItems $seasonalRows "seasonal-gate" "Name" @("AllowedSeasons","Kind","Components") $seasonalColumns
Add-SearchIndexItems $searchItems $assetReferenceRows "asset-reference" "Asset" @("AssetType","Component","Name") $assetReferenceColumns
Add-SearchIndexItems $searchItems $visualAudioComponentRows "visual-audio-component" "Name" @("Component","AssetReferences","AudioEvents") $visualAudioComponentColumns
Add-SearchIndexItems $searchItems $toolEditorRows "tools-editor" "Name" @("Area","GroupPath","LinkedTextures","VegetationGroup","VegetationItems","AssetReferences") $toolEditorColumns
Add-SearchIndexItems $searchItems $notificationNarrationRows "notification-narration" "Name" @("Kind","MessageKey","DialogLines") $notificationNarrationColumns
Add-SearchIndexItems $searchItems $terrainPropRows "terrain-prop" "TerrainProp" @("BlockingType","CategoryTags","AllowedSediments") $terrainPropColumns
Add-SearchIndexItems $searchItems $depositResourceTypeRows "deposit-resource-type" "DepositResourceType" @("Category","GeneratedChance","NpcCharacteristic") $depositResourceTypeColumns
Add-SearchIndexItems $searchItems $depositRows "deposit" "Deposit" @("DepositResourceType","HarvestResources","AllowedSediments") $depositColumns
Add-SearchIndexItems $searchItems $mapGenRows "mapgen" "Name" @("Component","LocaTag","Deposits") $mapGenColumns
Add-SearchIndexItems $searchItems $npcUnitRows "npc-unit" "Unit" @("EncounterRefs","RaidCapable","Boss") $npcUnitColumns
Add-SearchIndexItems $searchItems $npcBaseRows "npc-base" "Base" @("EncounterRefs","SpawnUnits","CustomRaid") $npcBaseColumns
Add-SearchIndexItems $searchItems $encounterCombatRows "encounter-combat" "Name" @("Component","References","Values") $encounterCombatColumns
Add-SearchIndexItems $searchItems $factionRows "faction" "Name" @("Component","Faction","SubFaction") $factionColumns
Add-SearchIndexItems $searchItems $productionChainRows "production-chain" "StartResource" @("EndType","EndTarget","Chain") $productionChainColumns
Add-SearchIndexItems $searchItems $artifactRows "artifact" "Artifact" @("Category","CombatBoost","TreasureTier") $artifactColumns
Add-SearchIndexItems $searchItems $treasureHunterRecipeRows "treasure-hunter-recipe" "Recipe" @("TargetCount","ArtifactTargets","CombatBoostTargets") $treasureHunterRecipeColumns
Add-SearchIndexItems $searchItems $treasureHunterTargetRows "treasure-hunter-target" "Target" @("Recipe","TargetCategory") $treasureHunterTargetColumns
Add-SearchIndexItems $searchItems $treasureAreaRows "treasure-area" "TreasureArea" @("Category","Entity") $treasureAreaColumns
Add-SearchIndexItems $searchItems $shrineBuildingRows "shrine-building" "Shrine" @("Abilities","ManaRecipes") $shrineBuildingColumns
Add-SearchIndexItems $searchItems $shrineAbilityRows "shrine-ability" "Ability" @("Type","UsageType","ManaCost") $shrineAbilityColumns
Add-SearchIndexItems $searchItems $shrineRecipeRows "shrine-recipe" "Recipe" @("Identifier","Inputs") $shrineRecipeColumns

Write-SearchIndex $searchItems (Join-Path $outPath "search-index.json")

$readme = @(
  "# Local Generated Catalog",
  "",
  "Generated from local XML files. This directory is ignored by Git.",
  "",
  "Current generated counts:",
  "",
  "- Buildings: $($buildingRows.Count)",
  "- Building dependency rows: $($buildingDependencyRows.Count)",
  "- Building-production links: $($buildingProductionRows.Count)",
  "- Recipes: $($recipeRows.Count)",
  "- Units: $($unitRows.Count)",
  "- Unit equipment rows: $($unitEquipmentRows.Count)",
  "- Resources: $($resourceRows.Count)",
  "- Resource flow rows: $($resourceFlowRows.Count)",
  "- Resource usage rows: $($resourceUsageRows.Count)",
  "- Localization rows: $($localizationRows.Count)",
  "- Localization unique keys: $($localizationUniqueKeys.Count)",
  "- Localization declared tags: $($localizationDeclaredTags.Count)",
  "- Localization unique declared tags: $($localizationUniqueDeclaredTags.Count)",
  "- Localization referenced keys: $($localizationReferencedKeys.Count)",
  "- Localization unique referenced keys: $($localizationUniqueReferencedKeys.Count)",
  "- Localization keys not in local tag index: $($localizationNotInLocalTagIndex.Count)",
  "- Localization unique keys not in local tag index: $($localizationUniqueNotInLocalTagIndex.Count)",
  "- Objective flow rows: $($objectiveFlowRows.Count)",
  "- Objective flow rows with preconditions: $($objectiveFlowWithPreconditions.Count)",
  "- Objective flow rows with rewards: $($objectiveFlowWithRewards.Count)",
  "- Objective flow rows with notifications: $($objectiveFlowWithNotifications.Count)",
  "- Objective flow tech tree rows: $($objectiveFlowTechTree.Count)",
  "- Tech tree rows: $($techTreeRows.Count)",
  "- Tech tree rows with objectives: $($techTreeWithObjectives.Count)",
  "- Unlock gate rows: $($unlockGateRows.Count)",
  "- DLC unlock gate rows: $($unlockGatesDlc.Count)",
  "- Unlock reward rows: $($unlockRewardRows.Count)",
  "- Unlock reward rows for buildings: $($unlockRewardsBuildings.Count)",
  "- Unlock reward rows for recipes: $($unlockRewardsRecipes.Count)",
  "- Unlock reward rows for recruitments: $($unlockRewardsRecruitments.Count)",
  "- Unlock reward rows for tech tree groups: $($unlockRewardsTechTree.Count)",
  "- Seasonal gate rows: $($seasonalRows.Count)",
  "- Seasonal rows with allowed seasons: $($seasonalWithAllowedSeasons.Count)",
  "- Asset reference rows: $($assetReferenceRows.Count)",
  "- Icon references: $($assetIcons.Count)",
  "- Prefab references: $($assetPrefabs.Count)",
  "- Mesh references: $($assetMeshes.Count)",
  "- Texture references: $($assetTextures.Count)",
  "- Audio event references: $($assetAudioEvents.Count)",
  "- Character kit references: $($assetCharacterKits.Count)",
  "- Visual/audio component rows: $($visualAudioComponentRows.Count)",
  "- Visual/audio components with assets: $($visualComponentsWithAssets.Count)",
  "- Visual/audio components with audio: $($visualComponentsWithAudio.Count)",
  "- Tools/editor rows: $($toolEditorRows.Count)",
  "- Tools terrain sediment rows: $($toolTerrainSediments.Count)",
  "- Tools vegetation group rows: $($toolVegetationGroups.Count)",
  "- Tools vegetation rows: $($toolVegetation.Count)",
  "- Tools texture rows: $($toolTextures.Count)",
  "- Tools globals and brush rows: $($toolGlobalsAndBrushes.Count)",
  "- Notification and narration rows: $($notificationNarrationRows.Count)",
  "- Notification rows: $($notificationRows.Count)",
  "- Narration dialog rows: $($narrationDialogRows.Count)",
  "- Referenced notification and narration rows: $($referencedNotificationNarrationRows.Count)",
  "- Unreferenced notification and narration rows: $($unreferencedNotificationNarrationRows.Count)",
  "- Terrain props: $($terrainPropRows.Count)",
  "- Terrain props with blocking: $($terrainPropsWithBlocking.Count)",
  "- Terrain props with sediment rules: $($terrainPropsWithSedimentRules.Count)",
  "- Deposit resource types: $($depositResourceTypeRows.Count)",
  "- Deposits: $($depositRows.Count)",
  "- Deposits with harvest resources: $($depositsWithHarvestResources.Count)",
  "- Growing or regrowing deposits: $($depositsGrowingOrRegrowing.Count)",
  "- Generated deposit types: $($generatedDepositTypes.Count)",
  "- MapGen rows: $($mapGenRows.Count)",
  "- MapGen rows with deposits: $($mapGenDepositRows.Count)",
  "- NPC units: $($npcUnitRows.Count)",
  "- NPC bases: $($npcBaseRows.Count)",
  "- Encounter and combat rows: $($encounterCombatRows.Count)",
  "- Faction rows: $($factionRows.Count)",
  "- NPC bosses: $($npcBossRows.Count)",
  "- Raid-capable NPC units: $($raidCapableNpcUnitRows.Count)",
  "- Custom raid NPC bases: $($customRaidNpcBaseRows.Count)",
  "- NPC unit drop rows: $($npcUnitDropRows.Count)",
  "- Infection-related encounter rows: $($npcInfectionRows.Count)",
  "- Production chains: $($productionChainRows.Count)",
  "- Artifacts and artifact-like resources: $($artifactRows.Count)",
  "- Treasure hunter recipes: $($treasureHunterRecipeRows.Count)",
  "- Treasure hunter targets: $($treasureHunterTargetRows.Count)",
  "- Treasure areas: $($treasureAreaRows.Count)",
  "- Shrine buildings: $($shrineBuildingRows.Count)",
  "- Shrine abilities: $($shrineAbilityRows.Count)",
  "- Shrine recipes: $($shrineRecipeRows.Count)",
  "- Production graph edges: $edgeCount",
  "- Production chain graph edges: $chainEdgeCount",
  "- Package filters: $($packageSummaries.Count)",
  "- Buildings with recipes: $($buildingsWithRecipes.Count)",
  "- Buildings with dependencies: $($buildingsWithDependencies.Count)",
  "- Buildings with storage: $($buildingsWithStorage.Count)",
  "- Recipes with inputs or outputs: $($recipesWithInputOrOutput.Count)",
  "- Units with recruitment cost: $($unitsWithRecruitmentCost.Count)",
  "- Unit equipment rows with producers: $($unitEquipmentWithProducer.Count)",
  "- Unit equipment rows without producers: $($unitEquipmentWithoutProducer.Count)",
  "- Resources produced: $($resourcesProduced.Count)",
  "- Resources consumed: $($resourcesConsumed.Count)",
  "- Resources stored: $($resourcesStored.Count)",
  "- Production chains to buildings: $($productionChainsToBuildings.Count)",
  "- Production chains to units: $($productionChainsToUnits.Count)",
  "- Production chains to shrines: $($productionChainsToShrines.Count)",
  "- Search index items: $($searchItems.Count)",
  "",
  "Files:",
  "",
  '- `buildings.md` / `buildings.csv`',
  '- `building-dependency-matrix.md` / `building-dependency-matrix.csv`',
  '- `building-production.md` / `building-production.csv`',
  '- `recipes.md` / `recipes.csv`',
  '- `units.md` / `units.csv`',
  '- `unit-equipment-matrix.md` / `unit-equipment-matrix.csv`',
  '- `resources.md` / `resources.csv`',
  '- `resource-flow.md` / `resource-flow.csv`',
  '- `resource-usage.md` / `resource-usage.csv`',
  '- `localization-index.md` / `localization-index.csv`',
  '- `objective-flow.md` / `objective-flow.csv`',
  '- `tech-tree.md` / `tech-tree.csv`',
  '- `unlock-gates.md` / `unlock-gates.csv`',
  '- `unlock-rewards.md` / `unlock-rewards.csv`',
  '- `seasonal-gates.md` / `seasonal-gates.csv`',
  '- `asset-references.md` / `asset-references.csv`',
  '- `visual-audio-components.md` / `visual-audio-components.csv`',
  '- `tools-editor.md` / `tools-editor.csv`',
  '- `notification-narration.md` / `notification-narration.csv`',
  '- `terrain-props.md` / `terrain-props.csv`',
  '- `deposit-resource-types.md` / `deposit-resource-types.csv`',
  '- `deposits.md` / `deposits.csv`',
  '- `mapgen.md` / `mapgen.csv`',
  '- `npc-units.md` / `npc-units.csv`',
  '- `npc-bases.md` / `npc-bases.csv`',
  '- `encounter-combat.md` / `encounter-combat.csv`',
  '- `factions.md` / `factions.csv`',
  '- `production-chains.md` / `production-chains.csv`',
  '- `production-graph.md`',
  '- `production-graph.mmd`',
  '- `production-chain-graph.md`',
  '- `production-chain-graph.mmd`',
  '- `search-index.json`',
  '- `systems/artifacts.md` / `.csv`',
  '- `systems/treasure-hunter-recipes.md` / `.csv`',
  '- `systems/treasure-hunter-targets.md` / `.csv`',
  '- `systems/treasure-areas.md` / `.csv`',
  '- `systems/shrine-buildings.md` / `.csv`',
  '- `systems/shrine-abilities.md` / `.csv`',
  '- `systems/shrine-recipes.md` / `.csv`',
  '- `systems/artifact-treasure-shrine-graph.md` / `.mmd`',
  '- `filters/package-summary.md` / `filters/package-summary.csv`',
  '- `filters/packages/<package>/buildings.md` / `.csv`',
  '- `filters/packages/<package>/building-dependency-matrix.md` / `.csv`',
  '- `filters/packages/<package>/building-production.md` / `.csv`',
  '- `filters/packages/<package>/recipes.md` / `.csv`',
  '- `filters/packages/<package>/units.md` / `.csv`',
  '- `filters/packages/<package>/unit-equipment-matrix.md` / `.csv`',
  '- `filters/packages/<package>/resources.md` / `.csv`',
  '- `filters/packages/<package>/resource-flow.md` / `.csv`',
  '- `filters/packages/<package>/resource-usage.md` / `.csv`',
  '- `filters/packages/<package>/localization-index.md` / `.csv`',
  '- `filters/packages/<package>/objective-flow.md` / `.csv`',
  '- `filters/packages/<package>/tech-tree.md` / `.csv`',
  '- `filters/packages/<package>/unlock-gates.md` / `.csv`',
  '- `filters/packages/<package>/unlock-rewards.md` / `.csv`',
  '- `filters/packages/<package>/seasonal-gates.md` / `.csv`',
  '- `filters/packages/<package>/asset-references.md` / `.csv`',
  '- `filters/packages/<package>/visual-audio-components.md` / `.csv`',
  '- `filters/packages/<package>/tools-editor.md` / `.csv`',
  '- `filters/packages/<package>/notification-narration.md` / `.csv`',
  '- `filters/packages/<package>/terrain-props.md` / `.csv`',
  '- `filters/packages/<package>/deposit-resource-types.md` / `.csv`',
  '- `filters/packages/<package>/deposits.md` / `.csv`',
  '- `filters/packages/<package>/mapgen.md` / `.csv`',
  '- `filters/packages/<package>/npc-units.md` / `.csv`',
  '- `filters/packages/<package>/npc-bases.md` / `.csv`',
  '- `filters/packages/<package>/encounter-combat.md` / `.csv`',
  '- `filters/packages/<package>/factions.md` / `.csv`',
  '- `filters/packages/<package>/production-chains.md` / `.csv`',
  '- `filters/packages/<package>/artifacts.md` / `.csv`',
  '- `filters/packages/<package>/treasure-hunter-recipes.md` / `.csv`',
  '- `filters/packages/<package>/shrine-abilities.md` / `.csv`',
  '- `filters/packages/<package>/shrine-recipes.md` / `.csv`',
  '- `filters/packages/<package>/production-graph.md` / `.mmd`',
  '- `filters/production/buildings-with-recipes.md` / `.csv`',
  '- `filters/production/buildings-with-gather-outputs.md` / `.csv`',
  '- `filters/production/buildings-with-storage.md` / `.csv`',
  '- `filters/production/buildings-with-dependencies.md` / `.csv`',
  '- `filters/production/recipes-with-input-output.md` / `.csv`',
  '- `filters/production/units-with-recruitment-cost.md` / `.csv`',
  '- `filters/production/unit-equipment-with-producers.md` / `.csv`',
  '- `filters/production/unit-equipment-without-producers.md` / `.csv`',
  '- `filters/production/resources-produced.md` / `.csv`',
  '- `filters/production/resources-consumed.md` / `.csv`',
  '- `filters/production/resources-stored.md` / `.csv`',
  '- `filters/localization/declared-tags.md` / `.csv`',
  '- `filters/localization/referenced-keys.md` / `.csv`',
  '- `filters/localization/not-in-local-tag-index.md` / `.csv`',
  '- `filters/objectives/with-preconditions.md` / `.csv`',
  '- `filters/objectives/with-rewards.md` / `.csv`',
  '- `filters/objectives/with-notifications.md` / `.csv`',
  '- `filters/objectives/tech-tree.md` / `.csv`',
  '- `filters/progression/tech-tree-with-objectives.md` / `.csv`',
  '- `filters/progression/dlc-unlock-gates.md` / `.csv`',
  '- `filters/progression/unlock-rewards-buildings.md` / `.csv`',
  '- `filters/progression/unlock-rewards-recipes.md` / `.csv`',
  '- `filters/progression/unlock-rewards-recruitments.md` / `.csv`',
  '- `filters/progression/unlock-rewards-tech-tree.md` / `.csv`',
  '- `filters/progression/seasonal-with-allowed-seasons.md` / `.csv`',
  '- `filters/assets/icons.md` / `.csv`',
  '- `filters/assets/prefabs.md` / `.csv`',
  '- `filters/assets/meshes.md` / `.csv`',
  '- `filters/assets/textures.md` / `.csv`',
  '- `filters/assets/audio-events.md` / `.csv`',
  '- `filters/assets/character-kits.md` / `.csv`',
  '- `filters/assets/visual-components-with-assets.md` / `.csv`',
  '- `filters/assets/visual-components-with-audio.md` / `.csv`',
  '- `filters/tools/terrain-sediments.md` / `.csv`',
  '- `filters/tools/vegetation-groups.md` / `.csv`',
  '- `filters/tools/vegetation.md` / `.csv`',
  '- `filters/tools/textures.md` / `.csv`',
  '- `filters/tools/globals-and-brushes.md` / `.csv`',
  '- `filters/notifications/notifications.md` / `.csv`',
  '- `filters/notifications/narration-dialogs.md` / `.csv`',
  '- `filters/notifications/referenced.md` / `.csv`',
  '- `filters/notifications/unreferenced.md` / `.csv`',
  '- `filters/world/terrain-props-with-blocking.md` / `.csv`',
  '- `filters/world/terrain-props-with-sediment-rules.md` / `.csv`',
  '- `filters/world/deposits-with-harvest-resources.md` / `.csv`',
  '- `filters/world/deposits-growing-or-regrowing.md` / `.csv`',
  '- `filters/world/generated-deposit-types.md` / `.csv`',
  '- `filters/world/mapgen-with-deposits.md` / `.csv`',
  '- `filters/combat/npc-units.md` / `.csv`',
  '- `filters/combat/npc-bases.md` / `.csv`',
  '- `filters/combat/bosses.md` / `.csv`',
  '- `filters/combat/raid-capable-units.md` / `.csv`',
  '- `filters/combat/custom-raid-bases.md` / `.csv`',
  '- `filters/combat/drop-parameters.md` / `.csv`',
  '- `filters/combat/infection.md` / `.csv`',
  '- `filters/combat/encounter-target-tags.md` / `.csv`',
  '- `filters/production/production-chains-to-buildings.md` / `.csv`',
  '- `filters/production/production-chains-to-units.md` / `.csv`',
  '- `filters/production/production-chains-to-shrines.md` / `.csv`',
  "",
  "Notes:",
  "",
  '- `OptimalWorkStep` is a building-level timing hint when present.',
  '- `RecipeWorkLoops` is derived from recipe work steps and is not a complete time value by itself.',
  "- Use the CSV files for filtering and spreadsheets.",
  "- Use Mermaid files for visual relationship diagrams.",
  "- Production chains are derived from explicit recipe input/output resources and known end-use references."
)
[System.IO.File]::WriteAllLines((Join-Path $outPath "README.md"), $readme, [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
  Buildings = $buildingRows.Count
  BuildingDependencyRows = $buildingDependencyRows.Count
  BuildingProductionLinks = $buildingProductionRows.Count
  Recipes = $recipeRows.Count
  Units = $unitRows.Count
  UnitEquipmentRows = $unitEquipmentRows.Count
  Resources = $resourceRows.Count
  ResourceFlowRows = $resourceFlowRows.Count
  ResourceUsageRows = $resourceUsageRows.Count
  LocalizationRows = $localizationRows.Count
  LocalizationUniqueKeys = $localizationUniqueKeys.Count
  LocalizationDeclaredTags = $localizationDeclaredTags.Count
  LocalizationUniqueDeclaredTags = $localizationUniqueDeclaredTags.Count
  LocalizationReferencedKeys = $localizationReferencedKeys.Count
  LocalizationUniqueReferencedKeys = $localizationUniqueReferencedKeys.Count
  LocalizationNotInLocalTagIndex = $localizationNotInLocalTagIndex.Count
  LocalizationUniqueNotInLocalTagIndex = $localizationUniqueNotInLocalTagIndex.Count
  ObjectiveFlowRows = $objectiveFlowRows.Count
  ObjectiveFlowWithPreconditions = $objectiveFlowWithPreconditions.Count
  ObjectiveFlowWithRewards = $objectiveFlowWithRewards.Count
  ObjectiveFlowWithNotifications = $objectiveFlowWithNotifications.Count
  ObjectiveFlowTechTreeRows = $objectiveFlowTechTree.Count
  TechTreeRows = $techTreeRows.Count
  TechTreeWithObjectives = $techTreeWithObjectives.Count
  UnlockGateRows = $unlockGateRows.Count
  UnlockGatesDlc = $unlockGatesDlc.Count
  UnlockRewardRows = $unlockRewardRows.Count
  UnlockRewardsBuildings = $unlockRewardsBuildings.Count
  UnlockRewardsRecipes = $unlockRewardsRecipes.Count
  UnlockRewardsRecruitments = $unlockRewardsRecruitments.Count
  UnlockRewardsTechTree = $unlockRewardsTechTree.Count
  SeasonalRows = $seasonalRows.Count
  SeasonalWithAllowedSeasons = $seasonalWithAllowedSeasons.Count
  AssetReferenceRows = $assetReferenceRows.Count
  AssetIcons = $assetIcons.Count
  AssetPrefabs = $assetPrefabs.Count
  AssetMeshes = $assetMeshes.Count
  AssetTextures = $assetTextures.Count
  AssetAudioEvents = $assetAudioEvents.Count
  AssetCharacterKits = $assetCharacterKits.Count
  VisualAudioComponentRows = $visualAudioComponentRows.Count
  VisualComponentsWithAssets = $visualComponentsWithAssets.Count
  VisualComponentsWithAudio = $visualComponentsWithAudio.Count
  ToolEditorRows = $toolEditorRows.Count
  ToolTerrainSediments = $toolTerrainSediments.Count
  ToolVegetationGroups = $toolVegetationGroups.Count
  ToolVegetation = $toolVegetation.Count
  ToolTextures = $toolTextures.Count
  ToolGlobalsAndBrushes = $toolGlobalsAndBrushes.Count
  NotificationNarrationRows = $notificationNarrationRows.Count
  NotificationRows = $notificationRows.Count
  NarrationDialogRows = $narrationDialogRows.Count
  ReferencedNotificationNarrationRows = $referencedNotificationNarrationRows.Count
  UnreferencedNotificationNarrationRows = $unreferencedNotificationNarrationRows.Count
  TerrainProps = $terrainPropRows.Count
  TerrainPropsWithBlocking = $terrainPropsWithBlocking.Count
  TerrainPropsWithSedimentRules = $terrainPropsWithSedimentRules.Count
  DepositResourceTypes = $depositResourceTypeRows.Count
  Deposits = $depositRows.Count
  DepositsWithHarvestResources = $depositsWithHarvestResources.Count
  DepositsGrowingOrRegrowing = $depositsGrowingOrRegrowing.Count
  GeneratedDepositTypes = $generatedDepositTypes.Count
  MapGenRows = $mapGenRows.Count
  MapGenRowsWithDeposits = $mapGenDepositRows.Count
  NpcUnits = $npcUnitRows.Count
  NpcBases = $npcBaseRows.Count
  EncounterCombatRows = $encounterCombatRows.Count
  FactionRows = $factionRows.Count
  NpcBosses = $npcBossRows.Count
  RaidCapableNpcUnits = $raidCapableNpcUnitRows.Count
  CustomRaidNpcBases = $customRaidNpcBaseRows.Count
  NpcUnitDropRows = $npcUnitDropRows.Count
  InfectionEncounterRows = $npcInfectionRows.Count
  ProductionChains = $productionChainRows.Count
  Artifacts = $artifactRows.Count
  TreasureHunterRecipes = $treasureHunterRecipeRows.Count
  TreasureHunterTargets = $treasureHunterTargetRows.Count
  TreasureAreas = $treasureAreaRows.Count
  ShrineBuildings = $shrineBuildingRows.Count
  ShrineAbilities = $shrineAbilityRows.Count
  ShrineRecipes = $shrineRecipeRows.Count
  GraphEdges = $edgeCount
  ProductionChainGraphEdges = $chainEdgeCount
  SearchIndexItems = $searchItems.Count
  PackageFilters = $packageSummaries.Count
  BuildingsWithRecipes = $buildingsWithRecipes.Count
  BuildingsWithDependencies = $buildingsWithDependencies.Count
  BuildingsWithStorage = $buildingsWithStorage.Count
  RecipesWithInputOrOutput = $recipesWithInputOrOutput.Count
  UnitsWithRecruitmentCost = $unitsWithRecruitmentCost.Count
  UnitEquipmentWithProducer = $unitEquipmentWithProducer.Count
  UnitEquipmentWithoutProducer = $unitEquipmentWithoutProducer.Count
  ResourcesProduced = $resourcesProduced.Count
  ResourcesConsumed = $resourcesConsumed.Count
  ResourcesStored = $resourcesStored.Count
  ProductionChainsToBuildings = $productionChainsToBuildings.Count
  ProductionChainsToUnits = $productionChainsToUnits.Count
  ProductionChainsToShrines = $productionChainsToShrines.Count
  Output = Get-RelativePath $outPath
}
