# Validate documentation structure and consistency against the SmartHopper template conventions
# Usage: .\Validate-Documentation.ps1 [-Path <docs-dir>] [-Verbose]

[CmdletBinding()]
param(
    [string]$Path,
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

function Test-CodeExamples {
    param([string]$FilePath)
    
    $issues = @()
    $content = Get-Content $FilePath -Raw
    
    # Check if Developer Reference section exists
    if ($content -match "## Developer Reference") {
        # Count code blocks
        $codeBlocks = [regex]::Matches($content, '```csharp').Count
        
        if ($codeBlocks -lt 2) {
            $issues += "Developer Reference should have at least 2 code examples (found: $codeBlocks)"
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

function Validate-DocumentationFile {
    param([string]$FilePath)
    
    Write-Log "Validating: $FilePath" "Info"
    
    $allIssues = @()
    
    # Run all tests
    $allIssues += Test-FileStructure $FilePath
    $allIssues += Test-Metadata $FilePath
    $allIssues += Test-CodeExamples $FilePath
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

function Validate-AllDocumentation {
    param([string]$DocsPath)
    
    Write-Log "Starting documentation validation..." "Info"
    Write-Log "Path: $DocsPath" "Info"
    Write-Log ""
    
    # Find all markdown files (exclude TEMPLATES folder itself)
    $mdFiles = Get-ChildItem -Path $DocsPath -Filter "*.md" -Recurse |
        Where-Object { $_.FullName -notlike "*\TEMPLATES\*" -and $_.FullName -notlike "*\Reviews\*" }
    
    if ($mdFiles.Count -eq 0) {
        Write-Log "No markdown files found in $DocsPath" "Warning"
        return
    }
    
    Write-Log "Found $($mdFiles.Count) documentation files" "Info"
    Write-Log ""
    
    $passCount = 0
    $failCount = 0
    
    foreach ($file in $mdFiles) {
        if (Validate-DocumentationFile $file.FullName) {
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

# Main execution
if (Test-Path $Path) {
    $result = Validate-AllDocumentation $Path
    exit $result
} else {
    Write-Log "Path not found: $Path" "Error"
    exit 1
}
