# Unified script that scans project structure and updates both
# .github/labels.yml and .github/labeler.yml with dynamic labels.
# Only ADDS new component:/scope:/provider: labels; never removes existing ones.
# Preserves all non-dynamic labels (status:, priority:, close:, automated, etc.)

[CmdletBinding()]
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$Apply,
    [switch]$Check
)

Set-Location (Resolve-Path $RepoRoot)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

$labelsPath = Resolve-Path ".github\labels.yml"
$labelerPath = Resolve-Path ".github\labeler.yml"

# ─── Helpers ────────────────────────────────────────────────────────

function Get-ComponentLabelName {
    param([string]$FileName)
    # Trim .cs and Component/Components suffix
    $base = $FileName -replace '\.cs$',''
    $base = $base -replace 'Components?$',''

    # If starts with AI, add space after AI (but keep the rest as-is)
    if ($base -match '^AI(.+)') {
        return "AI $($Matches[1])"
    }
    return $base
}

function Get-ComponentFileFilter {
    param([System.IO.FileInfo]$File)
    $File.Name -notlike "*.Designer.cs" -and
    $File.Name -notlike "*Attributes.cs" -and
    $File.Name -ne "AssemblyInfo.cs" -and
    $File.Name -ne "SmartHopperAssemblyPriority.cs" -and
    $File.Directory.Name -ne "Properties" -and
    $File.FullName -notmatch '\\obj\\'
}

# ─── 1. Discover component: labels ──────────────────────────────────

$componentLabels = @{}
$componentEntries = @()
$componentsDir = "src\SmartHopper.Components"

