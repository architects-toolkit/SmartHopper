# Update copyright year in Directory.Build.props and AboutDialog.cs

[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [switch]$Check
)

Set-Location (Resolve-Path $Root)

$currentYear = (Get-Date).Year
$initialYear = 2024

$path = Resolve-Path "..\Directory.Build.props"

if (-not (Test-Path $path)) {
    Write-Host "Directory.Build.props not found at $path" -ForegroundColor Red
    exit 2
}

# Determine the new year format based on fixed initial year
if ($currentYear -eq $initialYear) {
    $newYears = "$initialYear"
}
else {
    $newYears = "$initialYear-$currentYear"
}

$anyChanged = $false

# --- 1. Directory.Build.props ---
try {
    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $originalContent = $content

    # Pattern: <Copyright> tag
    $tagPattern = '(<Copyright>Copyright \(c\) )(\d{4})(?:-(\d{4}))?( Marc Roca Musach</Copyright>)'
    if ($content -match $tagPattern) {
        $startYear = [int]$Matches[2]
        $endYear = if ($Matches[3]) { [int]$Matches[3] } else { $startYear }

        if (-not ($endYear -eq $currentYear -and $startYear -eq $initialYear)) {
            $replacement = "`${1}$newYears`${4}"
            $content = [System.Text.RegularExpressions.Regex]::Replace($content, $tagPattern, $replacement)
        }
    }

    if ($content -ne $originalContent) {
        if (-not $Check) {
            [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
            Write-Host "Updated copyright year to $newYears : $path"
        }
        $anyChanged = $true
    }
    else {
        Write-Host "Copyright year already up to date: $path"
    }
}
catch {
    Write-Host "Error updating ${path}: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

# --- 2. AboutDialog.cs ---
$aboutPath = Resolve-Path "..\src\SmartHopper.Menu\Dialogs\AboutDialog.cs"

if (-not (Test-Path $aboutPath)) {
    Write-Host "AboutDialog.cs not found at $aboutPath" -ForegroundColor Red
    exit 2
}

try {
    $aboutContent = [System.IO.File]::ReadAllText($aboutPath, [System.Text.Encoding]::UTF8)
    $originalAboutContent = $aboutContent

    # Pattern: 'Copyright (c) YYYY(-YYYY) Marc Roca Musach' in C# string literal
    # Use case-sensitive match to avoid modifying the file header which uses uppercase (C)
    $aboutPattern = '(Copyright \(c\) )(\d{4})(?:-(\d{4}))?( Marc Roca Musach)'
    if ($aboutContent -cmatch $aboutPattern) {
        $startYear = [int]$Matches[2]
        $endYear = if ($Matches[3]) { [int]$Matches[3] } else { $startYear }

        if (-not ($endYear -eq $currentYear -and $startYear -eq $initialYear)) {
            $replacement = "`${1}$newYears`${4}"
            $aboutContent = [System.Text.RegularExpressions.Regex]::Replace($aboutContent, $aboutPattern, $replacement, [System.Text.RegularExpressions.RegexOptions]::None)
        }
    }

    if ($aboutContent -ne $originalAboutContent) {
        if (-not $Check) {
            [System.IO.File]::WriteAllText($aboutPath, $aboutContent, [System.Text.Encoding]::UTF8)
            Write-Host "Updated copyright year to $newYears : $aboutPath"
        }
        $anyChanged = $true
    }
    else {
        Write-Host "Copyright year already up to date: $aboutPath"
    }
}
catch {
    Write-Host "Error updating ${aboutPath}: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

# --- Summary ---
if ($anyChanged) {
    if ($Check) {
        Write-Host "Copyright year update required." -ForegroundColor Yellow
        exit 1
    }
}
else {
    if ($Check) {
        Write-Host "Copyright year already up to date." -ForegroundColor Green
        exit 0
    }

    Write-Host "Copyright year already up to date. No changes needed."
}
