<#
.SYNOPSIS
    Initializes the solution version for a new work session.

.DESCRIPTION
    Performs the following tasks (matching the /init workflow):
      1. Detects an X.Y.Z version indicator from the current Git branch name.
      2. Updates SolutionVersion in Solution.props to X.Y.Z-dev.YYMMDD.
      3. Updates the version and status badges in README.md.
      4. Ensures the top section in CHANGELOG.md is [Unreleased].

    Version transition rules:
      - 0.1.0-beta        -> 0.1.1-dev.YYMMDD   (increment patch, add -dev.date)
      - 0.1.0-dev          -> 0.1.0-dev.YYMMDD   (keep version, set date)
      - 0.1.0              -> 0.1.1-dev.YYMMDD   (increment patch, add -dev.date)
      - 0.1.0-dev.250101   -> 0.1.0-dev.YYMMDD   (keep version, update date)

    Badge color/status rules (from .github/actions/documentation/update-badges):
      - *-dev*    -> brown  / Unstable Development
      - *-alpha*  -> orange / Alpha
      - *-beta*   -> yellow / Beta
      - *-rc*     -> purple / Release Candidate
      - (stable)  -> brightgreen / Stable

.PARAMETER Version
    Explicit X.Y.Z base version to use. Takes priority over the version
    detected from the branch name.

.PARAMETER Help
    Displays detailed help information about this script.

.PARAMETER DryRun
    When set, shows what would change without modifying any files.

.PARAMETER DateOnly
    When set, only updates the date part (.YYMMDD) of the current version if it exists.
    Does not modify the base version (X.Y.Z) or pre-release type.

.PARAMETER BadgesOnly
    When set, only updates the version and status badges in README.md.
    Does not modify Solution.props or CHANGELOG.md.

.EXAMPLE
    .\Change-SolutionVersion.ps1
    .\Change-SolutionVersion.ps1 -Version 2.0.0
    .\Change-SolutionVersion.ps1 -Version 1.5.0 -DryRun
    .\Change-SolutionVersion.ps1 -DateOnly
    # If current version is 1.0.0-dev.250101, updates to 1.0.0-dev.YYMMDD (today's date)
    .\Change-SolutionVersion.ps1 -BadgesOnly
    # Updates README.md badges to match the current version in Solution.props
#>
param(
    [string]$Version,
    [switch]$Help,
    [switch]$DryRun,
    [switch]$DateOnly,
    [switch]$BadgesOnly
)

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

if ($Help) {
    Get-Help $PSCommandPath -Full
    exit 0
}

$ErrorActionPreference = "Stop"

$solutionRoot = Split-Path -Parent $PSScriptRoot
$solutionPropsPath = Join-Path $solutionRoot "Solution.props"
$readmePath = Join-Path $solutionRoot "README.md"
$changelogPath = Join-Path $solutionRoot "CHANGELOG.md"

$today = (Get-Date).ToString("yyMMdd")

# ---------------------------------------------------------------------------
# Helper: Parse a semantic version string into components
# ---------------------------------------------------------------------------
function ConvertTo-VersionComponents {
    param([string]$Version)

    if ($Version -match '^(\d+)\.(\d+)\.(\d+)(-([A-Za-z]+)(\.(\d+))?)?$') {
        return @{
            Major      = [int]$Matches[1]
            Minor      = [int]$Matches[2]
            Patch      = [int]$Matches[3]
            Suffix     = $Matches[4]       # e.g. "-dev.250101" or "-beta"
            PreType    = $Matches[5]       # e.g. "dev", "beta", "alpha"
            Date       = $Matches[7]       # e.g. "250101" or $null
        }
    }
    return $null
}

# ---------------------------------------------------------------------------
# Helper: Determine badge status text and color from a version string
# ---------------------------------------------------------------------------
function Get-BadgeInfo {
    param([string]$Version)

    if ($Version -like "*-dev*") {
        return @{ Color = "brown"; Text = "Unstable%20Development" }
    }
    elseif ($Version -like "*-alpha*") {
        return @{ Color = "orange"; Text = "Alpha" }
    }
    elseif ($Version -like "*-beta*") {
        return @{ Color = "yellow"; Text = "Beta" }
    }
    elseif ($Version -like "*-rc*") {
        return @{ Color = "purple"; Text = "Release%20Candidate" }
    }
    else {
        return @{ Color = "brightgreen"; Text = "Stable" }
    }
}

# ---------------------------------------------------------------------------
# Helper: Convert a version string for shields.io URL encoding
# ---------------------------------------------------------------------------
function ConvertTo-ShieldsVersion {
    param([string]$Version)

    # Shields.io escaping: '_' -> '__', '-' -> '--', ' ' -> '_'
    $escaped = $Version -replace '_', '__'
    $escaped = $escaped -replace '-', '--'
    $escaped = $escaped -replace ' ', '_'
    return $escaped
}

# ===== STEP 1: Determine base version ======================================
Write-Host "`n===== Step 1: Determine base version =====" -ForegroundColor Cyan