if (Test-Path $componentsDir) {
    $componentFiles = Get-ChildItem -Path $componentsDir -Filter "*.cs" -Recurse |
        Where-Object { Get-ComponentFileFilter $_ } |
        Sort-Object FullName

    foreach ($file in $componentFiles) {
        $name = Get-ComponentLabelName $file.Name
        $labelName = "component: $name"
        $relative = $file.FullName.Substring((Resolve-Path $RepoRoot).Path.Length).TrimStart('\','/')

        $componentLabels[$labelName] = @{
            Name = $labelName
            Color = "1B2638"
            Description = "Issues related to the $name component"
        }

        $componentEntries += @{
            Label = $labelName
            Path = $relative.Replace('\', '/')
        }
    }
}

# ─── 2. Discover scope: labels ─────────────────────────────────────

$scopeLabels = @{}
$scopeDirs = @("src\SmartHopper.Core", "src\SmartHopper.Infrastructure")
foreach ($dir in $scopeDirs) {
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Directory |
            Where-Object { $_.Name -ne "Properties" -and $_.Name -notlike "*.Tests" -and $_.Name -notin @("obj", "Resources", "bin") } |
            ForEach-Object {
                $folder = $_.Name
                $labelName = "scope: $folder"
                $scopeLabels[$labelName] = @{
                    Name = $labelName
                    Color = "000000"
                    Description = "Issues related to the $folder area"
                }
            }
    }
}

# ─── 3. Discover provider: labels ────────────────────────────────────

$providerLabels = @{}
Get-ChildItem -Path "src" -Directory -Filter "SmartHopper.Providers.*" |
    ForEach-Object {
        $providerName = $_.Name -replace '^SmartHopper\.Providers\.',''
        $labelName = "provider: $providerName"
        $providerLabels[$labelName] = @{
            Name = $labelName
            Color = "1A3636"
            Description = "Issues related to the $providerName provider"
        }
    }

# ─── 4. Build labels.yml content ────────────────────────────────────

$dynamicPrefixes = @('component:', 'scope:', 'provider:')
$preserveLabels = @()

if (Test-Path $labelsPath) {
    $content = Get-Content -Raw $labelsPath
    $pattern = '(?m)^- name: "(?<name>[^"]+)"\r?\n  color: "(?<color>[^"]+)"\r?\n  description: "(?<desc>[^"]*)"'
    $labelMatches = [regex]::Matches($content, $pattern)
    foreach ($m in $labelMatches) {
        $label = @{
            Name = $m.Groups['name'].Value
            Color = $m.Groups['color'].Value
            Description = $m.Groups['desc'].Value
        }
        $isDynamic = $false
        foreach ($prefix in $dynamicPrefixes) {
            if ($label.Name.StartsWith($prefix)) { $isDynamic = $true; break }
        }
        if (-not $isDynamic) { $preserveLabels += $label }
    }
}

$finalLabels = $preserveLabels | ForEach-Object { $_ }
$existingNames = $preserveLabels | ForEach-Object { $_.Name } | Sort-Object -Unique

foreach ($label in ($componentLabels.Values + $scopeLabels.Values + $providerLabels.Values | Sort-Object Name)) {
    if ($existingNames -notcontains $label.Name) {
        $finalLabels += @{
            Name = $label.Name
            Color = $label.Color
            Description = $label.Description
        }
    }
}

$finalLabels = $finalLabels | Sort-Object Name

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("# ──────────────────────────────────────────────────────────────")
[void]$sb.AppendLine("# Label Color Guide")
[void]$sb.AppendLine("# ──────────────────────────────────────────────────────────────")
[void]$sb.AppendLine("#")
[void]$sb.AppendLine("# Category            Hex       Purpose")
[void]$sb.AppendLine("# ─────────────────── ───────── ─────────────────────────────────")
[void]$sb.AppendLine("# scope:              000000    Internal code area (black)")
[void]$sb.AppendLine("# scope: Security     500       Security-sensitive (dark red)")
[void]$sb.AppendLine("# component:          1B2638    Grasshopper component (dark navy)")
[void]$sb.AppendLine("# provider:           1A3636    AI provider (dark teal)")
[void]$sb.AppendLine("# os:                 2B3A52    Platform tag (dark slate)")
[void]$sb.AppendLine("# close:              808080    Closure reason (gray)")
[void]$sb.AppendLine("# automated / ci      BFD4DB    CI/CD generated (silver-blue)")
[void]$sb.AppendLine("# model-verification  2EA44F    CI workflow (green)")
[void]$sb.AppendLine("# documentation       0075CA    Docs (blue)")
[void]$sb.AppendLine("# has-conflicts       E4E669    Merge warning (yellow)")
[void]$sb.AppendLine("# priority: critical  B60205    Urgency gradient — red")
[void]$sb.AppendLine("# priority: high      D93F0B    Urgency gradient — dark orange")
[void]$sb.AppendLine("# priority: medium    E4A511    Urgency gradient — amber")
[void]$sb.AppendLine("# priority: low       0075CA    Urgency gradient — blue")
[void]$sb.AppendLine("# status: blocked     D92D20    Workflow state — rose red")
[void]$sb.AppendLine("# status: in progress FBCA04    Workflow state — gold")
[void]$sb.AppendLine("# status: needs triage 0E8A16   Workflow state — green")
[void]$sb.AppendLine("# status: needs more  A855F7    Workflow state — orchid")
[void]$sb.AppendLine("# status: help wanted 14B8A6    Workflow state — teal")
[void]$sb.AppendLine("# status: needs attention FF6723  Workflow state — orange-red")
[void]$sb.AppendLine("# status: stale       CFD3D7    Workflow state — light gray")
[void]$sb.AppendLine("# status: needs testr D946EF    Workflow state — magenta (NEW)")
[void]$sb.AppendLine("# promotion: blocked  E11D48    Release pipeline — crimson")
[void]$sb.AppendLine("# promotion: freeze   F97316    Release pipeline — orange")
[void]$sb.AppendLine("# version:            FFFFFF    Version tracking (white)")
[void]$sb.AppendLine("# ──────────────────────────────────────────────────────────────")
[void]$sb.AppendLine("")

$groups = $finalLabels | Group-Object -Property { ($_.Name -split ':')[0] + ':' }

foreach ($group in ($groups | Sort-Object Name)) {
    [void]$sb.AppendLine("# $(($group.Name.TrimEnd(':')) ) Labels")
    foreach ($label in ($group.Group | Sort-Object Name)) {
        [void]$sb.AppendLine("- name: `"$($label.Name)`"")
        [void]$sb.AppendLine("  color: `"$($label.Color)`"")
        [void]$sb.AppendLine("  description: `"$($label.Description)`"")
    }
    [void]$sb.AppendLine("")
}

$labelsContent = $sb.ToString()

# ─── 5. Build labeler.yml content ───────────────────────────────────

$labelerEntries = @()
$labelerEntries += ""
$labelerEntries += "# --- Component labels ---"

foreach ($entry in ($componentEntries | Sort-Object Label)) {
    $labelerEntries += @"
"$($entry.Label)":
  - changed-files:
      - any-glob-to-any-file: '$($entry.Path)'
"@
}

# ─── 6. Check / Apply ───────────────────────────────────────────────

$labelsChanged = $false
$labelerChanged = $false

if ($Check -or $Apply) {
    # Check labels.yml
    if (Test-Path $labelsPath) {
        $oldContent = Get-Content -Raw $labelsPath
        $oldNorm = $oldContent -replace "\r\n", "\n"
        $newNorm = $labelsContent -replace "\r\n", "\n"
        if ($oldNorm -ne $newNorm) { $labelsChanged = $true }
    } else {
        $labelsChanged = $true
    }

    # Check labeler.yml
    if (Test-Path $labelerPath) {
        $existingContent = Get-Content -Raw $labelerPath
        $componentSectionPattern = '(?ms)\n# --- Component labels ---.*?(?=\n# --- |\z)'
        $cleanContent = [regex]::Replace($existingContent, $componentSectionPattern, "")
        $newLabelerContent = $cleanContent + ($labelerEntries -join "`n")
        $oldNorm = $existingContent -replace "\r\n", "\n"
        $newNorm = $newLabelerContent -replace "\r\n", "\n"
        if ($oldNorm -ne $newNorm) { $labelerChanged = $true }
    } else {
        $labelerChanged = $true
    }
}

if ($Check) {
    if (-not $labelsChanged -and -not $labelerChanged) {
        Write-Host "Both labels.yml and labeler.yml are up to date." -ForegroundColor Green
        exit 0
    }
    if ($labelsChanged) { Write-Host "labels.yml would be updated." -ForegroundColor Yellow }
    if ($labelerChanged) { Write-Host "labeler.yml would be updated." -ForegroundColor Yellow }
    exit 1
}

if ($Apply) {
    # Write labels.yml
    [System.IO.File]::WriteAllText($labelsPath, $labelsContent, $utf8NoBom)
    Write-Host "Updated $labelsPath" -ForegroundColor Green

    # Write labeler.yml
    $existingContent = if (Test-Path $labelerPath) { Get-Content -Raw $labelerPath } else { "" }
    $componentSectionPattern = '(?ms)\n# --- Component labels ---.*?(?=\n# --- |\z)'
    $cleanContent = [regex]::Replace($existingContent, $componentSectionPattern, "")
    $newLabelerContent = $cleanContent + ($labelerEntries -join "`n")
    [System.IO.File]::WriteAllText($labelerPath, $newLabelerContent, $utf8NoBom)
    Write-Host "Updated $labelerPath with $($componentEntries.Count) component entries" -ForegroundColor Green

    exit 0
}

# Default: print labels.yml to stdout
Write-Output $labelsContent
