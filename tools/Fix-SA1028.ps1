<#
.SYNOPSIS
Fixes StyleCop SA1028 by removing trailing whitespace at the end of lines.

.DESCRIPTION
Traverses the target path (file or directory) and removes any trailing spaces or tabs
from the end of each line in matching files. The script:
- Removes only trailing whitespace (spaces/tabs) at end of each line
- Preserves original EOL style (CRLF/LF) and final-EOL presence
- Preserves UTF-8 BOM if present
- Honors -WhatIf / -Confirm via SupportsShouldProcess
- Touches files only when a change is necessary

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
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1028.ps1

.EXAMPLE
# Fix a specific file called out by the analyzer
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-SA1028.ps1 -Path .\src\SmartHopper.Infrastructure\AIProviders\ProviderManager.cs
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

function Invoke-SA1028Fix {
    <#
    .SYNOPSIS
    Processes a single file and removes trailing whitespace at end-of-line.
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
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '[ \t]+$') {
            $lines[$i] = [Regex]::Replace($line, '[ \t]+$', '')
            $changed = $true
        }
    }

    if (-not $changed) { return $false }

    # Do not append a trailing EOL here. SA1518 fixer will enforce final newline policy.
    $newText = [string]::Join($eol, $lines)

    if ($PSCmdlet.ShouldProcess($FilePath, "Remove trailing whitespace per SA1028")) {
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
    if (Invoke-SA1028Fix -FilePath $f.FullName) { $fixed++ }
}

Write-Host "Processed: $processed, Fixed: $fixed" -ForegroundColor Cyan
exit 0
