# Update license headers in all .cs files to LGPL-3.0
 
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [switch]$Check
)
 
Set-Location (Resolve-Path $Root)
 
$newHeader = @'
/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-{current_year} Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */
'@

$currentYear = (Get-Date).Year
$newHeader = $newHeader.Replace('{current_year}', $currentYear)
 
function Remove-ExistingHeader {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )
 
    # Normalize: remove BOM if present.
    $t = $Text -replace '^[\uFEFF]', ''
 
    # Strip leading whitespace.
    $t = $t.TrimStart("`r", "`n", " ", "`t")
 
    # Case 1: Block comment header at the very beginning.
    if ($t -match '^/\*') {
        $endIdx = $t.IndexOf('*/')
        if ($endIdx -ge 0) {
            $after = $t.Substring($endIdx + 2)
            return $after.TrimStart("`r", "`n", " ", "`t")
        }

        return $t
    }
 
    # Case 2: Line comment header (// ...), possibly with blank lines between.
    if ($t -match '^//') {
        $lines = $t -split "`r?`n"
        $idx = 0
        while ($idx -lt $lines.Length) {
            $line = $lines[$idx]
            if ($line -match '^\s*//') {
                $idx++
                continue
            }
 
            if ($line -match '^\s*$') {
                # Allow blank lines inside the header.
                $idx++
                continue
            }
 
            break
        }
 
        $remaining = $lines[$idx..($lines.Length - 1)] -join "`r`n"
        return $remaining.TrimStart("`r", "`n")
    }
 
    return $t
}
 
$changedCount = 0
 
Get-ChildItem -Path "..\src" -Recurse -Filter *.cs | ForEach-Object {
    try {
        $path = $_.FullName
 
        # Skip auto-generated files like Resources.Designer.cs
        if ($_.Name -match '\.Designer\.cs$') {
            Write-Host "Skipping auto-generated file: $path" -ForegroundColor Gray
            return
        }
 
        # Read as UTF-8 text (handles BOM correctly)
        $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
 
        $body = Remove-ExistingHeader -Text $content
        $normalized = ($newHeader + "`r`n`r`n" + $body.TrimStart("`r", "`n"))
 
        $isDifferent = $normalized -ne ($content -replace '^[\uFEFF]', '')
        if ($isDifferent) {
            $changedCount++
        }

        if (-not $Check) {
            [System.IO.File]::WriteAllText($path, $normalized, [System.Text.Encoding]::UTF8)
            if ($isDifferent) {
                Write-Host "Header normalized: $path"
            }
        }
    }
    catch {
        Write-Host "Error updating $($_.FullName): $_" -ForegroundColor Red
        if ($Check) {
            exit 2
        }
    }
}
 
if ($Check) {
    if ($changedCount -gt 0) {
        Write-Host "Header normalization required in $changedCount file(s)." -ForegroundColor Yellow
        exit 1
    }
 
    Write-Host "All headers already normalized." -ForegroundColor Green
    exit 0
}
 
Write-Host "All license headers updated to LGPL-3.0. Updated $changedCount file(s)."
