# Validate documentation structure and consistency against the SmartHopper template conventions
# Usage: .\Validate-Documentation.ps1 [-Path <docs-dir>] [-Verbose]

[CmdletBinding()]
param(
    [string]$Path,
    [string[]]$Files,
    [switch]$Fix
)

# Resolve docs directory relative to repo root
if (-not $Path) {
    $Path = Join-Path (Split-Path -Parent $PSScriptRoot) "docs"
}

# Configuration
$RequiredSections = @(
    "End-User Guide",
    "Developer Reference",
    "Architecture & Design"
)

$RequiredMetadata = @(
    "Source Code",
    "Since Version",
    "Last Updated",
    "Documentation Maintainer"
)

# Colors for output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
}

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = "Info"
    )
    
    $color = $Colors[$Level]
    $prefix = switch($Level) {
        "Success" { "[PASS]" }
        "Warning" { "[WARN]" }
        "Error"   { "[FAIL]" }
        "Info"    { "[INFO]" }
    }
    
    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Test-FileStructure {
    param([string]$FilePath)
    
    $issues = @()
    $content = Get-Content $FilePath -Raw
    
    # Check for metadata section
    if ($content -notmatch "## Metadata") {
        $issues += "Missing Metadata section"
    }
    
    # Check for required sections
    foreach ($section in $RequiredSections) {
        if ($content -notmatch "## $section") {
            $issues += "Missing section: $section"
        }
    }
    
    # Check for "Why Read This?" section
    if ($content -notmatch "## Why Read This\?") {
        $issues += "Missing 'Why Read This?' section"
    }
    
    # Check for old emoji-based sections
    if ($content -match "🎯|👨‍💻|🏗️") {
        $issues += "Contains old emoji-based section headers"
    }
    
    return $issues
}

function Test-Metadata {
    param([string]$FilePath)
    
    $issues = @()
    $content = Get-Content $FilePath -Raw
    
    # Extract metadata section
    if ($content -match "## Metadata\s*\n([\s\S]*?)\n---") {
        $metadata = $matches[1]
        
        foreach ($field in $RequiredMetadata) {
            if ($metadata -notmatch $field) {
                $issues += "Missing metadata field: $field"
            }
        }
    }
    
    return $issues
}

function Test-Links {
    param([string]$FilePath)
    
    $issues = @()
    $content = Get-Content $FilePath -Raw
    $directory = Split-Path $FilePath
    
    # Remove fenced code blocks, inline code spans, and <code> blocks before link checking
    # to avoid false positives from path notation like [q0,q1,q2](0)
    $cleanContent = $content
    # Replace fenced code blocks (```...```) with spaces of same length
    $cleanContent = [regex]::Replace($cleanContent, '```[\s\S]*?```', { ' ' * $args[0].Length })
    # Replace inline code spans (`...`) with spaces of same length
    $cleanContent = [regex]::Replace($cleanContent, '`[^`\n]+`', { ' ' * $args[0].Length })
    # Replace <code>...</code> blocks with spaces of same length
    $cleanContent = [regex]::Replace($cleanContent, '<code>.*?</code>', { ' ' * $args[0].Length })
    
    # Find all markdown links in cleaned content
    $links = [regex]::Matches($cleanContent, '\[([^\]]+)\]\(([^)]+)\)')
    
    foreach ($link in $links) {
        $linkText = $link.Groups[1].Value
        $linkPath = $link.Groups[2].Value
        
        # Strip angle brackets from link path (autolink syntax: <url>)
        $linkPath = $linkPath -replace '^<', '' -replace '>$', ''
        
        # Skip external links and anchors
        if ($linkPath -match "^https?://" -or $linkPath -match "^#") {
            continue
        }
        
        # Strip anchor from path
        $cleanPath = $linkPath -replace '#.*$', ''
        if ([string]::IsNullOrWhiteSpace($cleanPath)) {
            continue
        }
        
        # Check if local file exists
        $fullPath = Join-Path $directory $cleanPath
        if (-not (Test-Path $fullPath)) {
            $issues += "Broken link: $linkText -> $linkPath"
        }
    }
    
    return $issues
}

function Test-Placeholders {
    param([string]$FilePath)
    
    $issues = @()
    $content = Get-Content $FilePath -Raw
    
    # Check for incomplete placeholders
    if ($content -match "\[PLACEHOLDER:[^\]]*\[To be added\]") {
        $issues += "Contains incomplete placeholder"
    }
    
    return $issues
}

