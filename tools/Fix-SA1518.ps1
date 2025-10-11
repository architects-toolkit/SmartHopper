<#
.SYNOPSIS
Fixes StyleCop SA1518 by removing blank lines at the end of files and ensuring a single final newline.

.DESCRIPTION
Traverses the target path (file or directory) and normalizes only the end-of-file region:
- Removes any trailing blank lines and trailing whitespace at EOF
- Ensures exactly one newline at EOF (preserves original newline style CRLF/LF)
- Preserves UTF-8 BOM presence if the original file had it

By default, it searches from the repository root (parent of the tools folder) for *.cs files, recursively,
excluding common build and VCS folders (bin, obj, .git, .vs).

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
powershell -ExecutionPolicy Bypass -File .\tools\Fix-SA1518.ps1

.EXAMPLE
# Fix a specific file
powershell -ExecutionPolicy Bypass -File .\tools\Fix-SA1518.ps1 -Path .\src\SmartHopper.Core\UI\Chat\ChatResourceManager.cs

.NOTES
- Implements SupportsShouldProcess so -WhatIf/-Confirm are honored.
- Only touches files when a change is actually needed.
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
        # If file doesn't exist yet, default to no BOM
        $hasBom = $false
    }
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

$root = Resolve-Path -LiteralPath $Path
$files = Get-FilesToProcess -Root $root -Include $Include -ExcludeDir $ExcludeDir -Recurse $Recurse

$processed = 0
$fixed = 0

foreach ($f in $files) {
    try {
        $original = Get-Content -LiteralPath $f.FullName -Raw -ErrorAction Stop
    } catch {
        Write-Warning "Failed to read: $($f.FullName). $_"
        continue
    }

    $eol = Get-EolStyle -Content $original

    # Remove all trailing whitespace/newlines at EOF, then append exactly one EOL
    $trimmed = [Regex]::Replace($original, "[\s\uFEFF]+$", "")
    $updated = $trimmed + $eol

    if ($updated -ne $original) {
        if ($PSCmdlet.ShouldProcess($f.FullName, "Normalize EOF (remove trailing blank lines, ensure single EOL)")) {
            try {
                Write-ContentPreserveBom -Path $f.FullName -Content $updated
                $fixed++
                Write-Host "Modified: $($f.FullName)"
            } catch {
                Write-Warning "Failed to write: $($f.FullName). $_"
            }
        }
    }
    $processed++
}

Write-Host "Processed: $processed, Fixed: $fixed" -ForegroundColor Cyan
if ($fixed -gt 0) { exit 0 } else { exit 0 }