$baseVersion = $null

if ($Version) {
    # Validate the explicit version parameter
    $versionParsed = ConvertTo-VersionComponents $Version
    if (-not $versionParsed) {
        Write-Error "Invalid -Version parameter: '$Version'. Expected format: X.Y.Z"
        exit 1
    }
    $baseVersion = $Version
    Write-Host "Using explicit version parameter: $baseVersion" -ForegroundColor Green
}
else {
    # Fall back to detecting version from branch name
    try {
        $branchName = & git -C $solutionRoot rev-parse --abbrev-ref HEAD 2>$null
        Write-Host "Current branch: $branchName"

        if ($branchName -match '(\d+\.\d+\.\d+)') {
            $baseVersion = $Matches[1]
            Write-Host "Detected version from branch: $baseVersion" -ForegroundColor Green
        }
        else {
            Write-Host "No X.Y.Z version indicator found in branch name."
        }
    }
    catch {
        Write-Warning "Could not determine Git branch: $_"
    }
}

# ===== STEP 2: Update Solution.props ======================================
Write-Host "`n===== Step 2: Update SolutionVersion in Solution.props =====" -ForegroundColor Cyan

if (-not (Test-Path $solutionPropsPath)) {
    Write-Error "Solution.props not found at $solutionPropsPath"
    exit 1
}

$xml = [xml](Get-Content $solutionPropsPath -Raw)
$currentVersion = $xml.Project.PropertyGroup.SolutionVersion
Write-Host "Current version: $currentVersion"

$parsed = ConvertTo-VersionComponents $currentVersion
if (-not $parsed) {
    Write-Error "Failed to parse current version: $currentVersion"
    exit 1
}

# BadgesOnly: skip version calculation and Solution.props update
if ($BadgesOnly) {
    $newVersion = $currentVersion
    Write-Host "`n[BadgesOnly] Using current version for badge update." -ForegroundColor Cyan
}
else {
    # Determine base X.Y.Z for the new version
    if ($DateOnly) {
        # Only update date for -dev versions
        if ($parsed.PreType -ne 'dev') {
            Write-Host "Current version is not a -dev version ($currentVersion). Skipping date update for non-dev versions." -ForegroundColor Yellow
            Write-Host "  Version remains: $currentVersion" -ForegroundColor Green
            exit 0
        }
        
        # Keep the same X.Y.Z and pre-release type, only update the date
        $newMajor = $parsed.Major
        $newMinor = $parsed.Minor
        $newPatch = $parsed.Patch
        
        # Determine pre-release type - use existing or default to 'dev'
        $preType = if ($parsed.PreType) { $parsed.PreType } else { 'dev' }
        
        $newVersion = "$newMajor.$newMinor.$newPatch-$preType.$today"
        Write-Host "[DateOnly] Keeping base version $newMajor.$newMinor.$newPatch, updating to date: $today" -ForegroundColor Cyan
    }
    elseif ($baseVersion) {
        # Use explicit or branch-detected version as the base
        $baseParsed = ConvertTo-VersionComponents $baseVersion
        if ($baseParsed) {
            # Check if base version ends with exactly '-dev' (no date suffix)
            if ($baseVersion -match '-dev$') {
                # Append date to -dev suffix
                $newVersion = "$baseVersion.$today"
                Write-Host "Base version ends with -dev, appending date: $newVersion" -ForegroundColor Cyan
            }
            else {
                # Use base version as-is (stable, -alpha, -beta, -rc, or -dev.YYMMDD already provided)
                $newVersion = $baseVersion
                Write-Host "Using base version as-is: $newVersion" -ForegroundColor Cyan
            }
            
            # Set numeric components from the parsed base version
            $newMajor = $baseParsed.Major
            $newMinor = $baseParsed.Minor
            $newPatch = $baseParsed.Patch
        }
        else {
            Write-Error "Failed to parse base version: $baseVersion"
            exit 1
        }
    }
    elseif ($parsed.PreType -eq 'dev') {
        # Already a -dev version -> keep same X.Y.Z
        $newMajor = $parsed.Major
        $newMinor = $parsed.Minor
        $newPatch = $parsed.Patch
    }
    else {
        # Not a -dev version (stable, beta, alpha, rc) -> increment patch
        $newMajor = $parsed.Major
        $newMinor = $parsed.Minor
        $newPatch = $parsed.Patch + 1
    }

    if (-not $DateOnly -and -not $newVersion) {
        $newVersion = "$newMajor.$newMinor.$newPatch-dev.$today"
    }
    Write-Host "New version: $newVersion" -ForegroundColor Green

    if ($DryRun) {
        Write-Host "[DRY RUN] Would update Solution.props: $currentVersion -> $newVersion" -ForegroundColor Yellow
    }
    else {
        $xml.Project.PropertyGroup.SolutionVersion = $newVersion
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
        $settings.Indent = $true
        $writer = [System.Xml.XmlWriter]::Create($solutionPropsPath, $settings)
        $xml.Save($writer)
        $writer.Close()
        Write-Host "Updated Solution.props successfully."
    }
}

