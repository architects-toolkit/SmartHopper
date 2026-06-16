<#
.SYNOPSIS
    Updates the provider model summary in DEV.md from *ProviderModels.cs files.

.DESCRIPTION
    Scans every src/SmartHopper.Providers.<Provider>/<Provider>ProviderModels.cs
    file, extracts non-deprecated models that are either defaults or verified,
    and rewrites the "Default Models by Provider" table in DEV.md.

    Exits with code 0 when DEV.md was modified, 1 when no change was needed,
    and 2 on error.

.PARAMETER DevFile
    Optional. Path to DEV.md. Defaults to DEV.md relative to the repo root.

.EXAMPLE
    pwsh -File tools/Update-DevProviderModels.ps1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string] $DevFile = ""
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrWhiteSpace($DevFile)) {
    $DevFile = Join-Path $repoRoot 'DEV.md'
}

function Get-ProviderModelsFromFile($filePath, $providerName) {
    $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    $content = $content -replace '^[\uFEFF]', ''

    $blockRx = [regex]::new(
        'new\s+AIModelCapabilities\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\},?',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    $results = [System.Collections.Generic.List[psobject]]::new()

    foreach ($block in $blockRx.Matches($content)) {
        $text = $block.Value

        if ([regex]::IsMatch($text, 'Deprecated\s*=\s*true')) { continue }

        $modelMatch = [regex]::Match($text, 'Model\s*=\s*"([^"]*)"')
        if (-not $modelMatch.Success) { continue }

        $defaultMatch = [regex]::Match($text, 'Default\s*=\s*([^,\r\n]+)')
        $verified = [regex]::IsMatch($text, 'Verified\s*=\s*true')

        if (-not $defaultMatch.Success -and -not $verified) { continue }

        $capabilitiesMatch = [regex]::Match($text, 'Capabilities\s*=\s*([^,\r\n]+)')
        $streamingMatch = [regex]::Match($text, 'SupportsStreaming\s*=\s*(true|false)')
        $rankMatch = [regex]::Match($text, 'Rank\s*=\s*(-?\d+)')

        $results.Add([pscustomobject]@{
            Provider = $providerName
            Model = $modelMatch.Groups[1].Value
            Verified = $verified
            Streaming = $streamingMatch.Success -and $streamingMatch.Groups[1].Value -eq 'true'
            Deprecated = $false
            DefaultFor = if ($defaultMatch.Success) { ConvertTo-CapabilityList $defaultMatch.Groups[1].Value } else { '-' }
            Capabilities = if ($capabilitiesMatch.Success) { ConvertTo-CapabilityList $capabilitiesMatch.Groups[1].Value } else { '-' }
            Rank = if ($rankMatch.Success) { [int]$rankMatch.Groups[1].Value } else { 0 }
        })
    }

    return $results
}

function Get-DiscouragedModelsFromFile($filePath, $providerName) {
    $content = [System.IO.File]::ReadAllText($filePath, [System.Text.Encoding]::UTF8)
    $content = $content -replace '^[\uFEFF]', ''

    $blockRx = [regex]::new(
        'new\s+AIModelCapabilities\s*\{(?:[^{}]|\{(?:[^{}]|\{[^{}]*\})*\})*\},?',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

    $results = [System.Collections.Generic.List[psobject]]::new()

    foreach ($block in $blockRx.Matches($content)) {
        $text = $block.Value

        $discouragedMatch = [regex]::Match($text, 'DiscouragedForTools\s*=\s*new\s+List<string>\s*\{([^}]*)\}')
        if (-not $discouragedMatch.Success) { continue }

        $modelMatch = [regex]::Match($text, 'Model\s*=\s*"([^"]*)"')
        if (-not $modelMatch.Success) { continue }

        $modelNames = [System.Collections.Generic.List[string]]::new()
        $modelNames.Add($modelMatch.Groups[1].Value)

        $aliasesMatch = [regex]::Match($text, 'Aliases\s*=\s*new\s+List<string>\s*\{([^}]*)\}')
        if ($aliasesMatch.Success) {
            foreach ($aliasMatch in [regex]::Matches($aliasesMatch.Groups[1].Value, '"([^"]+)"')) {
                $alias = $aliasMatch.Groups[1].Value
                if (-not $modelNames.Contains($alias)) {
                    $modelNames.Add($alias)
                }
            }
        }

        $tools = [System.Collections.Generic.List[string]]::new()
        foreach ($toolMatch in [regex]::Matches($discouragedMatch.Groups[1].Value, '"([^"]+)"')) {
            $tool = $toolMatch.Groups[1].Value
            if (-not $tools.Contains($tool)) {
                $tools.Add($tool)
            }
        }

        if ($tools.Count -eq 0) { continue }

        $rankMatch = [regex]::Match($text, 'Rank\s*=\s*(-?\d+)')
        $results.Add([pscustomobject]@{
            Provider = $providerName
            Models = $modelNames
            Tools = $tools
            Rank = if ($rankMatch.Success) { [int]$rankMatch.Groups[1].Value } else { 0 }
        })
    }

    return $results
}

function ConvertTo-CapabilityList($expression) {
    $names = [System.Collections.Generic.List[string]]::new()

    foreach ($match in [regex]::Matches($expression, 'AICapability\.([A-Za-z0-9_]+)')) {
        $name = $match.Groups[1].Value
        if ($name -ne 'None' -and -not $names.Contains($name)) {
            $names.Add($name)
        }
    }

    if ($names.Count -eq 0) {
        return '-'
    }

    return ($names -join ', ')
}

function ConvertTo-MarkdownCell($value) {
    return ($value -replace '\|', '\|').Trim()
}

function ConvertTo-CodeList($values) {
    return (($values | ForEach-Object { "``$_``" }) -join '/')
}

function ConvertTo-CodeCsv($values) {
    if ($values.Count -eq 1 -and $values[0] -eq '*') {
        return 'any tool'
    }

    return (($values | ForEach-Object { "``$_``" }) -join ', ')
}

try {
    $srcDir = Join-Path $repoRoot 'src'
    $providerFiles = Get-ChildItem -Path $srcDir -Filter '*ProviderModels.cs' -Recurse |
        Where-Object {
            $_.FullName -match 'SmartHopper\.Providers\.([A-Za-z0-9]+)[/\\]\1ProviderModels\.cs$'
        }

    if ($providerFiles.Count -eq 0) {
        Write-Error "No provider *ProviderModels.cs files found under $srcDir"
        exit 2
    }

    $allModels = [System.Collections.Generic.List[psobject]]::new()
    $discouragedModels = [System.Collections.Generic.List[psobject]]::new()
    foreach ($file in $providerFiles) {
        $providerMatch = [regex]::Match($file.DirectoryName, 'SmartHopper\.Providers\.([A-Za-z0-9]+)$')
        if (-not $providerMatch.Success) { continue }

        $provider = $providerMatch.Groups[1].Value
        $models = @(Get-ProviderModelsFromFile -filePath $file.FullName -providerName $provider)
        Write-Host "[$provider] Found $($models.Count) documented model(s)."

        foreach ($model in $models) {
            $allModels.Add($model)
        }

        $discouraged = @(Get-DiscouragedModelsFromFile -filePath $file.FullName -providerName $provider)
        foreach ($model in $discouraged) {
            $discouragedModels.Add($model)
        }
    }

    $sorted = $allModels | Sort-Object -Property `
        @{ Expression = { $_.Provider }; Ascending = $true },
        @{ Expression = { $_.Rank }; Descending = $true },
        @{ Expression = { $_.Model }; Ascending = $true }

    $sourceLines = $providerFiles |
        Sort-Object -Property FullName |
        ForEach-Object {
            $rootPath = $repoRoot.Path.TrimEnd('\', '/')
            $relative = $_.FullName.Substring($rootPath.Length + 1).Replace('\', '/')
            "- ``$relative``"
        }

    $brain = [char]::ConvertFromUtf32(0x1F9E0)
    $check = [char]0x2705
    $star = [char]0x2B50

    $sectionLines = [System.Collections.Generic.List[string]]::new()
    $sectionLines.Add("## $brain Default Models by Provider")
    $sectionLines.Add('')
    $sectionLines.Add('The following table summarizes the models explicitly registered as defaults or verified in each provider model registry. Source files:')
    $sectionLines.Add('')
    foreach ($line in $sourceLines) { $sectionLines.Add($line) }
    $sectionLines.Add('')
    $sectionLines.Add('Notes:')
    $sectionLines.Add('- "Default For" lists the feature areas the model is set as default for (e.g., `Text2Text`, `ToolChat`).')
    $sectionLines.Add('- "Capabilities" lists the core capability flags registered for the model.')
    $sectionLines.Add('- "Verified" reflects the `Verified` flag in the registry; "Deprecated" reflects the `Deprecated` flag (none of the current documented models are flagged deprecated).')
    $sectionLines.Add('')
    $sectionLines.Add('| Provider | Model | Verified | Streaming | Deprecated | Default For | Capabilities |')
    $sectionLines.Add('|---|---|:---:|:---:|:---:|---|---|')

    foreach ($model in $sorted) {
        $verified = if ($model.Verified) { $star } else { '-' }
        $streaming = if ($model.Streaming) { $check } else { '-' }
        $deprecated = if ($model.Deprecated) { $check } else { '-' }
        $sectionLines.Add("| $($model.Provider) | ``$($model.Model)`` | $verified | $streaming | $deprecated | $(ConvertTo-MarkdownCell $model.DefaultFor) | $(ConvertTo-MarkdownCell $model.Capabilities) |")
    }
    $sectionLines.Add('')

    if ($discouragedModels.Count -gt 0) {
        $sectionLines.Add('### Discouraged models for script tools')
        $sectionLines.Add('')
        $sectionLines.Add('Some models are still supported but **not recommended** for script-oriented tools due to quality and stability trade-offs. These models are marked with `DiscouragedForTools` in the provider registries and surface in the UI as a "Not Recommended" badge when used with those tools.')
        $sectionLines.Add('')

        $discouragedByProvider = $discouragedModels |
            Sort-Object -Property `
                @{ Expression = { $_.Provider }; Ascending = $true },
                @{ Expression = { $_.Rank }; Descending = $true },
                @{ Expression = { $_.Models[0] }; Ascending = $true } |
            Group-Object -Property Provider

        foreach ($providerGroup in $discouragedByProvider) {
            $sectionLines.Add("- **$($providerGroup.Name)**")
            foreach ($model in $providerGroup.Group) {
                $sectionLines.Add("  - $(ConvertTo-CodeList $model.Models) -> discouraged for: $(ConvertTo-CodeCsv $model.Tools)")
            }
        }

        $sectionLines.Add('')
    }

    if (-not (Test-Path $DevFile)) {
        Write-Error "DEV.md file not found: $DevFile"
        exit 2
    }

    $devLines = [System.IO.File]::ReadAllLines($DevFile, [System.Text.Encoding]::UTF8)
    $startIdx = -1
    $endIdx = -1

    for ($i = 0; $i -lt $devLines.Count; $i++) {
        if ($devLines[$i] -match '^##\s+.*Default Models by Provider\s*$') {
            $startIdx = $i
            continue
        }

        if ($startIdx -ge 0 -and $i -gt $startIdx -and $devLines[$i] -match '^##\s+') {
            $endIdx = $i
            break
        }
    }

    if ($startIdx -lt 0 -or $endIdx -le $startIdx) {
        Write-Error 'Could not find the Default Models by Provider section boundaries in DEV.md'
        exit 2
    }

    $before = if ($startIdx -gt 0) { $devLines[0..($startIdx - 1)] } else { @() }
    $after = $devLines[$endIdx..($devLines.Count - 1)]
    $newLines = @($before) + @($sectionLines) + @($after)
    $newContent = ($newLines -join "`n") + "`n"
    $oldContent = [System.IO.File]::ReadAllText($DevFile, [System.Text.Encoding]::UTF8)

    if ($newContent -eq $oldContent) {
        Write-Host "No changes needed in $DevFile"
        exit 1
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($DevFile, $newContent, $utf8NoBom)
    Write-Host "Updated $DevFile with $($sorted.Count) provider model row(s)."
    exit 0
}
catch {
    Write-Error $_
    exit 2
}
