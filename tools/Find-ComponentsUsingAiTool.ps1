# Find all SmartHopper component files that declare a specific AI tool in UsingAiTools
# Usage: .\Find-ComponentsUsingAiTool.ps1 -AiTool "text2text"

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AiTool,

    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$componentsDir = Join-Path $RepoRoot "src\SmartHopper.Components"
$componentsDir = Resolve-Path $componentsDir

$scriptPath = Join-Path $PSScriptRoot "Get-ComponentAiTools.ps1"

$matched = @()

$componentFiles = Get-ChildItem -Path $componentsDir -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -ne "AssemblyInfo.cs" }

foreach ($file in $componentFiles) {
    $tools = & $scriptPath -Path $file.FullName
    if ($tools -and $tools -contains $AiTool) {
        $relativePath = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/')
        $matched += $relativePath
    }
}

$matched | ForEach-Object { $_ }
exit 0