function Test-DocumentationFile {
    param([string]$FilePath)
    
    Write-Log "Validating: $FilePath" "Info"
    
    $content = Get-Content $FilePath -Raw
    
    # Honor explicit opt-out marker for non-template docs (e.g. design briefs).
    if ($content -match '<!--\s*docs-validation:\s*ignore\s*-->') {
        Write-Log "  Skipped (opt-out marker found)" "Info"
        return $true
    }
    
    $allIssues = @()
    
    # Run all tests
    $allIssues += Test-FileStructure $FilePath
    $allIssues += Test-Metadata $FilePath
    $allIssues += Test-Links $FilePath
    $allIssues += Test-Placeholders $FilePath
    
    if ($allIssues.Count -eq 0) {
        Write-Log "  All checks passed" "Success"
        return $true
    } else {
        foreach ($issue in $allIssues) {
            Write-Log "  - $issue" "Warning"
        }
        return $false
    }
}

function Test-AllDocumentation {
    param(
        [string]$DocsPath,
        [System.IO.FileInfo[]]$Files
    )
    
    Write-Log "Starting documentation validation..." "Info"
    
    # Use the provided file list, or discover markdown files under the docs path.
    if ($Files -and $Files.Count -gt 0) {
        $mdFiles = $Files
        $displayPath = "provided file list ($($mdFiles.Count) file(s))"
    } else {
        $displayPath = $DocsPath
        
        # Find all markdown files (exclude TEMPLATES and Reviews folders, and workflow
        # process guides that follow their own README-style structure).
        $mdFiles = Get-ChildItem -Path $DocsPath -Filter "*.md" -Recurse |
            Where-Object { $_.FullName -notlike "*\TEMPLATES\*" -and
                $_.FullName -notlike "*\Reviews\*" -and
                $_.FullName -notlike "*_WORKFLOW.md" }
    }
    
    Write-Log "Path: $displayPath" "Info"
    Write-Log ""
    
    if ($mdFiles.Count -eq 0) {
        Write-Log "No markdown files found in $displayPath" "Warning"
        return
    }
    
    Write-Log "Found $($mdFiles.Count) documentation files" "Info"
    Write-Log ""
    
    $passCount = 0
    $failCount = 0
    
    foreach ($file in $mdFiles) {
        if (Test-DocumentationFile $file.FullName) {
            $passCount++
        } else {
            $failCount++
        }
    }
    
    Write-Log ""
    Write-Log "Validation Summary:" "Info"
    Write-Log "  Passed: $passCount" "Success"
    Write-Log "  Failed: $failCount" "Warning"
    
    if ($failCount -eq 0) {
        Write-Log "All documentation files are valid!" "Success"
        return 0
    } else {
        Write-Log "$failCount file(s) need attention" "Error"
        return 1
    }
}

# Resolve the list of files to validate
$docsFiles = @()
if ($Files -and $Files.Count -gt 0) {
    foreach ($file in $Files) {
        if (Test-Path $file) {
            $docsFiles += (Get-Item $file)
        } else {
            Write-Log "File not found: $file" "Error"
            exit 1
        }
    }
} elseif ($Path) {
    if (Test-Path $Path -PathType Leaf) {
        $docsFiles = @(Get-Item $Path)
    } elseif (Test-Path $Path) {
        $docsFiles = Get-ChildItem -Path $Path -Filter "*.md" -Recurse |
            Where-Object { $_.FullName -notlike "*\TEMPLATES\*" -and
                $_.FullName -notlike "*\Reviews\*" -and
                $_.FullName -notlike "*_WORKFLOW.md" }
    } else {
        Write-Log "Path not found: $Path" "Error"
        exit 1
    }
} else {
    $Path = Join-Path (Split-Path -Parent $PSScriptRoot) "docs"
    $docsFiles = Get-ChildItem -Path $Path -Filter "*.md" -Recurse |
        Where-Object { $_.FullName -notlike "*\TEMPLATES\*" -and
            $_.FullName -notlike "*\Reviews\*" -and
            $_.FullName -notlike "*_WORKFLOW.md" }
}

# Main execution
if ($docsFiles.Count -gt 0) {
    $result = Test-AllDocumentation -DocsPath $Path -Files $docsFiles
    exit $result
} else {
    Write-Log "No markdown files to validate" "Warning"
    exit 0
}
