<#
.SYNOPSIS
Fixes StyleCop SA1515 by inserting a blank line before single-line comment lines when required.

.DESCRIPTION
Traverses the target path (file or directory) and inserts a blank line before lines that start with
'//' (single-line comments) when the previous line is not blank and not itself a comment or
preprocessor directive. The script:
- Treats lines starting with '///' as XML documentation and does not require a preceding blank line
- Does not insert a blank line when the comment is the first line of the file
- Does not insert a blank line when the previous line is:
  - blank
  - another single-line comment (//)
  - an XML documentation line (///)
  - a preprocessor directive (#...)
- Preserves original EOL style (CRLF/LF) and final-EOL presence
- Preserves UTF-8 BOM if present
- Honors -WhatIf / -Confirm via SupportsShouldProcess

.PARAMETER Path
A file or directory to process. Defaults to the repository root (parent of this script's folder).

.PARAMETER Include
Glob patterns of file names to include. Defaults to *.cs.

.PARAMETER ExcludeDir
Directory names to exclude anywhere in the path. Defaults: .git, bin, obj, .vs

.PARAMETER Recurse
Process directories recursively (default: $true). Set to $false to process only the top directory.

.EXAMPLE
# Fix all C# files in the repository
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1515.ps1

.EXAMPLE
# Fix a specific file
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1515.ps1 -Path .\src\SmartHopper.Infrastructure\AIProviders\ProviderManager.cs
#>

#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Position = 0)]
    [string]$Path = (Split-Path -Path $PSScriptRoot -Parent),

    [string[]]$Include = @('*.cs'),

    [string[]]$ExcludeDir = @('.git','bin','obj','.vs'),

    [bool]$Recurse = $true
)

function Test-PathHasExcludedDir {
    <#
    .SYNOPSIS
    Returns $true if the provided path contains any of the excluded directory names.
    #>
    param(
        [Parameter(Mandatory)] [string]$FullName,
        [Parameter(Mandatory)] [string[]]$Names
    )
    foreach ($name in $Names) {
        if ($FullName -match "\\$([Regex]::Escape($name))(\\|$)") { return $true }
    }
    return $false
}

function Get-FilesToProcess {
    <#
    .SYNOPSIS
    Enumerates files matching Include globs, excluding paths that contain excluded directories.
    #>
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] [string[]]$Include,
        [Parameter(Mandatory)] [string[]]$ExcludeDir,
        [Parameter(Mandatory)] [bool]$Recurse
    )

    if (Test-Path -LiteralPath $Root -PathType Leaf) {
        $file = Get-Item -LiteralPath $Root -ErrorAction Stop
        if (-not (Test-PathHasExcludedDir -FullName $file.FullName -Names $ExcludeDir)) { return ,$file }
        return @()
    }

    $opts = @{ LiteralPath = $Root; File = $true; ErrorAction = 'Stop' }
    if ($Recurse) { $opts['Recurse'] = $true }

    $files = Get-ChildItem @opts -Include $Include
    return $files | Where-Object { -not (Test-PathHasExcludedDir -FullName $_.FullName -Names $ExcludeDir) }
}

function Get-EolStyle {
    <#
    .SYNOPSIS
    Detects predominant EOL style (CRLF or LF) from content; defaults to CRLF if ambiguous.
    #>
    param([Parameter(Mandatory)] [string]$Content)
    if ($Content -match "\r\n") { return "`r`n" }
    elseif ($Content -match "\n") { return "`n" }
    else { return "`r`n" }
}

function Write-ContentPreserveBom {
    <#
    .SYNOPSIS
    Writes text preserving whether the original file had a UTF-8 BOM.
    #>
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Content
    )
    $hasBom = $false
    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            $hasBom = $true
        }
    } catch {
        $hasBom = $false
    }
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function ShouldInsertBeforeComment {
    <#
    .SYNOPSIS
    Determines whether a blank line should be inserted before a single-line comment at index $i.
    #>
    param(
        [Parameter()] [AllowNull()] [AllowEmptyCollection()] [string[]]$Lines,
        [Parameter(Mandatory)] [int]$i
    )

    # Validate inputs
    if (-not $Lines -or $Lines.Count -eq 0) { return $false }
    if ($i -lt 0 -or $i -ge $Lines.Count) { return $false }

    # Current line must be a single-line comment (//...) but not XML doc (///...)
    $line = $Lines[$i]
    if ($line -notmatch '^\s*//' -or $line -match '^\s*///') { return $false }

    # First line of file: no insertion required
    if ($i -le 0) { return $false }

    $prev = $Lines[$i - 1]

    # If previous line is blank → already separated
    if ($prev -match '^\s*$') { return $false }

    # If previous line opens a block/collection (ends with '{' or '[') → do not insert
    if ($prev -match '\{\s*$' -or $prev -match '\[\s*$') { return $false }

    # If previous line ends with ':' (e.g., switch/case labels) → do not insert
    if ($prev -match ':\s*$') { return $false }

    # If previous line is another single-line comment (//...) or XML doc (///...) → no insertion
    if ($prev -match '^\s*//' ) { return $false }

    # If previous line is a preprocessor directive (#...) → skip to avoid breaking regions/ifs
    if ($prev -match '^\s*#') { return $false }

    return $true
}

function Invoke-SA1515Fix {
    <#
    .SYNOPSIS
    Processes a single file and applies SA1515 fixes where needed.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([Parameter(Mandatory)] [string]$FilePath)

    try {
        $original = Get-Content -LiteralPath $FilePath -Raw -ErrorAction Stop
    } catch {
        Write-Warning "Failed to read: $FilePath. $_"
        return $false
    }

    $eol = Get-EolStyle -Content $original
    $lines = $original -split "\r?\n", 0

    $changed = $false
    $output = New-Object System.Collections.Generic.List[string]

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if (ShouldInsertBeforeComment -Lines $lines -i $i) {
            $output.Add("")
            $changed = $true
        }

        $output.Add($line)
    }

    if (-not $changed) { return $false }

    # Join lines using original EOL style without appending an extra final newline.
    # EOF normalization (single final newline) is handled by Fix-SA1518.
    $newText = [string]::Join($eol, $output)

    if ($PSCmdlet.ShouldProcess($FilePath, "Insert blank line(s) before single-line comment(s) per SA1515")) {
        try {
            Write-ContentPreserveBom -Path $FilePath -Content $newText
            Write-Host "Modified: $FilePath"
            return $true
        } catch {
            Write-Warning "Failed to write: $FilePath. $_"
            return $false
        }
    }
    return $false
}

$root = Resolve-Path -LiteralPath $Path
$files = Get-FilesToProcess -Root $root -Include $Include -ExcludeDir $ExcludeDir -Recurse $Recurse

$processed = 0
$fixed = 0

foreach ($f in $files) {
    $processed++
    if (Invoke-SA1515Fix -FilePath $f.FullName) { $fixed++ }
}

Write-Host "Processed: $processed, Fixed: $fixed" -ForegroundColor Cyan
exit 0
