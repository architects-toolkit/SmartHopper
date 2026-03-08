#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    Post-commit hook script to update InternalsVisibleTo attributes.

.DESCRIPTION
    This script runs after a commit is made and executes the Update-InternalsVisibleTo.ps1
    script to ensure InternalsVisibleTo attributes are properly configured.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Get the repository root (parent of .githooks directory)
$hookDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $hookDir

$scriptPath = Join-Path $repoRoot 'tools' 'Update-InternalsVisibleTo.ps1'

if (Test-Path $scriptPath) {
    Write-Host "Running Update-InternalsVisibleTo.ps1..." -ForegroundColor Cyan
    & $scriptPath
} else {
    Write-Warning "Update-InternalsVisibleTo.ps1 not found at: $scriptPath"
}
