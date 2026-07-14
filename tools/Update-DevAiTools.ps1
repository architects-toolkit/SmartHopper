<#
.SYNOPSIS
    Synchronizes the AI Tools table in DEV.md with the actual tool definitions in source code.

.DESCRIPTION
    Scans all .cs files under src/SmartHopper.Core.Grasshopper/AITools (and other configured paths),
    extracts AITool definitions, and ensures each tool has a row in the DEV.md "AI Tools" table.

    Inconsistencies reported:
    - Tools in code but missing from DEV.md (auto-added unless -DryRun)
    - Tools in DEV.md but not found in code (orphaned)
    - Mismatched category or description between code and DEV.md
    - Missing status columns or other malformed rows

    Exits with code 0 when DEV.md was modified, 1 when no change was needed,
    and 2 on error.

.PARAMETER DevFile
    Optional. Path to DEV.md. Defaults to DEV.md relative to the repo root.

.PARAMETER ToolsDir
    Optional. Path to the AITools source directory. Defaults to
    src/SmartHopper.Core.Grasshopper/AITools relative to the repo root.

.PARAMETER DryRun
    If set, prints what would change without writing to DEV.md.

.PARAMETER Update
    If set, also updates category and description for existing rows from code values.
    Development status columns (Planned, In Progress, Testing, Released) are preserved.

.EXAMPLE
    pwsh -File tools/Update-DevAiTools.ps1

.EXAMPLE
    pwsh -File tools/Update-DevAiTools.ps1 -DryRun

.EXAMPLE
    pwsh -File tools/Update-DevAiTools.ps1 -Update
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)][string] $DevFile = "",
    [Parameter(Mandatory = $false)][string] $ToolsDir = "",
    [Parameter(Mandatory = $false)][switch] $DryRun,
    [Parameter(Mandatory = $false)][switch] $Update
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrWhiteSpace($DevFile)) {
    $DevFile = Join-Path $repoRoot 'DEV.md'
}
if ([string]::IsNullOrWhiteSpace($ToolsDir)) {
    $ToolsDir = Join-Path $repoRoot 'src\SmartHopper.Core.Grasshopper\AITools'
}

# ---------------------------------------------------------------------------
# Regex helpers
# ---------------------------------------------------------------------------

function Get-BalancedBlockRegex {
    param([string]$Open, [string]$Close)
    return "(?:[^$Open$Close]|$Open(?:[^$Open$Close]|$Open(?:[^$Open$Close]|$Open[^$Open$Close]*$Close)*$Close)*$Close)*"
}

