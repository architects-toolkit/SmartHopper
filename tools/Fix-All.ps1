<#
.SYNOPSIS
Runs all StyleCop fixers in sequence for this repo.

.DESCRIPTION
Invokes the following scripts in order, forwarding common parameters:
1. Fix-SA1513.ps1
2. Fix-SA1515.ps1
3. Fix-SA1028.ps1
4. Fix-SA1518.ps1

Each fixer is called with the provided -Path, -Include, -ExcludeDir, and -Recurse values.
-WhatIf and -Confirm are forwarded to inner scripts to honor dry-run and confirmation.

.PARAMETER Path
A file or directory to process. Defaults to the repository root (parent of the tools folder).

.PARAMETER Include
Glob patterns of file names to include. Defaults to *.cs.

.PARAMETER ExcludeDir
Directory names to exclude anywhere in the path. Defaults: .git, bin, obj, .vs

.PARAMETER Recurse
Process directories recursively (default: $true). Set to $false to process only the top directory.

.EXAMPLE
# Run all fixers on the entire repo
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-All.ps1

.EXAMPLE
# Run against a single file with WhatIf (no changes)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\Fix-All.ps1 -Path .\src\Foo\Bar.cs -WhatIf
#>

#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [Parameter(Position = 0)]
    [string]$Path = (Split-Path -Path $PSScriptRoot -Parent),

    [string[]]$Include = @('*.cs'),

    [string[]]$ExcludeDir = @('.git','bin','obj','.vs'),

    [bool]$Recurse = $true
)

function Invoke-Fixer {
    <#
    .SYNOPSIS
    Helper to invoke a fixer script with forwarded parameters and WhatIf/Confirm semantics.
    #>
    param(
        [Parameter(Mandatory)] [string]$ScriptName
    )

    $scriptPath = Join-Path -Path $PSScriptRoot -ChildPath $ScriptName
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
        Write-Warning "Fixer script not found: $scriptPath"
        return
    }

    $common = @{
        Path = $Path
        Include = $Include
        ExcludeDir = $ExcludeDir
        Recurse = $Recurse
    }

    # Forward WhatIf/Confirm explicitly to inner scripts
    if ($WhatIfPreference) { $common['WhatIf'] = $true }
    if ($PSBoundParameters.ContainsKey('Confirm')) { $common['Confirm'] = $true }

    if ($PSCmdlet.ShouldProcess($scriptPath, 'Run fixer')) {
        Write-Host "Running: $ScriptName" -ForegroundColor Cyan
        & $scriptPath @common
    }
}

Write-Host "Starting Fix-All pipeline" -ForegroundColor Green

Invoke-Fixer -ScriptName 'Fix-SA1513.ps1'
Invoke-Fixer -ScriptName 'Fix-SA1515.ps1'
Invoke-Fixer -ScriptName 'Fix-SA1028.ps1'
Invoke-Fixer -ScriptName 'Fix-SA1518.ps1'

Write-Host "Fix-All pipeline completed" -ForegroundColor Green
