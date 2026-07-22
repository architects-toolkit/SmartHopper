<#
.SYNOPSIS
    Updates the model-verification issue template dropdown with all non-deprecated
    models from each *ProviderModels.cs file.

.DESCRIPTION
    Scans every src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs file,
    extracts non-deprecated model names together with their provider, and rewrites
    the options list in .github/ISSUE_TEMPLATE/model-verification.yml between the
    AUTO-GENERATED-MODEL-OPTIONS markers.

    Models are sorted by provider (alphabetical), then by Rank (descending, so the
    most relevant models appear first within each provider group).

    Exits with code 0 when the template was modified, 1 when no change was needed,
    and 2 on error.

.PARAMETER TemplateFile
    Optional. Path to the issue template YAML file.
    Defaults to .github/ISSUE_TEMPLATE/model-verification.yml relative to the repo root.

.EXAMPLE
    pwsh -File tools/Update-ModelVerificationTemplate.ps1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string] $TemplateFile = ""
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrWhiteSpace($TemplateFile)) {
    $TemplateFile = Join-Path $repoRoot '.github/ISSUE_TEMPLATE/model-verification.yml'
}

$startMarker = '# AUTO-GENERATED-MODEL-OPTIONS-START'
$endMarker   = '# AUTO-GENERATED-MODEL-OPTIONS-END'

# ---------------------------------------------------------------------------
# Helper: Parse model blocks from a ProviderModels.cs file
# ---------------------------------------------------------------------------
function Get-ProviderModelsFromFile($filePath, $providerName) {
    $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    $content = $content -replace '^[\uFEFF]', ''

    $blockRx = [regex]::new(
        'new\s+AIModelCapabilities\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\},?',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
    $blocks = $blockRx.Matches($content)
    $results = [System.Collections.Generic.List[psobject]]::new()

    foreach ($block in $blocks) {
        $text = $block.Value

        # Skip deprecated models
        if ([regex]::IsMatch($text, 'Deprecated\s*=\s*true')) { continue }

        # Extract model name
        $modelMatch = [regex]::Match($text, 'Model\s*=\s*"([^"]*)"')
        if (-not $modelMatch.Success) { continue }
        $model = $modelMatch.Groups[1].Value

        # Extract rank (default to 0 if not present)
        $rankMatch = [regex]::Match($text, 'Rank\s*=\s*(-?\d+)')
        $rank = if ($rankMatch.Success) { [int]$rankMatch.Groups[1].Value } else { 0 }

        $results.Add([pscustomobject]@{
            Provider = $providerName
            Model    = $model
            Rank     = $rank
        })
    }

    return $results
}

# ---------------------------------------------------------------------------
# 1. Discover all *ProviderModels.cs files
# ---------------------------------------------------------------------------
$srcDir = Join-Path $repoRoot 'src'
$providerFiles = Get-ChildItem -Path $srcDir -Filter '*ProviderModels.cs' -Recurse |
    Where-Object {
        $_.FullName -match 'SmartHopper\.Providers\.(\w+)[/\\]\1ProviderModels\.cs$'
    }

if ($providerFiles.Count -eq 0) {
    Write-Error "No *ProviderModels.cs files found under $srcDir"
    exit 2
}

# ---------------------------------------------------------------------------
# 2. Extract non-deprecated models from each file
# ---------------------------------------------------------------------------
$allModels = [System.Collections.Generic.List[psobject]]::new()

foreach ($file in $providerFiles) {
    $providerMatch = [regex]::Match($file.DirectoryName, 'SmartHopper\.Providers\.(\w+)$')
    if (-not $providerMatch.Success) { continue }
    $provider = $providerMatch.Groups[1].Value

    $models = Get-ProviderModelsFromFile -filePath $file.FullName -providerName $provider
    Write-Host "[$provider] Found $($models.Count) non-deprecated model(s)."

    foreach ($m in $models) {
        $allModels.Add($m)
    }
}

Write-Host "Total non-deprecated models: $($allModels.Count)"

# ---------------------------------------------------------------------------
# 3. Sort: provider (alpha ascending), then rank (descending)
# ---------------------------------------------------------------------------
$sorted = $allModels | Sort-Object -Property `
    @{ Expression = { $_.Provider }; Ascending = $true },
    @{ Expression = { $_.Rank };     Descending = $true }

# Build option lines (8-space indent + "- Provider / Model")
$optionLines = [System.Collections.Generic.List[string]]::new()
foreach ($m in $sorted) {
    $optionLines.Add("        - $($m.Provider) / $($m.Model)")
}

# ---------------------------------------------------------------------------
# 4. Read template and replace between markers
# ---------------------------------------------------------------------------
if (-not (Test-Path $TemplateFile)) {
    Write-Error "Template file not found: $TemplateFile"
    exit 2
}

$templateLines = [System.IO.File]::ReadAllLines($TemplateFile, [System.Text.Encoding]::UTF8)

$startIdx = -1
$endIdx   = -1

for ($i = 0; $i -lt $templateLines.Count; $i++) {
    if ($templateLines[$i].TrimEnd() -match [regex]::Escape($startMarker)) {
        $startIdx = $i
    }
    if ($templateLines[$i].TrimEnd() -match [regex]::Escape($endMarker)) {
        $endIdx = $i
    }
}

if ($startIdx -lt 0 -or $endIdx -lt 0 -or $endIdx -le $startIdx) {
    Write-Error "Could not find AUTO-GENERATED-MODEL-OPTIONS markers in $TemplateFile"
    exit 2
}

# Preserve the marker lines themselves, replace everything between
$before  = $templateLines[0..$startIdx]
$after   = $templateLines[$endIdx..($templateLines.Count - 1)]
$newLines = @($before) + @($optionLines) + @($after)

$newContent = ($newLines -join "`n") + "`n"
$oldContent = [System.IO.File]::ReadAllText($TemplateFile, [System.Text.Encoding]::UTF8)

if ($newContent -eq $oldContent) {
    Write-Host "No changes needed in $TemplateFile"
    exit 1
}

[System.IO.File]::WriteAllText($TemplateFile, $newContent, $utf8NoBom)
Write-Host "Updated $TemplateFile with $($optionLines.Count) model option(s)."
exit 0