function Get-AIToolBlocks($content) {
    $pattern = 'new\s+AITool\s*\((?<block>' + (Get-BalancedBlockRegex -Open '\(' -Close '\)') + ')\)'
    $rx = [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $rx.Matches($content)
}

function Get-BlockProperty($block, $propName) {
    # Matches C# string literals: "...", @"...", $"...", $@"...", @$"..."
    $pattern = $propName + ':\s*(?:@?\$|\$?@)?"(?<val>(?:[^"\\]|\\.|"")*?)"'
    $m = [regex]::Match($block, $pattern)
    if ($m.Success) {
        return $m.Groups['val'].Value
    }
    return $null
}

function Get-NameExpression($block) {
    $pattern = 'name:\s*(?:"(?<literal>[^"]+)"|this\.(?<field>\w+)|(?<var>\w+)|\$"(?<interp>[^"]*)")'
    $m = [regex]::Match($block, $pattern)
    if (-not $m.Success) { return $null }

    if ($m.Groups['literal'].Success) {
        return @{ Kind = 'literal'; Value = $m.Groups['literal'].Value }
    }
    if ($m.Groups['field'].Success) {
        return @{ Kind = 'field'; Value = $m.Groups['field'].Value }
    }
    if ($m.Groups['var'].Success) {
        return @{ Kind = 'variable'; Value = $m.Groups['var'].Value }
    }
    if ($m.Groups['interp'].Success) {
        return @{ Kind = 'interp'; Value = $m.Groups['interp'].Value }
    }
    return $null
}

function Resolve-FieldValue($content, $fieldName) {
    $pattern = 'private\s+(?:readonly\s+)?string\s+' + $fieldName + '\s*=\s*"([^"]+)"'
    $m = [regex]::Match($content, $pattern)
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Resolve-DescriptionPlaceholders($description, $properties) {
    if (-not $description) { return $description }
    $result = $description
    foreach ($match in [regex]::Matches($description, '\{this\.(\w+)\}')) {
        $propName = $match.Groups[1].Value
        if ($properties.ContainsKey($propName)) {
            $result = $result -replace [regex]::Escape($match.Value), $properties[$propName]
        }
    }
    return $result
}

function Resolve-VariableValue($content, $varName) {
    $escaped = [regex]::Escape($varName)

    # Pattern 1: interpolated string with this.Property suffix
    $p1 = '(?:private\s+)?(?:const\s+)?string\s+' + $escaped + '\s*=\s*\$"\{this\.(\w+)\}_([^"]+)"'
    $m1 = [regex]::Match($content, $p1)
    if ($m1.Success) {
        return @{ Kind = 'interp'; Property = $m1.Groups[1].Value; Suffix = $m1.Groups[2].Value }
    }

    # Pattern 2: simple string literal
    $p2 = '(?:private\s+)?(?:const\s+)?string\s+' + $escaped + '\s*=\s*"([^"]+)"'
    $m2 = [regex]::Match($content, $p2)
    if ($m2.Success) {
        return @{ Kind = 'literal'; Value = $m2.Groups[1].Value }
    }

    return $null
}

function Get-DerivedClassesInfo($allFiles, $baseClassName) {
    $results = [System.Collections.Generic.List[psobject]]::new()
    $pattern = 'class\s+(\w+)\s*:\s*' + [regex]::Escape($baseClassName)
    foreach ($file in $allFiles) {
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $content = $content -replace '^[\uFEFF]', ''
        $classMatch = [regex]::Match($content, $pattern)
        if (-not $classMatch.Success) { continue }

        $className = $classMatch.Groups[1].Value
        $propValues = @{}
        foreach ($propMatch in [regex]::Matches($content, 'protected\s+(?:override|virtual)\s+\??\w+\s+(\w+)\s*=>\s*"?([^"";\r\n]+)"?;?')) {
            $propName = $propMatch.Groups[1].Value
            $propValue = $propMatch.Groups[2].Value.Trim() -replace '^\?\s*', ''
            if ($propValue.StartsWith('"') -and $propValue.EndsWith('"')) {
                $propValue = $propValue.Substring(1, $propValue.Length - 2)
            }
            $propValues[$propName] = $propValue
        }

        $results.Add([pscustomobject]@{
            File = $file
            ClassName = $className
            Properties = $propValues
        })
    }
    return $results
}

# ---------------------------------------------------------------------------
# Scan source files
# ---------------------------------------------------------------------------

function Get-ToolsFromDirectory($directory) {
    $allCsFiles = Get-ChildItem -Path $directory -Filter '*.cs' -Recurse -File |
        Where-Object { $_.Name -notmatch '\.Designer\.cs$|\.g\.cs$|AssemblyInfo\.cs$' }

    $tools = [System.Collections.Generic.List[psobject]]::new()
    $errors = [System.Collections.Generic.List[string]]::new()

    foreach ($file in $allCsFiles) {
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $content = $content -replace '^[\uFEFF]', ''

        $hasProvider = $content -match ':\s*IAIToolProvider|:\s*DiscourseToolsBase'
        if (-not $hasProvider) { continue }

        $isAbstract = $content -match 'abstract\s+class'
        $blocks = Get-AIToolBlocks -content $content
        if ($blocks.Count -eq 0) { continue }

        foreach ($blockMatch in $blocks) {
            $block = $blockMatch.Groups['block'].Value
            $nameExpr = Get-NameExpression -block $block
            $category = Get-BlockProperty -block $block -propName 'category'
            $description = Get-BlockProperty -block $block -propName 'description'

            if (-not $nameExpr) {
                $errors.Add("[$($file.Name)] Could not extract tool name from AITool block")
                continue
            }

            $toolNames = [System.Collections.Generic.List[string]]::new()

            switch ($nameExpr.Kind) {
                'literal' {
                    $toolNames.Add($nameExpr.Value)
                }
                'field' {
                    $resolved = Resolve-FieldValue -content $content -fieldName $nameExpr.Value
                    if ($resolved) { $toolNames.Add($resolved) }
                    else { $errors.Add("[$($file.Name)] Could not resolve field '$($nameExpr.Value)'") }
                }
                'variable' {
                    $resolved = Resolve-VariableValue -content $content -varName $nameExpr.Value
                    if (-not $resolved) {
                        $errors.Add("[$($file.Name)] Could not resolve variable '$($nameExpr.Value)'")
                        continue
                    }
                    if ($resolved.Kind -eq 'literal') {
                        $toolNames.Add($resolved.Value)
                    }
                    elseif ($resolved.Kind -eq 'interp') {
                        $baseClassMatch = [regex]::Match($content, 'class\s+(\w+)\s*:\s*(\w+)')
                        if ($baseClassMatch.Success) {
                            $baseClassName = $baseClassMatch.Groups[1].Value
                            $derived = Get-DerivedClassesInfo -allFiles $allCsFiles -baseClassName $baseClassName
                            foreach ($d in $derived) {
                                $prefix = $d.Properties[$resolved.Property]
                                if ($prefix) {
                                    $toolName = "$prefix`_$($resolved.Suffix)"
                                    $resolvedDesc = Resolve-DescriptionPlaceholders -description $description -properties $d.Properties
                                    $tools.Add([pscustomobject]@{
                                        Name = $toolName
                                        Category = $category
                                        Description = $resolvedDesc
                                        SourceFile = $file.Name
                                        IsAbstract = $isAbstract
                                    })
                                }
                                else {
                                    $errors.Add("[$($d.File.Name)] Missing property '$($resolved.Property)' in '$($d.ClassName)'")
                                }
                            }
                            continue  # skip the default tool addition below
                        }
                    }
                }
                'interp' {
                    $interp = $nameExpr.Value
                    if ($interp -match '\{this\.(\w+)\}_(.+)') {
                        $propName = $Matches[1]
                        $suffix = $Matches[2]
                        $baseClassMatch = [regex]::Match($content, 'class\s+(\w+)\s*:\s*(\w+)')
                        if ($baseClassMatch.Success) {
                            $baseClassName = $baseClassMatch.Groups[1].Value
                            $derived = Get-DerivedClassesInfo -allFiles $allCsFiles -baseClassName $baseClassName
                            foreach ($d in $derived) {
                                $prefix = $d.Properties[$propName]
                                if ($prefix) {
                                    $toolNames.Add("$prefix`_$suffix")
                                }
                            }
                        }
                    }
                    else {
                        $errors.Add("[$($file.Name)] Unhandled interpolated name: $interp")
                    }
                }
            }

            foreach ($toolName in $toolNames) {
                $tools.Add([pscustomobject]@{
                    Name = $toolName
                    Category = $category
                    Description = $description
                    SourceFile = $file.Name
                    IsAbstract = $isAbstract
                })
            }
        }
    }

    return @{ Tools = $tools; Errors = $errors }
}

# ---------------------------------------------------------------------------
# Parse DEV.md AI Tools table
# ---------------------------------------------------------------------------

function Get-DevTableBounds($devLines) {
    $startIdx = -1
    $endIdx = -1

    for ($i = 0; $i -lt $devLines.Count; $i++) {
        if ($devLines[$i] -match '^###\s+AI\s+Tools\s*$') {
            $startIdx = $i
            continue
        }
        if ($startIdx -ge 0 -and $i -gt $startIdx -and $devLines[$i] -match '^#{1,3}\s+') {
            $endIdx = $i
            break
        }
    }
    if ($endIdx -lt 0 -and $startIdx -ge 0) { $endIdx = $devLines.Count }
    return @{ Start = $startIdx; End = $endIdx }
}

function ConvertFrom-DevTableRows($devLines, $startIdx, $endIdx) {
    $rows = [System.Collections.Generic.List[psobject]]::new()
    $inTable = $false

    for ($i = $startIdx; $i -lt $endIdx; $i++) {
        $line = $devLines[$i]
        if ($line -match '^\|\s*Tool Name') { $inTable = $true; continue }
        if ($line -match '^\|[-\s:|]+\|$') { continue }
        if ($inTable -and $line -match '^\|') {
            $cellText = $line.TrimStart('|').TrimEnd('|')
            $cells = $cellText -split '\s*\|\s*'
            if ($cells.Count -ge 7) {
                $rows.Add([pscustomobject]@{
                    Name = $cells[0].Trim().Trim('`')
                    Category = $cells[1].Trim()
                    Description = $cells[2].Trim()
                    Statuses = @($cells[3].Trim(); $cells[4].Trim(); $cells[5].Trim(); $cells[6].Trim())
                    LineIndex = $i
                })
            }
            else {
                $rows.Add([pscustomobject]@{
                    Name = '<malformed>'
                    Category = ''
                    Description = $line.Trim()
                    Statuses = @()
                    LineIndex = $i
                    Malformed = $true
                })
            }
        }
    }
    return $rows
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

try {
    if (-not (Test-Path $DevFile)) {
        Write-Error "DEV.md not found: $DevFile"
        exit 2
    }
    if (-not (Test-Path $ToolsDir)) {
        Write-Error "Tools directory not found: $ToolsDir"
        exit 2
    }

    $devContent = [System.IO.File]::ReadAllText($DevFile, [System.Text.Encoding]::UTF8)
    $devLines = $devContent -split "`r?`n"
    $bounds = Get-DevTableBounds -devLines $devLines

    if ($bounds.Start -lt 0) {
        Write-Error "Could not find '### AI Tools' section in DEV.md"
        exit 2
    }

    Write-Host "Found AI Tools section at lines $($bounds.Start + 1) to $($bounds.End)"

    $devRows = ConvertFrom-DevTableRows -devLines $devLines -startIdx $bounds.Start -endIdx $bounds.End
    Write-Host "DEV.md contains $($devRows.Count) AI tool row(s)."

    $scanResult = Get-ToolsFromDirectory -directory $ToolsDir
    $codeTools = $scanResult.Tools
    $scanErrors = $scanResult.Errors

    Write-Host "Found $($codeTools.Count) tool definition(s) in source code."

    if ($scanErrors.Count -gt 0) {
        Write-Host ""
        Write-Host "=== Scan Errors ===" -ForegroundColor Red
        foreach ($err in $scanErrors) {
            Write-Host "  ERROR: $err" -ForegroundColor Red
        }
    }

    $devByName = @{}
    foreach ($row in $devRows) {
        if (-not $row.Malformed -and -not [string]::IsNullOrWhiteSpace($row.Name)) {
            $devByName[$row.Name] = $row
        }
    }

    $codeByName = @{}
    foreach ($tool in $codeTools) {
        if (-not $codeByName.ContainsKey($tool.Name)) {
            $codeByName[$tool.Name] = $tool
        }
    }

    $inconsistencies = [System.Collections.Generic.List[psobject]]::new()

    foreach ($name in $codeByName.Keys | Sort-Object) {
        if (-not $devByName.ContainsKey($name)) {
            $inconsistencies.Add([pscustomobject]@{ Type = 'MissingInDev'; Name = $name; CodeTool = $codeByName[$name]; DevRow = $null })
        }
    }

    foreach ($name in $devByName.Keys | Sort-Object) {
        if (-not $codeByName.ContainsKey($name)) {
            $inconsistencies.Add([pscustomobject]@{ Type = 'OrphanedInDev'; Name = $name; CodeTool = $null; DevRow = $devByName[$name] })
        }
    }

    foreach ($name in $codeByName.Keys | Sort-Object) {
        if ($devByName.ContainsKey($name)) {
            $ct = $codeByName[$name]
            $dr = $devByName[$name]
            $issues = [System.Collections.Generic.List[string]]::new()
            if ($ct.Category -and $dr.Category -and $ct.Category -ne $dr.Category) {
                $issues.Add("category: dev='$($dr.Category)' vs code='$($ct.Category)'")
            }
            if ($ct.Description -and $dr.Description -and $ct.Description -ne $dr.Description) {
                $issues.Add("description mismatch")
            }
            if ($issues.Count -gt 0) {
                $inconsistencies.Add([pscustomobject]@{ Type = 'Mismatched'; Name = $name; CodeTool = $ct; DevRow = $dr; Issues = $issues })
            }
        }
    }

    foreach ($row in $devRows | Where-Object { $_.Malformed }) {
        $inconsistencies.Add([pscustomobject]@{ Type = 'Malformed'; Name = '<malformed>'; CodeTool = $null; DevRow = $row })
    }

    Write-Host ""
    Write-Host "=== Inconsistency Report ===" -ForegroundColor Cyan

    $missing = $inconsistencies | Where-Object { $_.Type -eq 'MissingInDev' }
    $orphaned = $inconsistencies | Where-Object { $_.Type -eq 'OrphanedInDev' }
    $mismatched = $inconsistencies | Where-Object { $_.Type -eq 'Mismatched' }
    $malformed = $inconsistencies | Where-Object { $_.Type -eq 'Malformed' }

    if ($missing.Count -gt 0) {
        Write-Host ""
        Write-Host "Tools in CODE but MISSING from DEV.md ($($missing.Count)):" -ForegroundColor Yellow
        foreach ($item in $missing) {
            $t = $item.CodeTool
            Write-Host "  + $($item.Name)  [$($t.SourceFile)] cat='$($t.Category)'" -ForegroundColor Yellow
            if ($t.Description) { Write-Host "    desc: $($t.Description)" -ForegroundColor DarkGray }
        }
    }

    if ($orphaned.Count -gt 0) {
        Write-Host ""
        Write-Host "Tools in DEV.md but NOT FOUND in code ($($orphaned.Count)):" -ForegroundColor Magenta
        foreach ($item in $orphaned) {
            Write-Host "  - $($item.Name)  [line $($item.DevRow.LineIndex + 1)]" -ForegroundColor Magenta
        }
    }

    if ($mismatched.Count -gt 0) {
        Write-Host ""
        Write-Host "Mismatched properties ($($mismatched.Count)):" -ForegroundColor DarkYellow
        foreach ($item in $mismatched) {
            Write-Host "  ~ $($item.Name): $($item.Issues -join ', ')" -ForegroundColor DarkYellow
        }
    }

    if ($malformed.Count -gt 0) {
        Write-Host ""
        Write-Host "Malformed DEV.md rows ($($malformed.Count)):" -ForegroundColor Red
        foreach ($item in $malformed) {
            Write-Host "  ! line $($item.DevRow.LineIndex + 1): $($item.DevRow.Description)" -ForegroundColor Red
        }
    }

    if ($inconsistencies.Count -eq 0) {
        Write-Host "No inconsistencies found. DEV.md is in sync with source code." -ForegroundColor Green
    }

    $needsUpdate = ($missing.Count -gt 0) -or ($mismatched.Count -gt 0) -or $Update
    if (-not $needsUpdate) {
        Write-Host ""
        Write-Host "No update needed."
        exit 1
    }

    if ($DryRun) {
        Write-Host ""
        Write-Host "(Dry-run mode: no files were modified.)" -ForegroundColor Cyan
        exit 1
    }

    # Rebuild the table section
    $tableStart = -1
    $tableEnd = -1
    for ($i = $bounds.Start; $i -lt $bounds.End; $i++) {
        if ($devLines[$i] -match '^\|\s*Tool Name' -and $tableStart -lt 0) { $tableStart = $i }
        if ($tableStart -ge 0 -and $devLines[$i] -match '^\|' -and $tableEnd -lt $i) { $tableEnd = $i }
    }
    if ($tableStart -lt 0) {
        Write-Error "Could not find table start in AI Tools section"
        exit 2
    }

    $before = $devLines[0..$tableStart]

    # Find the first non-table line after the table
    $notesStart = $tableEnd + 1
    for ($i = $tableEnd + 1; $i -lt $bounds.End; $i++) {
        if ($devLines[$i] -notmatch '^\|' -and $devLines[$i].Trim() -ne '') {
            $notesStart = $i
            break
        }
    }
    $after = if ($notesStart -lt $devLines.Count) { $devLines[$notesStart..($devLines.Count - 1)] } else { @() }

    # Build new rows: existing dev rows + missing code tools
    $newRows = [System.Collections.Generic.List[string]]::new()
    $newRows.Add('| Tool Name | Category | Description | Planned | In Progress | Testing | Released |')
    $newRows.Add('|-----------|----------|-------------|:-------:|:-----------:|:-------:|:--------:|')

    # Build rows: preserve DEV.md values, or override from code when -Update is passed
    foreach ($row in $devRows | Where-Object { -not $_.Malformed }) {
        $name = $row.Name
        $cat = $row.Category
        $desc = $row.Description
        $statuses = $row.Statuses

        if ($Update -and $codeByName.ContainsKey($name)) {
            $ct = $codeByName[$name]
            if ($ct.Category) { $cat = $ct.Category }
            if ($ct.Description) { $desc = $ct.Description }
        }

        $newRows.Add("| ``$name`` | $cat | $desc | $($statuses[0]) | $($statuses[1]) | $($statuses[2]) | $($statuses[3]) |")
    }

    # Add missing tools with default statuses
    foreach ($name in $codeByName.Keys | Sort-Object) {
        if (-not $devByName.ContainsKey($name)) {
            $ct = $codeByName[$name]
            $cat = if ($ct.Category) { $ct.Category } else { 'TBD' }
            $desc = if ($ct.Description) { $ct.Description } else { 'TBD' }
            $newRows.Add("| ``$name`` | $cat | $desc | - | - | - | - |")
        }
    }

    # Allow one blank line after table before notes
    $newSection = [System.Collections.Generic.List[string]]::new()
    $newSection.AddRange([string[]]$before)
    $newSection.AddRange([string[]]$newRows)
    if ($after.Count -gt 0) { $newSection.Add('') }
    $newSection.AddRange([string[]]$after)

    # Remove trailing empty lines inherited from the original file so the
    # output ends with exactly one trailing newline.
    while ($newSection.Count -gt 0 -and [string]::IsNullOrWhiteSpace($newSection[$newSection.Count - 1])) {
        $newSection.RemoveAt($newSection.Count - 1)
    }

    $newContent = ($newSection -join "`n") + "`n"
    $oldContent = [System.IO.File]::ReadAllText($DevFile, [System.Text.Encoding]::UTF8)

    if ($newContent -eq $oldContent) {
        Write-Host "No content changes."
        exit 1
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($DevFile, $newContent, $utf8NoBom)
    Write-Host "Updated $DevFile with $($newRows.Count - 2) AI tool row(s)."
    exit 0
}
catch {
    Write-Error $_
    exit 2
}
