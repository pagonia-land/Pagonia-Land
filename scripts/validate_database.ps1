param(
  [string]$Root = (Get-Location).Path,
  [string]$GameDir = "game-gdb",
  [switch]$TreatUnresolvedAsError
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "common.ps1")

$rootPath = (Resolve-Path -LiteralPath $Root).Path
$gamePath = Join-Path $rootPath $GameDir
$errors = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

function Add-ValidationError([string]$Message) {
  $script:errors.Add($Message) | Out-Null
  Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Add-ValidationWarning([string]$Message) {
  $script:warnings.Add($Message) | Out-Null
  Write-Host "[WARN]  $Message" -ForegroundColor Yellow
}

function Get-RelativePath([string]$Path) {
  return Get-PathRelativeTo $Path $rootPath
}

if (-not (Test-Path -LiteralPath $gamePath)) {
  Add-ValidationError "Game data directory not found: $gamePath"
  exit 1
}

$gamePath = (Resolve-Path -LiteralPath $gamePath).Path

$expectedPackages = @("core", "decorations1", "dlc1", "tools")
foreach ($package in $expectedPackages) {
  $packagePath = Join-Path $gamePath $package
  if (-not (Test-Path -LiteralPath $packagePath)) {
    Add-ValidationWarning "Expected local package folder missing: game-gdb\$package"
  }
}

$xmlFiles = @(Get-ChildItem -LiteralPath $gamePath -Recurse -File -Filter *.xml | Sort-Object FullName)
if ($xmlFiles.Count -eq 0) {
  Add-ValidationError "No XML files found below game directory."
  exit 1
}

$parsedDocs = New-Object System.Collections.Generic.List[object]
$definitions = @{}
$definitionRows = New-Object System.Collections.Generic.List[object]

function Get-GamePackage([string]$Path) {
  return Get-GameRelativePackage $Path $gamePath
}

function Get-Text($Node, [string]$XPath) {
  return Get-NodeText $Node $XPath
}

function Get-ChildNode($Node, [string]$XPath) {
  return Get-NodeChild $Node $XPath
}

function Test-EntityHasValueType($EntityRow, [string]$ValueType) {
  return ($null -ne $EntityRow -and $EntityRow.ValueTypes -contains $ValueType)
}

function Test-EntityLooksLikeUnit($EntityRow) {
  if ($null -eq $EntityRow) { return $false }
  $unitMarkers = @("Unit", "TaggedUnit", "RecruitmentCost", "UnitAnimations", "UnitEncounterParameters", "AspectWorker")
  foreach ($marker in $unitMarkers) {
    if ($EntityRow.ValueTypes -contains $marker) { return $true }
  }
  return $false
}

function Resolve-Entity([string]$Guid) {
  if ([string]::IsNullOrWhiteSpace($Guid) -or $Guid -eq $nullGuid) { return $null }
  if ($definitions.ContainsKey($Guid)) { return $definitions[$Guid] }
  return $null
}

function Add-AggregatedIssue($Rows, [string]$Message, [switch]$AsError) {
  $rowsList = New-Object System.Collections.Generic.List[object]
  foreach ($row in $Rows) {
    $rowsList.Add($row) | Out-Null
  }
  if ($rowsList.Count -eq 0) { return }

  $sample = ($rowsList | Select-Object -First 8 | ForEach-Object {
    if ($_.PSObject.Properties["Detail"]) {
      "$($_.File) [$($_.Entity)] $($_.Detail)"
    } else {
      "$($_.File) [$($_.Entity)]"
    }
  }) -join "; "

  $fullMessage = "$($rowsList.Count) $Message. Sample: $sample"
  if ($AsError) {
    Add-ValidationError $fullMessage
  } else {
    Add-ValidationWarning $fullMessage
  }
}

foreach ($file in $xmlFiles) {
  $relative = Get-RelativePath $file.FullName
  try {
    [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
    if ($doc.DocumentElement.Name -ne "EntityGroup") {
      Add-ValidationWarning "Unexpected XML root '$($doc.DocumentElement.Name)' in $relative"
    }

    $parsedDocs.Add([pscustomobject]@{
      File = $relative
      Package = Get-GamePackage $file.FullName
      Document = $doc
    })

    foreach ($entity in $doc.SelectNodes("//Entity[@Guid]")) {
      $guid = [string]$entity.Guid
      $valueTypes = @()
      $valuesNode = Get-ChildNode $entity "Values"
      if ($valuesNode) {
        $valueTypes = @($valuesNode.ChildNodes | ForEach-Object { $_.LocalName } | Where-Object { $_ })
      }

      $row = [pscustomobject]@{
        Guid = $guid
        Name = [string]$entity.Name
        File = $relative
        Package = Get-GamePackage $file.FullName
        ValueTypes = $valueTypes
        Node = $entity
      }

      $definitionRows.Add($row)
      if (-not $definitions.ContainsKey($guid)) {
        $definitions[$guid] = $row
      }
    }
  } catch {
    Add-ValidationError "XML parse failed in $relative`: $($_.Exception.Message)"
  }
}

$duplicateGroups = @($definitionRows | Group-Object Guid | Where-Object { $_.Count -gt 1 })
foreach ($group in $duplicateGroups) {
  $locations = ($group.Group | ForEach-Object { "$($_.File) [$($_.Name)]" }) -join "; "
  Add-ValidationError "Duplicate entity GUID $($group.Name): $locations"
}

$guidRegex = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"
$nullGuid = "00000000-0000-0000-0000-000000000000"
$referenceCount = 0
$resolvedCount = 0
$nullCount = 0
$unresolved = New-Object System.Collections.Generic.List[object]

foreach ($parsed in $parsedDocs) {
  foreach ($node in $parsed.Document.SelectNodes("//*[not(self::Entity)]")) {
    $text = ($node.InnerText).Trim()
    if ($text -match $guidRegex) {
      $referenceCount++
      if ($text -eq $nullGuid) {
        $nullCount++
      } elseif ($definitions.ContainsKey($text)) {
        $resolvedCount++
      } else {
        $unresolved.Add([pscustomobject]@{
          Guid = $text
          File = $parsed.File
          Element = $node.Name
        })
      }
    }
  }
}

if ($unresolved.Count -gt 0) {
  $grouped = @($unresolved | Group-Object Guid | Sort-Object Count -Descending)
  $sample = ($grouped | Select-Object -First 10 | ForEach-Object {
    $first = $_.Group[0]
    "$($_.Name) ($($_.Count)x, first: $($first.File) <$($first.Element)>)"
  }) -join "; "

  $message = "$($unresolved.Count) unresolved non-null GUID references found. Sample: $sample"
  if ($TreatUnresolvedAsError) {
    Add-ValidationError $message
  } else {
    Add-ValidationWarning $message
  }
}

$invalidBuildingRecipeLinks = New-Object System.Collections.Generic.List[object]
$invalidRecipeResources = New-Object System.Collections.Generic.List[object]
$recipesWithoutInputOutput = New-Object System.Collections.Generic.List[object]
$invalidBuildingCostResources = New-Object System.Collections.Generic.List[object]
$invalidEmploymentUnits = New-Object System.Collections.Generic.List[object]
$invalidRecruitmentCostResources = New-Object System.Collections.Generic.List[object]
$invalidSourceRecruitableUnits = New-Object System.Collections.Generic.List[object]
$buildingProductionLinkCount = 0
$recipeInputOutputResourceCount = 0
$buildingCostResourceCount = 0
$employmentUnitReferenceCount = 0
$recruitmentCostResourceCount = 0

foreach ($row in $definitionRows) {
  $entity = $row.Node
  $values = Get-ChildNode $entity "Values"
  if ($null -eq $values) { continue }

  $production = Get-ChildNode $values "AspectProduction"
  if ($production) {
    foreach ($recipeNode in $production.SelectNodes("Recipes/Item/Content/Recipe")) {
      $recipeGuid = ([string]$recipeNode.InnerText).Trim()
      if ([string]::IsNullOrWhiteSpace($recipeGuid) -or $recipeGuid -eq $nullGuid) { continue }
      $buildingProductionLinkCount++
      $target = Resolve-Entity $recipeGuid
      if (-not (Test-EntityHasValueType $target "ProductionRecipe")) {
        $invalidBuildingRecipeLinks.Add([pscustomobject]@{
          File = $row.File
          Entity = $row.Name
          Detail = "recipe link $recipeGuid does not resolve to ProductionRecipe"
        })
      }
    }
  }

  foreach ($employment in $values.SelectNodes("AspectBuildup/Employment | AspectProduction/Employment | AspectGatherer/Employment | AspectRecruitmentPlace/Employment")) {
    $unitGuid = Get-Text $employment "Unit"
    if ([string]::IsNullOrWhiteSpace($unitGuid) -or $unitGuid -eq $nullGuid) { continue }
    $employmentUnitReferenceCount++
    $target = Resolve-Entity $unitGuid
    if (-not (Test-EntityLooksLikeUnit $target)) {
      $invalidEmploymentUnits.Add([pscustomobject]@{
        File = $row.File
        Entity = $row.Name
        Detail = "employment unit $unitGuid does not resolve to a unit-like entity"
      })
    }
  }

  $buildup = Get-ChildNode $values "AspectBuildup"
  if ($buildup) {
    foreach ($cost in $buildup.SelectNodes("Costs/Item/Content")) {
      $resourceGuid = Get-Text $cost "Resource"
      if ([string]::IsNullOrWhiteSpace($resourceGuid) -or $resourceGuid -eq $nullGuid) { continue }
      $buildingCostResourceCount++
      $target = Resolve-Entity $resourceGuid
      if (-not (Test-EntityHasValueType $target "ResourceDescription")) {
        $invalidBuildingCostResources.Add([pscustomobject]@{
          File = $row.File
          Entity = $row.Name
          Detail = "construction resource $resourceGuid does not resolve to ResourceDescription"
        })
      }
    }
  }

  $recipe = Get-ChildNode $values "ProductionRecipe"
  if ($recipe) {
    $inputOutputCountForRecipe = 0
    foreach ($step in $recipe.SelectNodes("ProductionSteps/Item/Content")) {
      $type = Get-Text $step "Type"
      if ($type -ne "Input" -and $type -ne "Output") { continue }
      $resourceGuid = Get-Text $step "InputOutput/Resource"
      if ([string]::IsNullOrWhiteSpace($resourceGuid) -or $resourceGuid -eq $nullGuid) { continue }
      $recipeInputOutputResourceCount++
      $inputOutputCountForRecipe++
      $target = Resolve-Entity $resourceGuid
      if (-not (Test-EntityHasValueType $target "ResourceDescription")) {
        $invalidRecipeResources.Add([pscustomobject]@{
          File = $row.File
          Entity = $row.Name
          Detail = "$type resource $resourceGuid does not resolve to ResourceDescription"
        })
      }
    }

    if ($inputOutputCountForRecipe -eq 0) {
      $recipesWithoutInputOutput.Add([pscustomobject]@{
        File = $row.File
        Entity = $row.Name
        Detail = "has no non-null input/output resources"
      })
    }
  }

  $recruitmentCost = Get-ChildNode $values "RecruitmentCost"
  if ($recruitmentCost) {
    foreach ($cost in $recruitmentCost.SelectNodes("ResourceCosts/Item/Content")) {
      $resourceGuid = Get-Text $cost "Resource"
      if ([string]::IsNullOrWhiteSpace($resourceGuid)) {
        $resourceGuid = Get-Text $cost "Description"
      }
      if ([string]::IsNullOrWhiteSpace($resourceGuid) -or $resourceGuid -eq $nullGuid) { continue }
      $recruitmentCostResourceCount++
      $target = Resolve-Entity $resourceGuid
      if (-not (Test-EntityHasValueType $target "ResourceDescription")) {
        $invalidRecruitmentCostResources.Add([pscustomobject]@{
          File = $row.File
          Entity = $row.Name
          Detail = "recruitment resource $resourceGuid does not resolve to ResourceDescription"
        })
      }
    }

    $sourceGuid = Get-Text $recruitmentCost "SourceRecruitableUnit"
    if (-not [string]::IsNullOrWhiteSpace($sourceGuid) -and $sourceGuid -ne $nullGuid) {
      $target = Resolve-Entity $sourceGuid
      if (-not (Test-EntityLooksLikeUnit $target)) {
        $invalidSourceRecruitableUnits.Add([pscustomobject]@{
          File = $row.File
          Entity = $row.Name
          Detail = "source recruitable unit $sourceGuid does not resolve to a unit-like entity"
        })
      }
    }
  }
}

Add-AggregatedIssue $invalidBuildingRecipeLinks "building production recipe links do not resolve to recipe entities" -AsError
Add-AggregatedIssue $invalidRecipeResources "recipe input/output resources do not resolve to resource entities" -AsError
Add-AggregatedIssue $invalidBuildingCostResources "building construction cost resources do not resolve to resource entities" -AsError
Add-AggregatedIssue $invalidEmploymentUnits "employment unit references do not resolve to unit-like entities" -AsError
Add-AggregatedIssue $invalidRecruitmentCostResources "unit recruitment cost resources do not resolve to resource entities" -AsError
Add-AggregatedIssue $invalidSourceRecruitableUnits "source recruitable unit references do not resolve to unit-like entities" -AsError

if ($recipesWithoutInputOutput.Count -gt 0) {
  $sample = ($recipesWithoutInputOutput | Select-Object -First 8 | ForEach-Object { "$($_.File) [$($_.Entity)]" }) -join "; "
  Add-ValidationWarning "$($recipesWithoutInputOutput.Count) production recipes have no non-null input/output resources. This may be valid for animation, map, or template recipes. Sample: $sample"
}

$generatedFiles = @(
  "generated\analysis-summary.json",
  "generated\entities.json",
  "generated\references.json"
)

foreach ($relativeGenerated in $generatedFiles) {
  $path = Join-Path $rootPath $relativeGenerated
  if (Test-Path -LiteralPath $path) {
    try {
      Get-Content -Raw -LiteralPath $path | ConvertFrom-Json | Out-Null
    } catch {
      Add-ValidationWarning "Generated JSON is not valid: $relativeGenerated"
    }
  }
}

Write-Host ""
Write-Host "Validation summary"
Write-Host "------------------"
Write-Host "XML files:                  $($xmlFiles.Count)"
Write-Host "Entity definitions:         $($definitionRows.Count)"
Write-Host "Unique entity GUIDs:        $($definitions.Count)"
Write-Host "GUID-like references:       $referenceCount"
Write-Host "Resolved references:        $resolvedCount"
Write-Host "Null GUID references:       $nullCount"
Write-Host "Other unresolved references:$($unresolved.Count)"
Write-Host "Building recipe links:      $buildingProductionLinkCount"
Write-Host "Recipe resource refs:       $recipeInputOutputResourceCount"
Write-Host "Building cost resource refs:$buildingCostResourceCount"
Write-Host "Employment unit refs:       $employmentUnitReferenceCount"
Write-Host "Recruitment resource refs:  $recruitmentCostResourceCount"
Write-Host "Warnings:                   $($warnings.Count)"
Write-Host "Errors:                     $($errors.Count)"

if ($errors.Count -gt 0) {
  exit 1
}

exit 0