# ===== STEP 3 & 4: Update badges in README.md =============================
Write-Host "`n===== Step 3 & 4: Update badges in README.md =====" -ForegroundColor Cyan

if (-not (Test-Path $readmePath)) {
    Write-Warning "README.md not found at $readmePath; skipping badge update."
}
else {
    $readmeContent = Get-Content $readmePath -Raw
    $badgeInfo = Get-BadgeInfo $newVersion
    $shieldsVersion = ConvertTo-ShieldsVersion $newVersion

    Write-Host "Version badge: $shieldsVersion / $($badgeInfo.Color)"
    Write-Host "Status badge:  $($badgeInfo.Text) / $($badgeInfo.Color)"

    # Update VERSION badge (for-the-badge style)
    # Pattern: https://img.shields.io/badge/version-{VERSION}-{COLOR}?style=for-the-badge
    $readmeContent = $readmeContent -replace `
        '(https://img\.shields\.io/badge/version-)[^?]+(\?style=for-the-badge)', `
        "`${1}$shieldsVersion-$($badgeInfo.Color)`${2}"

    # Update STATUS badge (for-the-badge style)
    # Pattern: https://img.shields.io/badge/status-{TEXT}-{COLOR}?style=for-the-badge
    $readmeContent = $readmeContent -replace `
        '(https://img\.shields\.io/badge/status-)[^?]+(\?style=for-the-badge)', `
        "`${1}$($badgeInfo.Text)-$($badgeInfo.Color)`${2}"

    if ($DryRun) {
        Write-Host "[DRY RUN] Would update README.md badges." -ForegroundColor Yellow
    }
    else {
        [System.IO.File]::WriteAllText($readmePath, $readmeContent, $utf8NoBom)
        Write-Host "Updated README.md badges successfully."
    }
}

# ===== STEP 5: Ensure CHANGELOG.md top section is [Unreleased] ============
if (-not $BadgesOnly) {
    Write-Host "`n===== Step 5: Ensure CHANGELOG.md top section is [Unreleased] =====" -ForegroundColor Cyan

    if (-not (Test-Path $changelogPath)) {
        Write-Warning "CHANGELOG.md not found at $changelogPath; skipping."
    }
    else {
        $changelogLines = Get-Content $changelogPath -Encoding utf8

        # Find the first ## heading
        $firstHeadingIndex = -1
        for ($i = 0; $i -lt $changelogLines.Count; $i++) {
            if ($changelogLines[$i] -match '^## ') {
                $firstHeadingIndex = $i
                break
            }
        }

        if ($firstHeadingIndex -eq -1) {
            Write-Host "No ## heading found in CHANGELOG.md. Adding [Unreleased] section."
            if (-not $DryRun) {
                $changelogLines += ""
                $changelogLines += "## [Unreleased]"
                $changelogLines += ""
                [System.IO.File]::WriteAllLines($changelogPath, $changelogLines, $utf8NoBom)
                Write-Host "Added [Unreleased] section to CHANGELOG.md."
            }
            else {
                Write-Host "[DRY RUN] Would add [Unreleased] section." -ForegroundColor Yellow
            }
        }
        elseif ($changelogLines[$firstHeadingIndex] -match '^\#\# \[Unreleased\]') {
            Write-Host "Top section is already [Unreleased]. No changes needed." -ForegroundColor Green
        }
        else {
            Write-Host "Top section is: $($changelogLines[$firstHeadingIndex])"
            Write-Host "Inserting [Unreleased] section above it."

            if (-not $DryRun) {
                $before = $changelogLines[0..($firstHeadingIndex - 1)]
                $after = $changelogLines[$firstHeadingIndex..($changelogLines.Count - 1)]
                $changelogLines = $before + @("## [Unreleased]", "") + $after
                [System.IO.File]::WriteAllLines($changelogPath, $changelogLines, $utf8NoBom)
                Write-Host "Inserted [Unreleased] section in CHANGELOG.md."
            }
            else {
                Write-Host "[DRY RUN] Would insert [Unreleased] section above existing heading." -ForegroundColor Yellow
            }
        }
    }
}

# ===== Summary =============================================================
Write-Host "`n===== Summary =====" -ForegroundColor Cyan
if ($BadgesOnly) {
    Write-Host "  Mode:     BadgesOnly"
    Write-Host "  Version:  $currentVersion (unchanged)"
}
else {
    Write-Host "  Version:  $currentVersion -> $newVersion"
}
Write-Host "  Badge:    $shieldsVersion ($($badgeInfo.Color))"
Write-Host "  Status:   $($badgeInfo.Text) ($($badgeInfo.Color))"

if ($DryRun) {
    Write-Host "`n  [DRY RUN] No files were modified." -ForegroundColor Yellow
}
elseif ($BadgesOnly) {
    Write-Host "`n  README.md badges updated successfully." -ForegroundColor Green
}
else {
    Write-Host "`n  All files updated successfully." -ForegroundColor Green
}