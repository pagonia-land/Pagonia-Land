# Shared helpers for the database-analysis scripts in this folder (analyze / validate / catalog / diff and their callers).
#
# Dot-source from a sibling script:
#   . (Join-Path $PSScriptRoot "common.ps1")
#
# Functions defined here intentionally avoid global state. They only read their
# arguments and return values, so each script keeps full control over its own
# pipeline.

# Cross-platform package extraction. The first segment of a path relative to the
# game root is the package name (core, dlc1, decorations1, tools).
# Handles both Windows backslashes and POSIX forward slashes so the scripts also
# work under pwsh on Linux/macOS.
function Get-GameRelativePackage([string]$Path, [string]$GamePath) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($GamePath)) {
        return ""
    }

    $relative = Get-PathRelativeTo $Path $GamePath
    if ([string]::IsNullOrWhiteSpace($relative)) { return "" }

    return ($relative -split '[\\/]', 2)[0]
}

# Safe relative-path helper. Returns an empty string for unrelated paths instead
# of throwing on Substring length mismatches.
function Get-PathRelativeTo([string]$Path, [string]$BasePath) {
    if ([string]::IsNullOrWhiteSpace($Path) -or [string]::IsNullOrWhiteSpace($BasePath)) {
        return ""
    }

    $normalizedPath = $Path.TrimEnd([char]'\', [char]'/')
    $normalizedBase = $BasePath.TrimEnd([char]'\', [char]'/')

    if ($normalizedPath.Length -le $normalizedBase.Length) { return "" }
    if (-not $normalizedPath.StartsWith($normalizedBase, [System.StringComparison]::OrdinalIgnoreCase)) {
        return ""
    }

    return $normalizedPath.Substring($normalizedBase.Length + 1)
}

# Read a single text child element from an XML node. Returns the trimmed text or
# an empty string when the child is missing.
function Get-NodeText($Node, [string]$ChildName) {
    if ($null -eq $Node) { return "" }
    if ($Node -isnot [System.Xml.XmlNode]) { return ([string]$Node).Trim() }
    $child = $Node.SelectSingleNode($ChildName)
    if ($null -eq $child) { return "" }
    return ([string]$child.InnerText).Trim()
}

# Select one child node by name or XPath. Returns $null when the source is not
# an XML node or no match is found.
function Get-NodeChild($Node, [string]$XPath) {
    if ($null -eq $Node) { return $null }
    if ($Node -isnot [System.Xml.XmlNode]) { return $null }
    return $Node.SelectSingleNode($XPath)
}
