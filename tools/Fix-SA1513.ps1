# Fix-SA1513.ps1
# Inserts a blank line after a closing brace '}' when required by StyleCop SA1513.

<#
.SYNOPSIS
Fixes StyleCop SA1513 by inserting a blank line after a closing brace '}' when appropriate.

.DESCRIPTION
Traverses the target path (file or directory) and inserts a blank line after lines that contain only
'}' when the next line is neither blank, another '}', an else/catch/finally, nor a single-line comment.
This aligns with SA1513 while keeping brace blocks and control clauses attached when required.

Additional behaviors:
- Preserves the original EOL style (CRLF/LF) and final-EOL presence.
- Preserves UTF-8 BOM if the original file had it.
- Honors -WhatIf / -Confirm via SupportsShouldProcess.

.PARAMETER Path
File or directory to process. Defaults to the repository root (parent of this script's folder).

.PARAMETER Include
Glob patterns of file names to include. Defaults to *.cs.

.PARAMETER ExcludeDir
Directory names to exclude anywhere in the path. Defaults: .git, bin, obj, .vs

.PARAMETER Recurse
Process directories recursively (default: $true). Set to $false to process only the top directory.

.EXAMPLE
# Run from repo root or tools/ to fix all C# files
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1513.ps1

.EXAMPLE
# Fix a single file
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1513.ps1 -Path .\src\SmartHopper.Core\UI\Chat\ChatResourceManager.cs
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

function ShouldInsertBlankLine {
    <#
    .SYNOPSIS
    Determines whether a blank line should be inserted after a closing brace at index $i.
    #>
    param(
        [AllowEmptyCollection()] [string[]]$Lines,
        [int]$Index
    )
    if ($Index -lt 0) { return $false }
    $nextIndex = $Index + 1
    if ($nextIndex -ge $Lines.Count) { return $false } # EOF: nothing to insert after

    $next = $Lines[$nextIndex]

    # Already blank next line
    if ($next -match '^\s*$') { return $false }

    # Next line begins with another closing brace → keep braces together
    if ($next -match '^\s*\}') { return $false }

    # Next line begins with else/catch/finally → must stay attached to brace
    if ($next -match '^\s*(else|catch|finally)\b') { return $false }

    # Next line is a single-line comment directly after the brace: keep attached
    if ($next -match '^\s*//') { return $false }

    # XML doc comments and other code constructs should be separated by a blank line after a closing brace per SA1513
    return $true
}

function Repair-SA1513File {
    <#
    .SYNOPSIS
    Processes a single file and applies SA1513 fixes where needed.
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
    $hadFinalEol = $original -match "(\r?\n)$"
    $lines = $original -split '\r?\n', 0

    $changed = $false
    $output = New-Object System.Collections.Generic.List[string]
    $count = $lines.Count

    for ($i = 0; $i -lt $count; $i++) {
        $line = $lines[$i]
        $output.Add($line)

        # Match a line with only a closing brace (allow leading/trailing whitespace)
        if ($line -match '^\s*\}\s*$') {
            if (ShouldInsertBlankLine -Lines $lines -Index $i) {
                $output.Add("") # insert a blank line
                $changed = $true
            }
        }
    }

    if (-not $changed) { return $false }

    $newText = [string]::Join($eol, $output)
    if ($hadFinalEol) { $newText += $eol }

    if ($PSCmdlet.ShouldProcess($FilePath, "Insert blank line(s) after closing brace(s) per SA1513")) {
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
    if (Repair-SA1513File -FilePath $f.FullName) { $fixed++ }
}

Write-Host "Processed: $processed, Fixed: $fixed" -ForegroundColor Cyan
exit 0