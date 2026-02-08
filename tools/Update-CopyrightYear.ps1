# Update copyright year in Directory.Build.props

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

try {
    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $originalContent = $content

    # Determine the new year format based on fixed initial year
    if ($currentYear -eq $initialYear) {
        $newYears = "$initialYear"
    }
    else {
        $newYears = "$initialYear-$currentYear"
    }

    # Pattern: <Copyright> tag
    $tagPattern = '(<Copyright>Copyright \(c\) )(\d{4})(?:-(\d{4}))?( Marc Roca Musach</Copyright>)'
    if ($content -match $tagPattern) {
        $startYear = [int]$Matches[2]
        $endYear = if ($Matches[3]) { [int]$Matches[3] } else { $startYear }

        if (-not ($endYear -eq $currentYear -and $startYear -eq $initialYear)) {
            $replacement = "`$1$newYears`$4"
            $content = [System.Text.RegularExpressions.Regex]::Replace($content, $tagPattern, $replacement)
        }
    }

    if ($content -ne $originalContent) {
        if ($Check) {
            Write-Host "Copyright year update required in Directory.Build.props." -ForegroundColor Yellow
            exit 1
        }

        [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::UTF8)
        Write-Host "Updated copyright year to $newYears : $path"
    }
    else {
        if ($Check) {
            Write-Host "Copyright year already up to date." -ForegroundColor Green
            exit 0
        }

        Write-Host "Copyright year already up to date. No changes needed."
    }
}
catch {
    Write-Host "Error updating ${path}: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
